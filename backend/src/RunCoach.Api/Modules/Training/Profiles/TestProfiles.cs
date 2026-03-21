using System.Collections.Immutable;
using System.Globalization;
using RunCoach.Api.Modules.Training.Computations;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Profiles;

/// <summary>
/// All 5 test user profiles for POC 1. These are structured fixtures used by
/// the context assembler, eval suite, and console app. Each profile includes
/// a UserProfile, GoalState (with FitnessEstimate and TrainingPaces computed
/// from the deterministic calculators), and simulated training history.
/// </summary>
public static class TestProfiles
{
    private static readonly VdotCalculator VdotCalc = new();
    private static readonly PaceCalculator PaceCalc = new();

    /// <summary>
    /// Gets all 5 test profiles keyed by lowercase name.
    /// </summary>
    public static IReadOnlyDictionary<string, TestProfile> All => new Dictionary<string, TestProfile>(StringComparer.OrdinalIgnoreCase)
    {
        ["sarah"] = Sarah(),
        ["lee"] = Lee(),
        ["maria"] = Maria(),
        ["james"] = James(),
        ["priya"] = Priya(),
    };

    /// <summary>
    /// Beginner: 28F, 6 months experience, 15km/week, no race history,
    /// goal: first 5K in 8 weeks, no training history.
    /// </summary>
    public static TestProfile Sarah()
    {
        var userId = new Guid("00000000-0000-0000-0000-000000000001");
        var now = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(now);

        var profile = new UserProfile(
            UserId: userId,
            Name: "Sarah",
            Age: 28,
            Gender: "Female",
            WeightKg: 62m,
            HeightCm: 165m,
            RestingHeartRateAvg: 72,
            MaxHeartRate: null,
            RunningExperienceYears: 0.5m,
            CurrentWeeklyDistanceKm: 15m,
            CurrentLongRunKm: 5m,
            RecentRaceTimes: ImmutableArray<RaceTime>.Empty,
            InjuryHistory: ImmutableArray<InjuryNote>.Empty,
            Preferences: new UserPreferences(
                PreferredRunDays: [DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday],
                LongRunDay: DayOfWeek.Saturday,
                MaxRunDaysPerWeek: 3,
                PreferredUnits: "metric",
                AvailableTimePerRunMinutes: 45,
                Constraints: ImmutableArray<string>.Empty),
            CreatedOn: now,
            ModifiedOn: now);

        // No race history -> no VDOT, estimated paces based on beginner defaults.
        // Use estimated max HR as fallback.
        var estimatedMaxHr = PaceCalc.EstimateMaxHr(profile.Age);

        var fitnessEstimate = new FitnessEstimate(
            EstimatedVdot: null,
            TrainingPaces: new TrainingPaces(
                EasyPaceRange: new PaceRange(
                    MinPerKm: TimeSpan.FromSeconds(420),
                    MaxPerKm: TimeSpan.FromSeconds(480)),
                MarathonPace: null,
                ThresholdPace: null,
                IntervalPace: null,
                RepetitionPace: null),
            FitnessLevel: "Beginner",
            AssessmentBasis: $"No race history. Estimated max HR: {estimatedMaxHr} bpm (220-age). Easy pace estimated from current training volume.",
            AssessedOn: today);

        var goalState = new GoalState(
            GoalType: "RaceGoal",
            TargetRace: new RaceGoal(
                RaceName: "Local Parkrun 5K",
                Distance: "5K",
                RaceDate: today.AddDays(56),
                TargetTime: null,
                Priority: "Primary"),
            CurrentFitnessEstimate: fitnessEstimate);

        return new TestProfile(profile, goalState, ImmutableArray<WorkoutSummary>.Empty);
    }

