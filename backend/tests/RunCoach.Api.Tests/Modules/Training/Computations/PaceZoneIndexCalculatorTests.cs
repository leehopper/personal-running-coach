using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RunCoach.Api.Modules.Training.Computations;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Computations;

public class PaceZoneIndexCalculatorTests
{
    private readonly PaceZoneIndexCalculator _sut =
        new(NullLogger<PaceZoneIndexCalculator>.Instance);

    // ───────────────────────────── Legacy assertions (Scenario 1) ─────────────────────────────

    /// <summary>
    /// Validates index computation from known race times against the Daniels/Gilbert
    /// oxygen cost formula. Expected values match the published Daniels' tables.
    ///
    /// Reference:
    ///   Index 42: 5K=23:10, 10K=48:08
    ///   Index 46: 5K=21:24, 10K=44:32, Mar=3:24:35
    ///   Index 47: 5K=21:02, 10K=43:44, HM=1:36:30
    ///   Index 50: 5K=19:56, 10K=41:24, HM=1:31:35, Mar=3:10:49.
    /// </summary>
    [Theory]
    [InlineData("5K", "00:19:56", 50.0)]
    [InlineData("10K", "00:41:24", 50.0)]
    [InlineData("Half-Marathon", "01:36:30", 47.0)]
    [InlineData("Marathon", "03:24:35", 46.0)]
    public void CalculateIndex_FromStandardDistance_MatchesDanielsTable(
        string distance,
        string timeStr,
        decimal expectedIndex)
    {
        // Arrange
        var time = TimeSpan.Parse(timeStr, CultureInfo.InvariantCulture);
        var raceTime = new RaceTime(distance, time, new DateOnly(2025, 1, 1), null);

        // Act
        var actualIndex = _sut.CalculateIndex(raceTime);

        // Assert
        actualIndex.Should().NotBeNull();
        actualIndex!.Value.Should().BeApproximately(
            expectedIndex,
            0.5m,
            because: $"index for {distance} in {timeStr} should be ~{expectedIndex} per Daniels' tables");
    }

    [Fact]
    public void CalculateIndex_LeesProfile10K48Minutes_ProducesExpectedIndex()
    {
        // Arrange
        var expectedIndex = 42.0m;
        var raceTime = new RaceTime("10K", TimeSpan.FromMinutes(48), new DateOnly(2025, 6, 15), null);

        // Act
        var actualIndex = _sut.CalculateIndex(raceTime);

        // Assert
        actualIndex.Should().NotBeNull();
        actualIndex!.Value.Should().BeApproximately(
            expectedIndex,
            0.5m,
            because: "Lee's 10K time of 48:00 should produce an index ~42");
    }

    // ─────────────────────────── New distances (Scenario 2) ───────────────────────────────────
    [Theory]
    [InlineData("1500m", "00:06:00", 44.4)]
    [InlineData("1 mile", "00:06:30", 44.3)]
    [InlineData("3k", "00:12:30", 45.7)]
    [InlineData("2 mile", "00:14:00", 43.7)]
    [InlineData("15k", "01:12:00", 43.3)]
    public void CalculateIndex_NewDistances_MatchesDanielsTableWithinTolerance(
        string distance,
        string timeStr,
        decimal expectedIndex)
    {
        // Arrange
        var time = TimeSpan.Parse(timeStr, CultureInfo.InvariantCulture);
        var raceTime = new RaceTime(distance, time, new DateOnly(2025, 1, 1), null);

        // Act
        var actualIndex = _sut.CalculateIndex(raceTime);

        // Assert
        actualIndex.Should().NotBeNull();
        actualIndex!.Value.Should().BeApproximately(
            expectedIndex,
            2.0m,
            because: $"index for {distance} in {timeStr} should be near {expectedIndex} per Daniels' tables");
    }

    // ──────────────────── Duration guards (Scenarios 3 & 4) ──────────────────────────────────
    [Fact]
    public void CalculateIndex_DurationBelow3Point5Minutes_ReturnsNull()
    {
        // Arrange — 3:00 over 1500m
        var raceTime = new RaceTime("1500m", TimeSpan.FromMinutes(3.0), new DateOnly(2025, 1, 1), null);

        // Act
        var actual = _sut.CalculateIndex(raceTime);

        // Assert
        actual.Should().BeNull(because: "race durations below 3.5 min are outside the valid range");
    }

    [Fact]
    public void CalculateIndex_DurationAbove300Minutes_ReturnsNull()
    {
        // Arrange — 6 hours over marathon
        var raceTime = new RaceTime("Marathon", TimeSpan.FromMinutes(361), new DateOnly(2025, 1, 1), null);

        // Act
        var actual = _sut.CalculateIndex(raceTime);

        // Assert
        actual.Should().BeNull(because: "race durations above 300 min are outside the valid range");
    }

    // ──────────────────── Velocity guard (Scenario 5) ────────────────────────────────────────
    [Fact]
    public void CalculateIndex_VelocityBelow50MetersPerMinute_ReturnsNull()
    {
        // Arrange — 1500m in 60 min = 25 m/min (well below 50)
        var raceTime = new RaceTime("1500m", TimeSpan.FromMinutes(60), new DateOnly(2025, 1, 1), null);

        // Act
        var actual = _sut.CalculateIndex(raceTime);

        // Assert
        actual.Should().BeNull(because: "velocity below 50 m/min is outside the valid range");
    }

