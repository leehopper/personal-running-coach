using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Profiles;

namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Runs context injection experiments by assembling prompts with different
/// configurations and optionally calling the LLM to collect responses.
///
/// Supports two modes:
/// - Dry run: Assembles prompts and captures metadata without LLM calls.
///   Useful for verifying infrastructure and token budgets.
/// - Live run: Assembles prompts and calls the LLM, capturing full responses.
///   Requires a configured ICoachingLlm instance.
///
/// Results are written as JSON files to the experiment output directory.
/// </summary>
public sealed class ExperimentRunner
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ExperimentContextAssembler _assembler = new();
    private readonly ICoachingLlm? _llm;
    private readonly string _outputDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperimentRunner"/> class.
    /// </summary>
    /// <param name="outputDir">Directory to write experiment results to.</param>
    /// <param name="llm">Optional LLM client for live runs. Null for dry runs.</param>
    public ExperimentRunner(string outputDir, ICoachingLlm? llm = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);
        _outputDir = outputDir;
        _llm = llm;
    }

    /// <summary>
    /// Runs a single experiment variation against a profile in dry-run mode.
    /// Assembles the prompt and captures metadata without calling the LLM.
    /// </summary>
    /// <param name="config">The experiment configuration.</param>
    /// <param name="profileName">The test profile name to use.</param>
    /// <param name="userMessage">Optional custom user message.</param>
    /// <returns>The experiment result with prompt metadata.</returns>
    public ExperimentResult DryRun(
        ExperimentConfig config,
        string profileName,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        var profile = LoadProfile(profileName);
        var input = BuildInput(profile, config, userMessage);
        var assembled = _assembler.Assemble(input, config);

        var sectionCount = assembled.StartSections.Length
            + assembled.MiddleSections.Length
            + assembled.EndSections.Length;

        return new ExperimentResult
        {
            VariationId = config.VariationId,
            Category = config.Category,
            Description = config.Description,
            ProfileName = profileName,
            EstimatedPromptTokens = assembled.EstimatedTokenCount,
            SectionCount = sectionCount,
            StartSectionCount = assembled.StartSections.Length,
            MiddleSectionCount = assembled.MiddleSections.Length,
            EndSectionCount = assembled.EndSections.Length,
            Timestamp = DateTime.UtcNow.ToString("o"),
        };
    }

    /// <summary>
    /// Runs a single experiment variation against a profile with a live LLM call.
    /// Requires the LLM client to be configured.
    /// </summary>
    /// <param name="config">The experiment configuration.</param>
    /// <param name="profileName">The test profile name to use.</param>
    /// <param name="userMessage">Optional custom user message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The experiment result with LLM response.</returns>
    /// <exception cref="InvalidOperationException">Thrown when LLM client is not configured.</exception>
    public async Task<ExperimentResult> LiveRunAsync(
        ExperimentConfig config,
        string profileName,
        string? userMessage = null,
        CancellationToken ct = default)
    {
        if (_llm is null)
        {
            throw new InvalidOperationException(
                "LLM client is not configured. Pass an ICoachingLlm instance to the constructor for live runs.");
        }

        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        var profile = LoadProfile(profileName);
        var input = BuildInput(profile, config, userMessage);
        var assembled = _assembler.Assemble(input, config);

        var sectionCount = assembled.StartSections.Length
            + assembled.MiddleSections.Length
            + assembled.EndSections.Length;

        string? response = null;
        string? error = null;
        var hasJsonPlan = false;

        try
        {
            var fullUserMessage = BuildUserMessageFromSections(assembled);
            response = await _llm.GenerateAsync(assembled.SystemPrompt, fullUserMessage, ct).ConfigureAwait(false);
            hasJsonPlan = response.Contains("```json", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
        }

        return new ExperimentResult
        {
            VariationId = config.VariationId,
            Category = config.Category,
            Description = config.Description,
            ProfileName = profileName,
            EstimatedPromptTokens = assembled.EstimatedTokenCount,
            SectionCount = sectionCount,
            StartSectionCount = assembled.StartSections.Length,
            MiddleSectionCount = assembled.MiddleSections.Length,
            EndSectionCount = assembled.EndSections.Length,
            LlmResponse = response,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Error = error,
            HasJsonPlan = hasJsonPlan,
        };
    }

    /// <summary>
    /// Runs all variations in an experiment category against a profile.
    /// </summary>
    /// <param name="variations">The set of experiment variations to run.</param>
    /// <param name="profileName">The test profile name to use.</param>
    /// <param name="live">True for live LLM calls, false for dry runs.</param>
    /// <param name="userMessage">Optional custom user message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All experiment results.</returns>
    public async Task<ImmutableArray<ExperimentResult>> RunAllAsync(
        ImmutableArray<ExperimentConfig> variations,
        string profileName,
        bool live = false,
        string? userMessage = null,
        CancellationToken ct = default)
    {
        var results = ImmutableArray.CreateBuilder<ExperimentResult>();

        foreach (var config in variations)
        {
            var result = live
                ? await LiveRunAsync(config, profileName, userMessage, ct).ConfigureAwait(false)
                : DryRun(config, profileName, userMessage);

            results.Add(result);
        }

        return results.ToImmutable();
    }

    /// <summary>
    /// Writes experiment results to a JSON file in the output directory.
    /// </summary>
    /// <param name="fileName">The output file name (without path).</param>
    /// <param name="results">The experiment results to write.</param>
    public void WriteResults(string fileName, ImmutableArray<ExperimentResult> results)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        Directory.CreateDirectory(_outputDir);
        var path = Path.Combine(_outputDir, fileName);
        var json = JsonSerializer.Serialize(results, WriteOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Writes a single experiment result to a JSON file in the output directory.
    /// </summary>
    /// <param name="result">The experiment result to write.</param>
    public void WriteResult(ExperimentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Directory.CreateDirectory(_outputDir);
        var fileName = $"{result.VariationId}-{result.ProfileName}.json";
        var path = Path.Combine(_outputDir, fileName);
        var json = JsonSerializer.Serialize(result, WriteOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Loads a test profile by name.
    /// </summary>
    private static TestProfile LoadProfile(string name)
    {
        if (!TestProfiles.All.TryGetValue(name, out var profile))
        {
            var validNames = string.Join(", ", TestProfiles.All.Keys);
            throw new ArgumentException(
                $"Unknown profile '{name}'. Valid profiles: {validNames}",
                nameof(name));
        }

        return profile;
    }

    /// <summary>
    /// Builds assembler input from a profile and experiment config.
    /// </summary>
    private static ContextAssemblerInput BuildInput(
        TestProfile profile,
        ExperimentConfig config,
        string? userMessage)
    {
        // Determine conversation history based on config.
        var conversation = config.ConversationTurns > 0
            ? SampleConversations.GetIntermediateTurns(config.ConversationTurns)
            : SampleConversations.Empty;

        var message = userMessage ?? BuildDefaultUserMessage(profile);

        return new ContextAssemblerInput(
            profile.UserProfile,
            profile.GoalState,
            profile.GoalState.CurrentFitnessEstimate,
            profile.GoalState.CurrentFitnessEstimate.TrainingPaces,
            profile.TrainingHistory,
            conversation,
            message);
    }

    /// <summary>
    /// Builds the default plan generation request message.
    /// </summary>
    private static string BuildDefaultUserMessage(TestProfile profile)
    {
        var goalDescription = profile.GoalState.TargetRace is not null
            ? $"a {profile.GoalState.TargetRace.Distance} ({profile.GoalState.TargetRace.RaceName})"
            : $"a {profile.GoalState.GoalType} plan";

        return $"""
            I'm {profile.UserProfile.Name}. Please generate a complete training plan for me.
            I'm training for {goalDescription}.

            Please provide:
            1. A MacroPlan with phased periodization
            2. A MesoWeek template for the current week
            3. MicroWorkout details for my next 3 training days

            Respond with the plan as a JSON object in a ```json code fence, followed by coaching notes.
            """;
    }

    /// <summary>
    /// Builds the full user message text from assembled prompt sections.
    /// </summary>
    private static string BuildUserMessageFromSections(AssembledPrompt assembled)
    {
        var parts = new List<string>();

        foreach (var section in assembled.StartSections)
        {
            parts.Add($"=== {section.Key.ToUpperInvariant()} ===\n{section.Content}");
        }

        foreach (var section in assembled.MiddleSections)
        {
            parts.Add($"=== {section.Key.ToUpperInvariant()} ===\n{section.Content}");
        }

        foreach (var section in assembled.EndSections)
        {
            parts.Add($"=== {section.Key.ToUpperInvariant()} ===\n{section.Content}");
        }

        return string.Join("\n\n", parts);
    }
}