    /// <summary>
    /// Intermediate: 34M, 3 years experience, 40km/week, recent 10K at 48:00,
    /// goal: sub-1:45 HM in 16 weeks, 3 weeks training history.
    /// </summary>
    public static TestProfile Lee()
    {
        var userId = new Guid("00000000-0000-0000-0000-000000000002");
        var now = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(now);

        var raceTime = new RaceTime("10K", TimeSpan.FromMinutes(48), new DateOnly(2026, 2, 15), "Flat course, mild weather");
        var vdot = VdotCalc.CalculateVdot(raceTime)!.Value;
        var paces = PaceCalc.CalculatePaces(vdot);

        var profile = new UserProfile(
            UserId: userId,
            Name: "Lee",
            Age: 34,
            Gender: "Male",
            WeightKg: 75m,
            HeightCm: 178m,
            RestingHeartRateAvg: 58,
            MaxHeartRate: 186,
            RunningExperienceYears: 3m,
            CurrentWeeklyDistanceKm: 40m,
            CurrentLongRunKm: 14m,
            RecentRaceTimes: [raceTime],
            InjuryHistory: [
                new InjuryNote("Mild IT band tightness", new DateOnly(2025, 9, 1), "Recovered"),
            ],
            Preferences: new UserPreferences(
                PreferredRunDays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday],
                LongRunDay: DayOfWeek.Sunday,
                MaxRunDaysPerWeek: 5,
                PreferredUnits: "metric",
                AvailableTimePerRunMinutes: 75,
                Constraints: ImmutableArray<string>.Empty),
            CreatedOn: now,
            ModifiedOn: now);

        var fitnessEstimate = new FitnessEstimate(
            EstimatedVdot: vdot,
            TrainingPaces: paces,
            FitnessLevel: "Intermediate",
            AssessmentBasis: $"10K race time of 48:00 (2026-02-15) -> VDOT {vdot}",
            AssessedOn: today);

        var goalState = new GoalState(
            GoalType: "RaceGoal",
            TargetRace: new RaceGoal(
                RaceName: "Spring Half Marathon",
                Distance: "Half-Marathon",
                RaceDate: today.AddDays(112),
                TargetTime: TimeSpan.FromMinutes(105),
                Priority: "Primary"),
            CurrentFitnessEstimate: fitnessEstimate);

        var trainingHistory = BuildLeeTrainingHistory(today, paces);

