using FluentAssertions;
using RunCoach.Api.Modules.Training.Computations;

namespace RunCoach.Api.Tests.Modules.Training.Computations;

public class DanielsGilbertEquationsTests
{
    // OxygenCost — VO₂ = −4.60 + 0.182258·v + 0.000104·v²
    // Worked example: index 50, 5 km in 19:56 → velocity ≈ 250.84 m/min
    // VO₂ = −4.60 + 0.182258·250.84 + 0.000104·250.84² ≈ 47.68
    [Theory]
    [InlineData(200.0, 36.0116)] // slow easy-run pace: −4.60 + 0.182258·200 + 0.000104·40000
    [InlineData(250.84, 47.68)] // index-50 5 km race pace (19:56)
    [InlineData(300.0, 59.4374)] // fast interval pace: −4.60 + 0.182258·300 + 0.000104·90000
    public void OxygenCost_KnownVelocity_ReturnsExpectedVo2(double velocity, double expectedVo2)
    {
        // Act
        var actual = DanielsGilbertEquations.OxygenCost(velocity);

        // Assert
        actual.Should().BeApproximately(
            expectedVo2,
            0.05,
            because: $"VO₂ at {velocity} m/min should match the Daniels–Gilbert 1979 equation");
    }

    [Fact]
    public void OxygenCost_ZeroVelocity_ReturnsNegativeConstant()
    {
        // At v = 0 the polynomial reduces to the constant term −4.60
        var actual = DanielsGilbertEquations.OxygenCost(0.0);

        actual.Should().BeApproximately(
            -4.60,
            1e-9,
            because: "VO₂ at zero velocity equals the constant term −4.60");
    }

    // FractionalUtilization — %VO₂max decays from ~1.08 toward 0.8
    // Short durations → high fraction; long durations → approaches 0.8
    [Theory]
    [InlineData(5.0, 1.0915)] // ~5 min: fraction > 1 (very high intensity, short effort)
    [InlineData(19.93, 0.9532)] // index-50 5 km race (~19:56): ~0.953
    [InlineData(60.0, 0.8880)] // 60 min: fraction decays toward baseline
    [InlineData(240.0, 0.8088)] // 4 hours: approaches 0.8 baseline closely
    public void FractionalUtilization_KnownDuration_ReturnsExpectedFraction(
        double timeMinutes,
        double expectedFraction)
    {
        // Act
        var actual = DanielsGilbertEquations.FractionalUtilization(timeMinutes);

        // Assert
        actual.Should().BeApproximately(
            expectedFraction,
            0.002,
            because: $"fractional utilization at {timeMinutes} min should match the Daniels–Gilbert 1979 equation");
    }

    [Fact]
    public void FractionalUtilization_VeryLongDuration_ApproachesBaseline()
    {
        // As t → ∞ both exponential terms vanish; result → 0.8
        var actual = DanielsGilbertEquations.FractionalUtilization(10_000.0);

        actual.Should().BeApproximately(
            0.8,
            1e-6,
            because: "fractional utilization at infinite duration approaches the 0.8 baseline");
    }

    // Cross-check: index 50, 5 km in 19:56
    // VDOT = OxygenCost(velocity) / FractionalUtilization(time) ≈ 50
    [Fact]
    public void ForwardEquations_Index50_5KmIn19m56s_ReproducesPublishedIndex()
    {
        // Arrange — 5 km in 19:56 = 1196 s = 19.9333... min, velocity = 5000 / 19.9333 m/min
        var timeMinutes = ((19.0 * 60.0) + 56.0) / 60.0;
        var velocity = 5000.0 / timeMinutes;
        var expectedIndex = 50.0;

        // Act
        var vo2 = DanielsGilbertEquations.OxygenCost(velocity);
        var fracUtil = DanielsGilbertEquations.FractionalUtilization(timeMinutes);
        var actualIndex = vo2 / fracUtil;

        // Assert
        actualIndex.Should().BeApproximately(
            expectedIndex,
            0.5,
            because: "index 50 at 5 km / 19:56 is the canonical Daniels published anchor");
    }

