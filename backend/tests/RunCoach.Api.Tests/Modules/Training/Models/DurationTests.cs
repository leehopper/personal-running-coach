using FluentAssertions;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Models;

public class DurationTests
{
    [Fact]
    public void FromMinutes_RoundTrip_PreservesTicksAndMinutes()
    {
        // Act
        var actual = Duration.FromMinutes(25.0);

        // Assert
        actual.Ticks.Should().Be(TimeSpan.FromMinutes(25.0).Ticks);
        actual.TotalMinutes.Should().Be(25.0);
    }

    [Fact]
    public void FromTicks_RoundTrip_PreservesTicksAndTimeSpan()
    {
        // Arrange
        var expectedTicks = TimeSpan.FromMinutes(42.5).Ticks;

        // Act
        var actual = Duration.FromTicks(expectedTicks);

        // Assert
        actual.Ticks.Should().Be(expectedTicks);
        actual.ToTimeSpan().Should().Be(TimeSpan.FromMinutes(42.5));
    }

    [Fact]
    public void FromSeconds_AndFromMinutes_AgreeOnTicks()
    {
        // Act
        var fromSeconds = Duration.FromSeconds(600.0);
        var fromMinutes = Duration.FromMinutes(10.0);

        // Assert — record equality on the same tick count.
        fromSeconds.Should().Be(fromMinutes);
    }

    [Fact]
    public void FromMinutes_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => Duration.FromMinutes(-1.0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromTicks_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => Duration.FromTicks(-1L);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
