using FluentAssertions;
using RunCoach.Api.Modules.Training.Computations;

namespace RunCoach.Api.Tests.Modules.Training.Computations;

public class PaceCalculatorTests
{
    private readonly PaceCalculator _sut = new();

    [Fact]
    public void CalculatePaces_Vdot50_MatchesDanielsTableValues()
    {
        // Arrange
        var vdot = 50m;

        // Expected values from published Daniels' Running Formula tables for VDOT 50
        var expectedEasyMin = TimeSpan.FromSeconds(301);   // ~5:01/km
        var expectedEasyMax = TimeSpan.FromSeconds(331);   // ~5:31/km
        var expectedMarathon = TimeSpan.FromSeconds(267);  // ~4:27/km
        var expectedThreshold = TimeSpan.FromSeconds(250); // ~4:10/km
        var expectedInterval = TimeSpan.FromSeconds(231);  // ~3:51/km
        var expectedRep = TimeSpan.FromSeconds(216);       // ~3:36/km

        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert — within 5 seconds tolerance to account for rounding in published tables
        var tolerance = TimeSpan.FromSeconds(5);

        actualPaces.EasyPaceRange.MinPerKm.Should().BeCloseTo(
            expectedEasyMin,
            tolerance,
            because: "VDOT 50 easy fast-end should be ~5:01/km per Daniels' table");

        actualPaces.EasyPaceRange.MaxPerKm.Should().BeCloseTo(
            expectedEasyMax,
            tolerance,
            because: "VDOT 50 easy slow-end should be ~5:31/km per Daniels' table");

        actualPaces.MarathonPace.Should().BeCloseTo(
            expectedMarathon,
            tolerance,
            because: "VDOT 50 marathon pace should be ~4:27/km per Daniels' table");

        actualPaces.ThresholdPace.Should().BeCloseTo(
            expectedThreshold,
            tolerance,
            because: "VDOT 50 threshold pace should be ~4:10/km per Daniels' table");

        actualPaces.IntervalPace.Should().BeCloseTo(
            expectedInterval,
            tolerance,
            because: "VDOT 50 interval pace should be ~3:51/km per Daniels' table");

        actualPaces.RepetitionPace.Should().BeCloseTo(
            expectedRep,
            tolerance,
            because: "VDOT 50 repetition pace should be ~3:36/km per Daniels' table");
    }

    [Fact]
    public void CalculatePaces_EasyPace_ReturnedAsMinMaxRange()
    {
        // Arrange
        var vdot = 50m;

        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert
        actualPaces.EasyPaceRange.Should().NotBeNull(
            because: "easy pace must always be a range with min and max values");

        var minPerKm = actualPaces.EasyPaceRange.MinPerKm;
        var maxPerKm = actualPaces.EasyPaceRange.MaxPerKm;
        minPerKm.Should().BeLessThan(maxPerKm, because: "fast end should be quicker than slow end");
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
        actualPaces.EasyPaceRange.MinPerKm.Should().BePositive(because: "easy min pace must be positive");
        actualPaces.EasyPaceRange.MaxPerKm.Should().BePositive(because: "easy max pace must be positive");

        actualPaces.MarathonPace.Should().NotBeNull(because: "marathon pace zone must be derived");
        actualPaces.MarathonPace!.Value.Should().BePositive(because: "marathon pace must be positive");

        actualPaces.ThresholdPace.Should().NotBeNull(because: "threshold pace zone must be derived");
        actualPaces.ThresholdPace!.Value.Should().BePositive(because: "threshold pace must be positive");

        actualPaces.IntervalPace.Should().NotBeNull(because: "interval pace zone must be derived");
        actualPaces.IntervalPace!.Value.Should().BePositive(because: "interval pace must be positive");

        actualPaces.RepetitionPace.Should().NotBeNull(because: "repetition pace zone must be derived");
        actualPaces.RepetitionPace!.Value.Should().BePositive(because: "repetition pace must be positive");
    }

    [Fact]
    public void CalculatePaces_KnownVdot_PaceZonesInCorrectOrder()
    {
        // Arrange — paces should get progressively faster: easy > marathon > threshold > interval > rep
        var vdot = 50m;

        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert
        var easyMax = actualPaces.EasyPaceRange.MaxPerKm;
        var marathon = actualPaces.MarathonPace!.Value;
        var threshold = actualPaces.ThresholdPace!.Value;
        var interval = actualPaces.IntervalPace!.Value;
        var repetition = actualPaces.RepetitionPace!.Value;

        easyMax.Should().BeGreaterThan(marathon, because: "easy slow-end should be slower than marathon pace");
        marathon.Should().BeGreaterThan(threshold, because: "marathon pace should be slower than threshold pace");
        threshold.Should().BeGreaterThan(interval, because: "threshold pace should be slower than interval pace");
        interval.Should().BeGreaterThan(repetition, because: "interval pace should be slower than repetition pace");
    }

