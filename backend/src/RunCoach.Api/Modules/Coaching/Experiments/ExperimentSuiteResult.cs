using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Captures the complete result of running all 4 experiment categories
/// across multiple profiles. Contains both individual results and
/// category-level groupings for analysis.
/// </summary>
public sealed record ExperimentSuiteResult
{
    /// <summary>
    /// Gets the total number of experiment runs.
    /// </summary>
    public required int TotalRuns { get; init; }

    /// <summary>
    /// Gets the number of runs that completed without errors.
    /// </summary>
    public required int PassedRuns { get; init; }

    /// <summary>
    /// Gets the number of runs that encountered errors.
    /// </summary>
    public required int FailedRuns { get; init; }

    /// <summary>
    /// Gets a value indicating whether this was a live run (with LLM API calls)
    /// or a dry run (prompt assembly only).
    /// </summary>
    public required bool IsLiveRun { get; init; }

    /// <summary>
    /// Gets the timestamp when the suite was completed.
    /// </summary>
    public required string Timestamp { get; init; }

    /// <summary>
    /// Gets all individual experiment results.
    /// </summary>
    public required ImmutableArray<ExperimentResult> Results { get; init; }

    /// <summary>
    /// Gets results from Experiment 1: Token Budget.
    /// </summary>
    public required ImmutableArray<ExperimentResult> TokenBudgetResults { get; init; }

    /// <summary>
    /// Gets results from Experiment 2: Positional Placement.
    /// </summary>
    public required ImmutableArray<ExperimentResult> PositionalPlacementResults { get; init; }

    /// <summary>
    /// Gets results from Experiment 3: Summarization Level.
    /// </summary>
    public required ImmutableArray<ExperimentResult> SummarizationLevelResults { get; init; }

    /// <summary>
    /// Gets results from Experiment 4: Conversation History.
    /// </summary>
    public required ImmutableArray<ExperimentResult> ConversationHistoryResults { get; init; }
}
