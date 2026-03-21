using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Observations from the token budget experiment.
/// </summary>
public sealed record TokenBudgetObservations
{
    /// <summary>
    /// Gets average token usage per variation ID.
    /// </summary>
    public required ImmutableDictionary<string, double> AverageTokensByVariation { get; init; }

    /// <summary>
    /// Gets whether each variation produced a JSON plan (or is a dry run).
    /// </summary>
    public required ImmutableDictionary<string, bool> AllVariationsProducedPlans { get; init; }

    /// <summary>
    /// Gets minimum tokens used across all token budget runs.
    /// </summary>
    public required int MinTokensUsed { get; init; }

    /// <summary>
    /// Gets maximum tokens used across all token budget runs.
    /// </summary>
    public required int MaxTokensUsed { get; init; }

    /// <summary>
    /// Gets average section count per variation.
    /// </summary>
    public required ImmutableDictionary<string, double> SectionCountsByVariation { get; init; }
}
