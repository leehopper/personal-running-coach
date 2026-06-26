using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Training.Workouts;

/// <summary>
/// Unit coverage for <see cref="LoggedWorkoutDetailMapper"/> — the public, null-prescription
/// tolerant <see cref="WorkoutLog"/> → <see cref="LoggedWorkoutDetail"/> projection used by the
/// Slice 4B conversation recent-logs feed. Distinct from the adaptation handler's private
/// <c>BuildDetail</c> (which dereferences a non-null snapshot); an off-plan log (null
/// <c>Prescription</c>) must map to a generic workout-type label, not throw.
/// </summary>
public sealed class LoggedWorkoutDetailMapperTests
{
    [Fact]
    public void ToLoggedWorkoutDetail_OnPlanLog_UsesThePrescriptionWorkoutType()
    {
        // Arrange
        var log = BuildLog(prescriptionType: WorkoutType.Easy);

        // Act
        var detail = LoggedWorkoutDetailMapper.ToLoggedWorkoutDetail(log);

        // Assert
        detail.WorkoutType.Should().Be("Easy");
    }

    [Fact]
    public void ToLoggedWorkoutDetail_OffPlanLog_UsesGenericRunLabel()
    {
        // Arrange — an off-plan log has a null Prescription; the mapper must not throw.
        var log = BuildLog(prescriptionType: null);

        // Act
        var detail = LoggedWorkoutDetailMapper.ToLoggedWorkoutDetail(log);

        // Assert
        detail.WorkoutType.Should().Be("Run");
    }

    [Fact]
    public void ToLoggedWorkoutDetail_MapsCoreActualsAndNotes()
    {
        // Arrange
        var log = BuildLog(prescriptionType: null);

        // Act
        var detail = LoggedWorkoutDetailMapper.ToLoggedWorkoutDetail(log);

        // Assert
        detail.OccurredOn.Should().Be(new DateOnly(2026, 6, 20));
        detail.Distance.Meters.Should().BeApproximately(5000, 0.001);
        detail.Duration.TotalSeconds.Should().BeApproximately(1500, 0.001);
        detail.Notes.Should().Be("felt good");
        detail.Metrics.Should().NotBeNull();
    }

    [Fact]
    public void ToLoggedWorkoutDetail_ThrowsArgumentNullException_WhenLogIsNull()
    {
        // Arrange + Act
        var act = () => LoggedWorkoutDetailMapper.ToLoggedWorkoutDetail(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static WorkoutLog BuildLog(WorkoutType? prescriptionType)
    {
        return new WorkoutLog
        {
            WorkoutLogId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid().ToString(),
            IdempotencyKey = Guid.NewGuid(),
            OccurredOn = new DateOnly(2026, 6, 20),
            Distance = Distance.FromMeters(5000),
            Duration = Duration.FromSeconds(1500),
            CompletionStatus = CompletionStatus.Complete,
            Notes = "felt good",
            Metrics = null,
            Prescription = prescriptionType is null
                ? null
                : new WorkoutPrescriptionSnapshot { WorkoutType = prescriptionType.Value },
        };
    }
}
