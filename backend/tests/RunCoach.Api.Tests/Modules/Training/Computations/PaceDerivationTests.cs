using FluentAssertions;
using RunCoach.Api.Modules.Training.Computations;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Computations;

/// <summary>
/// Unit tests for <see cref="PaceDerivation"/>, the shared divide-by-zero-guarded
/// pace-derivation utility lifted out of the presentation-layer
/// <c>RecentLogFormatter</c> (Slice 3 PR2 / Unit 1).
/// </summary>
public sealed class PaceDerivationTests
{
    [Fact]
    public void TryDerive_WithPositiveDistanceAndDuration_ReturnsSecondsPerKm()
    {
        // Arrange — 40 minutes over 8 km is exactly 5:00/km = 300 sec/km.
        var distance = Distance.FromKilometers(8.0);
        var duration = Duration.FromMinutes(40.0);

        // Act
        var actual = PaceDerivation.TryDerive(distance, duration);

        // Assert
        actual.Should().NotBeNull();
        actual!.Value.SecondsPerKm.Should().BeApproximately(300.0, 1e-9);
    }

    [Fact]
    public void TryDerive_WithZeroDistance_ReturnsNullWithoutThrowing()
    {
        // Arrange — a skipped/degenerate log carries no distance.
        var distance = Distance.FromKilometers(0);
        var duration = Duration.FromMinutes(30);

        // Act
        var act = () => PaceDerivation.TryDerive(distance, duration);

        // Assert — defined "no pace" result, never a divide-by-zero or Pace-ctor throw.
        act.Should().NotThrow();
        act().Should().BeNull(because: "a zero-distance log cannot yield a meaningful pace");
    }

    [Fact]
    public void TryDerive_WithZeroDuration_ReturnsNullWithoutThrowing()
    {
        // Arrange
        var distance = Distance.FromKilometers(5);
        var duration = Duration.FromTicks(0);

        // Act
        var act = () => PaceDerivation.TryDerive(distance, duration);

        // Assert
        act.Should().NotThrow();
        act().Should().BeNull(because: "a zero-duration log cannot yield a meaningful pace");
    }
}
