using FluentAssertions;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Models;

public class DecimalRangeTests
{
    [Fact]
    public void Constructor_ValidRange_CreatesDecimalRange()
    {
        // Arrange
        var expectedMin = 30m;
        var expectedMax = 50m;

        // Act
        var actual = new DecimalRange(expectedMin, expectedMax);

        // Assert
        actual.Min.Should().Be(expectedMin);
        actual.Max.Should().Be(expectedMax);
    }

    [Fact]
    public void Constructor_EqualMinAndMax_CreatesDecimalRange()
    {
        // Arrange
        var expectedValue = 40m;

        // Act
        var actual = new DecimalRange(expectedValue, expectedValue);

        // Assert
        actual.Min.Should().Be(expectedValue);
        actual.Max.Should().Be(expectedValue);
    }

    [Fact]
    public void Constructor_ZeroMinAndMax_CreatesDecimalRange()
    {
        // Act
        var actual = new DecimalRange(0m, 0m);

        // Assert
        actual.Min.Should().Be(0m);
        actual.Max.Should().Be(0m);
    }

    [Fact]
    public void Constructor_ZeroMinPositiveMax_CreatesDecimalRange()
    {
        // Act
        var actual = new DecimalRange(0m, 50m);

        // Assert
        actual.Min.Should().Be(0m);
        actual.Max.Should().Be(50m);
    }

    [Fact]
    public void Constructor_InvertedRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange — Min > Max is invalid
        var higher = 50m;
        var lower = 30m;

        // Act
        var act = () => new DecimalRange(higher, lower);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("min");
    }

    [Fact]
    public void Constructor_NegativeMin_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new DecimalRange(-1m, 50m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("min");
    }

    [Fact]
    public void Constructor_NegativeMax_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new DecimalRange(0m, -1m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("max");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var a = new DecimalRange(30m, 50m);
        var b = new DecimalRange(30m, 50m);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var a = new DecimalRange(30m, 50m);
        var b = new DecimalRange(35m, 50m);

        // Assert
        a.Should().NotBe(b);
    }
}
