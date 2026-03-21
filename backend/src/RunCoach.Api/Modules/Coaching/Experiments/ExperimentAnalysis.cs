using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Structured analysis of experiment results across all 4 categories.
/// Provides observations and metrics suitable for the findings document.
/// </summary>
public sealed record ExperimentAnalysis
{
    /// <summary>
    /// Gets observations from Experiment 1: Token Budget.
    /// </summary>
    public required TokenBudgetObservations TokenBudgetObservations { get; init; }

    /// <summary>
    /// Gets observations from Experiment 2: Positional Placement.
    /// </summary>
    public required PositionalPlacementObservations PositionalPlacementObservations { get; init; }

    /// <summary>
    /// Gets observations from Experiment 3: Summarization Level.
    /// </summary>
    public required SummarizationLevelObservations SummarizationLevelObservations { get; init; }

    /// <summary>
    /// Gets observations from Experiment 4: Conversation History.
    /// </summary>
    public required ConversationHistoryObservations ConversationHistoryObservations { get; init; }

    /// <summary>
    /// Gets cross-validation observations comparing baseline and secondary profiles.
    /// </summary>
    public required CrossValidationObservations CrossValidationObservations { get; init; }

    /// <summary>
    /// Gets the total number of experiment runs.
    /// </summary>
    public required int TotalExperimentRuns { get; init; }

    /// <summary>
    /// Gets the number of runs that produced live LLM responses.
    /// </summary>
    public required int TotalLiveResponses { get; init; }

    /// <summary>
    /// Gets the number of dry runs (no LLM response).
    /// </summary>
    public required int TotalDryRuns { get; init; }

    /// <summary>
    /// Gets all distinct profile names used across experiments.
    /// </summary>
    public required ImmutableArray<string> AllProfilesUsed { get; init; }
}
