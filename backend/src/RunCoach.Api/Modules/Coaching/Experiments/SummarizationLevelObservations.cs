using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Observations from the summarization level experiment.
/// </summary>
public sealed record SummarizationLevelObservations
{
    /// <summary>
    /// Gets average token usage per variation ID.
    /// </summary>
    public required ImmutableDictionary<string, double> AverageTokensByVariation { get; init; }

    /// <summary>
    /// Gets a value indicating whether weekly summary uses fewer tokens than per-workout detail.
    /// </summary>
    public required bool WeeklySummaryUsesFewerTokens { get; init; }

    /// <summary>
    /// Gets the percentage of tokens saved by using weekly summaries vs. per-workout detail.
    /// </summary>
    public required double TokenSavingsPercent { get; init; }
}
