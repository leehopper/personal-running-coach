using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Observations from the positional placement experiment.
/// </summary>
public sealed record PositionalPlacementObservations
{
    /// <summary>
    /// Gets average start section count per variation.
    /// </summary>
    public required ImmutableDictionary<string, double> StartSectionCountsByVariation { get; init; }

    /// <summary>
    /// Gets average middle section count per variation.
    /// </summary>
    public required ImmutableDictionary<string, double> MiddleSectionCountsByVariation { get; init; }

    /// <summary>
    /// Gets average end section count per variation.
    /// </summary>
    public required ImmutableDictionary<string, double> EndSectionCountsByVariation { get; init; }

    /// <summary>
    /// Gets a value indicating whether token usage is roughly consistent across placements
    /// (since placement should not significantly affect token count).
    /// </summary>
    public required bool TokenUsageConsistent { get; init; }
}
