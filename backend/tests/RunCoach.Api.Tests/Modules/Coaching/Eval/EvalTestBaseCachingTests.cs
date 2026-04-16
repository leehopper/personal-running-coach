using FluentAssertions;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Tests for <see cref="EvalTestBase"/> caching infrastructure initialization.
/// Uses a concrete test fixture to verify the base class behavior.
/// </summary>
[Trait("Category", "Eval")]
public sealed class EvalTestBaseCachingTests : EvalTestBase
{
    [Fact]
    public async Task CreateSonnetScenarioRunAsync_ReturnsScenarioRun()
    {
        if (!CanRunEvals)
        {
            return;
        }

        // Act
        await using var run = await CreateSonnetScenarioRunAsync("test.sonnet.init");

        // Assert
        run.Should().NotBeNull();
        run.ChatConfiguration.Should().NotBeNull();
        run.ChatConfiguration!.ChatClient.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateHaikuScenarioRunAsync_ReturnsScenarioRun()
    {
        if (!CanRunEvals)
        {
            return;
        }

        // Act
        await using var run = await CreateHaikuScenarioRunAsync("test.haiku.init");

        // Assert
        run.Should().NotBeNull();
        run.ChatConfiguration.Should().NotBeNull();
        run.ChatConfiguration!.ChatClient.Should().NotBeNull();
    }

    [Fact]
    public void Settings_HasCorrectDefaults()
    {
        // Assert
        Settings.Should().NotBeNull();
        Settings.ModelId.Should().Be("claude-sonnet-4-6");
        Settings.JudgeModelId.Should().Be("claude-haiku-4-5");
        Settings.MaxTokens.Should().Be(8192);
    }

    [Fact]
    public void Assembler_IsAvailable()
    {
        // Assert
        Assembler.Should().NotBeNull();
    }

    [Fact]
    public async Task AssembleContextAsync_WithProfile_ReturnsAssembledPrompt()
    {
        // Arrange
        var profile = LoadProfile("lee");

        // Act
        var assembled = await AssembleContextAsync(profile, ct: TestContext.Current.CancellationToken);

        // Assert
        assembled.Should().NotBeNull();
        assembled.SystemPrompt.Should().NotBeNullOrWhiteSpace();
        assembled.EstimatedTokenCount.Should().BeGreaterThan(0);
    }
}
