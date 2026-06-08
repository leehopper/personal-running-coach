using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Training.Adaptation;

/// <summary>
/// Unit tests for <see cref="DeviationEngine"/> (Slice 3 PR2 / Unit 1): the
/// deterministic comparison of a logged workout's actuals against its frozen
/// prescription snapshot.
/// </summary>
public sealed class DeviationEngineTests
{
    private readonly DeviationEngine _sut = new();

    [Fact]
    public void Evaluate_WithNullPrescriptionSnapshot_ReturnsNullNoOp()
    {
        // Arrange — an off-plan log carries no prescription snapshot.
        var log = Log(km: 8, minutes: 40, prescription: null);

        // Act
        var actual = _sut.Evaluate(log);

        // Assert
        actual.Should().BeNull(because: "an off-plan log has no prescription to deviate from");
    }

    [Fact]
    public void Evaluate_PaceSlowerThanSlowBound_ReportsBandMembershipWithSignedMagnitude()
    {
        // Arrange — band 280–320 sec/km; 10 km in 60 min = 360 sec/km (slower than the 320 Slow bound).
        var snapshot = Snapshot(km: 10, minutes: 50, paceFast: 280, paceSlow: 320, type: WorkoutType.Tempo);
        var log = Log(km: 10, minutes: 60, prescription: snapshot);

        // Act
        var actual = _sut.Evaluate(log);

        // Assert — pace is band membership (slower-than-slow) with a signed magnitude, not a scalar diff.
        actual.Should().NotBeNull();
        actual!.PaceBand.Should().Be(PaceBandMembership.SlowerThanSlow);
        actual.PaceDeviationSecondsPerKm.Should().BeApproximately(
            40.0, 1e-6, because: "360 sec/km is 40 sec/km slower than the 320 Slow bound");
        actual.DistanceDeviationPercent.Should().BeApproximately(0.0, 1e-6);
        actual.DurationDeviationPercent.Should().BeApproximately(
            20.0, 1e-6, because: "60 min vs the prescribed 50 min is +20%");
    }

    [Fact]
    public void Evaluate_PaceInsideBand_ReportsInsideBandWithZeroMagnitude()
    {
        // Arrange — band 280–320; 10 km in 50 min = 300 sec/km (inside the band).
        var snapshot = Snapshot(km: 10, minutes: 50, paceFast: 280, paceSlow: 320);
        var log = Log(km: 10, minutes: 50, prescription: snapshot);

        // Act
        var actual = _sut.Evaluate(log);

        // Assert
        actual!.PaceBand.Should().Be(PaceBandMembership.InsideBand);
        actual.PaceDeviationSecondsPerKm.Should().Be(0.0);
    }

    [Fact]
    public void Evaluate_PaceFasterThanFastBound_ReportsNegativeMagnitude()
    {
        // Arrange — band 280–320; 10 km in 45 min = 270 sec/km (faster than the 280 Fast bound).
        var snapshot = Snapshot(km: 10, minutes: 50, paceFast: 280, paceSlow: 320);
        var log = Log(km: 10, minutes: 45, prescription: snapshot);

        // Act
        var actual = _sut.Evaluate(log);

        // Assert
        actual!.PaceBand.Should().Be(PaceBandMembership.FasterThanFast);
        actual.PaceDeviationSecondsPerKm.Should().BeApproximately(
            -10.0, 1e-6, because: "270 sec/km is 10 sec/km faster than the 280 Fast bound");
    }

    [Fact]
    public void Evaluate_ZeroDistanceCompleteLog_ReportsUnknownPaceWithoutThrowing()
    {
        // Arrange — a degenerate zero-distance log must not divide by zero or invent a slow pace.
        var log = Log(km: 0, minutes: 30, prescription: Snapshot(), status: CompletionStatus.Complete);

        // Act
        var act = () => _sut.Evaluate(log);

        // Assert
        act.Should().NotThrow();
        act()!.PaceBand.Should().Be(
            PaceBandMembership.Unknown,
            because: "a zero-distance log yields no derivable pace and must not produce a spurious deviation");
        act()!.PaceDeviationSecondsPerKm.Should().Be(0.0);
    }

