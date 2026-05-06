using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Onboarding;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Unit tests for <see cref="AnthropicContentBlock.Validate"/> covering the
/// Type vs Text contract that the closed-shape grammar (DEC-058) cannot
/// enforce at the schema level.
/// </summary>
public sealed class AnthropicContentBlockTests
{
    [Fact]
    public void Validate_TextWithNonEmptyText_DoesNotThrow()
    {
        // Arrange
        var block = new AnthropicContentBlock
        {
            Type = AnthropicContentBlockType.Text,
            Text = "Hello, runner.",
        };

        // Act
        var act = () => block.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ThinkingWithEmptyText_DoesNotThrow()
    {
        // Arrange
        var block = new AnthropicContentBlock
        {
            Type = AnthropicContentBlockType.Thinking,
            Text = string.Empty,
        };

        // Act
        var act = () => block.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_TextWithEmptyOrNullText_Throws(string? text)
    {
        // Arrange
        var block = new AnthropicContentBlock
        {
            Type = AnthropicContentBlockType.Text,
            Text = text!,
        };

        // Act
        var act = () => block.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Type=Text must carry a non-empty Text payload*");
    }

    [Fact]
    public void Validate_ThinkingWithNonEmptyText_Throws()
    {
        // Arrange
        var block = new AnthropicContentBlock
        {
            Type = AnthropicContentBlockType.Thinking,
            Text = "should be empty for thinking blocks",
        };

        // Act
        var act = () => block.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Type=Thinking must carry an empty Text payload*");
    }

    [Fact]
    public void Validate_UnknownType_Throws()
    {
        // Arrange — out-of-range enum cast to simulate a malformed payload.
        var block = new AnthropicContentBlock
        {
            Type = (AnthropicContentBlockType)999,
            Text = string.Empty,
        };

        // Act
        var act = () => block.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown AnthropicContentBlockType*");
    }
}
