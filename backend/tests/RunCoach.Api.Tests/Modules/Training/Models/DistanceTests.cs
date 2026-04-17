using FluentAssertions;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Models;

public class DistanceTests
{
    [Fact]
    public void FromKilometers_RoundTrip_PreservesMetersKilometersAndMiles()
    {
        // Arrange
        var expected = Distance.FromKilometers(5.0);

        // Act
        var actualMeters = expected.Meters;
        var actualKilometers = expected.Kilometers;
        var actualMiles = expected.Miles;

        // Assert
        actualMeters.Should().BeApproximately(5000.0, 1e-10);
        actualKilometers.Should().BeApproximately(5.0, 1e-10);
        actualMiles.Should().BeApproximately(3.10686, 1e-5);
    }

    [Fact]
    public void FromMeters_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => Distance.FromMeters(-1.0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromKilometers_ZeroValue_CreatesZeroDistance()
    {
        // Act
        var actual = Distance.FromKilometers(0.0);

        // Assert
        actual.Meters.Should().Be(0.0);
        actual.Kilometers.Should().Be(0.0);
    }

    [Fact]
    public void FromMiles_RoundTrip_ConvertsBackToMiles()
    {
        // Arrange
        var expectedMiles = 6.2137;

        // Act
        var actual = Distance.FromMiles(expectedMiles);

        // Assert
        actual.Miles.Should().BeApproximately(expectedMiles, 1e-4);
    }

    [Fact]
    public void RecordEquality_SameMeters_AreEqual()
    {
        // Arrange
        var a = Distance.FromMeters(5000.0);
        var b = Distance.FromKilometers(5.0);

        // Assert
        a.Should().Be(b);
    }
}
