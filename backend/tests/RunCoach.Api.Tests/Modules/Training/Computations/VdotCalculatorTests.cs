using System.Globalization;
using FluentAssertions;
using RunCoach.Api.Modules.Training.Computations;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Computations;

public class VdotCalculatorTests
{
    private readonly VdotCalculator _sut = new();

    /// <summary>
    /// Validates VDOT computation from known race times against the Daniels/Gilbert
    /// oxygen cost formula. Expected values are from the published Daniels' tables
    /// (3rd edition), verified against the analytical formula.
    ///
    /// Reference mapping (Daniels' tables):
    ///   VDOT 42: 5K=23:10, 10K=48:08
    ///   VDOT 46: 5K=21:24, 10K=44:32, Mar=3:24:35
    ///   VDOT 47: 5K=21:02, 10K=43:44, HM=1:36:30
    ///   VDOT 50: 5K=19:56, 10K=41:24, HM=1:31:35, Mar=3:10:49.
    /// </summary>
    [Theory]
    [InlineData("5K", "00:19:56", 50.0)]
    [InlineData("10K", "00:41:24", 50.0)]
    [InlineData("Half-Marathon", "01:36:30", 47.0)]
    [InlineData("Marathon", "03:24:35", 46.0)]
    public void CalculateVdot_FromStandardDistance_MatchesDanielsTable(
        string distance,
        string timeStr,
        decimal expectedVdot)
    {
        // Arrange
        var time = TimeSpan.Parse(timeStr, CultureInfo.InvariantCulture);
        var raceTime = new RaceTime(distance, time, new DateOnly(2025, 1, 1), null);

        // Act
        var actualVdot = _sut.CalculateVdot(raceTime);

        // Assert
        actualVdot.Should().NotBeNull();
        actualVdot!.Value.Should().BeApproximately(
            expectedVdot,
            0.5m,
            because: $"VDOT for {distance} in {timeStr} should be ~{expectedVdot} per Daniels' tables");
    }

    [Fact]
    public void CalculateVdot_LeesProfile10K48Minutes_ProducesExpectedVdot()
    {
        // Arrange
        // Lee's 10K time of 48:00 corresponds to VDOT ~42 per the Daniels/Gilbert formula.
        // Daniels' table: VDOT 42 -> 10K = 48:08, confirming this is correct.
        var expectedVdot = 42.0m;
        var raceTime = new RaceTime("10K", TimeSpan.FromMinutes(48), new DateOnly(2025, 6, 15), null);

        // Act
        var actualVdot = _sut.CalculateVdot(raceTime);

        // Assert
        actualVdot.Should().NotBeNull();
        actualVdot!.Value.Should().BeApproximately(
            expectedVdot,
            0.5m,
            because: "Lee's 10K time of 48:00 should produce a VDOT ~42 for downstream pace validation");
    }

    [Fact]
    public void CalculateVdot_UnsupportedDistance_ReturnsNull()
    {
        // Arrange
        var raceTime = new RaceTime("15K", TimeSpan.FromMinutes(60), new DateOnly(2025, 1, 1), null);

        // Act
        var actualVdot = _sut.CalculateVdot(raceTime);

        // Assert
        actualVdot.Should().BeNull(because: "15K is not a supported standard distance");
    }

    [Fact]
    public void CalculateVdot_ZeroTime_ReturnsNull()
    {
        // Arrange
        var raceTime = new RaceTime("5K", TimeSpan.Zero, new DateOnly(2025, 1, 1), null);

        // Act
        var actualVdot = _sut.CalculateVdot(raceTime);

        // Assert
        actualVdot.Should().BeNull(because: "a zero-duration race time is invalid");
    }

    [Fact]
    public void CalculateVdot_EmptyCollection_ReturnsNull()
    {
        // Arrange
        var raceTimes = Array.Empty<RaceTime>();

        // Act
        var actualVdot = _sut.CalculateVdot(raceTimes);

        // Assert
        actualVdot.Should().BeNull(because: "no race history means no VDOT can be computed");
    }

    [Fact]
    public void CalculateVdot_MultipleRaceTimes_ReturnsBestVdot()
    {
        // Arrange — 5K 19:56 (~VDOT 50) should be selected over 10K 48:00 (~VDOT 42)
        var raceTimes = new[]
        {
            new RaceTime("5K", TimeSpan.Parse("00:19:56", CultureInfo.InvariantCulture), new DateOnly(2025, 3, 1), null),
            new RaceTime("10K", TimeSpan.FromMinutes(48), new DateOnly(2025, 6, 15), null),
        };

        // Act
        var actualVdot = _sut.CalculateVdot(raceTimes);

        // Assert
        actualVdot.Should().NotBeNull();
        actualVdot!.Value.Should().BeApproximately(
            50.0m,
            0.5m,
            because: "the best VDOT from multiple races should be selected");
    }

    [Fact]
    public void CalculateVdot_CollectionWithOnlyUnsupportedDistances_ReturnsNull()
    {
        // Arrange
        var raceTimes = new[]
        {
            new RaceTime("15K", TimeSpan.FromMinutes(60), new DateOnly(2025, 1, 1), null),
            new RaceTime("8K", TimeSpan.FromMinutes(35), new DateOnly(2025, 2, 1), null),
        };

        // Act
        var actualVdot = _sut.CalculateVdot(raceTimes);

        // Assert
        actualVdot.Should().BeNull(because: "no supported distances means no VDOT can be computed");
    }

    [Theory]
    [InlineData("5k")]
    [InlineData("5K")]
    [InlineData("10k")]
    [InlineData("10K")]
    [InlineData("half-marathon")]
    [InlineData("Half-Marathon")]
    [InlineData("HM")]
    [InlineData("hm")]
    [InlineData("marathon")]
    [InlineData("Marathon")]
    [InlineData("halfmarathon")]
    public void CalculateVdot_DistanceCaseVariations_AreSupported(string distance)
    {
        // Arrange
        var raceTime = new RaceTime(distance, TimeSpan.FromMinutes(30), new DateOnly(2025, 1, 1), null);

        // Act
        var actualVdot = _sut.CalculateVdot(raceTime);

        // Assert
        actualVdot.Should().NotBeNull(because: $"distance '{distance}' should be recognized");
    }

    [Theory]
    [InlineData("5K", "00:23:10", 42.0)]
    [InlineData("10K", "00:48:08", 42.0)]
    [InlineData("5K", "00:21:24", 46.0)]
    [InlineData("10K", "00:44:32", 46.0)]
    public void CalculateVdot_CrossDistanceConsistency_SameVdotFromEquivalentPerformances(
        string distance,
        string timeStr,
        decimal expectedVdot)
    {
        // Arrange — equivalent performances at different distances should yield the same VDOT
        var time = TimeSpan.Parse(timeStr, CultureInfo.InvariantCulture);
        var raceTime = new RaceTime(distance, time, new DateOnly(2025, 1, 1), null);

        // Act
        var actualVdot = _sut.CalculateVdot(raceTime);

        // Assert
        actualVdot.Should().NotBeNull();
        actualVdot!.Value.Should().BeApproximately(
            expectedVdot,
            0.5m,
            because: $"equivalent {distance} performance should produce VDOT ~{expectedVdot}");
    }
}
