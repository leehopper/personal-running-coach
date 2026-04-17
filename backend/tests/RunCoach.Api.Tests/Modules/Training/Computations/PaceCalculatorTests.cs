using FluentAssertions;
using RunCoach.Api.Modules.Training.Computations;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Computations;

public class PaceCalculatorTests
{
    private readonly PaceCalculator _sut = new();

    [Fact]
    public void CalculatePaces_Vdot50_MatchesDanielsTableValues()
    {
        // Arrange
        var vdot = 50m;

        // Expected values from published Daniels' Running Formula (4th edition) tables for VDOT 50.
        // Corrected per DEC-040: original table had an off-by-one row shift from VDOT 50-85.
        // Marathon=271 verified via race prediction (3:10:49); Threshold=255 and Interval=235
        // verified against published per-1000m columns and Daniels-Gilbert equation cross-reference.
        var expectedEasyFast = Pace.FromSecondsPerKm(306);   // ~5:06/km
        var expectedEasySlow = Pace.FromSecondsPerKm(338);   // ~5:38/km
        var expectedMarathon = Pace.FromSecondsPerKm(271);   // ~4:31/km
        var expectedThreshold = Pace.FromSecondsPerKm(255);  // ~4:15/km
        var expectedInterval = Pace.FromSecondsPerKm(235);   // ~3:55/km
        var expectedRep = Pace.FromSecondsPerKm(218);        // ~3:38/km

        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert — within 5 seconds tolerance to account for rounding in published tables
        var tolerance = 5.0;

        actualPaces.EasyPaceRange!.Fast.SecondsPerKm.Should().BeApproximately(
            expectedEasyFast.SecondsPerKm,
            tolerance,
            because: "VDOT 50 easy fast-end should be ~5:06/km per Daniels' table");

        actualPaces.EasyPaceRange.Slow.SecondsPerKm.Should().BeApproximately(
            expectedEasySlow.SecondsPerKm,
            tolerance,
            because: "VDOT 50 easy slow-end should be ~5:38/km per Daniels' table");

        actualPaces.MarathonPace!.Value.SecondsPerKm.Should().BeApproximately(
            expectedMarathon.SecondsPerKm,
            tolerance,
            because: "VDOT 50 marathon pace should be ~4:31/km per Daniels' table");

        actualPaces.ThresholdPace!.Value.SecondsPerKm.Should().BeApproximately(
            expectedThreshold.SecondsPerKm,
            tolerance,
            because: "VDOT 50 threshold pace should be ~4:15/km per Daniels' table");

        actualPaces.IntervalPace!.Value.SecondsPerKm.Should().BeApproximately(
            expectedInterval.SecondsPerKm,
            tolerance,
            because: "VDOT 50 interval pace should be ~3:55/km per Daniels' table");

        actualPaces.RepetitionPace!.Value.SecondsPerKm.Should().BeApproximately(
            expectedRep.SecondsPerKm,
            tolerance,
            because: "VDOT 50 repetition pace should be ~3:38/km per Daniels' table");
    }

    [Fact]
    public void CalculatePaces_EasyPace_ReturnedAsFastSlowRange()
    {
        // Arrange
        var vdot = 50m;

        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert
        actualPaces.EasyPaceRange.Should().NotBeNull(
            because: "easy pace must always be a range with fast and slow values");

        actualPaces.EasyPaceRange!.Fast.IsFasterThan(actualPaces.EasyPaceRange.Slow).Should().BeTrue(
            because: "fast end should be quicker than slow end");
    }

    [Fact]
    public void CalculatePaces_KnownVdot_AllFiveZonesDerived()
    {
        // Arrange
        var vdot = 45m;

        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert
        actualPaces.EasyPaceRange.Should().NotBeNull(because: "easy pace zone must be derived");
        actualPaces.EasyPaceRange!.Fast.SecondsPerKm.Should().BePositive(because: "easy fast pace must be positive");
        actualPaces.EasyPaceRange.Slow.SecondsPerKm.Should().BePositive(because: "easy slow pace must be positive");

        actualPaces.MarathonPace.Should().NotBeNull(because: "marathon pace zone must be derived");
        actualPaces.MarathonPace!.Value.SecondsPerKm.Should().BePositive(because: "marathon pace must be positive");

        actualPaces.ThresholdPace.Should().NotBeNull(because: "threshold pace zone must be derived");
        actualPaces.ThresholdPace!.Value.SecondsPerKm.Should().BePositive(because: "threshold pace must be positive");

        actualPaces.IntervalPace.Should().NotBeNull(because: "interval pace zone must be derived");
        actualPaces.IntervalPace!.Value.SecondsPerKm.Should().BePositive(because: "interval pace must be positive");

        actualPaces.RepetitionPace.Should().NotBeNull(because: "repetition pace zone must be derived");
        actualPaces.RepetitionPace!.Value.SecondsPerKm.Should().BePositive(because: "repetition pace must be positive");
    }

