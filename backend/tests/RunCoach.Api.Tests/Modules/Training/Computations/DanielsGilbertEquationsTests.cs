using FluentAssertions;
using RunCoach.Api.Modules.Training.Computations;

namespace RunCoach.Api.Tests.Modules.Training.Computations;

public class DanielsGilbertEquationsTests
{
    // OxygenCost — VO₂ = −4.60 + 0.182258·v + 0.000104·v²
    // Worked example: index 50, 5 km in 19:56 → velocity ≈ 250.84 m/min
    // VO₂ = −4.60 + 0.182258·250.84 + 0.000104·250.84² ≈ 47.68
    [Theory]
    [InlineData(200.0, 30.4516)] // slow easy-run pace
    [InlineData(250.84, 47.68)] // index-50 5 km race pace (19:56)
    [InlineData(300.0, 63.97)] // fast interval pace
    public void OxygenCost_KnownVelocity_ReturnsExpectedVo2(double velocity, double expectedVo2)
    {
        // Act
        var actual = DanielsGilbertEquations.OxygenCost(velocity);

        // Assert
        actual.Should().BeApproximately(expectedVo2, 0.05,
            because: $"VO₂ at {velocity} m/min should match the Daniels–Gilbert 1979 equation");
    }

    [Fact]
    public void OxygenCost_ZeroVelocity_ReturnsNegativeConstant()
    {
        // At v = 0 the polynomial reduces to the constant term −4.60
        var actual = DanielsGilbertEquations.OxygenCost(0.0);

        actual.Should().BeApproximately(-4.60, 1e-9,
            because: "VO₂ at zero velocity equals the constant term −4.60");
    }

    // FractionalUtilization — %VO₂max decays from ~1.08 toward 0.8
    // Short durations → high fraction; long durations → approaches 0.8
    [Theory]
    [InlineData(5.0, 1.0)] // ~5 min: fraction > 1 (very high intensity, short effort)
    [InlineData(19.93, 0.9605)] // index-50 5 km race (~19:56): expected ~0.961
    [InlineData(60.0, 0.8360)] // 60 min: fraction decays toward baseline
    [InlineData(240.0, 0.8012)] // 4 hours: approaches 0.8 baseline closely
    public void FractionalUtilization_KnownDuration_ReturnsExpectedFraction(
        double timeMinutes,
        double expectedFraction)
    {
        // Act
        var actual = DanielsGilbertEquations.FractionalUtilization(timeMinutes);

        // Assert
        actual.Should().BeApproximately(expectedFraction, 0.002,
            because: $"fractional utilization at {timeMinutes} min should match the Daniels–Gilbert 1979 equation");
    }

    [Fact]
    public void FractionalUtilization_VeryLongDuration_ApproachesBaseline()
    {
        // As t → ∞ both exponential terms vanish; result → 0.8
        var actual = DanielsGilbertEquations.FractionalUtilization(10_000.0);

        actual.Should().BeApproximately(0.8, 1e-6,
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
        actualIndex.Should().BeApproximately(expectedIndex, 0.5,
            because: "index 50 at 5 km / 19:56 is the canonical Daniels published anchor");
    }
}
