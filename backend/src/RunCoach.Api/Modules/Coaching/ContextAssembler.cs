using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Coaching.Prompts;
using RunCoach.Api.Modules.Training.Models;
using IPromptSanitizer = RunCoach.Api.Modules.Coaching.Sanitization.IPromptSanitizer;
using SanitizationPromptSection = RunCoach.Api.Modules.Coaching.Sanitization.PromptSection;

namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Assembles the full prompt payload from user data, enforcing positional
/// layout and token budget per the context-injection-v1.yaml specification.
///
/// Positional layout follows U-curve attention research:
///   START (stable prefix): system prompt, user profile, goal, fitness, paces
///   MIDDLE (variable):     training history (per-workout + weekly summaries)
///   END (conversational):  conversation history, current user message
///
/// Token estimation uses character ratio: characters / 4 with 10% safety margin.
/// Budget enforcement applies a 5-step overflow cascade when total exceeds 15K tokens.
///
/// The system prompt is loaded from versioned YAML files via <see cref="IPromptStore"/>
/// and context templates are rendered using <see cref="PromptRenderer"/>.
/// </summary>
/// <remarks>
/// FUTURE: Before wiring user-facing endpoints, add prompt injection sanitization for
/// all user-controlled free-text fields that flow into assembled prompt sections:
///
/// - <c>UserProfile.Name</c> (user_profile section)
/// - <c>InjuryNote.Description</c> (user_profile section)
/// - <c>RaceTime.Conditions</c> (user_profile section)
/// - <c>UserPreferences.Constraints</c> (user_profile section)
/// - <c>RaceGoal.RaceName</c> (goal_state section)
/// - <c>WorkoutSummary.Notes</c> (training_history section)
/// - <c>ConversationTurn.UserMessage</c> (conversation_history section)
/// - <c>ContextAssemblerInput.CurrentUserMessage</c> (current_user_message section)
///
/// Sanitization should strip or neutralize patterns that could alter LLM instruction
/// following (e.g., "ignore previous instructions", role-play injection, system prompt
/// overrides). Consider a dedicated <c>IPromptSanitizer</c> applied at section boundaries.
/// Currently safe — POC has no user-facing input endpoints; all data is programmatic test fixtures.
/// See also: <see cref="PromptRenderer"/> which sanitizes token values against template injection.
/// </remarks>
public sealed partial class ContextAssembler : IContextAssembler
{
    /// <summary>
    /// Total token budget for the assembled prompt payload.
    /// </summary>
    internal const int TotalTokenBudget = 15_000;

    /// <summary>
    /// Characters per token for the character ratio estimation method.
    /// </summary>
    internal const int CharsPerToken = 4;

    /// <summary>
    /// Safety margin percentage applied to token estimates (10%).
    /// </summary>
    internal const double SafetyMarginPercent = 0.10;

    /// <summary>
    /// Maximum conversation turns before truncation.
    /// </summary>
    internal const int MaxConversationTurns = 10;

    /// <summary>
    /// Reduced conversation turn limit for aggressive truncation.
    /// </summary>
    internal const int ReducedConversationTurns = 3;

    /// <summary>
    /// Maximum weeks of per-workout (Layer 1) detail.
    /// </summary>
    internal const int MaxLayer1Weeks = 2;

    /// <summary>
    /// Maximum weeks of weekly summary (Layer 2) data.
    /// </summary>
    internal const int MaxLayer2Weeks = 4;

    /// <summary>
    /// The prompt ID used to look up the coaching system prompt in the store.
    /// </summary>
    internal const string CoachingPromptId = "coaching-system";

    /// <summary>
    /// Filename of the onboarding system prompt YAML loaded directly off
    /// disk by <see cref="ComposeForOnboardingAsync"/>. This file uses the
    /// <c>{id}-{version}.yaml</c> convention rather than the prompt store's
    /// <c>{id}.{version}.yaml</c> convention, so it is read directly via the
    /// content root rather than registered with <see cref="IPromptStore"/>.
    /// </summary>
    internal const string OnboardingPromptFileName = "onboarding-v1.yaml";

