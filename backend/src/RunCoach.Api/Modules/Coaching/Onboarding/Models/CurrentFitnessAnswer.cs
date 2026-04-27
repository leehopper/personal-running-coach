using System.ComponentModel;
using System.Globalization;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Normalized answer for the CurrentFitness topic. Captures the runner's recent training volume,
/// longest recent run, and an optional recent race result that drives pace-zone calculations.
/// </summary>
public sealed record CurrentFitnessAnswer
{
    /// <summary>
    /// Gets the typical weekly running distance in kilometers over the past four weeks.
    /// </summary>
    [Description("Typical weekly running distance in kilometers over the past four weeks.")]
    public required double TypicalWeeklyKm { get; init; }

    /// <summary>
    /// Gets the longest single run completed in the past four weeks, in kilometers.
    /// </summary>
    [Description("Longest single run completed in the past four weeks, in kilometers.")]
    public required double LongestRecentRunKm { get; init; }

    /// <summary>
    /// Gets an optional recent race distance in kilometers when the runner has one to share.
    /// </summary>
    [Description("Optional recent race distance in kilometers. Null if the runner has no recent race result.")]
    public required double? RecentRaceDistanceKm { get; init; }

    /// <summary>
    /// Gets the optional recent race time as an ISO-8601 duration (e.g. PT0H45M30S).
    /// </summary>
    [Description("Optional recent race time as ISO-8601 duration (e.g. PT0H45M30S). Null if the runner has no recent race result.")]
    public required string? RecentRaceTimeIso { get; init; }

    /// <summary>
    /// Gets the runner's self-reported fitness summary in their own words.
    /// </summary>
    [Description("Self-reported fitness summary in the runner's own words.")]
    public required string Description { get; init; } = string.Empty;

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when exactly one of
    /// <see cref="RecentRaceDistanceKm"/> and <see cref="RecentRaceTimeIso"/> is set.
    /// Both must be non-null (a full race result) or both must be null (no race result).
    /// Call this immediately after deserialization to catch half-populated payloads
    /// before they reach downstream calculations.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when only one of the two recent-race fields is populated.
    /// </exception>
    public void EnsureValid()
    {
        var hasDistance = RecentRaceDistanceKm.HasValue;
        var hasTime = RecentRaceTimeIso is not null;
        if (hasDistance != hasTime)
        {
            throw new InvalidOperationException(
                $"RecentRaceDistanceKm and RecentRaceTimeIso must both be set or both be null. " +
                $"Got: DistanceKm={RecentRaceDistanceKm?.ToString(CultureInfo.InvariantCulture) ?? "null"}, TimeIso={RecentRaceTimeIso ?? "null"}.");
        }
    }
}
