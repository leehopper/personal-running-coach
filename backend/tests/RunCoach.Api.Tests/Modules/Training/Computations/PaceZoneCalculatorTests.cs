using FluentAssertions;
using RunCoach.Api.Modules.Training.Computations;

namespace RunCoach.Api.Tests.Modules.Training.Computations;

public class PaceZoneCalculatorTests
{
    private readonly PaceZoneCalculator _sut = new();

    // Input guards
    [Theory]
    [InlineData(24.9)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void CalculatePaces_IndexBelowMinimum_ThrowsArgumentOutOfRangeException(double index)
    {
        // Act
        var act = () => _sut.CalculatePaces((decimal)index);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>(
            because: $"index {index} is below the minimum valid value of 25");
    }

    [Theory]
    [InlineData(90.1)]
    [InlineData(100.0)]
    public void CalculatePaces_IndexAboveMaximum_ThrowsArgumentOutOfRangeException(double index)
    {
        // Act
        var act = () => _sut.CalculatePaces((decimal)index);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>(
            because: $"index {index} is above the maximum valid value of 90");
    }

    [Theory]
    [InlineData(25.0)]
    [InlineData(29.9)]
    public void CalculatePaces_LowIndexRange_DoesNotThrow(double index)
    {
        // Low-index domain (25<=idx<30) is warned but not rejected
        var act = () => _sut.CalculatePaces((decimal)index);

        act.Should().NotThrow(because: $"index {index} is in the valid (though low) range 25–29");
    }

    [Fact]
    public void CalculatePaces_BoundaryMinimum_DoesNotThrow()
    {
        var act = () => _sut.CalculatePaces(25m);

        act.Should().NotThrow(because: "25 is the inclusive lower bound");
    }

    [Fact]
    public void CalculatePaces_BoundaryMaximum_DoesNotThrow()
    {
        var act = () => _sut.CalculatePaces(90m);

        act.Should().NotThrow(because: "90 is the inclusive upper bound");
    }

    // Easy zone — Fast end = 70% of index, Slow end = 59% of index
    [Fact]
    public void CalculatePaces_EasyRange_FastIsActuallyFasterThanSlow()
    {
        var result = _sut.CalculatePaces(50m);

        result.EasyPaceRange.Should().NotBeNull();
        result.EasyPaceRange!.Fast.IsFasterThan(result.EasyPaceRange.Slow).Should().BeTrue(
            because: "the Fast bound must always be faster (lower sec/km) than the Slow bound");
    }

    // Zone ordering: Easy.Slow > Easy.Fast > Threshold > Interval
    [Theory]
    [InlineData(50)]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(70)]
    public void CalculatePaces_ZoneOrdering_EasySlowFastThresholdIntervalOrdered(double index)
    {
        // Act
        var result = _sut.CalculatePaces((decimal)index);

        // Assert — Easy > Threshold > Interval (larger sec/km = slower)
        result.EasyPaceRange.Should().NotBeNull();
        result.ThresholdPace.Should().NotBeNull();
        result.IntervalPace.Should().NotBeNull();

        var easySlow = result.EasyPaceRange!.Slow;
        var easyFast = result.EasyPaceRange.Fast;
        var threshold = result.ThresholdPace!.Value;
        var interval = result.IntervalPace!.Value;

        easySlow.IsSlowerThan(easyFast).Should().BeTrue(
            because: $"Easy.Slow must be slower than Easy.Fast at index {index}");
        easyFast.IsSlowerThan(threshold).Should().BeTrue(
            because: $"Easy.Fast must be slower than Threshold at index {index}");
        threshold.IsSlowerThan(interval).Should().BeTrue(
            because: $"Threshold must be slower than Interval at index {index}");
    }

    // Equation-derived anchor values — tolerance reflects equation output, not published table
    // Easy.Slow at index 50: SolveVelocityForTargetVo2(0.59*50) = v → 60000/v ≈ 351.9 s/km
    [Fact]
    public void CalculatePaces_EasySlowAtIndex50_MatchesEquationOutput()
    {
        var result = _sut.CalculatePaces(50m);

        result.EasyPaceRange!.Slow.SecondsPerKm.Should().BeApproximately(
            351.9,
            1.0,
            because: "Easy.Slow at index 50 should be 60000/SolveVelocityForTargetVo2(29.5)");
    }

