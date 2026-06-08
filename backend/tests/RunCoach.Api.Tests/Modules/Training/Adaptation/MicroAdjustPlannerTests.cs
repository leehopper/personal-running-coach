using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Adaptation;

namespace RunCoach.Api.Tests.Modules.Training.Adaptation;

/// <summary>
/// Unit tests for <see cref="MicroAdjustPlanner"/> (Slice 3 PR2 / Unit 1): the
/// deterministic forward swap of a missed key workout that never stacks two key
/// workouts on consecutive days.
/// </summary>
public sealed class MicroAdjustPlannerTests
{
    [Fact]
    public void TryPlanSwap_MovesMissedKeyWorkoutForwardToNearestValidDay()
    {
        // Arrange — Monday's Tempo (key) is missed; Tue/Wed/Fri are easy days.
        var week = new[]
        {
            W(0, WorkoutType.Easy),
            W(1, WorkoutType.Tempo),
            W(2, WorkoutType.Easy),
            W(3, WorkoutType.Easy),
            W(4, WorkoutType.Interval),
            W(5, WorkoutType.Easy),
            W(6, WorkoutType.LongRun),
        };

        // Act
        var planned = MicroAdjustPlanner.TryPlanSwap(week, missedDayOfWeek: 1, weekNumber: 1, out var diff);

        // Assert — swapped forward to the nearest valid (non-stacking) day, Tuesday.
        planned.Should().BeTrue();
        diff.WorkoutChanges.Should().HaveCount(2);
        diff.WeeklyTargetChanges.Should().BeEmpty();
        MovedKeyWorkoutDay(diff).Should().Be(2, because: "Tuesday is the nearest forward day that does not stack key workouts");
        AssertNoConsecutiveKeyWorkouts(Apply(week, diff));
    }

    [Fact]
    public void TryPlanSwap_SkipsForwardDaysThatWouldStackKeyWorkouts()
    {
        // Arrange — Tuesday and Thursday are easy but flank Wednesday's Interval session,
        // so only Friday yields a non-stacking swap.
        var week = new[]
        {
            W(1, WorkoutType.Tempo),
            W(2, WorkoutType.Easy),
            W(3, WorkoutType.Interval),
            W(4, WorkoutType.Easy),
            W(5, WorkoutType.Easy),
        };

        // Act
        var planned = MicroAdjustPlanner.TryPlanSwap(week, missedDayOfWeek: 1, weekNumber: 1, out var diff);

        // Assert
        planned.Should().BeTrue();
        MovedKeyWorkoutDay(diff).Should().Be(5, because: "Tue and Thu would both stack against Wednesday's key workout");
        AssertNoConsecutiveKeyWorkouts(Apply(week, diff));
    }

    [Fact]
    public void TryPlanSwap_MovesKeyWorkoutForwardOnly()
    {
        // Arrange — the missed key workout is on Saturday; there is no later day to move it to.
        var week = new[]
        {
            W(2, WorkoutType.Easy),
            W(4, WorkoutType.Easy),
            W(6, WorkoutType.Tempo),
        };

        // Act
        var planned = MicroAdjustPlanner.TryPlanSwap(week, missedDayOfWeek: 6, weekNumber: 1, out var diff);

        // Assert — never reschedules backward; no forward slot means no deterministic swap.
        planned.Should().BeFalse();
        diff.WorkoutChanges.Should().BeEmpty();
    }

    [Fact]
    public void TryPlanSwap_MissedDayHoldsNonKeyWorkout_ReturnsFalse()
    {
        // Arrange — the missed day is an easy run; the micro-adjust swap only reschedules key workouts.
        var week = new[]
        {
            W(1, WorkoutType.Easy),
            W(3, WorkoutType.Tempo),
            W(5, WorkoutType.Easy),
        };

        // Act
        var planned = MicroAdjustPlanner.TryPlanSwap(week, missedDayOfWeek: 1, weekNumber: 1, out var diff);

        // Assert
        planned.Should().BeFalse();
        diff.WorkoutChanges.Should().BeEmpty();
    }

    [Fact]
    public void TryPlanSwap_MissedDayHasNoScheduledWorkout_ReturnsFalse()
    {
        // Arrange — there is no workout on the claimed missed day (a rest day).
        var week = new[]
        {
            W(1, WorkoutType.Tempo),
            W(3, WorkoutType.Easy),
        };

        // Act
        var planned = MicroAdjustPlanner.TryPlanSwap(week, missedDayOfWeek: 2, weekNumber: 1, out _);

        // Assert
        planned.Should().BeFalse();
    }

    [Fact]
    public void TryPlanSwap_EveryForwardDayWouldStack_ReturnsFalse()
    {
        // Arrange — both easy forward days are flanked by key workouts; no valid swap exists.
        var week = new[]
        {
            W(1, WorkoutType.Tempo),
            W(2, WorkoutType.Easy),
            W(3, WorkoutType.Interval),
            W(4, WorkoutType.Easy),
            W(5, WorkoutType.Interval),
            W(6, WorkoutType.Repetition),
        };

        // Act
        var planned = MicroAdjustPlanner.TryPlanSwap(week, missedDayOfWeek: 1, weekNumber: 1, out var diff);

        // Assert — orchestration escalates this micro-adjust to an L2 restructure.
        planned.Should().BeFalse();
        diff.WorkoutChanges.Should().BeEmpty();
    }

    private static int MovedKeyWorkoutDay(PlanAdaptationDiff diff) =>
        diff.WorkoutChanges
            .Single(change => change.After is { } after && WorkoutKind.IsKey(after.WorkoutType))
            .DayOfWeek;

    private static List<WorkoutOutput> Apply(IReadOnlyList<WorkoutOutput> week, PlanAdaptationDiff diff)
    {
        var byDay = week.ToDictionary(workout => workout.DayOfWeek);
        foreach (var change in diff.WorkoutChanges)
        {
            if (change.After is { } after)
            {
                byDay[change.DayOfWeek] = after;
            }
            else
            {
                byDay.Remove(change.DayOfWeek);
            }
        }

        return byDay.Values.OrderBy(workout => workout.DayOfWeek).ToList();
    }

    private static void AssertNoConsecutiveKeyWorkouts(IReadOnlyList<WorkoutOutput> week)
    {
        var keyDays = week
            .Where(workout => WorkoutKind.IsKey(workout.WorkoutType))
            .Select(workout => workout.DayOfWeek)
            .OrderBy(day => day)
            .ToList();

        for (var i = 1; i < keyDays.Count; i++)
        {
            (keyDays[i] - keyDays[i - 1]).Should().BeGreaterThan(
                1, because: "no two key workouts may land on consecutive days");
        }
    }

    private static WorkoutOutput W(int dayOfWeek, WorkoutType type) =>
        new()
        {
            DayOfWeek = dayOfWeek,
            WorkoutType = type,
            Title = type.ToString(),
            TargetDistanceKm = 10,
            TargetDurationMinutes = 50,
            TargetPaceEasySecPerKm = 330,
            TargetPaceFastSecPerKm = 280,
            Segments = Array.Empty<WorkoutSegmentOutput>(),
            WarmupNotes = string.Empty,
            CooldownNotes = string.Empty,
            CoachingNotes = string.Empty,
            PerceivedEffort = 5,
        };
}
