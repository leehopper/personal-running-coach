using FluentAssertions;
using RunCoach.Api.Modules.Training.Computations;

namespace RunCoach.Api.Tests.Modules.Training.Computations;

public class HeartRateZoneCalculatorTests
{
    private readonly HeartRateZoneCalculator _sut = new();

    [Theory]
    [InlineData(40, 180)] // 208 - 0.7*40 = 180
    [InlineData(30, 187)] // 208 - 0.7*30 = 187
    [InlineData(60, 166)] // 208 - 0.7*60 = 166
    public void EstimateMaxHr_TanakaFormula_ReturnsExpectedBpm(int age, int expectedMaxHr)
    {
        // Act
        var actualMaxHr = _sut.EstimateMaxHr(age);

        // Assert
        actualMaxHr.Should().Be(
            expectedMaxHr,
            because: $"Tanaka formula: 208 - 0.7 * {age} = {expectedMaxHr}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void EstimateMaxHr_AgeOutOfRange_ThrowsArgumentOutOfRange(int age)
    {
        // Act
        var act = () => _sut.EstimateMaxHr(age);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculateZones_HrMaxMode_EasyBandMatchesExpected()
    {
        // Arrange — maxHr=180, restingHr=null -> %HRmax mode
        // Easy: 65%..79% of 180 = 117..142.2 -> 117..142
        var expectedEasyLower = 117;
        var expectedEasyUpper = 142;

        // Act
        var actualZones = _sut.CalculateZones(maxHr: 180, restingHr: null);

        // Assert
        actualZones.Easy.Lower.Should().Be(
            expectedEasyLower,
            because: "65% of 180 = 117");
        actualZones.Easy.Upper.Should().Be(
            expectedEasyUpper,
            because: "79% of 180 = 142.2 rounds to 142");
    }

    [Fact]
    public void CalculateZones_HrMaxMode_AllBandsMatchExpected()
    {
        // Arrange — full band verification for maxHr=180, restingHr=null
        // Marathon: 80%..85% of 180 = 144..153
        // Threshold: 88%..92% of 180 = 158.4..165.6 -> 158..166
        // Interval: 98%..100% of 180 = 176.4..180 -> 176..180

        // Act
        var actualZones = _sut.CalculateZones(maxHr: 180, restingHr: null);

        // Assert
        actualZones.Marathon.Lower.Should().Be(144, because: "80% of 180 = 144");
        actualZones.Marathon.Upper.Should().Be(153, because: "85% of 180 = 153");

        actualZones.Threshold.Lower.Should().Be(158, because: "88% of 180 = 158.4 rounds to 158");
        actualZones.Threshold.Upper.Should().Be(166, because: "92% of 180 = 165.6 rounds to 166");

        actualZones.Interval.Lower.Should().Be(176, because: "98% of 180 = 176.4 rounds to 176");
        actualZones.Interval.Upper.Should().Be(180, because: "100% of 180 = 180");

        actualZones.Repetition.Should().BeNull(because: "Daniels assigns no HR target to R-zone workouts");
    }

    [Fact]
    public void CalculateZones_KarvonenMode_EasyBandDiffersFromHrMaxMode()
    {
        // Arrange — restingHr=50 activates Karvonen %HRR path
        // Karvonen Easy lower = 50 + 0.65*(180-50) = 50 + 84.5 = 134.5 -> 135
        // %HRmax Easy lower = 117 — should differ

        // Act
        var hrMaxZones = _sut.CalculateZones(maxHr: 180, restingHr: null);
        var karvonenZones = _sut.CalculateZones(maxHr: 180, restingHr: 50);

        // Assert
        karvonenZones.Easy.Lower.Should().NotBe(
            hrMaxZones.Easy.Lower,
            because: "Karvonen formula produces different (typically higher) lower bound than %HRmax");
        karvonenZones.Repetition.Should().BeNull(
            because: "Repetition is always null regardless of method");
    }

    [Fact]
    public void CalculateZones_KarvonenMode_BandsAreTypicallyNarrower()
    {
        // Arrange — restingHr=50 at maxHr=180
        // Karvonen Easy: 50+0.65*130=134.5->135, 50+0.79*130=152.7->153 -> width=18
        // %HRmax Easy: 117..142 -> width=25

        // Act
        var hrMaxZones = _sut.CalculateZones(maxHr: 180, restingHr: null);
        var karvonenZones = _sut.CalculateZones(maxHr: 180, restingHr: 50);

        var hrMaxWidth = hrMaxZones.Easy.Upper - hrMaxZones.Easy.Lower;
        var karvonenWidth = karvonenZones.Easy.Upper - karvonenZones.Easy.Lower;

        // Assert
        karvonenWidth.Should().BeLessThan(
            hrMaxWidth,
            because: "Karvonen easy band is typically narrower than %HRmax band when resting HR is provided");
    }

    [Fact]
    public void CalculateZones_HrMaxMode_BoundariesAreIntegers()
    {
        // Act
        var actualZones = _sut.CalculateZones(maxHr: 181, restingHr: null);

        // Assert — all boundaries should be positive whole-number bpm values
        actualZones.Easy.Lower.Should().BeGreaterThan(0);
        actualZones.Easy.Upper.Should().BeGreaterThan(0);
        actualZones.Marathon.Lower.Should().BeGreaterThan(0);
        actualZones.Marathon.Upper.Should().BeGreaterThan(0);
        actualZones.Threshold.Lower.Should().BeGreaterThan(0);
        actualZones.Threshold.Upper.Should().BeGreaterThan(0);
        actualZones.Interval.Lower.Should().BeGreaterThan(0);
        actualZones.Interval.Upper.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(180, 180)] // restingHr == maxHr
    [InlineData(180, 200)] // restingHr > maxHr
    [InlineData(60, 61)] // edge: one above
    public void CalculateZones_RestingHrEqualOrGreaterThanMaxHr_ThrowsArgumentOutOfRange(int maxHr, int restingHr)
    {
        // Act
        var act = () => _sut.CalculateZones(maxHr, restingHr);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(160)]
    [InlineData(180)]
    [InlineData(200)]
    public void CalculateZones_HrMaxMode_AnyValidMaxHr_ZonesAreOrderedFromSlowToFast(int maxHr)
    {
        // Act
        var actualZones = _sut.CalculateZones(maxHr, restingHr: null);

        // Assert — the percentage bands must partition the HR range in
        // increasing-intensity order: Easy < Marathon < Threshold < Interval.
        actualZones.Easy.Upper.Should().BeLessThan(
            actualZones.Marathon.Lower,
            because: "Easy zone must end before Marathon zone begins");
        actualZones.Marathon.Upper.Should().BeLessThan(
            actualZones.Threshold.Lower,
            because: "Marathon zone must end before Threshold zone begins");
        actualZones.Threshold.Upper.Should().BeLessThan(
            actualZones.Interval.Lower,
            because: "Threshold zone must end before Interval zone begins");
    }

    [Theory]
    [InlineData(160, 50)]
    [InlineData(180, 50)]
    [InlineData(200, 60)]
    public void CalculateZones_KarvonenMode_AnyValidInputs_ZonesAreOrderedFromSlowToFast(int maxHr, int restingHr)
    {
        // Act
        var actualZones = _sut.CalculateZones(maxHr, restingHr);

        // Assert — Karvonen bands must also respect Easy < Marathon < Threshold < Interval.
        actualZones.Easy.Upper.Should().BeLessThan(actualZones.Marathon.Lower);
        actualZones.Marathon.Upper.Should().BeLessThan(actualZones.Threshold.Lower);
        actualZones.Threshold.Upper.Should().BeLessThan(actualZones.Interval.Lower);
    }
}
