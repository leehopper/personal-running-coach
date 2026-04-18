using FluentAssertions;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Models;

public class PaceTests
{
    [Fact]
    public void FromSecondsPerKm_ValidValue_StoresValueAsSeconds()
    {
        // Act
        var pace = Pace.FromSecondsPerKm(300.0);

        // Assert
        pace.SecondsPerKm.Should().Be(300.0);
    }

    [Fact]
    public void FromSecondsPerKm_ZeroValue_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => Pace.FromSecondsPerKm(0.0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromSecondsPerKm_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => Pace.FromSecondsPerKm(-1.0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromTimeSpan_RoundTrip_PreservesSeconds()
    {
        // Arrange
        var timePerKm = TimeSpan.FromSeconds(360.0);

        // Act
        var pace = Pace.FromTimeSpan(timePerKm);
        var roundTripped = pace.ToTimeSpan();

        // Assert
        roundTripped.TotalSeconds.Should().BeApproximately(
            360.0,
            1e-10);
    }

    [Fact]
    public void IsFasterThan_LowerSecondsPerKm_ReturnsTrue()
    {
        // Arrange
        var faster = Pace.FromSecondsPerKm(300.0);
        var slower = Pace.FromSecondsPerKm(360.0);

        // Act
        var actual = faster.IsFasterThan(slower);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void IsFasterThan_HigherSecondsPerKm_ReturnsFalse()
    {
        // Arrange
        var slower = Pace.FromSecondsPerKm(360.0);
        var faster = Pace.FromSecondsPerKm(300.0);

        // Act
        var actual = slower.IsFasterThan(faster);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsFasterThan_EqualSecondsPerKm_ReturnsFalse()
    {
        // Arrange
        var pace = Pace.FromSecondsPerKm(300.0);

        // Act
        var actual = pace.IsFasterThan(pace);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsSlowerThan_HigherSecondsPerKm_ReturnsTrue()
    {
        // Arrange
        var slower = Pace.FromSecondsPerKm(360.0);
        var faster = Pace.FromSecondsPerKm(300.0);

        // Act
        var actual = slower.IsSlowerThan(faster);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void IsSlowerThan_LowerSecondsPerKm_ReturnsFalse()
    {
        // Arrange
        var faster = Pace.FromSecondsPerKm(300.0);
        var slower = Pace.FromSecondsPerKm(360.0);

        // Act
        var actual = faster.IsSlowerThan(slower);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsSlowerThan_EqualSecondsPerKm_ReturnsFalse()
    {
        // Arrange
        var pace = Pace.FromSecondsPerKm(300.0);

        // Act
        var actual = pace.IsSlowerThan(pace);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_SameSecondsPerKm_AreEqual()
    {
        // Arrange
        var a = Pace.FromSecondsPerKm(300.0);
        var b = Pace.FromSecondsPerKm(300.0);

        // Assert
        a.Should().Be(b);
    }
}