    /// <summary>
    /// Serializer options for inlining captured onboarding slot answers in
    /// the user message. CamelCase + no indentation + string-enum converter
    /// keeps the rendered JSON byte-stable across replays so the cache
    /// prefix bytes do not drift.
    /// </summary>
    private static readonly System.Text.Json.JsonSerializerOptions OnboardingSlotSerializerOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly IPromptStore _promptStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ContextAssembler> _logger;
    private readonly IPromptSanitizer? _sanitizer;
    private readonly Lazy<Task<string>>? _onboardingSystemPromptCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextAssembler"/> class
    /// for the legacy plan-generation path. <see cref="ComposeForOnboardingAsync"/>
    /// is unavailable on instances built via this constructor — call the
    /// onboarding-aware constructor below instead.
    /// </summary>
    /// <param name="promptStore">The prompt store for loading YAML templates.</param>
    /// <param name="timeProvider">Time provider for deterministic date calculations.</param>
    /// <param name="logger">Logger instance.</param>
    public ContextAssembler(IPromptStore promptStore, TimeProvider timeProvider, ILogger<ContextAssembler> logger)
    {
        ArgumentNullException.ThrowIfNull(promptStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _promptStore = promptStore;
        _timeProvider = timeProvider;
        _logger = logger;
        _sanitizer = null;
        _onboardingSystemPromptCache = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextAssembler"/> class
    /// wired for both plan-generation and onboarding flows. The
    /// <paramref name="sanitizer"/> is invoked per-section by
    /// <see cref="ComposeForOnboardingAsync"/> per R-068 / DEC-059 (Slice 1
    /// § Unit 6); the <paramref name="environment"/> + <paramref name="promptSettings"/>
    /// combo resolves the onboarding YAML file off disk independently of the
    /// dot-versioned prompt store convention.
    /// </summary>
    /// <param name="promptStore">The prompt store for loading versioned coaching system prompts.</param>
    /// <param name="timeProvider">Time provider for deterministic date calculations.</param>
    /// <param name="sanitizer">Layered prompt-injection sanitizer (Slice 1 § Unit 6 / DEC-059).</param>
    /// <param name="environment">Host environment used to resolve the prompts content root.</param>
    /// <param name="promptSettings">Prompt-store settings — used to resolve the prompts base directory.</param>
    /// <param name="logger">Logger instance.</param>
    public ContextAssembler(
        IPromptStore promptStore,
        TimeProvider timeProvider,
        IPromptSanitizer sanitizer,
        IHostEnvironment environment,
        IOptions<PromptStoreSettings> promptSettings,
        ILogger<ContextAssembler> logger)
    {
        ArgumentNullException.ThrowIfNull(promptStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(sanitizer);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(promptSettings);
        ArgumentNullException.ThrowIfNull(logger);

        _promptStore = promptStore;
        _timeProvider = timeProvider;
        _logger = logger;
        _sanitizer = sanitizer;

        var basePath = Path.Combine(environment.ContentRootPath, promptSettings.Value.BasePath);
        var onboardingFilePath = Path.Combine(basePath, OnboardingPromptFileName);
        _onboardingSystemPromptCache = new Lazy<Task<string>>(
            () => LoadOnboardingSystemPromptAsync(onboardingFilePath),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public async Task<AssembledPrompt> AssembleAsync(ContextAssemblerInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Load system prompt from YAML.
        var activeVersion = _promptStore.GetActiveVersion(CoachingPromptId);
        var template = await _promptStore.GetPromptAsync(CoachingPromptId, activeVersion, ct).ConfigureAwait(false);

        // FUTURE: template.ContextTemplate is loaded but not yet used — wire into PromptRenderer
        // when context injection goes production (currently sections are built programmatically).

        // Build section content for token replacement.
        var startSections = BuildStartSections(input);
        var middleSections = BuildMiddleSections(input.TrainingHistory);
        var endSections = BuildEndSections(input.ConversationHistory, input.CurrentUserMessage);

        // Use the static system prompt from YAML (contains zero athlete data).
        var systemPrompt = template.StaticSystemPrompt.TrimEnd();

        // Calculate total token estimate.
        var totalTokens = EstimateTokens(systemPrompt)
            + SumTokens(startSections)
            + SumTokens(middleSections)
            + SumTokens(endSections);

        // Apply overflow cascade if over budget.
        if (totalTokens > TotalTokenBudget)
        {
            LogOverflowCascadeTriggered(_logger, totalTokens, TotalTokenBudget);
            (middleSections, endSections, totalTokens) = ApplyOverflowCascade(
                input, startSections, middleSections, endSections, systemPrompt);
        }

        return new AssembledPrompt(
            systemPrompt,
            startSections.ToImmutableArray(),
            middleSections.ToImmutableArray(),
            endSections.ToImmutableArray(),
            totalTokens);
    }

    /// <inheritdoc />
    public async Task<OnboardingPromptComposition> ComposeForOnboardingAsync(
        OnboardingView view,
        OnboardingTopic currentTopic,
        string userInput,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (_sanitizer is null || _onboardingSystemPromptCache is null)
        {
            throw new InvalidOperationException(
                "ContextAssembler was constructed without onboarding dependencies. " +
                "Use the six-arg constructor (with IPromptSanitizer, IHostEnvironment, " +
                "IOptions<PromptStoreSettings>) for the onboarding flow.");
        }

        ct.ThrowIfCancellationRequested();

        var systemPrompt = await _onboardingSystemPromptCache.Value.WaitAsync(ct).ConfigureAwait(false);

        // Sanitize the runner's free-text input. The Spotlighting delimiter
        // wrap (with per-turn nonce) is appended on the non-cached prompt
        // tail so the cacheable prefix stays byte-identical across replays
        // per DEC-047.
        var sanitized = await _sanitizer
            .SanitizeAsync(userInput, SanitizationPromptSection.CurrentUserMessage, ct)
            .ConfigureAwait(false);

        var userMessage = BuildOnboardingUserMessage(view, currentTopic, sanitized.Sanitized);

        return new OnboardingPromptComposition(
            SystemPrompt: systemPrompt,
            UserMessage: userMessage,
            Findings: sanitized.Findings.ToImmutableArray(),
            Neutralized: sanitized.Neutralized);
    }

    /// <inheritdoc />
    public int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var rawEstimate = (double)text.Length / CharsPerToken;
        var withMargin = rawEstimate * (1.0 + SafetyMarginPercent);

        return (int)Math.Ceiling(withMargin);
    }

    /// <summary>
    /// Groups workouts by ISO week (Monday-based) and returns weekly summaries
    /// ordered by most recent week first.
    /// </summary>
    private static IEnumerable<WeekGroup> GroupByWeek(List<WorkoutSummary> workouts)
    {
        return workouts
            .GroupBy(w => (ISOWeek.GetYear(w.Date.ToDateTime(TimeOnly.MinValue)) * 100)
                + ISOWeek.GetWeekOfYear(w.Date.ToDateTime(TimeOnly.MinValue)))
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var weekWorkouts = g.OrderBy(w => w.Date).ToList();
                var isoYear = g.Key / 100;
                var isoWeek = g.Key % 100;
                var weekStart = DateOnly.FromDateTime(ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Monday));

                return new WeekGroup(
                    weekStart,
                    weekWorkouts.Sum(w => w.DistanceKm),
                    weekWorkouts.Count,
                    weekWorkouts.Where(w => w.WorkoutType == "LongRun").Select(w => w.DistanceKm).FirstOrDefault(),
                    weekWorkouts);
            });
    }

    private static int SumTokens(List<PromptSection> sections)
    {
        return sections.Sum(s => s.EstimatedTokens);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token budget exceeded ({TokenEstimate} > {Budget}), applying overflow cascade")]
    private static partial void LogOverflowCascadeTriggered(ILogger logger, int tokenEstimate, int budget);

    private static string FormatWorkoutDetail(WorkoutSummary workout)
    {
        var notes = string.IsNullOrWhiteSpace(workout.Notes) ? string.Empty : $" | {workout.Notes}";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{workout.Date:yyyy-MM-dd} | {workout.WorkoutType} | {workout.DistanceKm} km | {workout.DurationMinutes} min | {FormatTimeSpan(workout.AveragePacePerKm)}/km{notes}");
    }

    private static string FormatWeekSummary(WeekGroup week)
    {
        var longRun = week.LongRunKm > 0
            ? $" | Long run: {week.LongRunKm} km"
            : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Week of {week.WeekStart:yyyy-MM-dd}: {week.TotalDistanceKm} km total | {week.NumberOfRuns} runs{longRun}");
    }

    private static string FormatRaceTime(RaceTime race)
    {
        var conditions = string.IsNullOrWhiteSpace(race.Conditions) ? string.Empty : $" ({race.Conditions})";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{race.Distance} in {FormatTimeSpan(race.Time)} on {race.Date:yyyy-MM-dd}{conditions}");
    }

    private static string FormatInjuryNote(InjuryNote injury)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{injury.Description} ({injury.DateReported:yyyy-MM-dd}, {injury.Status})");
    }

    private static string FormatPreferences(UserPreferences prefs)
    {
        var parts = new List<string>
        {
            $"{prefs.MaxRunDaysPerWeek} days/week max",
            $"Long run: {prefs.LongRunDay}",
            $"Units: {prefs.PreferredUnits}",
        };

        if (prefs.AvailableTimePerRunMinutes.HasValue)
        {
            parts.Add($"Max {prefs.AvailableTimePerRunMinutes.Value} min/run");
        }

        if (prefs.Constraints.Length > 0)
        {
            parts.AddRange(prefs.Constraints);
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Formats a TimeSpan as M:SS or H:MM:SS for human-readable pace/time display.
    /// </summary>
    private static string FormatTimeSpan(TimeSpan ts)
    {
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : ts.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private static string FormatPace(Pace pace) => FormatTimeSpan(pace.ToTimeSpan());

    /// <summary>
    /// Builds the onboarding turn user message. Layout is intentional: the
    /// captured-so-far slot summary precedes the current-topic line so the
    /// LLM sees its working memory before the prompt for the next answer.
    /// The sanitized + delimiter-wrapped runner input lands LAST per the
    /// non-cached-tail convention (DEC-047 / Spotlighting).
    /// </summary>
    private static string BuildOnboardingUserMessage(
        OnboardingView view,
        OnboardingTopic currentTopic,
        string sanitizedUserInput)
    {
        var sb = new StringBuilder();

        sb.AppendLine("ONBOARDING STATE (captured so far):");
        AppendSlotLine(sb, "PrimaryGoal", view.PrimaryGoal);
        AppendSlotLine(sb, "TargetEvent", view.TargetEvent);
        AppendSlotLine(sb, "CurrentFitness", view.CurrentFitness);
        AppendSlotLine(sb, "WeeklySchedule", view.WeeklySchedule);
        AppendSlotLine(sb, "InjuryHistory", view.InjuryHistory);
        AppendSlotLine(sb, "Preferences", view.Preferences);

        if (view.OutstandingClarifications.Count > 0)
        {
            var topics = string.Join(", ", view.OutstandingClarifications);
            sb.AppendLine(CultureInfo.InvariantCulture, $"OUTSTANDING_CLARIFICATIONS: {topics}");
        }

        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"CURRENT_TOPIC: {currentTopic}");
        sb.AppendLine();
        sb.Append(sanitizedUserInput);

        return sb.ToString();
    }

    private static void AppendSlotLine<T>(StringBuilder sb, string label, T? value)
        where T : class
    {
        if (value is null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {label}: <not yet captured>");
            return;
        }

        // Closed-shape answer records serialize to a stable JSON shape per
        // T01.3. Inlining the JSON keeps the user message byte-stable across
        // replays — System.Text.Json with declared property order means the
        // same input deterministically yields the same output.
        var json = System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), OnboardingSlotSerializerOptions);
        sb.AppendLine(CultureInfo.InvariantCulture, $"  {label}: {json}");
    }

    /// <summary>
    /// Loads the onboarding system prompt YAML from disk. Parses the
    /// <c>static_system_prompt</c> top-level key with YamlDotNet and returns
    /// the trimmed content. The result is cached by the
    /// <see cref="_onboardingSystemPromptCache"/> <see cref="Lazy{T}"/> so
    /// every onboarding turn after the first reuses the same byte-equal
    /// string instance.
    /// </summary>
    private static async Task<string> LoadOnboardingSystemPromptAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Onboarding prompt YAML not found at expected path '{filePath}'. " +
                $"This file is created by Slice 1 / T01.3.",
                filePath);
        }

        var yaml = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var doc = deserializer.Deserialize<OnboardingYamlDocument>(yaml);
        if (string.IsNullOrWhiteSpace(doc?.StaticSystemPrompt))
        {
            throw new InvalidOperationException(
                $"Onboarding prompt YAML at '{filePath}' is missing the 'static_system_prompt' key.");
        }

        return doc.StaticSystemPrompt.TrimEnd();
    }

    /// <summary>
    /// Builds the START sections: user profile, goal state, fitness estimate, training paces.
    /// These are stable prefix content with high attention.
    /// </summary>
    private List<PromptSection> BuildStartSections(ContextAssemblerInput input)
    {
        return
        [
            BuildUserProfileSection(input.UserProfile),
            BuildGoalStateSection(input.GoalState),
            BuildFitnessEstimateSection(input.FitnessEstimate),
            BuildTrainingPacesSection(input.TrainingPaces),
        ];
    }

    /// <summary>
    /// Builds the MIDDLE sections: training history.
    /// Variable content in the lower attention zone.
    /// </summary>
    private List<PromptSection> BuildMiddleSections(ImmutableArray<WorkoutSummary> trainingHistory)
    {
        var sections = new List<PromptSection>();

        if (trainingHistory.Length > 0)
        {
            sections.Add(BuildTrainingHistorySection(trainingHistory, useLayer2Only: false));
        }

        return sections;
    }

    /// <summary>
    /// Builds the END sections: conversation history and current user message.
    /// Conversational content with high recency attention.
    /// </summary>
    private List<PromptSection> BuildEndSections(
        ImmutableArray<ConversationTurn> conversationHistory,
        string currentUserMessage)
    {
        var sections = new List<PromptSection>();

        if (conversationHistory.Length > 0)
        {
            var turns = conversationHistory.Length > MaxConversationTurns
                ? conversationHistory[^MaxConversationTurns..]
                : conversationHistory;
            sections.Add(BuildConversationHistorySection(turns));
        }

        sections.Add(new PromptSection(
            "current_user_message",
            currentUserMessage,
            EstimateTokens(currentUserMessage)));

        return sections;
    }

    /// <summary>
    /// Applies the 5-step overflow cascade defined in context-injection-v1.yaml:
    /// 1. Reduce training history to Layer 2 only (weekly summaries)
    /// 2. Truncate oldest conversation turns
    /// 3. Remove relevant plan context (not used in POC 1)
    /// 4. Reduce training history to most recent 2 weeks only
    /// 5. Truncate conversation to most recent 3 turns.
    /// </summary>
    private (List<PromptSection> Middle, List<PromptSection> End, int TotalTokens) ApplyOverflowCascade(
        ContextAssemblerInput input,
        List<PromptSection> startSections,
        List<PromptSection> middleSections,
        List<PromptSection> endSections,
        string systemPrompt)
    {
        var currentMiddle = middleSections;
        var currentEnd = endSections;

        // Step 1: Reduce training history to Layer 2 only (weekly summaries).
        if (input.TrainingHistory.Length > 0)
        {
            currentMiddle =
            [
                BuildTrainingHistorySection(input.TrainingHistory, useLayer2Only: true),
            ];
        }

        var total = CalculateTotal(startSections, currentMiddle, currentEnd, systemPrompt);
        if (total <= TotalTokenBudget)
        {
            return (currentMiddle, currentEnd, total);
        }

        // Step 2: Truncate oldest conversation turns (keep most recent half).
        if (input.ConversationHistory.Length > 2)
        {
            var keepCount = Math.Max(2, input.ConversationHistory.Length / 2);
            var truncatedTurns = input.ConversationHistory[^keepCount..];
            currentEnd = BuildEndSections(truncatedTurns, input.CurrentUserMessage);
        }

        total = CalculateTotal(startSections, currentMiddle, currentEnd, systemPrompt);
        if (total <= TotalTokenBudget)
        {
            return (currentMiddle, currentEnd, total);
        }

        // Step 3: Remove relevant plan context (not used in POC 1, skip).

        // Step 4: Reduce training history to most recent 2 weeks only.
        if (input.TrainingHistory.Length > 0)
        {
            var twoWeeksAgo = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime).AddDays(-14);
            var recentHistory = input.TrainingHistory
                .Where(w => w.Date >= twoWeeksAgo)
                .ToImmutableArray();

            currentMiddle = recentHistory.Length > 0
                ? [BuildTrainingHistorySection(recentHistory, useLayer2Only: true)]
                : [];
        }

        total = CalculateTotal(startSections, currentMiddle, currentEnd, systemPrompt);
        if (total <= TotalTokenBudget)
        {
            return (currentMiddle, currentEnd, total);
        }

        // Step 5: Truncate conversation to most recent 3 turns.
        if (input.ConversationHistory.Length > ReducedConversationTurns)
        {
            var truncatedTurns = input.ConversationHistory[^ReducedConversationTurns..];
            currentEnd = BuildEndSections(truncatedTurns, input.CurrentUserMessage);
        }

        total = CalculateTotal(startSections, currentMiddle, currentEnd, systemPrompt);

        return (currentMiddle, currentEnd, total);
    }

    private int CalculateTotal(
        List<PromptSection> start,
        List<PromptSection> middle,
        List<PromptSection> end,
        string systemPrompt)
    {
        return EstimateTokens(systemPrompt) + SumTokens(start) + SumTokens(middle) + SumTokens(end);
    }

    private PromptSection BuildUserProfileSection(UserProfile profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Name: {profile.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Age: {profile.Age} | Gender: {profile.Gender}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Experience: {profile.RunningExperienceYears} years");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Current weekly distance: {profile.CurrentWeeklyDistanceKm} km");

        if (profile.CurrentLongRunKm.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Current long run: {profile.CurrentLongRunKm.Value} km");
        }

        if (profile.WeightKg.HasValue && profile.HeightCm.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Weight: {profile.WeightKg.Value} kg | Height: {profile.HeightCm.Value} cm");
        }
        else if (profile.WeightKg.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Weight: {profile.WeightKg.Value} kg");
        }
        else if (profile.HeightCm.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Height: {profile.HeightCm.Value} cm");
        }

        if (profile.RestingHeartRateAvg.HasValue && profile.MaxHeartRate.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Resting HR: {profile.RestingHeartRateAvg.Value} bpm | Max HR: {profile.MaxHeartRate.Value} bpm");
        }
        else if (profile.RestingHeartRateAvg.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Resting HR: {profile.RestingHeartRateAvg.Value} bpm");
        }
        else if (profile.MaxHeartRate.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Max HR: {profile.MaxHeartRate.Value} bpm");
        }

        if (profile.RecentRaceTimes.Length > 0)
        {
            var races = string.Join("; ", profile.RecentRaceTimes.Select(FormatRaceTime));
            sb.AppendLine(CultureInfo.InvariantCulture, $"Recent races: {races}");
        }
        else
        {
            sb.AppendLine("Recent races: None");
        }

        if (profile.InjuryHistory.Length > 0)
        {
            var injuries = string.Join("; ", profile.InjuryHistory.Select(FormatInjuryNote));
            sb.AppendLine(CultureInfo.InvariantCulture, $"Injury history: {injuries}");
        }
        else
        {
            sb.AppendLine("Injury history: None");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"Preferences: {FormatPreferences(profile.Preferences)}");

        var content = sb.ToString().TrimEnd();

        return new PromptSection("user_profile", content, EstimateTokens(content));
    }

    private PromptSection BuildGoalStateSection(GoalState goal)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Goal type: {goal.GoalType}");

        if (goal.TargetRace is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Target race: {goal.TargetRace.RaceName} — {goal.TargetRace.Distance} on {goal.TargetRace.RaceDate:yyyy-MM-dd}");

            if (goal.TargetRace.TargetTime.HasValue)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"Target time: {FormatTimeSpan(goal.TargetRace.TargetTime.Value)}");
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"Priority: {goal.TargetRace.Priority}");
        }

        var content = sb.ToString().TrimEnd();

        return new PromptSection("goal_state", content, EstimateTokens(content));
    }

    private PromptSection BuildFitnessEstimateSection(FitnessEstimate fitness)
    {
        var sb = new StringBuilder();

        if (fitness.EstimatedPaceZoneIndex.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Pace-zone index: {fitness.EstimatedPaceZoneIndex.Value}");
        }
        else
        {
            sb.AppendLine("Pace-zone index: Not available (no race history)");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"Fitness level: {fitness.FitnessLevel}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Assessment basis: {fitness.AssessmentBasis}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Assessed on: {fitness.AssessedOn:yyyy-MM-dd}");

        var content = sb.ToString().TrimEnd();

        return new PromptSection("fitness_estimate", content, EstimateTokens(content));
    }

    private PromptSection BuildTrainingPacesSection(TrainingPaces paces)
    {
        var sb = new StringBuilder();
        if (paces.EasyPaceRange is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Easy pace: {FormatPace(paces.EasyPaceRange.Fast)} - {FormatPace(paces.EasyPaceRange.Slow)} /km");
        }

        if (paces.MarathonPace.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Marathon pace: {FormatPace(paces.MarathonPace.Value)} /km");
        }

        if (paces.ThresholdPace.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Threshold pace: {FormatPace(paces.ThresholdPace.Value)} /km");
        }

        if (paces.IntervalPace.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Interval pace: {FormatPace(paces.IntervalPace.Value)} /km");
        }

        if (paces.RepetitionPace.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Repetition pace: {FormatPace(paces.RepetitionPace.Value)} /km");
        }

        if (paces.FastRepetitionPace.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Fast-repetition pace: {FormatPace(paces.FastRepetitionPace.Value)} /km");
        }

        var content = sb.ToString().TrimEnd();

        return new PromptSection("training_paces", content, EstimateTokens(content));
    }

    /// <summary>
    /// Builds the training history section. Uses Layer 1 (per-workout detail) for
    /// recent workouts and Layer 2 (weekly summaries) for older weeks.
    /// When useLayer2Only is true, all history is summarized as weekly totals.
    /// </summary>
    private PromptSection BuildTrainingHistorySection(
        ImmutableArray<WorkoutSummary> history,
        bool useLayer2Only)
    {
        if (history.Length == 0)
        {
            return new PromptSection("training_history", string.Empty, 0);
        }

        var sb = new StringBuilder();
        var sorted = history.OrderByDescending(w => w.Date).ToList();

        if (useLayer2Only)
        {
            // Layer 2: weekly summaries only.
            var weeks = GroupByWeek(sorted).Take(MaxLayer2Weeks);
            foreach (var week in weeks)
            {
                sb.AppendLine(FormatWeekSummary(week));
            }
        }
        else
        {
            // Layer 1 for recent weeks, Layer 2 for older weeks.
            var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
            var layer1Cutoff = today.AddDays(-7 * MaxLayer1Weeks);

            var recentWorkouts = sorted.Where(w => w.Date >= layer1Cutoff).ToList();
            var olderWorkouts = sorted.Where(w => w.Date < layer1Cutoff).ToList();

            if (recentWorkouts.Count > 0)
            {
                foreach (var workout in recentWorkouts)
                {
                    sb.AppendLine(FormatWorkoutDetail(workout));
                }
            }

            if (olderWorkouts.Count > 0)
            {
                var olderWeeks = GroupByWeek(olderWorkouts).Take(MaxLayer2Weeks);
                foreach (var week in olderWeeks)
                {
                    sb.AppendLine(FormatWeekSummary(week));
                }
            }
        }

        var content = sb.ToString().TrimEnd();

        return new PromptSection("training_history", content, EstimateTokens(content));
    }

    private PromptSection BuildConversationHistorySection(ImmutableArray<ConversationTurn> turns)
    {
        var sb = new StringBuilder();

        foreach (var turn in turns)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"[User]: {turn.UserMessage}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"[Coach]: {turn.CoachMessage}");
        }

        var content = sb.ToString().TrimEnd();

        return new PromptSection("conversation_history", content, EstimateTokens(content));
    }

    /// <summary>
    /// Minimal YAML deserialization shape for <c>onboarding-v1.yaml</c>. Only
    /// the <c>static_system_prompt</c> field is read — metadata and other
    /// fields are ignored via <c>IgnoreUnmatchedProperties</c>. The setter is
    /// invoked by YamlDotNet via reflection; SonarAnalyzer's S3459 / S1144
    /// rules cannot see that path so the setter is suppressed locally.
    /// </summary>
    private sealed class OnboardingYamlDocument
    {
#pragma warning disable S3459, S1144, CA1822 // YamlDotNet sets the property via reflection.
        public string? StaticSystemPrompt { get; set; }
#pragma warning restore S3459, S1144, CA1822
    }

    /// <summary>
    /// Internal grouping type for weekly workout aggregation.
    /// </summary>
    private sealed record WeekGroup(
        DateOnly WeekStart,
        decimal TotalDistanceKm,
        int NumberOfRuns,
        decimal LongRunKm,
        List<WorkoutSummary> Workouts);
}