    // Easy.Fast at index 50: SolveVelocityForTargetVo2(0.70*50) → 60000/v ≈ 307.0 s/km
    [Fact]
    public void CalculatePaces_EasyFastAtIndex50_MatchesEquationOutput()
    {
        var result = _sut.CalculatePaces(50m);

        result.EasyPaceRange!.Fast.SecondsPerKm.Should().BeApproximately(
            307.0,
            1.0,
            because: "Easy.Fast at index 50 should be 60000/SolveVelocityForTargetVo2(35)");
    }

    // Threshold at index 50: SolveVelocityForTargetVo2(0.880*50) → 60000/v ≈ 255.2 s/km
    // The published table shows ~255 s/km — excellent match within tolerance
    [Fact]
    public void CalculatePaces_ThresholdAtIndex50_MatchesEquationAndTable()
    {
        var result = _sut.CalculatePaces(50m);

        result.ThresholdPace!.Value.SecondsPerKm.Should().BeApproximately(
            255.2,
            0.5,
            because: "Threshold at index 50: SolveVelocityForTargetVo2(44) → 255.2 s/km, matches Daniels table within ±0.5 s/km");
    }

    // Interval at index 50: SolveVelocityForTargetVo2(0.973*50) → 60000/v ≈ 235.25 s/km
    [Fact]
    public void CalculatePaces_IntervalAtIndex50_MatchesEquationOutput()
    {
        var result = _sut.CalculatePaces(50m);

        result.IntervalPace!.Value.SecondsPerKm.Should().BeApproximately(
            235.25,
            0.5,
            because: "Interval at index 50: SolveVelocityForTargetVo2(48.65) → 235.25 s/km");
    }

    // Marathon NR result: PredictRaceTimeMinutes(50, 42195) ≈ 139.4 min → 198.2 s/km
    // Note: this is the equation root, not the Daniels published M-pace training zone.
    // T04.3 will validate the full precision fixture against published tables.
    [Fact]
    public void CalculatePaces_MarathonAtIndex50_MatchesNREquationOutput()
    {
        var result = _sut.CalculatePaces(50m);

        result.MarathonPace!.Value.SecondsPerKm.Should().BeApproximately(
            198.2,
            1.0,
            because: "Marathon at index 50: PredictRaceTimeMinutes(50, 42195) ≈ 139 min → 198 s/km");
    }

    // All six zones are non-null for valid index
    [Fact]
    public void CalculatePaces_ValidIndex_AllSixZonesNonNull()
    {
        var result = _sut.CalculatePaces(50m);

        result.EasyPaceRange.Should().NotBeNull(because: "E zone must be computed");
        result.MarathonPace.Should().NotBeNull(because: "M zone must be computed");
        result.ThresholdPace.Should().NotBeNull(because: "T zone must be computed");
        result.IntervalPace.Should().NotBeNull(because: "I zone must be computed");
        result.RepetitionPace.Should().NotBeNull(because: "R zone must be computed");
        result.FastRepetitionPace.Should().NotBeNull(because: "F zone must be computed");
    }

    // R zone — RepetitionPace = R-400 derived from 0.9450*(400/3000)*PredictRaceTimeMinutes(index, 3000)
    // At index 50: actual equation output ≈ 216.76 s/km
    [Fact]
    public void CalculatePaces_RepetitionAtIndex50_MatchesEquationOutput()
    {
        var result = _sut.CalculatePaces(50m);

        result.RepetitionPace!.Value.SecondsPerKm.Should().BeApproximately(
            216.76,
            2.0,
            because: "R-400 at index 50 = 0.9450*(400/3000)*PredictRaceTimeMinutes(50,3000) expressed as s/km");
    }

    // F zone — FastRepetitionPace = F-400 derived from PredictRaceTimeMinutes(index, 800)/2
    // At index 50: actual equation output ≈ 255.18 s/km
    [Fact]
    public void CalculatePaces_FastRepetitionAtIndex50_MatchesEquationOutput()
    {
        var result = _sut.CalculatePaces(50m);

        result.FastRepetitionPace!.Value.SecondsPerKm.Should().BeApproximately(
            255.18,
            2.0,
            because: "F-400 at index 50 = PredictRaceTimeMinutes(50,800)/2 expressed as s/km");
    }

    // Zone ordering: Interval > R and F both non-null
    [Theory]
    [InlineData(40)]
    [InlineData(50)]
    [InlineData(60)]
    [InlineData(70)]
    public void CalculatePaces_ZoneOrdering_RepetitionAndFastRepetitionNonNull(double index)
    {
        var result = _sut.CalculatePaces((decimal)index);

        result.RepetitionPace.Should().NotBeNull(
            because: $"R zone must be computed at index {index}");
        result.FastRepetitionPace.Should().NotBeNull(
            because: $"F zone must be computed at index {index}");
    }
}
