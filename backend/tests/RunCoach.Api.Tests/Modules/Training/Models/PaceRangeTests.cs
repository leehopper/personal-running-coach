using FluentAssertions;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Models;

public class PaceRangeTests
{
    [Fact]
    public void Constructor_ValidRange_CreatesPaceRange()
    {
        // Arrange
        var expectedFast = Pace.FromSecondsPerKm(300); // 5:00/km
        var expectedSlow = Pace.FromSecondsPerKm(360); // 6:00/km

        // Act
        var actual = new PaceRange(expectedFast, expectedSlow);

        // Assert
        actual.Fast.Should().Be(expectedFast);
        actual.Slow.Should().Be(expectedSlow);
    }

    [Fact]
    public void Constructor_EqualFastAndSlow_CreatesPaceRange()
    {
        // Arrange
        var expectedPace = Pace.FromSecondsPerKm(300);

        // Act
        var actual = new PaceRange(expectedPace, expectedPace);

        // Assert
        actual.Fast.Should().Be(expectedPace);
        actual.Slow.Should().Be(expectedPace);
    }

    [Fact]
    public void Constructor_FastSlowerThanSlow_ThrowsArgumentException()
    {
        // Arrange — Fast pace that is actually slower (higher sec/km) than Slow is invalid
        var slowPace = Pace.FromSecondsPerKm(300);  // 5:00/km — faster
        var fastPace = Pace.FromSecondsPerKm(360);  // 6:00/km — slower (invalid as Fast)

        // Act
        var act = () => new PaceRange(fastPace, slowPace);

        // Assert
        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("fast");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var a = new PaceRange(Pace.FromSecondsPerKm(300), Pace.FromSecondsPerKm(360));
        var b = new PaceRange(Pace.FromSecondsPerKm(300), Pace.FromSecondsPerKm(360));

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var a = new PaceRange(Pace.FromSecondsPerKm(300), Pace.FromSecondsPerKm(360));
        var b = new PaceRange(Pace.FromSecondsPerKm(310), Pace.FromSecondsPerKm(360));

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void Fast_IsFasterThanSlow()
    {
        // Arrange
        var range = new PaceRange(Pace.FromSecondsPerKm(300), Pace.FromSecondsPerKm(360));

        // Assert
        range.Fast.IsFasterThan(range.Slow).Should().BeTrue(
            because: "Fast end should always be faster than Slow end");
    }

    [Fact]
    public void WithExpression_CannotReassignFastOrSlow()
    {
        // Arrange — a valid range.
        var range = new PaceRange(Pace.FromSecondsPerKm(300), Pace.FromSecondsPerKm(360));

        // Assert — the Fast and Slow properties are declared { get; } (no init setter),
        // so a `with` expression that tries to reassign them is a compile-time error.
        // This prevents invariant bypass like:
        //     range with { Slow = Pace.FromSecondsPerKm(280) }
        // from producing a PaceRange where Slow is faster than Fast.
        //
        // Compile-time error behaviour cannot be asserted at runtime; this test
        // pins the runtime invariant and documents the compile-time guard via
        // this comment and the XML docs on PaceRange itself.
        range.Fast.IsFasterThan(range.Slow).Should().BeTrue();
    }
}
