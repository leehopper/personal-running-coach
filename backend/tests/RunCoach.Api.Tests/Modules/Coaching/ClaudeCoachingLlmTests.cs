using Anthropic;
using Anthropic.Models.Messages;
using Anthropic.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RunCoach.Api.Modules.Coaching;

namespace RunCoach.Api.Tests.Modules.Coaching;

public class ClaudeCoachingLlmTests
{
    private static readonly CoachingLlmSettings DefaultSettings = new()
    {
        ApiKey = "sk-test-key-for-unit-tests",
        ModelId = "claude-sonnet-4-5-20241022",
        Temperature = 0.3,
        MaxTokens = 4096,
        MaxRetries = 3,
        TimeoutSeconds = 120,
    };

    private readonly IAnthropicClient _mockClient = Substitute.For<IAnthropicClient>();
    private readonly IMessageService _mockMessages = Substitute.For<IMessageService>();
    private readonly ILogger<ClaudeCoachingLlm> _logger = NullLogger<ClaudeCoachingLlm>.Instance;

    public ClaudeCoachingLlmTests()
    {
        _mockClient.Messages.Returns(_mockMessages);
    }

    [Fact]
    public async Task GenerateAsync_SendsCorrectParams_ToAnthropicClient()
    {
        // Arrange
        var expectedSystemPrompt = "You are a running coach.";
        var expectedUserMessage = "Generate a plan for Lee.";
        var expectedResponse = "Here is your training plan.";

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse(expectedResponse);
            });

        var sut = CreateSut();

        // Act
        await sut.GenerateAsync(expectedSystemPrompt, expectedUserMessage, CancellationToken.None);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.Model.ToString().Should().Contain(DefaultSettings.ModelId);
        capturedParams.MaxTokens.Should().Be(DefaultSettings.MaxTokens);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsTextContent_FromSingleTextBlock()
    {
        // Arrange
        var expectedText = "Here is your training plan with phases and workouts.";
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse(expectedText));

        var sut = CreateSut();

        // Act
        var actualText = await sut.GenerateAsync("system", "user message", CancellationToken.None);

        // Assert
        actualText.Should().Be(expectedText);
    }

    [Fact]
    public async Task GenerateAsync_ConcatenatesText_FromMultipleTextBlocks()
    {
        // Arrange
        var response = BuildMultiTextResponse("Part 1. ", "Part 2.");
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var sut = CreateSut();

        // Act
        var actualText = await sut.GenerateAsync("system", "user message", CancellationToken.None);

        // Assert
        actualText.Should().Be("Part 1. Part 2.");
    }

    [Fact]
    public async Task GenerateAsync_ReturnsEmpty_WhenNoContentBlocks()
    {
        // Arrange
        var response = BuildEmptyResponse();
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var sut = CreateSut();

        // Act
        var actualText = await sut.GenerateAsync("system", "user message", CancellationToken.None);

        // Assert
        actualText.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_PropagatesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns<Message>(callInfo =>
            {
                callInfo.ArgAt<CancellationToken>(1).ThrowIfCancellationRequested();
                return BuildTextResponse("should not reach");
            });

        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateAsync("system", "user", cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GenerateAsync_ThrowsArgumentException_WhenSystemPromptIsEmpty()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateAsync(string.Empty, "user message", CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("systemPrompt");
    }

    [Fact]
    public async Task GenerateAsync_ThrowsArgumentException_WhenSystemPromptIsWhitespace()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateAsync("   ", "user message", CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("systemPrompt");
    }

    [Fact]
    public async Task GenerateAsync_ThrowsArgumentException_WhenUserMessageIsEmpty()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateAsync("system", string.Empty, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("userMessage");
    }

    [Fact]
    public async Task GenerateAsync_ThrowsArgumentException_WhenUserMessageIsNull()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateAsync("system", null!, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("userMessage");
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenApiKeyIsMissing()
    {
        // Arrange
        var settingsWithNoKey = new CoachingLlmSettings
        {
            ApiKey = string.Empty,
            ModelId = "claude-sonnet-4-5-20241022",
        };
        var options = Options.Create(settingsWithNoKey);

        // Act & Assert
        var act = () => new ClaudeCoachingLlm(options, _logger);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API key*not configured*");
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenModelIdIsMissing()
    {
        // Arrange
        var settingsWithNoModel = new CoachingLlmSettings
        {
            ApiKey = "sk-test-key",
            ModelId = string.Empty,
        };
        var options = Options.Create(settingsWithNoModel);

        // Act & Assert
        var act = () => new ClaudeCoachingLlm(options, _logger);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*model ID*not configured*");
    }

    [Fact]
    public void Constructor_Succeeds_WithValidSettings()
    {
        // Arrange
        var options = Options.Create(DefaultSettings);

        // Act
        var sut = new ClaudeCoachingLlm(options, _logger);

        // Assert
        sut.Should().NotBeNull();
    }

    [Fact]
    public void DefaultSettings_HaveExpectedValues()
    {
        // Arrange & Act
        var settings = new CoachingLlmSettings();

        // Assert
        settings.ApiKey.Should().BeEmpty();
        settings.ModelId.Should().Be("claude-sonnet-4-5-20241022");
        settings.Temperature.Should().BeApproximately(0.3, 0.001);
        settings.MaxTokens.Should().Be(4096);
        settings.MaxRetries.Should().Be(3);
        settings.TimeoutSeconds.Should().Be(120);
    }

    [Fact]
    public void SectionName_IsAnthropic()
    {
        // Assert
        CoachingLlmSettings.SectionName.Should().Be("Anthropic");
    }

    [Fact]
    public async Task GenerateAsync_UsesModelIdFromSettings()
    {
        // Arrange
        var customSettings = DefaultSettings with { ModelId = "claude-opus-4-20250514" };
        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("response");
            });

        var sut = new ClaudeCoachingLlm(_mockClient, customSettings, _logger);

        // Act
        await sut.GenerateAsync("system", "user", CancellationToken.None);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.Model.ToString().Should().Contain("claude-opus-4-20250514");
    }

    [Fact]
    public async Task GenerateAsync_UsesTemperatureFromSettings()
    {
        // Arrange
        var customSettings = DefaultSettings with { Temperature = 0.7 };
        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("response");
            });

        var sut = new ClaudeCoachingLlm(_mockClient, customSettings, _logger);

        // Act
        await sut.GenerateAsync("system", "user", CancellationToken.None);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.Temperature.Should().NotBeNull();
        capturedParams.Temperature!.Value.Should().BeApproximately(0.7, 0.001);
    }

    [Fact]
    public async Task GenerateAsync_UsesMaxTokensFromSettings()
    {
        // Arrange
        var customSettings = DefaultSettings with { MaxTokens = 8192 };
        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("response");
            });

        var sut = new ClaudeCoachingLlm(_mockClient, customSettings, _logger);

        // Act
        await sut.GenerateAsync("system", "user", CancellationToken.None);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.MaxTokens.Should().Be(8192);
    }

    [Fact]
    public void ExtractTextContent_ReturnsEmpty_WhenResponseHasNoContent()
    {
        // Arrange
        var response = BuildEmptyResponse();

        // Act
        var actualText = ClaudeCoachingLlm.ExtractTextContent(response);

        // Assert
        actualText.Should().BeEmpty();
    }

    [Fact]
    public void ExtractTextContent_ReturnsSingleBlockText()
    {
        // Arrange
        var expectedText = "Single block content";
        var response = BuildTextResponse(expectedText);

        // Act
        var actualText = ClaudeCoachingLlm.ExtractTextContent(response);

        // Assert
        actualText.Should().Be(expectedText);
    }

    [Fact]
    public void ExtractTextContent_ConcatenatesMultipleTextBlocks()
    {
        // Arrange
        var response = BuildMultiTextResponse("First ", "Second");

        // Act
        var actualText = ClaudeCoachingLlm.ExtractTextContent(response);

        // Assert
        actualText.Should().Be("First Second");
    }

    /// <summary>
    /// Builds a Message response with a single text content block.
    /// Uses FromRawUnchecked to construct the SDK types since they have
    /// required members that cannot be set via initializers in tests.
    /// </summary>
    private static Message BuildTextResponse(string text)
    {
        var raw = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["id"] = ToJsonElement("msg_test_001"),
            ["type"] = ToJsonElement("message"),
            ["role"] = ToJsonElement("assistant"),
            ["model"] = ToJsonElement("claude-sonnet-4-5-20241022"),
            ["stop_reason"] = ToJsonElement("end_turn"),
            ["content"] = ToJsonElement(new[]
            {
                new { type = "text", text },
            }),
            ["usage"] = ToJsonElement(new { input_tokens = 100, output_tokens = 50 }),
        };

        return Message.FromRawUnchecked(raw);
    }

    /// <summary>
    /// Builds a Message response with multiple text content blocks.
    /// </summary>
    private static Message BuildMultiTextResponse(params string[] texts)
    {
        var contentArray = texts.Select(t => new { type = "text", text = t }).ToArray();

        var raw = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["id"] = ToJsonElement("msg_test_002"),
            ["type"] = ToJsonElement("message"),
            ["role"] = ToJsonElement("assistant"),
            ["model"] = ToJsonElement("claude-sonnet-4-5-20241022"),
            ["stop_reason"] = ToJsonElement("end_turn"),
            ["content"] = ToJsonElement(contentArray),
            ["usage"] = ToJsonElement(new { input_tokens = 100, output_tokens = 50 }),
        };

        return Message.FromRawUnchecked(raw);
    }

    /// <summary>
    /// Builds a Message response with an empty content array.
    /// </summary>
    private static Message BuildEmptyResponse()
    {
        var raw = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["id"] = ToJsonElement("msg_test_003"),
            ["type"] = ToJsonElement("message"),
            ["role"] = ToJsonElement("assistant"),
            ["model"] = ToJsonElement("claude-sonnet-4-5-20241022"),
            ["stop_reason"] = ToJsonElement("end_turn"),
            ["content"] = ToJsonElement(Array.Empty<object>()),
            ["usage"] = ToJsonElement(new { input_tokens = 100, output_tokens = 0 }),
        };

        return Message.FromRawUnchecked(raw);
    }

    private static System.Text.Json.JsonElement ToJsonElement(object value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        return System.Text.Json.JsonDocument.Parse(json).RootElement.Clone();
    }

    private ClaudeCoachingLlm CreateSut()
    {
        return new ClaudeCoachingLlm(_mockClient, DefaultSettings, _logger);
    }
}
