using FluentAssertions;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Training.Workouts;

public class WorkoutSplitTests
{
    [Fact]
    public void Constructor_ValidValues_RoundTripsEveryField()
    {
        // Act
        var actual = new WorkoutSplit(2, 1000.0, 295.0, 295.0, 145);

        // Assert
        actual.Index.Should().Be(2);
        actual.DistanceMeters.Should().Be(1000.0);
        actual.DurationSeconds.Should().Be(295.0);
        actual.PaceSecPerKm.Should().Be(295.0);
        actual.AverageHeartRate.Should().Be(145);
    }

    [Fact]
    public void Constructor_NullHeartRate_IsAllowed()
    {
        // Act
        var actual = new WorkoutSplit(1, 1000.0, 300.0, 300.0, AverageHeartRate: null);

        // Assert — heart rate is the only optional field.
        actual.AverageHeartRate.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveIndex_Throws(int index)
    {
        // Act
        var act = () => new WorkoutSplit(index, 1000.0, 300.0, 300.0, null);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("Index");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public void Constructor_NonPositiveDistance_Throws(double distanceMeters)
    {
        // Act
        var act = () => new WorkoutSplit(1, distanceMeters, 300.0, 300.0, null);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("DistanceMeters");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public void Constructor_NonPositiveDuration_Throws(double durationSeconds)
    {
        // Act
        var act = () => new WorkoutSplit(1, 1000.0, durationSeconds, 300.0, null);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("DurationSeconds");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public void Constructor_NonPositivePace_Throws(double paceSecPerKm)
    {
        // Act
        var act = () => new WorkoutSplit(1, 1000.0, 300.0, paceSecPerKm, null);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("PaceSecPerKm");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_NonPositiveHeartRate_Throws(int averageHeartRate)
    {
        // Act
        var act = () => new WorkoutSplit(1, 1000.0, 300.0, 300.0, averageHeartRate);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("AverageHeartRate");
    }
}