    // ──────────────────── Low-index warning (Scenario 6) ─────────────────────────────────────
    [Fact]
    public void CalculateIndex_LowIndex_LogsWarning()
    {
        // Arrange — Marathon in 4h produces index ~37, below the R-035 threshold of 39
        var logger = Substitute.For<ILogger<PaceZoneIndexCalculator>>();
        logger.IsEnabled(LogLevel.Warning).Returns(true);
        var sut = new PaceZoneIndexCalculator(logger);
        var raceTime = new RaceTime("Marathon", TimeSpan.FromHours(4.0), new DateOnly(2025, 1, 1), null);

        // Act
        var index = sut.CalculateIndex(raceTime);

        // Assert
        index.Should().NotBeNull();
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ──────────────────── Unsupported distance (Scenario from legacy) ────────────────────────
    [Fact]
    public void CalculateIndex_UnsupportedDistance_ReturnsNull()
    {
        // Arrange
        var raceTime = new RaceTime("8K", TimeSpan.FromMinutes(35), new DateOnly(2025, 1, 1), null);

        // Act
        var actual = _sut.CalculateIndex(raceTime);

        // Assert
        actual.Should().BeNull(because: "8K is not a supported standard distance");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void CalculateIndex_ZeroOrNegativeTime_ReturnsNull(int totalMinutes)
    {
        // Arrange
        var raceTime = new RaceTime("5K", TimeSpan.FromMinutes(totalMinutes), new DateOnly(2025, 1, 1), null);

        // Act
        var actual = _sut.CalculateIndex(raceTime);

        // Assert
        actual.Should().BeNull(because: "a zero or negative race time is invalid");
    }

    // ──────────────────── Collection overload ─────────────────────────────────────────────────
    [Fact]
    public void CalculateIndex_EmptyCollection_ReturnsNull()
    {
        // Arrange
        var raceTimes = Array.Empty<RaceTime>();

        // Act
        var actual = _sut.CalculateIndex(raceTimes);

        // Assert
        actual.Should().BeNull(because: "no race history means no index can be computed");
    }

    [Fact]
    public void CalculateIndex_MultipleRaceTimes_ReturnsBestIndex()
    {
        // Arrange — 5K 19:56 (~index 50) should be selected over 10K 48:00 (~index 42)
        var raceTimes = new[]
        {
            new RaceTime("5K", TimeSpan.Parse("00:19:56", CultureInfo.InvariantCulture), new DateOnly(2025, 3, 1), null),
            new RaceTime("10K", TimeSpan.FromMinutes(48), new DateOnly(2025, 6, 15), null),
        };

        // Act
        var actual = _sut.CalculateIndex(raceTimes);

        // Assert
        actual.Should().NotBeNull();
        actual!.Value.Should().BeApproximately(
            50.0m,
            0.5m,
            because: "the best index from multiple races should be selected");
    }

    [Fact]
    public void CalculateIndex_CollectionWithOnlyUnsupportedDistances_ReturnsNull()
    {
        // Arrange
        var raceTimes = new[]
        {
            new RaceTime("8K", TimeSpan.FromMinutes(35), new DateOnly(2025, 1, 1), null),
            new RaceTime("12K", TimeSpan.FromMinutes(55), new DateOnly(2025, 2, 1), null),
        };

        // Act
        var actual = _sut.CalculateIndex(raceTimes);

        // Assert
        actual.Should().BeNull(because: "no supported distances means no index can be computed");
    }

    // ──────────────────── Distance case variations ────────────────────────────────────────────
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
    [InlineData("1500m")]
    [InlineData("1 mile")]
    [InlineData("1mile")]
    [InlineData("mile")]
    [InlineData("3k")]
    [InlineData("3K")]
    [InlineData("2 mile")]
    [InlineData("2mile")]
    [InlineData("15k")]
    [InlineData("15K")]
    public void CalculateIndex_DistanceCaseVariations_AreSupported(string distance)
    {
        // Arrange
        var raceTime = new RaceTime(distance, TimeSpan.FromMinutes(30), new DateOnly(2025, 1, 1), null);

        // Act
        var actual = _sut.CalculateIndex(raceTime);

        // Assert
        actual.Should().NotBeNull(because: $"distance '{distance}' should be recognized");
    }

    // ──────────────────── Cross-distance consistency ─────────────────────────────────────────
    [Theory]
    [InlineData("5K", "00:23:10", 42.0)]
    [InlineData("10K", "00:48:08", 42.0)]
    [InlineData("5K", "00:21:24", 46.0)]
    [InlineData("10K", "00:44:32", 46.0)]
    public void CalculateIndex_CrossDistanceConsistency_SameIndexFromEquivalentPerformances(
        string distance,
        string timeStr,
        decimal expectedIndex)
    {
        // Arrange
        var time = TimeSpan.Parse(timeStr, CultureInfo.InvariantCulture);
        var raceTime = new RaceTime(distance, time, new DateOnly(2025, 1, 1), null);

        // Act
        var actual = _sut.CalculateIndex(raceTime);

        // Assert
        actual.Should().NotBeNull();
        actual!.Value.Should().BeApproximately(
            expectedIndex,
            0.5m,
            because: $"equivalent {distance} performance should produce index ~{expectedIndex}");
    }
}
