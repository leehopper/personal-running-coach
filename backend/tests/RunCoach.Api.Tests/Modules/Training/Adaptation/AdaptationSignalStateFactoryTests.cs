using FluentAssertions;
using RunCoach.Api.Modules.Training.Adaptation;

namespace RunCoach.Api.Tests.Modules.Training.Adaptation;

/// <summary>
/// Unit tests for <see cref="AdaptationSignalState.Create"/> — the validating
/// factory at the persistence rehydration boundary (Slice 3 Unit 5, DEC-078
/// resolution) — and for <see cref="AdaptationSignalStateDocument"/>'s round-trip
/// through it. The rules under test: clamp the rolling score into
/// [0, MaxRollingDeviationScore], reject an undefined <see cref="PlanState"/>
/// encoding, reject a negative missed-day streak, and reject
/// <see cref="PlanState.NeedsAdjustment"/> paired with a null LastAdaptationOn
/// (which would silently disable the cooldown half of the hysteresis).
/// </summary>
public sealed class AdaptationSignalStateFactoryTests
{
    private static readonly DateOnly AnyDay = new(2026, 6, 1);

    [Fact]
    public void Create_ValidInBandValues_PassThroughUnchanged()
    {
        // Act
        var actual = AdaptationSignalState.Create(
            PlanState.MinorDeviation, 1.5, 2, AnyDay);

        // Assert
        var expected = new AdaptationSignalState(PlanState.MinorDeviation, 1.5, 2, AnyDay);
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(7.5)]
    [InlineData(3.0001)]
    [InlineData(double.PositiveInfinity)]
    public void Create_ScoreAboveMax_ClampsToMaxRollingDeviationScore(double storedScore)
    {
        // Act
        var actual = AdaptationSignalState.Create(PlanState.OnTrack, storedScore, 0, null);

        // Assert — MaxRollingDeviationScore is 3.0 (= RestructureEnterScore); an
        // out-of-band stored value must not widen the hysteresis dead-zone.
        actual.RollingDeviationScore.Should().Be(3.0);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(-100.0)]
    [InlineData(double.NegativeInfinity)]
    public void Create_NegativeScore_ClampsToZero(double storedScore)
    {
        // Act
        var actual = AdaptationSignalState.Create(PlanState.OnTrack, storedScore, 0, null);

        // Assert
        actual.RollingDeviationScore.Should().Be(0.0);
    }

    [Fact]
    public void Create_BoundaryScores_AreNotAltered()
    {
        // Act
        var actualAtZero = AdaptationSignalState.Create(PlanState.OnTrack, 0.0, 0, null);
        var actualAtMax = AdaptationSignalState.Create(PlanState.OnTrack, 3.0, 0, null);

        // Assert — the clamp is inclusive at both ends.
        actualAtZero.RollingDeviationScore.Should().Be(0.0);
        actualAtMax.RollingDeviationScore.Should().Be(3.0);
    }

    [Fact]
    public void Create_NaNScore_Throws()
    {
        // Act
        var act = () => AdaptationSignalState.Create(PlanState.OnTrack, double.NaN, 0, null);

        // Assert — NaN slips through Math.Clamp (IEEE comparisons are all false),
        // so the factory must reject it rather than store an unclampable value.
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("rollingDeviationScore");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_NegativeConsecutiveMissedDays_Throws(int storedStreak)
    {
        // Act
        var act = () => AdaptationSignalState.Create(PlanState.OnTrack, 0.0, storedStreak, null);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("consecutiveMissedDays");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void Create_UndefinedPlanState_Throws(int storedEncoding)
    {
        // Act — the enum persists as its numeric encoding, so a drifted or
        // hand-edited row can carry a value no PlanState member maps to.
        var act = () => AdaptationSignalState.Create((PlanState)storedEncoding, 0.0, 0, null);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("planState");
    }

    [Fact]
    public void Create_NeedsAdjustmentWithNullLastAdaptationOn_Throws()
    {
        // Act
        var act = () => AdaptationSignalState.Create(PlanState.NeedsAdjustment, 2.0, 0, null);

        // Assert — a restructure is the only transition into NeedsAdjustment and it
        // always stamps LastAdaptationOn; a null would silently disable the cooldown
        // half of the asymmetric hysteresis.
        act.Should().Throw<ArgumentException>()
            .WithParameterName("lastAdaptationOn");
    }

    [Fact]
    public void Create_NeedsAdjustmentWithLastAdaptationOn_Succeeds()
    {
        // Act
        var actual = AdaptationSignalState.Create(PlanState.NeedsAdjustment, 2.0, 1, AnyDay);

        // Assert
        actual.PlanState.Should().Be(PlanState.NeedsAdjustment);
        actual.LastAdaptationOn.Should().Be(AnyDay);
    }

    [Theory]
    [InlineData(PlanState.OnTrack)]
    [InlineData(PlanState.MinorDeviation)]
    public void Create_NonDeadZoneStatesWithNullLastAdaptationOn_Succeed(PlanState planState)
    {
        // Act — null LastAdaptationOn is the legitimate pre-first-restructure shape
        // for every state except NeedsAdjustment.
        var actual = AdaptationSignalState.Create(planState, 0.5, 0, null);

        // Assert
        actual.PlanState.Should().Be(planState);
        actual.LastAdaptationOn.Should().BeNull();
    }

    [Fact]
    public void Document_FromThenToState_RoundTripsTheState()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var expected = new AdaptationSignalState(PlanState.NeedsAdjustment, 2.5, 3, AnyDay);

        // Act
        var document = AdaptationSignalStateDocument.From(planId, expected);
        var actual = document.ToState();

        // Assert — the persistable shape carries the identity and loses nothing.
        document.PlanId.Should().Be(planId);
        actual.Should().Be(expected);
    }

    [Fact]
    public void Document_ToState_FlowsThroughValidatingFactory()
    {
        // Arrange — simulate a stored row whose score drifted out of band (e.g. a
        // hand-edited document or a row persisted before a threshold tightening).
        var outOfBand = new AdaptationSignalStateDocument(
            Guid.NewGuid(), PlanState.MinorDeviation, 99.0, 2, AnyDay);

        // Act
        var actual = outOfBand.ToState();

        // Assert — rehydration clamps through Create rather than trusting JSONB.
        actual.RollingDeviationScore.Should().Be(3.0);
    }

    [Fact]
    public void Document_ToState_CorruptDeadZoneRow_FailsLoudly()
    {
        // Arrange — NeedsAdjustment with a null LastAdaptationOn can only come from
        // a corrupted or hand-edited row; rehydration must fail loudly, not hand the
        // classifier a state with the cooldown silently disabled.
        var corrupt = new AdaptationSignalStateDocument(
            Guid.NewGuid(), PlanState.NeedsAdjustment, 2.0, 0, null);

        // Act
        var act = () => corrupt.ToState();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("lastAdaptationOn");
    }

    [Fact]
    public void Document_From_NullState_Throws()
    {
        // Act
        var act = () => AdaptationSignalStateDocument.From(Guid.NewGuid(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("state");
    }
}