    [Fact]
    public void Evaluate_ZeroDurationCompleteLog_ReportsUnknownPaceWithoutThrowing()
    {
        // Arrange
        var log = Log(km: 5, minutes: 0, prescription: Snapshot(), status: CompletionStatus.Complete);

        // Act
        var act = () => _sut.Evaluate(log);

        // Assert
        act.Should().NotThrow();
        act()!.PaceBand.Should().Be(PaceBandMembership.Unknown);
    }

    [Fact]
    public void Evaluate_SkippedKeyWorkout_ReportsUnknownPaceAndCarriesCompletionAndKeyFlags()
    {
        // Arrange — a skipped interval session: no actuals, but still a key workout.
        var log = Log(
            km: 0,
            minutes: 0,
            prescription: Snapshot(type: WorkoutType.Interval),
            status: CompletionStatus.Skipped);

        // Act
        var actual = _sut.Evaluate(log);

        // Assert
        actual!.PaceBand.Should().Be(PaceBandMembership.Unknown);
        actual.CompletionStatus.Should().Be(CompletionStatus.Skipped);
        actual.IsKeyWorkout.Should().BeTrue();
    }

    [Theory]
    [InlineData(WorkoutType.Tempo, true)]
    [InlineData(WorkoutType.Interval, true)]
    [InlineData(WorkoutType.Repetition, true)]
    [InlineData(WorkoutType.LongRun, true)]
    [InlineData(WorkoutType.Easy, false)]
    [InlineData(WorkoutType.Recovery, false)]
    [InlineData(WorkoutType.CrossTrain, false)]
    public void Evaluate_ClassifiesKeyWorkoutsByPrescribedType(WorkoutType type, bool expectedIsKey)
    {
        // Arrange
        var log = Log(km: 10, minutes: 50, prescription: Snapshot(type: type));

        // Act
        var actual = _sut.Evaluate(log);

        // Assert
        actual!.IsKeyWorkout.Should().Be(expectedIsKey);
    }

    [Fact]
    public void Evaluate_CarriesTheLogOccurredOnDate()
    {
        // Arrange
        var occurredOn = new DateOnly(2026, 6, 3);
        var log = Log(km: 10, minutes: 50, prescription: Snapshot(), occurredOn: occurredOn);

        // Act
        var actual = _sut.Evaluate(log);

        // Assert
        actual!.OccurredOn.Should().Be(occurredOn);
    }

    private static WorkoutLog Log(
        double km,
        double minutes,
        WorkoutPrescriptionSnapshot? prescription,
        CompletionStatus status = CompletionStatus.Complete,
        DateOnly? occurredOn = null) =>
        new()
        {
            WorkoutLogId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid().ToString(),
            IdempotencyKey = Guid.NewGuid(),
            OccurredOn = occurredOn ?? new DateOnly(2026, 6, 1),
            Distance = Distance.FromKilometers(km),
            Duration = Duration.FromMinutes(minutes),
            CompletionStatus = status,
            Prescription = prescription,
            CreatedOn = default,
            ModifiedOn = default,
        };

    private static WorkoutPrescriptionSnapshot Snapshot(
        double km = 10.0,
        double minutes = 50.0,
        double paceFast = 280.0,
        double paceSlow = 320.0,
        WorkoutType type = WorkoutType.Tempo) =>
        WorkoutPrescriptionSnapshot.Create(
            sourcePlanId: Guid.NewGuid(),
            weekNumber: 1,
            dayOfWeek: 3,
            workoutType: type,
            prescribedDistance: Distance.FromKilometers(km),
            prescribedDuration: Duration.FromMinutes(minutes),
            prescribedPaceFast: Pace.FromSecondsPerKm(paceFast),
            prescribedPaceSlow: Pace.FromSecondsPerKm(paceSlow));
}
