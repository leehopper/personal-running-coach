using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// A runner's profile containing biographical data, fitness indicators,
/// race history, injury history, and training preferences.
/// </summary>
public sealed record UserProfile
{
    public UserProfile(
        Guid userId,
        string name,
        int age,
        string gender,
        decimal? weightKg,
        decimal? heightCm,
        int? restingHeartRateAvg,
        int? maxHeartRate,
        decimal runningExperienceYears,
        decimal currentWeeklyDistanceKm,
        decimal? currentLongRunKm,
        ImmutableArray<RaceTime> recentRaceTimes,
        ImmutableArray<InjuryNote> injuryHistory,
        UserPreferences preferences,
        DateTime createdOn,
        DateTime modifiedOn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(gender);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(age, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(age, 150);
        ArgumentOutOfRangeException.ThrowIfNegative(runningExperienceYears);
        ArgumentOutOfRangeException.ThrowIfNegative(currentWeeklyDistanceKm);

        if (weightKg.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(weightKg.Value, 0m);
        }

        if (heightCm.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(heightCm.Value, 0m);
        }

        if (restingHeartRateAvg.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(restingHeartRateAvg.Value, 0);
        }

        if (maxHeartRate.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxHeartRate.Value, 0);
        }

        if (currentLongRunKm.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(currentLongRunKm.Value, 0m);
        }

        ArgumentNullException.ThrowIfNull(preferences);

        UserId = userId;
        Name = name;
        Age = age;
        Gender = gender;
        WeightKg = weightKg;
        HeightCm = heightCm;
        RestingHeartRateAvg = restingHeartRateAvg;
        MaxHeartRate = maxHeartRate;
        RunningExperienceYears = runningExperienceYears;
        CurrentWeeklyDistanceKm = currentWeeklyDistanceKm;
        CurrentLongRunKm = currentLongRunKm;
        RecentRaceTimes = recentRaceTimes;
        InjuryHistory = injuryHistory;
        Preferences = preferences;
        CreatedOn = createdOn;
        ModifiedOn = modifiedOn;
    }

    public Guid UserId { get; init; }

    public string Name { get; init; }

    public int Age { get; init; }

    public string Gender { get; init; }

    public decimal? WeightKg { get; init; }

    public decimal? HeightCm { get; init; }

    public int? RestingHeartRateAvg { get; init; }

    public int? MaxHeartRate { get; init; }

    public decimal RunningExperienceYears { get; init; }

    public decimal CurrentWeeklyDistanceKm { get; init; }

    public decimal? CurrentLongRunKm { get; init; }

    public ImmutableArray<RaceTime> RecentRaceTimes { get; init; }

    public ImmutableArray<InjuryNote> InjuryHistory { get; init; }

    public UserPreferences Preferences { get; init; }

    public DateTime CreatedOn { get; init; }

    public DateTime ModifiedOn { get; init; }
}