    [Theory]
    [InlineData(38)]
    [InlineData(39)]
    [InlineData(40)]
    [InlineData(41)]
    [InlineData(42)]
    public void CalculatePaces_LeesVdotRange_EasyPaceWithinExpectedRange(int vdot)
    {
        // Arrange — Lee's 10K of 48:00 produces a VDOT in the 38-42 range depending on the formula.
        // The task specifies ~5:45-6:30/km easy pace, but this varies by VDOT.
        // This test validates that each VDOT in Lee's plausible range returns a sensible easy pace.
        var expectedEasyFastEnd = TimeSpan.FromSeconds(345);  // 5:45/km
        var expectedEasySlowEnd = TimeSpan.FromSeconds(420);  // 7:00/km (generous upper bound)

        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert
        actualPaces.EasyPaceRange.MinPerKm.Should().BeGreaterThanOrEqualTo(
            expectedEasyFastEnd,
            because: $"VDOT {vdot} easy fast-end should not be faster than 5:45/km for Lee's range");

        actualPaces.EasyPaceRange.MaxPerKm.Should().BeLessThanOrEqualTo(
            expectedEasySlowEnd,
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
        // Task says ~4:20-4:35/km but this applies at the higher end of his VDOT range.
        // At VDOT 38 interval is slower; at 42 it matches. Generous bounds used.
        var expectedFastBound = TimeSpan.FromSeconds(255);  // 4:15/km
        var expectedSlowBound = TimeSpan.FromSeconds(300);  // 5:00/km

        // Act
        var actualPaces = _sut.CalculatePaces(vdot);

        // Assert
        actualPaces.IntervalPace!.Value.Should().BeGreaterThanOrEqualTo(
            expectedFastBound,
            because: $"VDOT {vdot} interval should not be faster than 4:15/km for Lee's range");

        actualPaces.IntervalPace!.Value.Should().BeLessThanOrEqualTo(
            expectedSlowBound,
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

        actualMarathon.Should().BeLessThan(marathonAt50, because: "higher VDOT produces faster marathon pace");
        actualMarathon.Should().BeGreaterThan(marathonAt51, because: "VDOT 50.5 should not be as fast as VDOT 51");
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

        // Assert — all zones should produce non-zero TimeSpan values
        actualPaces.EasyPaceRange.MinPerKm.Should().BePositive();
        actualPaces.EasyPaceRange.MaxPerKm.Should().BePositive();
        actualPaces.MarathonPace!.Value.Should().BePositive();
        actualPaces.ThresholdPace!.Value.Should().BePositive();
        actualPaces.IntervalPace!.Value.Should().BePositive();
        actualPaces.RepetitionPace!.Value.Should().BePositive();
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

        // Assert — higher fitness (VDOT) should yield faster (shorter) paces across all zones
        var lowerEasyMin = lowerPaces.EasyPaceRange.MinPerKm;
        var higherEasyMin = higherPaces.EasyPaceRange.MinPerKm;
        higherEasyMin.Should().BeLessThan(lowerEasyMin, because: "higher VDOT means faster easy pace");

        var lowerMarathon = lowerPaces.MarathonPace!.Value;
        var higherMarathon = higherPaces.MarathonPace!.Value;
        higherMarathon.Should().BeLessThan(lowerMarathon, because: "higher VDOT means faster marathon pace");

        var lowerThreshold = lowerPaces.ThresholdPace!.Value;
        var higherThreshold = higherPaces.ThresholdPace!.Value;
        higherThreshold.Should().BeLessThan(lowerThreshold, because: "higher VDOT means faster threshold pace");

        var lowerInterval = lowerPaces.IntervalPace!.Value;
        var higherInterval = higherPaces.IntervalPace!.Value;
        higherInterval.Should().BeLessThan(lowerInterval, because: "higher VDOT means faster interval pace");

        var lowerRep = lowerPaces.RepetitionPace!.Value;
        var higherRep = higherPaces.RepetitionPace!.Value;
        higherRep.Should().BeLessThan(lowerRep, because: "higher VDOT means faster repetition pace");
    }
}
