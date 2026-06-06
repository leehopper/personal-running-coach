using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Training.Workouts;

/// <summary>
/// Unit tests for the <see cref="WorkoutPrescriptionSnapshot.Create"/> validating
/// factory (slice-2b PR3). The factory exists so an inverted fast/slow pace pair
/// fails at the construction site rather than only when the computed
/// <see cref="WorkoutPrescriptionSnapshot.PrescribedPace"/> view is later read.
/// </summary>
public class WorkoutPrescriptionSnapshotTests
{
    [Fact]
    public void Create_WithInvertedPaceBounds_ThrowsAtConstruction()
    {
        // Arrange — fast = 330 s/km is SLOWER than slow = 280 s/km (inverted).
        var act = () => WorkoutPrescriptionSnapshot.Create(
            sourcePlanId: Guid.NewGuid(),
            weekNumber: 2,
            dayOfWeek: 4,
            workoutType: WorkoutType.Tempo,
            prescribedDistance: Distance.FromKilometers(10.0),
            prescribedDuration: Duration.FromMinutes(50.0),
            prescribedPaceFast: Pace.FromSecondsPerKm(330.0),
            prescribedPaceSlow: Pace.FromSecondsPerKm(280.0));

        // Assert — the guard fires at Create time, not at PrescribedPace read time.
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithOrderedPaceBounds_ExposesPrescribedPaceRange()
    {
        // Arrange / Act — fast = 280 s/km is faster than slow = 330 s/km (ordered).
        var actual = WorkoutPrescriptionSnapshot.Create(
            sourcePlanId: Guid.NewGuid(),
            weekNumber: 2,
            dayOfWeek: 4,
            workoutType: WorkoutType.Tempo,
            prescribedDistance: Distance.FromKilometers(10.0),
            prescribedDuration: Duration.FromMinutes(50.0),
            prescribedPaceFast: Pace.FromSecondsPerKm(280.0),
            prescribedPaceSlow: Pace.FromSecondsPerKm(330.0));

        // Assert — the computed PaceRange view is readable without throwing.
        actual.PrescribedPace.Fast.SecondsPerKm.Should().Be(280.0);
        actual.PrescribedPace.Slow.SecondsPerKm.Should().Be(330.0);
    }

    [Fact]
    public void Create_WithEqualPaceBounds_ExposesPrescribedPaceRange()
    {
        // Arrange / Act — fast == slow (300 s/km): the equality edge of the ordered
        // guard. The guard uses a strict IsSlowerThan, so equal bounds are valid and
        // a mistaken strict-< guard would regress here.
        var actual = WorkoutPrescriptionSnapshot.Create(
            sourcePlanId: Guid.NewGuid(),
            weekNumber: 2,
            dayOfWeek: 4,
            workoutType: WorkoutType.Tempo,
            prescribedDistance: Distance.FromKilometers(10.0),
            prescribedDuration: Duration.FromMinutes(50.0),
            prescribedPaceFast: Pace.FromSecondsPerKm(300.0),
            prescribedPaceSlow: Pace.FromSecondsPerKm(300.0));

        // Assert — equal bounds construct without throwing and expose the single pace.
        actual.PrescribedPace.Fast.SecondsPerKm.Should().Be(300.0);
        actual.PrescribedPace.Slow.SecondsPerKm.Should().Be(300.0);
    }
}
