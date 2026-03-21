using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Orchestrates running all 4 context injection experiments across multiple profiles.
/// Produces structured results suitable for analysis and findings documentation.
///
/// Experiment plan (per the POC 1 spec):
/// 1. Token Budget: 3 variations x 2 profiles = 6 runs
/// 2. Positional Placement: 3 variations x 2 profiles = 6 runs
/// 3. Summarization Level: 3 variations x 2 profiles = 6 runs (profiles with history only)
/// 4. Conversation History: 2 variations x 2 profiles = 4 runs
/// Total: ~22 runs
///
/// Supports both dry-run mode (prompt assembly verification) and live mode (LLM API calls).
/// Results are written as JSON files organized by experiment category.
/// </summary>
public sealed class ExperimentExecutor
{
    /// <summary>
    /// Default baseline profile for all experiments.
    /// </summary>
    public const string BaselineProfile = "lee";

    /// <summary>
    /// Default cross-validation profile (must have training history for summarization experiment).
    /// </summary>
    public const string CrossValidationProfile = "maria";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ExperimentRunner _runner;
    private readonly string _outputDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperimentExecutor"/> class.
    /// </summary>
    /// <param name="outputDir">Root directory for experiment results.</param>
    /// <param name="llm">Optional LLM client for live runs. Null for dry runs.</param>
    public ExperimentExecutor(string outputDir, ICoachingLlm? llm = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);
        _outputDir = outputDir;
        _runner = new ExperimentRunner(outputDir, llm);
    }

    /// <summary>
    /// Generates an analysis summary of the experiment results.
    /// This provides structured observations suitable for the findings document.
    /// </summary>
    /// <param name="suite">The experiment suite result to analyze.</param>
    /// <returns>A structured analysis of the experiment results.</returns>
    public static ExperimentAnalysis Analyze(ExperimentSuiteResult suite)
    {
        ArgumentNullException.ThrowIfNull(suite);

        return new ExperimentAnalysis
        {
            TokenBudgetObservations = AnalyzeTokenBudget(suite.TokenBudgetResults),
            PositionalPlacementObservations = AnalyzePositionalPlacement(suite.PositionalPlacementResults),
            SummarizationLevelObservations = AnalyzeSummarizationLevel(suite.SummarizationLevelResults),
            ConversationHistoryObservations = AnalyzeConversationHistory(suite.ConversationHistoryResults),
            CrossValidationObservations = AnalyzeCrossValidation(suite),
            TotalExperimentRuns = suite.TotalRuns,
            TotalLiveResponses = suite.Results.Count(r => r.LlmResponse is not null),
            TotalDryRuns = suite.Results.Count(r => r.LlmResponse is null),
            AllProfilesUsed = suite.Results
                .Select(r => r.ProfileName)
                .Distinct()
                .Order()
                .ToImmutableArray(),
        };
    }

    /// <summary>
    /// Runs all 4 experiments across baseline and cross-validation profiles.
    /// Returns a complete experiment suite result containing all individual results.
    /// </summary>
    /// <param name="live">True for live LLM calls, false for dry runs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The complete experiment suite result.</returns>
    public async Task<ExperimentSuiteResult> RunAllExperimentsAsync(
        bool live = false,
        CancellationToken ct = default)
    {
        var allResults = ImmutableArray.CreateBuilder<ExperimentResult>();

        // Experiment 1: Token Budget (3 variations x 2 profiles = 6 runs)
        var tokenBudgetResults = await RunCategoryAsync(
            ExperimentVariations.TokenBudget,
            [BaselineProfile, CrossValidationProfile],
            live,
            ct).ConfigureAwait(false);
        allResults.AddRange(tokenBudgetResults);

        // Experiment 2: Positional Placement (3 variations x 2 profiles = 6 runs)
        var positionalResults = await RunCategoryAsync(
            ExperimentVariations.PositionalPlacement,
            [BaselineProfile, CrossValidationProfile],
            live,
            ct).ConfigureAwait(false);
        allResults.AddRange(positionalResults);

        // Experiment 3: Summarization Level (3 variations x 2 profiles = 6 runs)
        // Only profiles with training history (Lee has 3 weeks, Maria has 4 weeks).
        var summarizationResults = await RunCategoryAsync(
            ExperimentVariations.SummarizationLevel,
            [BaselineProfile, CrossValidationProfile],
            live,
            ct).ConfigureAwait(false);
        allResults.AddRange(summarizationResults);

        // Experiment 4: Conversation History (2 variations x 2 profiles = 4 runs)
        var conversationResults = await RunCategoryAsync(
            ExperimentVariations.ConversationHistory,
            [BaselineProfile, CrossValidationProfile],
            live,
            ct).ConfigureAwait(false);
        allResults.AddRange(conversationResults);

        return new ExperimentSuiteResult
        {
            TotalRuns = allResults.Count,
            PassedRuns = allResults.Count(r => r.Error is null),
            FailedRuns = allResults.Count(r => r.Error is not null),
            IsLiveRun = live,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Results = allResults.ToImmutable(),
            TokenBudgetResults = tokenBudgetResults,
            PositionalPlacementResults = positionalResults,
            SummarizationLevelResults = summarizationResults,
            ConversationHistoryResults = conversationResults,
        };
    }

    /// <summary>
    /// Writes the full experiment suite results to the output directory as organized JSON files.
    /// Creates one summary file and individual category files.
    /// </summary>
    /// <param name="suite">The experiment suite result to write.</param>
    public void WriteResults(ExperimentSuiteResult suite)
    {
        ArgumentNullException.ThrowIfNull(suite);
        Directory.CreateDirectory(_outputDir);

        // Write individual category result files.
        WriteJsonFile("01-token-budget-results.json", suite.TokenBudgetResults);
        WriteJsonFile("02-positional-placement-results.json", suite.PositionalPlacementResults);
        WriteJsonFile("03-summarization-level-results.json", suite.SummarizationLevelResults);
        WriteJsonFile("04-conversation-history-results.json", suite.ConversationHistoryResults);

        // Write full suite summary.
        WriteJsonFile("00-experiment-suite-summary.json", suite);

        // Write individual result files for each variation.
        foreach (var result in suite.Results)
        {
            _runner.WriteResult(result);
        }
    }

    /// <summary>
    /// Analyzes token budget experiment results.
    /// </summary>
    private static TokenBudgetObservations AnalyzeTokenBudget(ImmutableArray<ExperimentResult> results)
    {
        var byVariation = results.GroupBy(r => r.VariationId).ToImmutableDictionary(
            g => g.Key,
            g => g.ToImmutableArray());

        var tokenUsageByVariation = byVariation.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Average(r => r.EstimatedPromptTokens));

        var hasJsonPlanByVariation = byVariation.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.All(r => r.HasJsonPlan || r.LlmResponse is null));

        return new TokenBudgetObservations
        {
            AverageTokensByVariation = tokenUsageByVariation,
            AllVariationsProducedPlans = hasJsonPlanByVariation,
            MinTokensUsed = results.Min(r => r.EstimatedPromptTokens),
            MaxTokensUsed = results.Max(r => r.EstimatedPromptTokens),
            SectionCountsByVariation = byVariation.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Average(r => (double)r.SectionCount)),
        };
    }

    /// <summary>
    /// Analyzes positional placement experiment results.
    /// </summary>
    private static PositionalPlacementObservations AnalyzePositionalPlacement(ImmutableArray<ExperimentResult> results)
    {
        var byVariation = results.GroupBy(r => r.VariationId).ToImmutableDictionary(
            g => g.Key,
            g => g.ToImmutableArray());

        return new PositionalPlacementObservations
        {
            StartSectionCountsByVariation = byVariation.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Average(r => (double)r.StartSectionCount)),
            MiddleSectionCountsByVariation = byVariation.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Average(r => (double)r.MiddleSectionCount)),
            EndSectionCountsByVariation = byVariation.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Average(r => (double)r.EndSectionCount)),
            TokenUsageConsistent = results.Select(r => r.EstimatedPromptTokens).Distinct().Count() <= 3,
        };
    }

    /// <summary>
    /// Analyzes summarization level experiment results.
    /// </summary>
    private static SummarizationLevelObservations AnalyzeSummarizationLevel(ImmutableArray<ExperimentResult> results)
    {
        var byVariation = results.GroupBy(r => r.VariationId).ToImmutableDictionary(
            g => g.Key,
            g => g.ToImmutableArray());

        var tokensByVariation = byVariation.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Average(r => r.EstimatedPromptTokens));

        var weeklyTokens = tokensByVariation.GetValueOrDefault("summarize-weekly", 0);
        var perWorkoutTokens = tokensByVariation.GetValueOrDefault("summarize-per-workout", 0);

        return new SummarizationLevelObservations
        {
            AverageTokensByVariation = tokensByVariation,
            WeeklySummaryUsesFewerTokens = weeklyTokens < perWorkoutTokens,
            TokenSavingsPercent = perWorkoutTokens > 0
                ? Math.Round((1.0 - (weeklyTokens / perWorkoutTokens)) * 100, 1)
                : 0,
        };
    }

    /// <summary>
    /// Analyzes conversation history experiment results.
    /// </summary>
    private static ConversationHistoryObservations AnalyzeConversationHistory(ImmutableArray<ExperimentResult> results)
    {
        var byVariation = results.GroupBy(r => r.VariationId).ToImmutableDictionary(
            g => g.Key,
            g => g.ToImmutableArray());

        var tokensByVariation = byVariation.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Average(r => r.EstimatedPromptTokens));

        var noHistoryTokens = tokensByVariation.GetValueOrDefault("conversation-0", 0);
        var withHistoryTokens = tokensByVariation.GetValueOrDefault("conversation-5", 0);

        return new ConversationHistoryObservations
        {
            AverageTokensByVariation = tokensByVariation,
            ConversationAddsTokens = withHistoryTokens > noHistoryTokens,
            AdditionalTokensFromConversation = (int)(withHistoryTokens - noHistoryTokens),
        };
    }

    /// <summary>
    /// Analyzes cross-validation (baseline vs. secondary profile) consistency.
    /// </summary>
    private static CrossValidationObservations AnalyzeCrossValidation(ExperimentSuiteResult suite)
    {
        var baselineResults = suite.Results.Where(r => r.ProfileName == BaselineProfile).ToList();
        var crossResults = suite.Results.Where(r => r.ProfileName == CrossValidationProfile).ToList();

        var baselineTokenAvg = baselineResults.Count > 0
            ? baselineResults.Average(r => r.EstimatedPromptTokens)
            : 0;
        var crossTokenAvg = crossResults.Count > 0
            ? crossResults.Average(r => r.EstimatedPromptTokens)
            : 0;

        return new CrossValidationObservations
        {
            BaselineProfile = BaselineProfile,
            CrossValidationProfile = CrossValidationProfile,
            BaselineAverageTokens = baselineTokenAvg,
            CrossValidationAverageTokens = crossTokenAvg,
            BaselineRunCount = baselineResults.Count,
            CrossValidationRunCount = crossResults.Count,
            BothProfilesHaveResults = baselineResults.Count > 0 && crossResults.Count > 0,
        };
    }

    /// <summary>
    /// Runs all variations in a category against multiple profiles.
    /// </summary>
    private async Task<ImmutableArray<ExperimentResult>> RunCategoryAsync(
        ImmutableArray<ExperimentConfig> variations,
        string[] profiles,
        bool live,
        CancellationToken ct)
    {
        var results = ImmutableArray.CreateBuilder<ExperimentResult>();

        foreach (var profile in profiles)
        {
            var profileResults = await _runner.RunAllAsync(
                variations, profile, live, ct: ct).ConfigureAwait(false);
            results.AddRange(profileResults);
        }

        return results.ToImmutable();
    }

    /// <summary>
    /// Writes an object as JSON to a file in the output directory.
    /// </summary>
    private void WriteJsonFile<T>(string fileName, T data)
    {
        var path = Path.Combine(_outputDir, fileName);
        var json = JsonSerializer.Serialize(data, WriteOptions);
        File.WriteAllText(path, json);
    }
}