    [Fact]
    public void CalculatePaces_KnownVdot_PaceZonesInCorrectOrder()
    {
        // Arrange — paces should get progressively faster: easy slow > marathon > threshold > interval > rep
        var vdot = 50m;

        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert
        var easySlow = actualPaces.EasyPaceRange!.Slow;
        var marathon = actualPaces.MarathonPace!.Value;
        var threshold = actualPaces.ThresholdPace!.Value;
        var interval = actualPaces.IntervalPace!.Value;
        var repetition = actualPaces.RepetitionPace!.Value;

        easySlow.IsSlowerThan(marathon).Should().BeTrue(because: "easy slow-end should be slower than marathon pace");
        marathon.IsSlowerThan(threshold).Should().BeTrue(because: "marathon pace should be slower than threshold pace");
        threshold.IsSlowerThan(interval).Should().BeTrue(because: "threshold pace should be slower than interval pace");
        interval.IsSlowerThan(repetition).Should().BeTrue(because: "interval pace should be slower than repetition pace");
    }

    [Theory]
    [InlineData(38)]
    [InlineData(39)]
    [InlineData(40)]
    [InlineData(41)]
    [InlineData(42)]
    public void CalculatePaces_LeesVdotRange_EasyPaceWithinExpectedRange(int vdot)
    {
        // Arrange — Lee's 10K of 48:00 produces a VDOT in the 38-42 range.
        // The task specifies ~5:45-6:30/km easy pace, but this varies by VDOT.
        var expectedEasyFastEnd = Pace.FromSecondsPerKm(345);  // 5:45/km
        var expectedEasySlowEnd = Pace.FromSecondsPerKm(420);  // 7:00/km (generous upper bound)

        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert — fast-end should not be faster than 5:45/km, and slow-end should not be slower than 7:00/km
        actualPaces.EasyPaceRange!.Fast.IsFasterThan(expectedEasyFastEnd).Should().BeFalse(
            because: $"VDOT {vdot} easy fast-end should not be faster than 5:45/km for Lee's range");

        actualPaces.EasyPaceRange.Slow.IsSlowerThan(expectedEasySlowEnd).Should().BeFalse(
            because: $"VDOT {vdot} easy slow-end should not be slower than 7:00/km for Lee's range");
    }

    [Theory]
    [InlineData(38)]
    [InlineData(39)]
    [InlineData(40)]
    [InlineData(41)]
    [InlineData(42)]
    public void CalculatePaces_LeesVdotRange_IntervalPaceWithinExpectedRange(int vdot)
    {
        // Arrange — Lee's interval pace across plausible VDOT range.
        var expectedFastBound = Pace.FromSecondsPerKm(255);  // 4:15/km
        var expectedSlowBound = Pace.FromSecondsPerKm(300);  // 5:00/km

        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert — interval should not be faster than 4:15/km, and not slower than 5:00/km
        actualPaces.IntervalPace!.Value.IsFasterThan(expectedFastBound).Should().BeFalse(
            because: $"VDOT {vdot} interval should not be faster than 4:15/km for Lee's range");

        actualPaces.IntervalPace!.Value.IsSlowerThan(expectedSlowBound).Should().BeFalse(
            because: $"VDOT {vdot} interval should not be slower than 5:00/km for Lee's range");
    }

    [Fact]
    public void EstimateMaxHr_Age34_Returns186()
    {
        // Arrange
        var age = 34;
        var expectedMaxHr = 186;

        // Act
        var actualMaxHr = _sut.EstimateMaxHr(age);

        // Assert
        actualMaxHr.Should().Be(expectedMaxHr, because: "220 - 34 = 186 per the 220-age formula");
    }

