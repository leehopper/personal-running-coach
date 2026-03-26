using FluentAssertions;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Models;

public class PaceRangeTests
{
    [Fact]
    public void Constructor_ValidRange_CreatesPaceRange()
    {
        // Arrange
        var expectedMin = TimeSpan.FromSeconds(300); // 5:00/km
        var expectedMax = TimeSpan.FromSeconds(360); // 6:00/km

        // Act
        var actual = new PaceRange(expectedMin, expectedMax);

        // Assert
        actual.MinPerKm.Should().Be(expectedMin);
        actual.MaxPerKm.Should().Be(expectedMax);
    }

    [Fact]
    public void Constructor_EqualMinAndMax_CreatesPaceRange()
    {
        // Arrange
        var expectedPace = TimeSpan.FromSeconds(300);

        // Act
        var actual = new PaceRange(expectedPace, expectedPace);

        // Assert
        actual.MinPerKm.Should().Be(expectedPace);
        actual.MaxPerKm.Should().Be(expectedPace);
    }

    [Fact]
    public void Constructor_InvertedRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange — MinPerKm (slower) > MaxPerKm (faster) is invalid
        var slower = TimeSpan.FromSeconds(360);
        var faster = TimeSpan.FromSeconds(300);

        // Act
        var act = () => new PaceRange(slower, faster);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("minPerKm");
    }

    [Fact]
    public void Constructor_ZeroMinPerKm_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new PaceRange(TimeSpan.Zero, TimeSpan.FromSeconds(300));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("minPerKm");
    }

    [Fact]
    public void Constructor_ZeroMaxPerKm_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new PaceRange(TimeSpan.FromSeconds(300), TimeSpan.Zero);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("maxPerKm");
    }

    [Fact]
    public void Constructor_NegativeMinPerKm_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new PaceRange(TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(300));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("minPerKm");
    }

    [Fact]
    public void Constructor_NegativeMaxPerKm_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new PaceRange(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(-1));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("maxPerKm");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var a = new PaceRange(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(360));
        var b = new PaceRange(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(360));

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var a = new PaceRange(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(360));
        var b = new PaceRange(TimeSpan.FromSeconds(310), TimeSpan.FromSeconds(360));

        // Assert
        a.Should().NotBe(b);
    }
}