    // SolveVelocityForTargetVo2 — closed-form positive root of the OxygenCost quadratic
    [Theory]
    [InlineData(36.0116)] // slow easy-run pace VO₂ (OxygenCost(200))
    [InlineData(47.68)] // index-50 race pace
    [InlineData(59.4374)] // fast interval pace VO₂ (OxygenCost(300))
    public void SolveVelocityForTargetVo2_RoundTrip_WithinTolerance(double targetVo2)
    {
        // Arrange
        var velocity = DanielsGilbertEquations.SolveVelocityForTargetVo2(targetVo2);

        // Act — feed the solved velocity back into OxygenCost
        var actualVo2 = DanielsGilbertEquations.OxygenCost(velocity);

        // Assert — round-trip must hold within 1e-6
        actualVo2.Should().BeApproximately(
            targetVo2,
            1e-6,
            because: $"OxygenCost(SolveVelocityForTargetVo2({targetVo2})) should equal {targetVo2} within 1e-6");
    }

    [Fact]
    public void SolveVelocityForTargetVo2_ReturnsPositiveVelocity()
    {
        // Any realistic VO₂ value (positive, reasonable) must yield a positive velocity
        var velocity = DanielsGilbertEquations.SolveVelocityForTargetVo2(47.68);

        velocity.Should().BePositive(because: "running velocity cannot be negative");
    }

    [Fact]
    public void SolveVelocityForTargetVo2_IsInverseOfOxygenCost_AtIndex50()
    {
        // Arrange — 5 km in 19:56 → known velocity
        var timeMinutes = ((19.0 * 60.0) + 56.0) / 60.0;
        var expectedVelocity = 5000.0 / timeMinutes;
        var vo2 = DanielsGilbertEquations.OxygenCost(expectedVelocity);

        // Act
        var actualVelocity = DanielsGilbertEquations.SolveVelocityForTargetVo2(vo2);

        // Assert
        actualVelocity.Should().BeApproximately(
            expectedVelocity,
            1e-6,
            because: "SolveVelocityForTargetVo2 must invert OxygenCost at the index-50 anchor");
    }

    // PredictRaceTimeMinutes — Newton-Raphson race-time predictor
    [Theory]
    [InlineData(50.0, 5000.0, 19.93)] // index 50, 5 km → ~19:56 per Daniels published table
    [InlineData(46.0, 42195.0, 204.58)] // index 46, marathon → ~3:24:35 per Daniels table
    [InlineData(50.0, 10000.0, 41.4)] // index 50, 10 km → ~41:24 per Daniels table
    public void PredictRaceTimeMinutes_KnownIndex_MatchesDanielsPublishedTable(
        double index,
        double distanceMeters,
        double expectedMinutes)
    {
        // Act
        var actual = DanielsGilbertEquations.PredictRaceTimeMinutes(index, distanceMeters);

        // Assert — allow ±0.5 min tolerance against published table values
        actual.Should().BeApproximately(
            expectedMinutes,
            0.5,
            because: $"index {index} over {distanceMeters}m should yield ~{expectedMinutes} min per Daniels tables");
    }

    [Fact]
    public void PredictRaceTimeMinutes_PathologicalInput_ThrowsInvalidOperationException()
    {
        // distance = 0 means velocity = 0 for all t; OxygenCost(0) = -4.60 < 0, so
        // FractionalUtilization(t)·OxygenCost(0) - index < 0 for all t — no convergence possible
        var act = () => DanielsGilbertEquations.PredictRaceTimeMinutes(50.0, 0.0);

        act.Should().Throw<InvalidOperationException>(
            because: "the 10-iteration cap must surface as InvalidOperationException on pathological inputs");
    }
}
