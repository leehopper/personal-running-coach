using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// A runner's profile containing biographical data, fitness indicators,
/// race history, injury history, and training preferences.
/// </summary>
public sealed record UserProfile(
    Guid UserId,
    string Name,
    int Age,
    string Gender,
    decimal? WeightKg,
    decimal? HeightCm,
    int? RestingHeartRateAvg,
    int? MaxHeartRate,
    decimal RunningExperienceYears,
    decimal CurrentWeeklyDistanceKm,
    decimal? CurrentLongRunKm,
    ImmutableArray<RaceTime> RecentRaceTimes,
    ImmutableArray<InjuryNote> InjuryHistory,
    UserPreferences Preferences,
    DateTime CreatedOn,
    DateTime ModifiedOn);
