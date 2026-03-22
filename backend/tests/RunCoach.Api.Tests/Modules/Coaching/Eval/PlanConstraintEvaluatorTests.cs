using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Unit tests for <see cref="PlanConstraintEvaluator"/>.
/// Deterministic — no API calls needed.
/// </summary>
public class PlanConstraintEvaluatorTests
{
    [Fact]
    public void Evaluate_ValidPlan_ReturnsNoViolations()
    {
        // Arrange
        var context = new PlanConstraintContext
        {
            MacroPlan = BuildValidMacroPlan(),
            MesoWeek = BuildValidMesoWeek(weeklyKm: 40),
            CurrentWeeklyKm = 40,
        };

        // Act
        var violations = PlanConstraintEvaluator.Evaluate(context);

        // Assert
        violations.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_MacroPlanTooShort_ReturnsViolation()
    {
        // Arrange
        var context = new PlanConstraintContext
        {
            MacroPlan = BuildValidMacroPlan() with { TotalWeeks = 2 },
        };

        // Act
        var violations = PlanConstraintEvaluator.Evaluate(context);

        // Assert
        violations.Should().ContainSingle().Which.Should().Contain("TotalWeeks");
    }

    [Fact]
    public void Evaluate_VolumeExceeds10Percent_ReturnsViolation()
    {
        // Arrange
        var context = new PlanConstraintContext
        {
            MesoWeek = BuildValidMesoWeek(weeklyKm: 50),
            CurrentWeeklyKm = 40,
        };

        // Act
        var violations = PlanConstraintEvaluator.Evaluate(context);

        // Assert
        violations.Should().ContainSingle().Which.Should().Contain("10%");
    }

    [Fact]
    public void Evaluate_BeginnerInsufficientRest_ReturnsViolation()
    {
        // Arrange — only 1 rest day for a beginner
        var days = Enumerable.Range(0, 7).Select(d => new MesoDayOutput
        {
            DayOfWeek = d,
            SlotType = d == 0 ? DaySlotType.Rest : DaySlotType.Run,
            WorkoutType = d == 0 ? null : WorkoutType.Easy,
            Notes = "test",
        }).ToArray();

        var context = new PlanConstraintContext
        {
            MesoWeek = new MesoWeekOutput
            {
                WeekNumber = 1,
                PhaseType = PhaseType.Base,
                WeeklyTargetKm = 20,
                IsDeloadWeek = false,
                Days = days,
                WeekSummary = "test",
            },
            IsBeginnerProfile = true,
        };

        // Act
        var violations = PlanConstraintEvaluator.Evaluate(context);

        // Assert
        violations.Should().Contain(v => v.Contains("rest day"));
    }

    [Fact]
    public void Evaluate_BeginnerAssignedIntervals_ReturnsViolation()
    {
        // Arrange
        var workouts = new[]
        {
            BuildWorkout(WorkoutType.Easy, "Easy Run"),
            BuildWorkout(WorkoutType.Interval, "Speed Session"),
        };

        var context = new PlanConstraintContext
        {
            Workouts = workouts,
            IsBeginnerProfile = true,
        };

        // Act
        var violations = PlanConstraintEvaluator.Evaluate(context);

        // Assert
        violations.Should().ContainSingle().Which.Should().Contain("Interval");
    }

    [Fact]
    public void Evaluate_InjuredDurationExceeds20Min_ReturnsViolation()
    {
        // Arrange
        var workouts = new[] { BuildWorkout(WorkoutType.Easy, "Easy Run") with { TargetDurationMinutes = 30 } };

        var context = new PlanConstraintContext
        {
            Workouts = workouts,
            IsInjuredProfile = true,
        };

        // Act
        var violations = PlanConstraintEvaluator.Evaluate(context);

        // Assert
        violations.Should().Contain(v => v.Contains("20min"));
    }

    [Fact]
    public void Evaluate_InjuredNonEasyWorkout_ReturnsViolation()
    {
        // Arrange
        var workouts = new[] { BuildWorkout(WorkoutType.Tempo, "Tempo Run") with { TargetDurationMinutes = 15 } };

        var context = new PlanConstraintContext
        {
            Workouts = workouts,
            IsInjuredProfile = true,
        };

        // Act
        var violations = PlanConstraintEvaluator.Evaluate(context);

        // Assert
        violations.Should().Contain(v => v.Contains("Tempo"));
    }

    [Fact]
    public void Evaluate_EasyPaceOutOfRange_ReturnsViolation()
    {
        // Arrange — easy pace is 200s/km but VDOT zone is 300-360s/km
        var paces = new TrainingPaces(
            new PaceRange(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(360)),
            MarathonPace: TimeSpan.FromSeconds(270),
            ThresholdPace: TimeSpan.FromSeconds(240),
            IntervalPace: TimeSpan.FromSeconds(210),
            RepetitionPace: TimeSpan.FromSeconds(195));

        var workouts = new[] { BuildWorkout(WorkoutType.Easy, "Easy Run") with { TargetPaceEasySecPerKm = 200 } };

        var context = new PlanConstraintContext
        {
            Workouts = workouts,
            TrainingPaces = paces,
        };

        // Act
        var violations = PlanConstraintEvaluator.Evaluate(context);

        // Assert
        violations.Should().ContainSingle().Which.Should().Contain("easy pace");
    }

    [Fact]
    public void Evaluate_FastPaceFasterThanRepFloor_ReturnsViolation()
    {
        // Arrange — fast pace 150s/km but rep pace is 195s/km (floor at 90% = 175s/km)
        var paces = new TrainingPaces(
            new PaceRange(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(360)),
            MarathonPace: TimeSpan.FromSeconds(270),
            ThresholdPace: TimeSpan.FromSeconds(240),
            IntervalPace: TimeSpan.FromSeconds(210),
            RepetitionPace: TimeSpan.FromSeconds(195));

        var workouts = new[] { BuildWorkout(WorkoutType.Interval, "Intervals") with { TargetPaceFastSecPerKm = 150 } };

        var context = new PlanConstraintContext
        {
            Workouts = workouts,
            TrainingPaces = paces,
        };

        // Act
        var violations = PlanConstraintEvaluator.Evaluate(context);

        // Assert
        violations.Should().Contain(v => v.Contains("fast pace"));
    }

    private static MacroPlanOutput BuildValidMacroPlan()
    {
        return new MacroPlanOutput
        {
            TotalWeeks = 12,
            GoalDescription = "Half Marathon",
            Phases =
            [
                new PlanPhaseOutput
                {
                    PhaseType = PhaseType.Base,
                    Weeks = 4,
                    WeeklyDistanceStartKm = 30,
                    WeeklyDistanceEndKm = 40,
                    IntensityDistribution = "80/20 easy/hard",
                    AllowedWorkoutTypes = [WorkoutType.Easy, WorkoutType.LongRun],
                    TargetPaceEasySecPerKm = 330,
                    TargetPaceFastSecPerKm = 0,
                    Notes = "Build aerobic base.",
                    IncludesDeload = false,
                },
            ],
            Rationale = "Standard periodization.",
            Warnings = "None.",
        };
    }

    private static MesoWeekOutput BuildValidMesoWeek(int weeklyKm)
    {
        var days = new[]
        {
            new MesoDayOutput { DayOfWeek = 0, SlotType = DaySlotType.Rest, Notes = "Rest" },
            new MesoDayOutput { DayOfWeek = 1, SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Easy, Notes = "Easy" },
            new MesoDayOutput { DayOfWeek = 2, SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Easy, Notes = "Easy" },
            new MesoDayOutput { DayOfWeek = 3, SlotType = DaySlotType.Rest, Notes = "Rest" },
            new MesoDayOutput { DayOfWeek = 4, SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Tempo, Notes = "Tempo" },
            new MesoDayOutput { DayOfWeek = 5, SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Easy, Notes = "Easy" },
            new MesoDayOutput { DayOfWeek = 6, SlotType = DaySlotType.Run, WorkoutType = WorkoutType.LongRun, Notes = "Long" },
        };

        return new MesoWeekOutput
        {
            WeekNumber = 1,
            PhaseType = PhaseType.Base,
            WeeklyTargetKm = weeklyKm,
            IsDeloadWeek = false,
            Days = days,
            WeekSummary = "Test week.",
        };
    }

    private static WorkoutOutput BuildWorkout(WorkoutType type, string title)
    {
        return new WorkoutOutput
        {
            DayOfWeek = 1,
            WorkoutType = type,
            Title = title,
            TargetDistanceKm = 8,
            TargetDurationMinutes = 45,
            TargetPaceEasySecPerKm = 330,
            TargetPaceFastSecPerKm = 0,
            Segments = [],
            WarmupNotes = "5 min jog",
            CooldownNotes = "5 min walk",
            CoachingNotes = "Keep it easy.",
            PerceivedEffort = 4,
        };
    }
}