    [Theory]
    [InlineData(20, 200)]
    [InlineData(30, 190)]
    [InlineData(50, 170)]
    [InlineData(65, 155)]
    public void EstimateMaxHr_VariousAges_Returns220MinusAge(int age, int expectedMaxHr)
    {
        // Act
        var actualMaxHr = _sut.EstimateMaxHr(age);

        // Assert
        actualMaxHr.Should().Be(
            expectedMaxHr,
            because: $"220 - {age} = {expectedMaxHr} per the 220-age formula");
    }

    [Fact]
    public void EstimateMaxHr_InvalidAge_ThrowsArgumentOutOfRange()
    {
        // Act & Assert
        var act = () => _sut.EstimateMaxHr(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculatePaces_BelowMinVdot_ThrowsArgumentOutOfRange()
    {
        // Act & Assert
        var act = () => _sut.CalculatePaces(29m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculatePaces_AboveMaxVdot_ThrowsArgumentOutOfRange()
    {
        // Act & Assert
        var act = () => _sut.CalculatePaces(86m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculatePaces_FractionalVdot_InterpolatesBetweenTableEntries()
    {
        // Arrange — VDOT 50.5 should interpolate between VDOT 50 and VDOT 51
        var vdot = 50.5m;

        var pacesAt50 = _sut.CalculatePaces(50m);
        var pacesAt51 = _sut.CalculatePaces(51m);

        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert — interpolated marathon pace should be between VDOT 50 and 51 values
        var marathonAt50 = pacesAt50.MarathonPace!.Value;
        var marathonAt51 = pacesAt51.MarathonPace!.Value;
        var actualMarathon = actualPaces.MarathonPace!.Value;

        actualMarathon.IsFasterThan(marathonAt50).Should().BeTrue(because: "higher VDOT produces faster marathon pace");
        actualMarathon.IsSlowerThan(marathonAt51).Should().BeTrue(because: "VDOT 50.5 should not be as fast as VDOT 51");
    }

    [Theory]
    [InlineData(30)]
    [InlineData(50)]
    [InlineData(65)]
    [InlineData(85)]
    public void CalculatePaces_BoundaryVdotValues_ProducesValidPaces(int vdot)
    {
        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert — all zones should produce positive sec/km values
        actualPaces.EasyPaceRange!.Fast.SecondsPerKm.Should().BePositive();
        actualPaces.EasyPaceRange.Slow.SecondsPerKm.Should().BePositive();
        actualPaces.MarathonPace!.Value.SecondsPerKm.Should().BePositive();
        actualPaces.ThresholdPace!.Value.SecondsPerKm.Should().BePositive();
        actualPaces.IntervalPace!.Value.SecondsPerKm.Should().BePositive();
        actualPaces.RepetitionPace!.Value.SecondsPerKm.Should().BePositive();
    }

    [Fact]
    public void CalculatePaces_HigherVdot_ProducesFasterPaces()
    {
        // Arrange
        var lowerVdot = 40m;
        var higherVdot = 60m;

        // Act
        var lowerPaces = _sut.CalculatePaces(lowerVdot);
        var higherPaces = _sut.CalculatePaces(higherVdot);

        // Assert — higher fitness (VDOT) should yield faster paces across all zones
        higherPaces.EasyPaceRange!.Fast.IsFasterThan(lowerPaces.EasyPaceRange!.Fast).Should().BeTrue(
            because: "higher VDOT means faster easy pace");

        higherPaces.MarathonPace!.Value.IsFasterThan(lowerPaces.MarathonPace!.Value).Should().BeTrue(
            because: "higher VDOT means faster marathon pace");

        higherPaces.ThresholdPace!.Value.IsFasterThan(lowerPaces.ThresholdPace!.Value).Should().BeTrue(
            because: "higher VDOT means faster threshold pace");

        higherPaces.IntervalPace!.Value.IsFasterThan(lowerPaces.IntervalPace!.Value).Should().BeTrue(
            because: "higher VDOT means faster interval pace");

        higherPaces.RepetitionPace!.Value.IsFasterThan(lowerPaces.RepetitionPace!.Value).Should().BeTrue(
            because: "higher VDOT means faster repetition pace");
    }
}
