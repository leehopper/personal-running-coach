using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Adaptation;

namespace RunCoach.Api.Tests.Modules.Training.Adaptation;

/// <summary>
/// Unit tests for <see cref="RestructureWorkoutResolver"/> (slice 3B F4): the shared
/// sparse-edit merge semantics — a present day (0..6) upserts, an absent day is
/// untouched, an out-of-range day is dropped, duplicates collapse last-wins — that
/// both <see cref="RestructureDiffCalculator"/> and the F4 consistency check resolve
/// through so they can never drift.
/// </summary>
public sealed class RestructureWorkoutResolverTests
{
    [Fact]
    public void IndexRevisedByDay_KeysWorkoutsByTheirDayOfWeek()
    {
        // Act
        var actual = RestructureWorkoutResolver.IndexRevisedByDay(
            [Workout(1, 5), Workout(3, 8)]);

        // Assert
        actual.Keys.Should().BeEquivalentTo([1, 3]);
        actual[1].TargetDistanceKm.Should().Be(5);
        actual[3].TargetDistanceKm.Should().Be(8);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void IndexRevisedByDay_DropsDaysOutsideZeroToSix(int malformedDay)
    {
        // Act
        var actual = RestructureWorkoutResolver.IndexRevisedByDay([Workout(malformedDay, 5)]);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void IndexRevisedByDay_CollapsesDuplicateDaysLastWins()
    {
        // Act — two edits target day 2; the later one wins.
        var actual = RestructureWorkoutResolver.IndexRevisedByDay(
            [Workout(2, 5), Workout(2, 9)]);

        // Assert
        actual.Should().ContainSingle();
        actual[2].TargetDistanceKm.Should().Be(9);
    }

    [Fact]
    public void ResolveResultingWorkouts_CarriesOverUntouchedExistingDays()
    {
        // Arrange — the live-pass shape: edit Mon/Wed/Sat, leave Thursday untouched.
        WorkoutOutput[] existing = [Workout(1, 7), Workout(3, 8), Workout(4, 6), Workout(6, 14)];
        WorkoutOutput[] revised = [Workout(1, 4), Workout(3, 8), Workout(6, 12)];

        // Act
        var actual = RestructureWorkoutResolver.ResolveResultingWorkouts(revised, existing);

        // Assert — the untouched Thursday (6 km) survives, so the week sums to 30.
        actual.Sum(workout => workout.TargetDistanceKm).Should().Be(30);
        actual.Should().Contain(workout => workout.DayOfWeek == 4 && workout.TargetDistanceKm == 6);
    }

    [Fact]
    public void ResolveResultingWorkouts_RevisedDayReplacesTheExistingOne()
    {
        // Arrange
        WorkoutOutput[] existing = [Workout(2, 10)];
        WorkoutOutput[] revised = [Workout(2, 4)];

        // Act
        var actual = RestructureWorkoutResolver.ResolveResultingWorkouts(revised, existing);

        // Assert — the revised distance wins; the old 10 km does not double-count.
        actual.Should().ContainSingle();
        actual[0].TargetDistanceKm.Should().Be(4);
    }

    [Fact]
    public void ResolveResultingWorkouts_AddsARevisedDayTheWeekNeverScheduled()
    {
        // Arrange — Thursday (day 4) had no existing workout.
        WorkoutOutput[] existing = [Workout(1, 7)];
        WorkoutOutput[] revised = [Workout(4, 5)];

        // Act
        var actual = RestructureWorkoutResolver.ResolveResultingWorkouts(revised, existing);

        // Assert
        actual.Sum(workout => workout.TargetDistanceKm).Should().Be(12);
        actual.Should().Contain(workout => workout.DayOfWeek == 4);
    }

    [Fact]
    public void ResolveResultingWorkouts_DropsOutOfRangeRevisedDays()
    {
        // Arrange
        WorkoutOutput[] existing = [Workout(1, 7)];
        WorkoutOutput[] revised = [Workout(9, 100)];

        // Act
        var actual = RestructureWorkoutResolver.ResolveResultingWorkouts(revised, existing);

        // Assert — the malformed day is ignored; only the existing week remains.
        actual.Should().ContainSingle();
        actual[0].DayOfWeek.Should().Be(1);
    }

    [Fact]
    public void ResolveResultingWorkouts_WithNoRevisions_ReturnsTheExistingWeek()
    {
        // Arrange
        WorkoutOutput[] existing = [Workout(1, 7), Workout(3, 8)];

        // Act
        var actual = RestructureWorkoutResolver.ResolveResultingWorkouts([], existing);

        // Assert
        actual.Sum(workout => workout.TargetDistanceKm).Should().Be(15);
    }

    private static WorkoutOutput Workout(int dayOfWeek, int targetDistanceKm) =>
        new()
        {
            DayOfWeek = dayOfWeek,
            WorkoutType = WorkoutType.Easy,
            Title = "Workout",
            TargetDistanceKm = targetDistanceKm,
            TargetDurationMinutes = 40,
            TargetPaceEasySecPerKm = 330,
            TargetPaceFastSecPerKm = 280,
            Segments = [],
            WarmupNotes = string.Empty,
            CooldownNotes = string.Empty,
            CoachingNotes = string.Empty,
            PerceivedEffort = 4,
        };
}
