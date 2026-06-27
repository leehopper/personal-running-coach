using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Unit coverage for <see cref="CandidatePrescriptionDto.FromSnapshot"/> (Slice 4B PR4) — the
/// server-authoritative projection of a resolved <see cref="WorkoutPrescriptionSnapshot"/> onto
/// the confirmation-card wire summary. Guards the field mapping (notably that the fast/easy pace
/// bounds are not swapped) and the off-plan <c>null</c> branch. The canonical-plan integration
/// fixture cannot catch a pace-band swap because its week-1 micro workout prescribes equal
/// fast/easy paces, so the swap guard lives here with distinct bounds.
/// </summary>
public class CandidatePrescriptionDtoTests
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
        var actual = CandidatePrescriptionDto.FromSnapshot(snapshot);

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
        // Arrange / Act — an off-plan run resolves no prescription snapshot.
        var actual = CandidatePrescriptionDto.FromSnapshot(null);

        // Assert
        actual.Should().BeNull(because: "an off-plan run carries no candidate prescription on the card");
    }
}
