using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Prompts;
using RunCoach.Api.Modules.Training.Models;

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
public sealed class ContextAssembler : IContextAssembler
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

    private readonly IPromptStore _promptStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextAssembler"/> class
    /// with an <see cref="IPromptStore"/> for loading versioned system prompts
    /// from YAML files.
    /// </summary>
    /// <param name="promptStore">The prompt store for loading YAML templates.</param>
    public ContextAssembler(IPromptStore promptStore)
    {
        ArgumentNullException.ThrowIfNull(promptStore);
        _promptStore = promptStore;
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
                var weekStart = weekWorkouts[0].Date;

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
            var twoWeeksAgo = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-14);
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

        if (profile.WeightKg.HasValue)
        {
            sb.Append(CultureInfo.InvariantCulture, $"Weight: {profile.WeightKg.Value} kg");
        }

        if (profile.HeightCm.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $" | Height: {profile.HeightCm.Value} cm");
        }
        else if (profile.WeightKg.HasValue)
        {
            sb.AppendLine();
        }

        if (profile.RestingHeartRateAvg.HasValue)
        {
            sb.Append(CultureInfo.InvariantCulture, $"Resting HR: {profile.RestingHeartRateAvg.Value} bpm");
        }

        if (profile.MaxHeartRate.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $" | Max HR: {profile.MaxHeartRate.Value} bpm");
        }
        else if (profile.RestingHeartRateAvg.HasValue)
        {
            sb.AppendLine();
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

        if (fitness.EstimatedVdot.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"VDOT: {fitness.EstimatedVdot.Value}");
        }
        else
        {
            sb.AppendLine("VDOT: Not available (no race history)");
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
        sb.AppendLine(CultureInfo.InvariantCulture, $"Easy pace: {FormatTimeSpan(paces.EasyPaceRange.MinPerKm)} - {FormatTimeSpan(paces.EasyPaceRange.MaxPerKm)} /km");

        if (paces.MarathonPace.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Marathon pace: {FormatTimeSpan(paces.MarathonPace.Value)} /km");
        }

        if (paces.ThresholdPace.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Threshold pace: {FormatTimeSpan(paces.ThresholdPace.Value)} /km");
        }

        if (paces.IntervalPace.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Interval pace: {FormatTimeSpan(paces.IntervalPace.Value)} /km");
        }

        if (paces.RepetitionPace.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Repetition pace: {FormatTimeSpan(paces.RepetitionPace.Value)} /km");
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
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
    /// Internal grouping type for weekly workout aggregation.
    /// </summary>
    private sealed record WeekGroup(
        DateOnly WeekStart,
        decimal TotalDistanceKm,
        int NumberOfRuns,
        decimal LongRunKm,
        List<WorkoutSummary> Workouts);
}