        return new TestProfile(profile, goalState, trainingHistory);
    }

    /// <summary>
    /// Advanced goalless: 42F, 10+ years, 55km/week, multiple marathon times,
    /// no current race goal (maintenance), 4 weeks training history.
    /// </summary>
    public static TestProfile Maria()
    {
        var userId = new Guid("00000000-0000-0000-0000-000000000003");
        var now = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(now);

        var raceTimes = ImmutableArray.Create(
            new RaceTime("Marathon", new TimeSpan(3, 28, 0), new DateOnly(2025, 10, 12), "Chicago Marathon, warm day"),
            new RaceTime("Half-Marathon", new TimeSpan(1, 38, 0), new DateOnly(2025, 6, 8), "Flat course, ideal conditions"),
            new RaceTime("Marathon", new TimeSpan(3, 22, 0), new DateOnly(2024, 4, 15), "Boston, hilly, cool"));

        var vdot = VdotCalc.CalculateVdot(raceTimes)!.Value;
        var paces = PaceCalc.CalculatePaces(vdot);

        var profile = new UserProfile(
            UserId: userId,
            Name: "Maria",
            Age: 42,
            Gender: "Female",
            WeightKg: 57m,
            HeightCm: 163m,
            RestingHeartRateAvg: 50,
            MaxHeartRate: 178,
            RunningExperienceYears: 12m,
            CurrentWeeklyDistanceKm: 55m,
            CurrentLongRunKm: 20m,
            RecentRaceTimes: raceTimes,
            InjuryHistory: [
                new InjuryNote("Achilles tendinitis (left)", new DateOnly(2023, 3, 1), "Recovered"),
            ],
            Preferences: new UserPreferences(
                PreferredRunDays: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday],
                LongRunDay: DayOfWeek.Sunday,
                MaxRunDaysPerWeek: 6,
                PreferredUnits: "metric",
                AvailableTimePerRunMinutes: 120,
                Constraints: ImmutableArray<string>.Empty),
            CreatedOn: now,
            ModifiedOn: now);

        var fitnessEstimate = new FitnessEstimate(
            EstimatedVdot: vdot,
            TrainingPaces: paces,
            FitnessLevel: "Advanced",
            AssessmentBasis: $"Best VDOT from 3 race results -> VDOT {vdot}",
            AssessedOn: today);

        var goalState = new GoalState(
            GoalType: "Maintenance",
            TargetRace: null,
            CurrentFitnessEstimate: fitnessEstimate);

        var trainingHistory = BuildMariaTrainingHistory(today, paces);

        return new TestProfile(profile, goalState, trainingHistory);
    }

    /// <summary>
    /// Return from injury: 38M, intermediate, recovering from plantar fasciitis,
    /// cleared for 20 min easy only, 2 weeks limited training history.
    /// </summary>
    public static TestProfile James()
    {
        var userId = new Guid("00000000-0000-0000-0000-000000000004");
        var now = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(now);

        // Pre-injury race time for VDOT estimation (before injury).
        var raceTime = new RaceTime("10K", new TimeSpan(0, 44, 0), new DateOnly(2025, 9, 20), "Pre-injury personal best");
        var vdot = VdotCalc.CalculateVdot(raceTime)!.Value;
        var paces = PaceCalc.CalculatePaces(vdot);

        var profile = new UserProfile(
            UserId: userId,
            Name: "James",
            Age: 38,
            Gender: "Male",
            WeightKg: 80m,
            HeightCm: 183m,
            RestingHeartRateAvg: 62,
            MaxHeartRate: null,
            RunningExperienceYears: 5m,
            CurrentWeeklyDistanceKm: 10m,
            CurrentLongRunKm: null,
            RecentRaceTimes: [raceTime],
            InjuryHistory: [
                new InjuryNote("Plantar fasciitis (right foot)", new DateOnly(2025, 12, 1), "Active"),
                new InjuryNote("Runner's knee (left)", new DateOnly(2024, 6, 15), "Recovered"),
            ],
            Preferences: new UserPreferences(
                PreferredRunDays: [DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday],
                LongRunDay: DayOfWeek.Saturday,
                MaxRunDaysPerWeek: 3,
                PreferredUnits: "metric",
                AvailableTimePerRunMinutes: 20,
                Constraints: [
                    "Cleared by physiotherapist for 20 minutes easy running only",
                    "No speed work until cleared",
                    "Must include walking breaks if any foot pain",
                ]),
            CreatedOn: now,
            ModifiedOn: now);

        var fitnessEstimate = new FitnessEstimate(
            EstimatedVdot: vdot,
            TrainingPaces: paces,
            FitnessLevel: "Intermediate (returning from injury)",
            AssessmentBasis: $"Pre-injury 10K time of 44:00 (2025-09-20) -> VDOT {vdot}. Current fitness likely lower due to injury layoff.",
            AssessedOn: today);

        var goalState = new GoalState(
            GoalType: "ReturnFromInjury",
            TargetRace: null,
            CurrentFitnessEstimate: fitnessEstimate);

        var trainingHistory = BuildJamesTrainingHistory(today, paces);

        return new TestProfile(profile, goalState, trainingHistory);
    }

    /// <summary>
    /// Constrained: 30F, advanced, 60km/week, marathon training,
    /// 4 days/week max, never before 7am, 3 weeks training history.
    /// </summary>
    public static TestProfile Priya()
    {
        var userId = new Guid("00000000-0000-0000-0000-000000000005");
        var now = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(now);

        var raceTimes = ImmutableArray.Create(
            new RaceTime("Half-Marathon", new TimeSpan(1, 32, 0), new DateOnly(2025, 11, 3), "PB, flat course"),
            new RaceTime("10K", TimeSpan.Parse("00:42:30", CultureInfo.InvariantCulture), new DateOnly(2025, 8, 10), null));

        var vdot = VdotCalc.CalculateVdot(raceTimes)!.Value;
        var paces = PaceCalc.CalculatePaces(vdot);

        var profile = new UserProfile(
            UserId: userId,
            Name: "Priya",
            Age: 30,
            Gender: "Female",
            WeightKg: 55m,
            HeightCm: 160m,
            RestingHeartRateAvg: 52,
            MaxHeartRate: 190,
            RunningExperienceYears: 7m,
            CurrentWeeklyDistanceKm: 60m,
            CurrentLongRunKm: 24m,
            RecentRaceTimes: raceTimes,
            InjuryHistory: ImmutableArray<InjuryNote>.Empty,
            Preferences: new UserPreferences(
                PreferredRunDays: [DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday, DayOfWeek.Sunday],
                LongRunDay: DayOfWeek.Sunday,
                MaxRunDaysPerWeek: 4,
                PreferredUnits: "metric",
                AvailableTimePerRunMinutes: 90,
                Constraints: [
                    "Maximum 4 run days per week due to work schedule",
                    "Never before 7:00 AM (childcare responsibilities)",
                ]),
            CreatedOn: now,
            ModifiedOn: now);

        var fitnessEstimate = new FitnessEstimate(
            EstimatedVdot: vdot,
            TrainingPaces: paces,
            FitnessLevel: "Advanced",
            AssessmentBasis: $"Best VDOT from HM 1:32:00 and 10K 42:30 -> VDOT {vdot}",
            AssessedOn: today);

        var goalState = new GoalState(
            GoalType: "RaceGoal",
            TargetRace: new RaceGoal(
                RaceName: "Autumn Marathon",
                Distance: "Marathon",
                RaceDate: today.AddDays(168),
                TargetTime: TimeSpan.FromMinutes(195),
                Priority: "Primary"),
            CurrentFitnessEstimate: fitnessEstimate);

        var trainingHistory = BuildPriyaTrainingHistory(today, paces);

        return new TestProfile(profile, goalState, trainingHistory);
    }

    /// <summary>
    /// Builds 3 weeks of simulated training history for Lee (intermediate runner).
    /// Mix of easy runs, one tempo, one long run per week.
    /// </summary>
    private static ImmutableArray<WorkoutSummary> BuildLeeTrainingHistory(DateOnly today, TrainingPaces paces)
    {
        var easyPace = AveragePace(paces.EasyPaceRange);
        var builder = ImmutableArray.CreateBuilder<WorkoutSummary>();

        for (var week = 3; week >= 1; week--)
        {
            var weekStart = today.AddDays(-7 * week);

            // Monday: easy run 7km
            builder.Add(new WorkoutSummary(
                weekStart,
                "Easy",
                7m,
                (int)(7m * (decimal)easyPace.TotalMinutes),
                easyPace,
                null));

            // Wednesday: tempo run 8km (at threshold pace for the work portion)
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(2),
                "Tempo",
                8m,
                (int)(8m * (decimal)paces.ThresholdPace!.Value.TotalMinutes),
                paces.ThresholdPace!.Value,
                "2km warm-up, 4km at tempo, 2km cool-down"));

            // Friday: easy run 6km
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(4),
                "Easy",
                6m,
                (int)(6m * (decimal)easyPace.TotalMinutes),
                easyPace,
                null));

            // Sunday: long run (increasing: 12, 13, 14 km)
            var longRunKm = 12m + (3 - week);
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(6),
                "LongRun",
                longRunKm,
                (int)(longRunKm * (decimal)paces.EasyPaceRange.MaxPerKm.TotalMinutes),
                paces.EasyPaceRange.MaxPerKm,
                null));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Builds 4 weeks of simulated training history for Maria (advanced, maintenance).
    /// Varied sessions: easy, tempo, intervals, long run, easy recovery.
    /// </summary>
    private static ImmutableArray<WorkoutSummary> BuildMariaTrainingHistory(DateOnly today, TrainingPaces paces)
    {
        var easyPace = AveragePace(paces.EasyPaceRange);
        var builder = ImmutableArray.CreateBuilder<WorkoutSummary>();

        for (var week = 4; week >= 1; week--)
        {
            var weekStart = today.AddDays(-7 * week);

            // Monday: easy run 8km
            builder.Add(new WorkoutSummary(
                weekStart,
                "Easy",
                8m,
                (int)(8m * (decimal)easyPace.TotalMinutes),
                easyPace,
                null));

            // Tuesday: intervals 10km total
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(1),
                "Intervals",
                10m,
                (int)((10m * (decimal)easyPace.TotalMinutes) * 0.85m),
                paces.IntervalPace!.Value,
                "2km warm-up, 5x1km at interval pace with 400m jog, 1.6km cool-down"));

            // Thursday: tempo 12km
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(3),
                "Tempo",
                12m,
                (int)(12m * (decimal)paces.ThresholdPace!.Value.TotalMinutes),
                paces.ThresholdPace!.Value,
                "2km warm-up, 8km at tempo, 2km cool-down"));

            // Friday: easy recovery 6km
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(4),
                "Easy",
                6m,
                (int)(6m * (decimal)paces.EasyPaceRange.MaxPerKm.TotalMinutes),
                paces.EasyPaceRange.MaxPerKm,
                "Recovery run"));

            // Saturday: easy 8km
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(5),
                "Easy",
                8m,
                (int)(8m * (decimal)easyPace.TotalMinutes),
                easyPace,
                null));

            // Sunday: long run (18-21km progressive)
            var longRunKm = 18m + (4 - week);
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(6),
                "LongRun",
                longRunKm,
                (int)(longRunKm * (decimal)paces.EasyPaceRange.MaxPerKm.TotalMinutes),
                paces.EasyPaceRange.MaxPerKm,
                null));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Builds 2 weeks of limited training history for James (returning from injury).
    /// Easy runs only, 20 minutes max, low volume.
    /// </summary>
    private static ImmutableArray<WorkoutSummary> BuildJamesTrainingHistory(DateOnly today, TrainingPaces paces)
    {
        // James is limited to easy pace only, 20 min max.
        var easyPace = paces.EasyPaceRange.MaxPerKm;
        var builder = ImmutableArray.CreateBuilder<WorkoutSummary>();

        for (var week = 2; week >= 1; week--)
        {
            var weekStart = today.AddDays(-7 * week);

            // Tuesday: 15 min easy run/walk
            var distKm1 = Math.Round(15m / (decimal)easyPace.TotalMinutes, 1);
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(1),
                "Easy",
                distKm1,
                15,
                easyPace,
                "Run/walk, foot felt OK"));

            // Thursday: 18 min easy run
            var distKm2 = Math.Round(18m / (decimal)easyPace.TotalMinutes, 1);
            var thursdayNotes = week == 2 ? "Some foot stiffness first 5 min, resolved" : "No foot pain";
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(3),
                "Easy",
                distKm2,
                18,
                easyPace,
                thursdayNotes));

            // Saturday: 20 min easy run
            var distKm3 = Math.Round(20m / (decimal)easyPace.TotalMinutes, 1);
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(5),
                "Easy",
                distKm3,
                20,
                easyPace,
                null));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Builds 3 weeks of simulated training history for Priya (constrained, 4 days/week).
    /// Mix of easy, tempo, intervals/marathon pace, and long run. Exactly 4 runs per week.
    /// </summary>
    private static ImmutableArray<WorkoutSummary> BuildPriyaTrainingHistory(DateOnly today, TrainingPaces paces)
    {
        var easyPace = AveragePace(paces.EasyPaceRange);
        var builder = ImmutableArray.CreateBuilder<WorkoutSummary>();

        for (var week = 3; week >= 1; week--)
        {
            var weekStart = today.AddDays(-7 * week);

            // Tuesday: easy run 10km
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(1),
                "Easy",
                10m,
                (int)(10m * (decimal)easyPace.TotalMinutes),
                easyPace,
                null));

            // Thursday: tempo 12km
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(3),
                "Tempo",
                12m,
                (int)(12m * (decimal)paces.ThresholdPace!.Value.TotalMinutes),
                paces.ThresholdPace!.Value,
                "3km warm-up, 6km at tempo, 3km cool-down"));

            // Saturday: intervals or marathon pace 14km (alternating weeks)
            var isIntervalWeek = week % 2 == 0;
            var satWorkoutType = isIntervalWeek ? "Intervals" : "MarathonPace";
            var satPace = isIntervalWeek ? paces.IntervalPace!.Value : paces.MarathonPace!.Value;
            var satNotes = isIntervalWeek
                ? "3km warm-up, 6x1km at interval pace with 400m jog, 2km cool-down"
                : "3km warm-up, 8km at marathon pace, 3km cool-down";
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(5),
                satWorkoutType,
                14m,
                (int)(14m * (decimal)satPace.TotalMinutes),
                satPace,
                satNotes));

            // Sunday: long run (22-26km progressive)
            var longRunKm = 22m + ((3 - week) * 2);
            builder.Add(new WorkoutSummary(
                weekStart.AddDays(6),
                "LongRun",
                longRunKm,
                (int)(longRunKm * (decimal)paces.EasyPaceRange.MaxPerKm.TotalMinutes),
                paces.EasyPaceRange.MaxPerKm,
                null));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Computes the average pace from a PaceRange (midpoint between min and max per km).
    /// </summary>
    private static TimeSpan AveragePace(PaceRange range)
    {
        var avgSeconds = (range.MinPerKm.TotalSeconds + range.MaxPerKm.TotalSeconds) / 2.0;
        return TimeSpan.FromSeconds(avgSeconds);
    }
}
