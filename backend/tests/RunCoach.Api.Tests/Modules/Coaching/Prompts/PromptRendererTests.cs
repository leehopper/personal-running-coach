using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Prompts;

namespace RunCoach.Api.Tests.Modules.Coaching.Prompts;

public class PromptRendererTests
{
    [Fact]
    public void Render_EmptyTokens_ReturnsTemplateUnchanged()
    {
        // Arrange
        var template = "Hello {{name}}, welcome to {{place}}.";
        var tokens = new Dictionary<string, string>();

        // Act
        var actual = PromptRenderer.Render(template, tokens);

        // Assert
        actual.Should().Be(template);
    }

    [Fact]
    public void Render_SingleToken_ReplacesCorrectly()
    {
        // Arrange
        var template = "Hello {{name}}, welcome.";
        var tokens = new Dictionary<string, string> { ["name"] = "Lee" };

        // Act
        var actual = PromptRenderer.Render(template, tokens);

        // Assert
        actual.Should().Be("Hello Lee, welcome.");
    }

    [Fact]
    public void Render_MultipleTokens_ReplacesAllCorrectly()
    {
        // Arrange
        var template = "{{profile}}\n{{training_history}}\n{{conversation}}";
        var tokens = new Dictionary<string, string>
        {
            ["profile"] = "Name: Lee",
            ["training_history"] = "Week 1: 30km",
            ["conversation"] = "[User]: Hi",
        };

        // Act
        var actual = PromptRenderer.Render(template, tokens);

        // Assert
        actual.Should().Be("Name: Lee\nWeek 1: 30km\n[User]: Hi");
    }

    [Fact]
    public void Render_UnmatchedTokens_LeavesThemInPlace()
    {
        // Arrange
        var template = "Hello {{name}}, your goal is {{goal}}.";
        var tokens = new Dictionary<string, string> { ["name"] = "Lee" };

        // Act
        var actual = PromptRenderer.Render(template, tokens);

        // Assert
        actual.Should().Be("Hello Lee, your goal is {{goal}}.");
    }

    [Fact]
    public void Render_DuplicateTokenInTemplate_ReplacesBothOccurrences()
    {
        // Arrange
        var template = "{{name}} is a runner. {{name}} runs daily.";
        var tokens = new Dictionary<string, string> { ["name"] = "Lee" };

        // Act
        var actual = PromptRenderer.Render(template, tokens);

        // Assert
        actual.Should().Be("Lee is a runner. Lee runs daily.");
    }

    [Fact]
    public void Render_EmptyReplacementValue_ReplacesWithEmptyString()
    {
        // Arrange
        var template = "Profile: {{profile}} End.";
        var tokens = new Dictionary<string, string> { ["profile"] = string.Empty };

        // Act
        var actual = PromptRenderer.Render(template, tokens);

        // Assert
        actual.Should().Be("Profile:  End.");
    }

    [Fact]
    public void Render_NullTemplate_ThrowsArgumentNullException()
    {
        // Arrange
        var tokens = new Dictionary<string, string>();

        // Act
        var act = () => PromptRenderer.Render(null!, tokens);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("template");
    }

    [Fact]
    public void Render_NullTokens_ThrowsArgumentNullException()
    {
        // Arrange
        var template = "Hello.";

        // Act
        var act = () => PromptRenderer.Render(template, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("tokens");
    }

    [Fact]
    public void Render_ContextTemplate_ReplacesAllSections()
    {
        // Arrange
        var template = "=== PROFILE ===\n{{profile}}\n=== PACES ===\n{{training_paces}}";
        var tokens = new Dictionary<string, string>
        {
            ["profile"] = "Name: Lee",
            ["training_paces"] = "Easy: 5:50-6:30/km",
        };

        // Act
        var actual = PromptRenderer.Render(template, tokens);

        // Assert
        actual.Should().Contain("Name: Lee");
        actual.Should().Contain("Easy: 5:50-6:30/km");
        actual.Should().NotContain("{{profile}}");
        actual.Should().NotContain("{{training_paces}}");
    }
}
