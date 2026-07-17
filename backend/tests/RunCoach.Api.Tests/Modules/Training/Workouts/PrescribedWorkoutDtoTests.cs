using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Training.Workouts;

/// <summary>
/// Unit coverage for <see cref="PrescribedWorkoutDto.FromSnapshot"/> — the
/// server-authoritative projection of a resolved
/// <see cref="WorkoutPrescriptionSnapshot"/> onto the <c>/log</c> prescribed-banner
/// wire summary. Guards the field mapping (notably that the fast/easy pace
/// bounds are not transposed) and the off-plan <c>null</c> branch.
/// </summary>
public class PrescribedWorkoutDtoTests
{
    [Fact]
    public void FromSnapshot_MapsEveryField_AndKeepsTheFastBoundFasterThanEasy()
    {
        // Arrange — distinct fast/easy bounds (fast = fewer sec/km) so a swapped
        // PrescribedPaceFast/PrescribedPaceSlow mapping would flip the asserted values.
        var snapshot = WorkoutPrescriptionSnapshot.Create(
            sourcePlanId: Guid.NewGuid(),
            weekNumber: 3,
            dayOfWeek: 2,
            workoutType: WorkoutType.Tempo,
            prescribedDistance: Distance.FromKilometers(10),
            prescribedDuration: Duration.FromMinutes(45),
            prescribedPaceFast: Pace.FromSecondsPerKm(240),
            prescribedPaceSlow: Pace.FromSecondsPerKm(330));

        // Act
        var actual = PrescribedWorkoutDto.FromSnapshot(snapshot);

        // Assert
        actual.Should().NotBeNull();
        actual!.WorkoutType.Should().Be("Tempo");
        actual.DistanceMeters.Should().Be(10_000);
        actual.DurationSeconds.Should().Be(2_700);
        actual.PaceFastSecPerKm.Should().Be(240, because: "the fast bound maps from PrescribedPaceFast");
        actual.PaceEasySecPerKm.Should().Be(330, because: "the easy bound maps from PrescribedPaceSlow");
        actual.PaceFastSecPerKm.Should().BeLessThan(
            actual.PaceEasySecPerKm,
            because: "fast is the harder, lower-sec/km bound — a swapped mapping would invert this");
    }

    [Fact]
    public void FromSnapshot_Null_ReturnsNull()
    {
        // Arrange / Act — an off-plan / rest / no-plan run resolves no snapshot.
        var actual = PrescribedWorkoutDto.FromSnapshot(null);

        // Assert
        actual.Should().BeNull(because: "an off-plan date carries no prescribed workout on the banner");
    }
}
