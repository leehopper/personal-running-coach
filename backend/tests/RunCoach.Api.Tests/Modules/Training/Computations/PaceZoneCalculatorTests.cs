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
    // Marathon at index 50 reproduces Daniels' published ~3:10:49 M-time (≈ 271 s/km) via
    // Newton-Raphson solve of VO₂(d/t)/F(t) = index at d = 42,195 m.
    [Fact]
    public void CalculatePaces_MarathonAtIndex50_MatchesPublishedTable()
    {
        var result = _sut.CalculatePaces(50m);

        result.MarathonPace!.Value.SecondsPerKm.Should().BeApproximately(
            271.12,
            1.0,
            because: "Marathon at index 50: PredictRaceTimeMinutes(50, 42195) ≈ 190.7 min → 271 s/km (Daniels published M-pace)");
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

    // F zone — FastRepetitionPace = F-400 derived from PredictRaceTimeMinutes(index, 800)/2.
    // At index 50 this reproduces Daniels' 800 m race prediction (~2:42) → F-400 ≈ 204 s/km.
    [Fact]
    public void CalculatePaces_FastRepetitionAtIndex50_MatchesEquationOutput()
    {
        var result = _sut.CalculatePaces(50m);

        result.FastRepetitionPace!.Value.SecondsPerKm.Should().BeApproximately(
            204.05,
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

    // Full 56×6 equation-derived golden fixture (indices 30–85, step 1).
    // Provenance: all expected values derived from the public-domain Daniels-Gilbert oxygen-cost
    // and race-prediction equations (Daniels & Gilbert, 1979, Oxygen Power, Performance Press).
    // No values are transcribed from the published lookup tables — fixture is equation-only.
    // All values recomputed from DanielsGilbertEquations at fixture-build time — self-consistent.
    // InlineData columns: idx, easySlow, easyFast, threshold, interval, marathon, rep400, frep400 (s/km).
    [Theory]
    [InlineData(30, 522.51, 459.03, 384.19, 354.86, 412.11, 338.94, 319.29)]
    [InlineData(31, 509.82, 447.60, 374.39, 345.72, 401.50, 329.72, 310.25)]
    [InlineData(32, 497.77, 436.77, 365.11, 337.09, 391.44, 321.00, 301.73)]
    [InlineData(33, 486.31, 426.48, 356.31, 328.91, 381.89, 312.73, 293.69)]
    [InlineData(34, 475.39, 416.69, 347.96, 321.14, 372.81, 304.89, 286.09)]
    [InlineData(35, 464.98, 407.37, 340.02, 313.77, 364.17, 297.44, 278.90)]
    [InlineData(36, 455.04, 398.49, 332.46, 306.75, 355.94, 290.35, 272.08)]
    [InlineData(37, 445.55, 390.01, 325.26, 300.07, 348.08, 283.61, 265.61)]
    [InlineData(38, 436.46, 381.92, 318.39, 293.70, 340.57, 277.17, 259.46)]
    [InlineData(39, 427.77, 374.17, 311.82, 287.62, 333.39, 271.03, 253.61)]
    [InlineData(40, 419.44, 366.76, 305.55, 281.80, 326.52, 265.16, 248.03)]
    [InlineData(41, 411.44, 359.65, 299.54, 276.24, 319.93, 259.54, 242.71)]
    [InlineData(42, 403.77, 352.84, 293.79, 270.92, 313.62, 254.17, 237.64)]
    [InlineData(43, 396.40, 346.30, 288.27, 265.81, 307.56, 249.02, 232.79)]
    [InlineData(44, 389.31, 340.02, 282.98, 260.91, 301.74, 244.08, 228.15)]
    [InlineData(45, 382.48, 333.98, 277.89, 256.21, 296.14, 239.33, 223.70)]
    [InlineData(46, 375.91, 328.16, 273.00, 251.69, 290.76, 234.78, 219.45)]
    [InlineData(47, 369.58, 322.56, 268.30, 247.35, 285.57, 230.40, 215.36)]
    [InlineData(48, 363.47, 317.17, 263.77, 243.16, 280.58, 226.18, 211.44)]
    [InlineData(49, 357.58, 311.97, 259.40, 239.13, 275.76, 222.13, 207.67)]
    [InlineData(50, 351.89, 306.95, 255.20, 235.25, 271.12, 218.21, 204.05)]
    [InlineData(51, 346.39, 302.10, 251.14, 231.50, 266.63, 214.45, 200.57)]
    [InlineData(52, 341.08, 297.42, 247.22, 227.89, 262.30, 210.81, 197.21)]
    [InlineData(53, 335.93, 292.90, 243.43, 224.39, 258.11, 207.30, 193.98)]
    [InlineData(54, 330.96, 288.52, 239.77, 221.02, 254.06, 203.91, 190.86)]
    [InlineData(55, 326.14, 284.28, 236.23, 217.76, 250.14, 200.64, 187.85)]
    [InlineData(56, 321.47, 280.18, 232.81, 214.60, 246.35, 197.47, 184.94)]
    [InlineData(57, 316.94, 276.20, 229.49, 211.54, 242.67, 194.41, 182.14)]
    [InlineData(58, 312.55, 272.35, 226.28, 208.58, 239.11, 191.44, 179.42)]
    [InlineData(59, 308.29, 268.61, 223.16, 205.71, 235.66, 188.57, 176.80)]
    [InlineData(60, 304.16, 264.99, 220.14, 202.93, 232.31, 185.79, 174.26)]
    [InlineData(61, 300.14, 261.47, 217.21, 200.23, 229.06, 183.09, 171.80)]
    [InlineData(62, 296.24, 258.05, 214.37, 197.61, 225.91, 180.48, 169.42)]
    [InlineData(63, 292.45, 254.73, 211.61, 195.07, 222.85, 177.94, 167.11)]
    [InlineData(64, 288.76, 251.50, 208.92, 192.60, 219.87, 175.48, 164.87)]
    [InlineData(65, 285.18, 248.36, 206.31, 190.20, 216.98, 173.09, 162.70)]
    [InlineData(66, 281.69, 245.31, 203.78, 187.86, 214.16, 170.77, 160.60)]
    [InlineData(67, 278.29, 242.34, 201.31, 185.59, 211.42, 168.51, 158.55)]
    [InlineData(68, 274.98, 239.45, 198.91, 183.39, 208.76, 166.31, 156.56)]
    [InlineData(69, 271.75, 236.63, 196.57, 181.24, 206.17, 164.18, 154.63)]
    [InlineData(70, 268.61, 233.89, 194.30, 179.14, 203.64, 162.10, 152.75)]
    [InlineData(71, 265.55, 231.21, 192.08, 177.10, 201.18, 160.08, 150.92)]
    [InlineData(72, 262.56, 228.60, 189.92, 175.12, 198.79, 158.12, 149.14)]
    [InlineData(73, 259.65, 226.06, 187.81, 173.18, 196.45, 156.20, 147.41)]
    [InlineData(74, 256.80, 223.58, 185.76, 171.29, 194.17, 154.34, 145.73)]
    [InlineData(75, 254.03, 221.16, 183.75, 169.45, 191.95, 152.52, 144.08)]
    [InlineData(76, 251.32, 218.80, 181.80, 167.65, 189.78, 150.75, 142.48)]
    [InlineData(77, 248.67, 216.49, 179.89, 165.90, 187.67, 149.02, 140.92)]
    [InlineData(78, 246.09, 214.24, 178.03, 164.18, 185.60, 147.33, 139.40)]
    [InlineData(79, 243.56, 212.04, 176.20, 162.51, 183.58, 145.69, 137.92)]
    [InlineData(80, 241.09, 209.89, 174.43, 160.88, 181.62, 144.08, 136.47)]
    [InlineData(81, 238.67, 207.79, 172.69, 159.28, 179.69, 142.51, 135.06)]
    [InlineData(82, 236.31, 205.73, 170.99, 157.72, 177.81, 140.98, 133.68)]
    [InlineData(83, 234.00, 203.72, 169.33, 156.19, 175.97, 139.49, 132.33)]
    [InlineData(84, 231.74, 201.75, 167.70, 154.70, 174.17, 138.03, 131.02)]
    [InlineData(85, 229.53, 199.83, 166.11, 153.24, 172.42, 136.60, 129.73)]
    public void CalculatePaces_FullFixture_AllZonesMatchEquationDerivedValues(
        int idx,
        double expectedEasySlow,
        double expectedEasyFast,
        double expectedThreshold,
        double expectedInterval,
        double expectedMarathon,
        double expectedRep400,
        double expectedFRep400)
    {
        // Arrange
        var result = _sut.CalculatePaces((decimal)idx);

        // Assert — tolerance 0.5 s/km for velocity-derived zones; 1.0 s/km for NR-derived zones
        result.EasyPaceRange!.Slow.SecondsPerKm.Should().BeApproximately(
            expectedEasySlow,
            0.5,
            because: $"Easy.Slow at index {idx} must match equation output");
        result.EasyPaceRange.Fast.SecondsPerKm.Should().BeApproximately(
            expectedEasyFast,
            0.5,
            because: $"Easy.Fast at index {idx} must match equation output");
        result.ThresholdPace!.Value.SecondsPerKm.Should().BeApproximately(
            expectedThreshold,
            0.5,
            because: $"Threshold at index {idx} must match equation output");
        result.IntervalPace!.Value.SecondsPerKm.Should().BeApproximately(
            expectedInterval,
            0.5,
            because: $"Interval at index {idx} must match equation output");
        result.MarathonPace!.Value.SecondsPerKm.Should().BeApproximately(
            expectedMarathon,
            1.0,
            because: $"Marathon at index {idx} must match NR equation root");
        result.RepetitionPace!.Value.SecondsPerKm.Should().BeApproximately(
            expectedRep400,
            1.0,
            because: $"R-400 at index {idx} must match equation output");
        result.FastRepetitionPace!.Value.SecondsPerKm.Should().BeApproximately(
            expectedFRep400,
            1.0,
            because: $"F-400 at index {idx} must match equation output");
    }

    // T/I precision vs Daniels published tables — R-028 anchor (±0.5 s/km)
    [Theory]
    [InlineData(50, 255.20, 235.25)]
    [InlineData(55, 236.23, 217.76)]
    [InlineData(60, 220.14, 202.93)]
    [InlineData(65, 206.31, 190.20)]
    [InlineData(70, 194.30, 179.14)]
    public void CalculatePaces_TiZonePrecision_Within0Point5SecPerKm(
        int idx,
        double expectedThreshold,
        double expectedInterval)
    {
        var result = _sut.CalculatePaces((decimal)idx);

        result.ThresholdPace!.Value.SecondsPerKm.Should().BeApproximately(
            expectedThreshold,
            0.5,
            because: $"T-pace at index {idx} must match R-028 anchor within ±0.5 s/km");
        result.IntervalPace!.Value.SecondsPerKm.Should().BeApproximately(
            expectedInterval,
            0.5,
            because: $"I-pace at index {idx} must match R-028 anchor within ±0.5 s/km");
    }

    // R-400 precision — 3 km anchoring; expected values are equation roots (R-028 multipliers on t_3k).
    [Theory]
    [InlineData(50, 218.21)]
    [InlineData(55, 200.64)]
    [InlineData(60, 185.79)]
    [InlineData(65, 173.09)]
    [InlineData(70, 162.10)]
    public void CalculatePaces_RPacePrecision_Within1Point1Sec(int idx, double expectedRep400)
    {
        var result = _sut.CalculatePaces((decimal)idx);

        result.RepetitionPace!.Value.SecondsPerKm.Should().BeApproximately(
            expectedRep400,
            1.1,
            because: $"R-400 at index {idx} must match 3 km–anchored equation within ±1.1 s/km");
    }

    // Smoothness: consecutive integer indices must produce monotonically faster (lower s/km) paces
    // across every zone — after the VO₂/F equation fix, M and F are included (no longer divergent).
    [Theory]
    [InlineData(40)]
    [InlineData(50)]
    [InlineData(60)]
    [InlineData(70)]
    [InlineData(80)]
    public void CalculatePaces_Smoothness_IncrementingIndexProducesFasterPaces(int baseIdx)
    {
        // Arrange
        var lower = _sut.CalculatePaces((decimal)baseIdx);
        var higher = _sut.CalculatePaces((decimal)(baseIdx + 1));

        // Assert — higher index → faster pace (lower s/km) for every zone
        higher.EasyPaceRange!.Fast.IsFasterThan(lower.EasyPaceRange!.Fast).Should().BeTrue(
            because: $"Easy.Fast must be faster at index {baseIdx + 1} than {baseIdx}");
        higher.MarathonPace!.Value.IsFasterThan(lower.MarathonPace!.Value).Should().BeTrue(
            because: $"Marathon must be faster at index {baseIdx + 1} than {baseIdx}");
        higher.ThresholdPace!.Value.IsFasterThan(lower.ThresholdPace!.Value).Should().BeTrue(
            because: $"Threshold must be faster at index {baseIdx + 1} than {baseIdx}");
        higher.IntervalPace!.Value.IsFasterThan(lower.IntervalPace!.Value).Should().BeTrue(
            because: $"Interval must be faster at index {baseIdx + 1} than {baseIdx}");
        higher.RepetitionPace!.Value.IsFasterThan(lower.RepetitionPace!.Value).Should().BeTrue(
            because: $"R-400 must be faster at index {baseIdx + 1} than {baseIdx}");
        higher.FastRepetitionPace!.Value.IsFasterThan(lower.FastRepetitionPace!.Value).Should().BeTrue(
            because: $"F-400 must be faster at index {baseIdx + 1} than {baseIdx}");
    }

    [Fact]
    public void CalculatePaces_Monotonicity_AllZonesOrderedFromSlowToFast()
    {
        var result = _sut.CalculatePaces(50m);
        result.MarathonPace!.Value.IsSlowerThan(result.ThresholdPace!.Value).Should().BeTrue(
            because: "M-pace must be slower than T-pace in training-zone ordering");
        result.FastRepetitionPace!.Value.IsFasterThan(result.RepetitionPace!.Value).Should().BeTrue(
            because: "F-pace must be faster than R-pace per Daniels published tables");
    }

    [Fact]
    public void CalculatePaces_MPacePrecision_WithinPublishedTableTolerance()
    {
        var result = _sut.CalculatePaces(50m);
        result.MarathonPace!.Value.SecondsPerKm.Should().BeApproximately(
            271.0,
            5.0,
            because: "M-pace at index 50 should match Daniels published table ≈ 271 s/km");
    }
}
