using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;
using RunCoach.Api.Modules.Training.Profiles;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Unit tests for <see cref="EvalTestBase"/> static helpers and infrastructure.
/// These tests do NOT require an API key since they only test non-LLM functionality.
/// </summary>
public sealed class EvalTestBaseTests
{
    [Theory]
    [InlineData("Record", EvalCacheMode.Record)]
    [InlineData("record", EvalCacheMode.Record)]
    [InlineData("RECORD", EvalCacheMode.Record)]
    [InlineData("Replay", EvalCacheMode.Replay)]
    [InlineData("replay", EvalCacheMode.Replay)]
    [InlineData("REPLAY", EvalCacheMode.Replay)]
    [InlineData("Auto", EvalCacheMode.Auto)]
    [InlineData("auto", EvalCacheMode.Auto)]
    [InlineData("AUTO", EvalCacheMode.Auto)]
    public void ParseCacheMode_ValidValues_ParsesCaseInsensitively(string envValue, EvalCacheMode expected)
    {
        EvalTestBase.ParseCacheMode(envValue).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("invalid")]
    [InlineData("Recording")]
    public void ParseCacheMode_InvalidOrEmpty_DefaultsToAuto(string? envValue)
    {
        EvalTestBase.ParseCacheMode(envValue).Should().Be(EvalCacheMode.Auto);
    }

    [Fact]
    public void ResolveEffectiveMode_AutoWithApiKey_ReturnsRecord()
    {
        EvalTestBase.ResolveEffectiveMode(EvalCacheMode.Auto, hasApiKey: true)
            .Should().Be(EvalCacheMode.Record);
    }

    [Fact]
    public void ResolveEffectiveMode_AutoWithoutApiKey_ReturnsReplay()
    {
        EvalTestBase.ResolveEffectiveMode(EvalCacheMode.Auto, hasApiKey: false)
            .Should().Be(EvalCacheMode.Replay);
    }

    [Fact]
    public void ResolveEffectiveMode_ExplicitRecord_IgnoresApiKeyStatus()
    {
        EvalTestBase.ResolveEffectiveMode(EvalCacheMode.Record, hasApiKey: false)
            .Should().Be(EvalCacheMode.Record);
    }

    [Fact]
    public void ResolveEffectiveMode_ExplicitReplay_IgnoresApiKeyStatus()
    {
        EvalTestBase.ResolveEffectiveMode(EvalCacheMode.Replay, hasApiKey: true)
            .Should().Be(EvalCacheMode.Replay);
    }

    [Fact]
    public async Task ReplayGuardChatClient_ThrowsWithClientName()
    {
        var innerStub = Substitute.For<IChatClient>();
        using var client = new ReplayGuardChatClient(innerStub, "sonnet");

        var act = () => client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "test")],
            cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cache miss for 'sonnet' client*")
            .WithMessage("*EVAL_CACHE_MODE=Record*");
    }

    [Fact]
    public void ResolveEffectiveMode_RecordWithoutKey_StillReturnsRecord()
    {
        // Record mode is explicit — ResolveEffectiveMode should return Record
        // even without API key. The constructor handles the fail-fast.
        EvalTestBase.ResolveEffectiveMode(EvalCacheMode.Record, hasApiKey: false)
            .Should().Be(EvalCacheMode.Record);
    }

    [Fact]
    public void LoadProfile_ValidName_ReturnsProfile()
    {
        // Arrange & Act
        var profile = EvalTestBase.LoadProfile("lee");

        // Assert
        profile.Should().NotBeNull();
        profile.UserProfile.Name.Should().Be("Lee");
    }

    [Fact]
    public void LoadProfile_CaseInsensitive_ReturnsProfile()
    {
        // Arrange & Act
        var profile = EvalTestBase.LoadProfile("Sarah");

        // Assert
        profile.Should().NotBeNull();
        profile.UserProfile.Name.Should().Be("Sarah");
    }

    [Fact]
    public void LoadProfile_InvalidName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => EvalTestBase.LoadProfile("unknown");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Unknown profile*");
    }

    [Fact]
    public void LoadProfile_AllFiveProfiles_LoadSuccessfully()
    {
        // Arrange
        var expectedNames = new[] { "sarah", "lee", "maria", "james", "priya" };

        // Act & Assert
        foreach (var name in expectedNames)
        {
            var profile = EvalTestBase.LoadProfile(name);
            profile.Should().NotBeNull(because: $"profile '{name}' should be loadable");
            profile.UserProfile.Should().NotBeNull();
            profile.GoalState.Should().NotBeNull();
        }
    }

    [Fact]
    public void GetOutputDirectory_ReturnsPathContainingPoc1EvalResults()
    {
        // Arrange & Act
        var actualDir = EvalTestBase.GetOutputDirectory();

        // Assert
        actualDir.Should().EndWith("poc1-eval-results");
        actualDir.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WriteEvalResultAsync_WritesJsonFile()
    {
        // Arrange
        var scenarioName = $"test-write-{Guid.NewGuid():N}";
        var testData = new { Message = "test eval output", Timestamp = DateTime.UtcNow.ToString("o") };

        try
        {
            // Act
            await EvalTestBase.WriteEvalResultAsync(scenarioName, testData);

            // Assert
            var outputPath = Path.Combine(EvalTestBase.GetOutputDirectory(), $"{scenarioName}.json");
            File.Exists(outputPath).Should().BeTrue();

            var content = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
            content.Should().Contain("test eval output");
        }
        finally
        {
            // Cleanup
            var outputPath = Path.Combine(EvalTestBase.GetOutputDirectory(), $"{scenarioName}.json");
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
