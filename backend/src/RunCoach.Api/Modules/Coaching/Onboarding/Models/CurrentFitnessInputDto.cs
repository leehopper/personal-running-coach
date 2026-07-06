using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Wire-input shape for the CurrentFitness topic on POST /api/v1/onboarding/answers. A loosened,
/// non-throwing counterpart to <see cref="CurrentFitnessAnswer"/> (see
/// <see cref="PrimaryGoalInputDto"/> for the rationale). Distances are canonical kilometers; the
/// form converts from the runner's chosen display unit before submitting (DEC-086).
/// </summary>
/// <param name="TypicalWeeklyKm">Typical weekly running distance in kilometers (validated &gt;= 0).</param>
/// <param name="LongestRecentRunKm">Longest single run in the past four weeks, in kilometers (validated &gt;= 0).</param>
/// <param name="RecentRaceDistanceKm">Optional recent race distance in kilometers (validated &gt;= 0 when present).</param>
/// <param name="RecentRaceTimeIso">Optional recent race time as an ISO-8601 duration (e.g. PT0H45M30S).</param>
/// <param name="Description">Optional runner-supplied free-text nuance for this topic.</param>
public sealed record CurrentFitnessInputDto(
    [property: JsonRequired] double TypicalWeeklyKm,
    [property: JsonRequired] double LongestRecentRunKm,
    double? RecentRaceDistanceKm,
    string? RecentRaceTimeIso,
    string? Description);
