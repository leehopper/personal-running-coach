using FluentAssertions;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

public sealed class VoiceProseGuardTests
{
    [Fact]
    public void FindViolation_CleanGruffProse_ReturnsNull()
    {
        // Arrange
        var actual = VoiceProseGuard.FindViolation(
            "Cut Sunday to 9 km. Legs were flat from the first km. Build back to 14 next week.");

        // Assert
        actual.Should().BeNull();
    }

    [Theory]
    [InlineData("You ran hard — too hard.")] // em dash U+2014
    [InlineData("Easy run, 8–10 km.")] // en dash U+2013
    public void FindViolation_Dash_ReturnsViolation(string value)
    {
        // Assert
        VoiceProseGuard.FindViolation(value).Should().NotBeNull();
    }

    [Theory]
    [InlineData("Block starts 2026-06-13, ends 2026-07-04.")] // plain hyphen-minus dates
    [InlineData("Easy run, 8-10 km.")] // plain hyphen-minus range, not a banned dash
    public void FindViolation_PlainHyphenMinus_ReturnsNull(string value)
    {
        // Assert
        VoiceProseGuard.FindViolation(value).Should().BeNull();
    }

    [Fact]
    public void FindViolation_Exclamation_ReturnsViolation()
    {
        // Assert
        VoiceProseGuard.FindViolation("Strong work today!").Should().NotBeNull();
    }

    [Theory]
    [InlineData("Love it. Let us lock the goal.")]
    [InlineData("Great foundation to build on.")]
    [InlineData("That is AMAZING progress.")]
    public void FindViolation_SycophancyPhrase_ReturnsViolation(string value)
    {
        // Assert
        VoiceProseGuard.FindViolation(value).Should().NotBeNull();
    }

    [Fact]
    public void AssertClean_NestedObjectWithBannedPhrase_Throws()
    {
        // Arrange
        var output = new { rationale = "Love that target.", nested = new { note = "fine" } };

        // Act
        var act = () => VoiceProseGuard.AssertClean("test", output);

        // Assert
        act.Should().Throw<Xunit.Sdk.XunitException>().WithMessage("*Love that*");
    }

    [Fact]
    public void AssertClean_ArrayElementWithBannedPhrase_Throws()
    {
        // Arrange
        var output = new { notes = new[] { "fine", "Love that target." } };

        // Act
        var act = () => VoiceProseGuard.AssertClean("test", output);

        // Assert
        act.Should().Throw<Xunit.Sdk.XunitException>().WithMessage("*Love that*");
    }

    [Fact]
    public void AssertClean_CleanObject_DoesNotThrow()
    {
        // Arrange
        var output = new { rationale = "Held the week flat. Volume rebuilds Monday." };

        // Act
        var act = () => VoiceProseGuard.AssertClean("test", output);

        // Assert
        act.Should().NotThrow();
    }
}
