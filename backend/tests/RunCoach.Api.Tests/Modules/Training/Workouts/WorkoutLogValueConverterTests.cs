using FluentAssertions;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Training.Workouts;

/// <summary>
/// Unit tests for the repository's first EF <c>ValueConverter</c>s (DEC-072).
/// These exercise the converters directly through their model⇄provider
/// delegates so a units mistake is caught without a database round-trip.
/// </summary>
public class WorkoutLogValueConverterTests
{
    [Fact]
    public void DistanceConverter_RoundTrip_PreservesMeters()
    {
        // Arrange
        var converter = new DistanceValueConverter();
        var expected = Distance.FromMeters(5000.0);

        // Act
        var provider = (double)converter.ConvertToProvider(expected)!;
        var actual = (Distance)converter.ConvertFromProvider(provider)!;

        // Assert — stored as meters, round-trips with value semantics.
        provider.Should().Be(5000.0);
        actual.Meters.Should().Be(5000.0);
        actual.Should().Be(expected);
    }

    [Fact]
    public void DurationConverter_RoundTrip_PreservesMinutesAsTicks()
    {
        // Arrange
        var converter = new DurationValueConverter();
        var expected = Duration.FromMinutes(25.0);

        // Act
        var provider = (long)converter.ConvertToProvider(expected)!;
        var actual = (Duration)converter.ConvertFromProvider(provider)!;

        // Assert — stored as ticks (NOT minutes), round-trips to 25 minutes.
        provider.Should().Be(TimeSpan.FromMinutes(25.0).Ticks);
        actual.TotalMinutes.Should().Be(25.0);
        actual.Should().Be(expected);
    }

    [Fact]
    public void PaceConverter_RoundTrip_PreservesSecondsPerKm()
    {
        // Arrange
        var converter = new PaceValueConverter();
        var expected = Pace.FromSecondsPerKm(300.0);

        // Act
        var provider = (double)converter.ConvertToProvider(expected)!;
        var actual = (Pace)converter.ConvertFromProvider(provider)!;

        // Assert
        provider.Should().Be(300.0);
        actual.SecondsPerKm.Should().Be(300.0);
    }
}
