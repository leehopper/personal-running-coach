using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Normalized answer for the CurrentFitness topic. Captures the runner's recent training volume,
/// longest recent run, and an optional recent race result that drives pace-zone calculations.
/// </summary>
public sealed record CurrentFitnessAnswer
{
    private readonly double _typicalWeeklyKm;
    private readonly double _longestRecentRunKm;
    private readonly double? _recentRaceDistanceKm;

    /// <summary>
    /// Gets the typical weekly running distance in kilometers over the past four weeks.
    /// </summary>
    [Description("Typical weekly running distance in kilometers over the past four weeks.")]
    public required double TypicalWeeklyKm
    {
        get => _typicalWeeklyKm;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(TypicalWeeklyKm), value, "Must be greater than or equal to 0.");
            }

            _typicalWeeklyKm = value;
        }
    }

    /// <summary>
    /// Gets the longest single run completed in the past four weeks, in kilometers.
    /// </summary>
    [Description("Longest single run completed in the past four weeks, in kilometers.")]
    public required double LongestRecentRunKm
    {
        get => _longestRecentRunKm;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(LongestRecentRunKm), value, "Must be greater than or equal to 0.");
            }

            _longestRecentRunKm = value;
        }
    }

    /// <summary>
    /// Gets an optional recent race distance in kilometers when the runner has one to share.
    /// </summary>
    [Description("Optional recent race distance in kilometers. Null if the runner has no recent race result.")]
    public required double? RecentRaceDistanceKm
    {
        get => _recentRaceDistanceKm;
        init
        {
            if (value.HasValue && value.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(RecentRaceDistanceKm), value.Value, "Must be greater than or equal to 0 when non-null.");
            }

            _recentRaceDistanceKm = value;
        }
    }

    /// <summary>
    /// Gets the optional recent race time as an ISO-8601 duration (e.g. PT0H45M30S).
    /// </summary>
    [Description("Optional recent race time as ISO-8601 duration (e.g. PT0H45M30S). Null if the runner has no recent race result.")]
    public required string? RecentRaceTimeIso { get; init; }

    /// <summary>
    /// Gets the runner's self-reported fitness summary in their own words.
    /// </summary>
    [Description("Self-reported fitness summary in the runner's own words.")]
    public required string Description { get; init; }
}
