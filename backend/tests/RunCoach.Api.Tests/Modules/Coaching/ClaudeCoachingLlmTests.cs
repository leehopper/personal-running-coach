using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Anthropic.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Tests.Modules.Coaching;

public class ClaudeCoachingLlmTests
{
    private static readonly CoachingLlmSettings DefaultSettings = new()
    {
        ApiKey = "sk-test-key-for-unit-tests",
        ModelId = "claude-sonnet-4-5-20250514",
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
        await sut.GenerateAsync(expectedSystemPrompt, expectedUserMessage, TestContext.Current.CancellationToken);

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
        var actualText = await sut.GenerateAsync("system", "user message", TestContext.Current.CancellationToken);

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
        var actualText = await sut.GenerateAsync("system", "user message", TestContext.Current.CancellationToken);

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
        var actualText = await sut.GenerateAsync("system", "user message", TestContext.Current.CancellationToken);

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
        await sut.Invoking(s => s.GenerateAsync(string.Empty, "user message", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("systemPrompt");
    }

    [Fact]
    public async Task GenerateAsync_ThrowsArgumentException_WhenSystemPromptIsWhitespace()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateAsync("   ", "user message", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("systemPrompt");
    }

    [Fact]
    public async Task GenerateAsync_ThrowsArgumentException_WhenUserMessageIsEmpty()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateAsync("system", string.Empty, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("userMessage");
    }

    [Fact]
    public async Task GenerateAsync_ThrowsArgumentException_WhenUserMessageIsNull()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateAsync("system", null!, TestContext.Current.CancellationToken))
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
            ModelId = "claude-sonnet-4-5-20250514",
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
        settings.ModelId.Should().Be("claude-sonnet-4-6");
        settings.Temperature.Should().BeApproximately(0.3, 0.001);
        settings.MaxTokens.Should().Be(8192);
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
        await sut.GenerateAsync("system", "user", TestContext.Current.CancellationToken);

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
        await sut.GenerateAsync("system", "user", TestContext.Current.CancellationToken);

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
        await sut.GenerateAsync("system", "user", TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task GenerateStructuredAsync_SendsOutputConfig_WithJsonSchema()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(
            new MacroPlanOutput
            {
                TotalWeeks = 12,
                GoalDescription = "Run a marathon",
                Rationale = "Progressive build",
                Warnings = "None",
                Phases = [],
            },
            ClaudeCoachingLlm.StructuredOutputSerializerOptions);

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse(jsonResponse);
            });

        var sut = CreateSut();

        // Act
        var result = await sut.GenerateStructuredAsync<MacroPlanOutput>(
            "system", "user message", TestContext.Current.CancellationToken);

        // Assert — OutputConfig carries a JsonOutputFormat with the MacroPlanOutput schema
        capturedParams.Should().NotBeNull();
        capturedParams!.OutputConfig.Should().NotBeNull();
        var jsonFormat = capturedParams.OutputConfig!.Format.Should().BeOfType<JsonOutputFormat>().Which;
        jsonFormat.Schema.Should().NotBeNull();
        jsonFormat.Schema.Should().ContainKey("properties");

        var propertiesJson = jsonFormat.Schema["properties"].GetRawText();
        var properties = JsonDocument.Parse(propertiesJson).RootElement;
        properties.TryGetProperty("total_weeks", out _).Should().BeTrue("schema should contain total_weeks");
        properties.TryGetProperty("goal_description", out _).Should().BeTrue("schema should contain goal_description");
        properties.TryGetProperty("phases", out _).Should().BeTrue("schema should contain phases");

        // Assert — deserialized result is correct
        result.Should().NotBeNull();
        result.GoalDescription.Should().Be("Run a marathon");
        result.TotalWeeks.Should().Be(12);
    }

    [Fact]
    public async Task GenerateStructuredAsync_DeserializesTypedRecord_FromJsonResponse()
    {
        // Arrange
        var expected = new MacroPlanOutput
        {
            TotalWeeks = 16,
            GoalDescription = "Sub-2 hour half marathon",
            Rationale = "Focus on easy mileage",
            Warnings = "Build gradually",
            Phases =
            [
                new PlanPhaseOutput
                {
                    PhaseType = PhaseType.Base,
                    Weeks = 4,
                    WeeklyDistanceStartKm = 25,
                    WeeklyDistanceEndKm = 35,
                    IntensityDistribution = "80/20",
                    AllowedWorkoutTypes = [WorkoutType.Easy, WorkoutType.LongRun],
                    TargetPaceEasySecPerKm = 360,
                    TargetPaceFastSecPerKm = 300,
                    Notes = "Build aerobic foundation",
                    IncludesDeload = false,
                },
            ],
        };

        var jsonResponse = JsonSerializer.Serialize(
            expected,
            ClaudeCoachingLlm.StructuredOutputSerializerOptions);

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse(jsonResponse));

        var sut = CreateSut();

        // Act
        var actual = await sut.GenerateStructuredAsync<MacroPlanOutput>(
            "system", "user message", TestContext.Current.CancellationToken);

        // Assert
        actual.GoalDescription.Should().Be("Sub-2 hour half marathon");
        actual.TotalWeeks.Should().Be(16);
        actual.Phases.Should().HaveCount(1);
        actual.Phases[0].PhaseType.Should().Be(PhaseType.Base);
        actual.Phases[0].TargetPaceEasySecPerKm.Should().Be(360);
    }

    [Fact]
    public async Task GenerateStructuredAsync_ThrowsArgumentException_WhenSystemPromptIsEmpty()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateStructuredAsync<MacroPlanOutput>(
                string.Empty, "user message", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("systemPrompt");
    }

    [Fact]
    public async Task GenerateStructuredAsync_ThrowsArgumentException_WhenUserMessageIsEmpty()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateStructuredAsync<MacroPlanOutput>(
                "system", string.Empty, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("userMessage");
    }

    [Fact]
    public async Task GenerateStructuredAsync_PropagatesCancellation()
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
        await sut.Invoking(s => s.GenerateStructuredAsync<MacroPlanOutput>(
                "system", "user", cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GenerateStructuredAsync_ThrowsJsonException_WhenResponseIsMalformedJson()
    {
        // Arrange — return syntactically invalid JSON that cannot be deserialized.
        // While constrained decoding prevents this in production, the guard ensures
        // a clear JsonException propagates if the invariant ever breaks.
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("{ not valid json !!!"));

        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateStructuredAsync<MacroPlanOutput>(
                "system", "user message", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task GenerateStructuredAsync_ThrowsJsonException_WhenRequiredFieldsAreMissing()
    {
        // Arrange — return valid JSON that only has total_weeks but is missing
        // goal_description, phases, rationale, and warnings. System.Text.Json
        // enforces the C# 'required' keyword, so deserialization should throw
        // JsonException for the missing required properties.
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("""{"total_weeks": 12}"""));

        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateStructuredAsync<MacroPlanOutput>(
                "system", "user message", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task GenerateStructuredAsync_ThrowsInvalidOperationException_WhenJsonIsNullLiteral()
    {
        // Arrange — return the JSON literal "null", which deserializes to null
        // for reference types. While constrained decoding makes this structurally
        // unreachable in production, the guard prevents silent null propagation
        // if the invariant ever breaks.
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("null"));

        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateStructuredAsync<MacroPlanOutput>(
                "system", "user message", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*null literal*");
    }

    [Fact]
    public void AsIChatClient_ReturnsNonNullIChatClient()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var chatClient = sut.AsIChatClient();

        // Assert
        chatClient.Should().NotBeNull();
        chatClient.Should().BeAssignableTo<IChatClient>();
    }

    [Fact]
    public async Task GenerateAsync_ThrowsInvalidOperationException_WhenStopReasonIsMaxTokens()
    {
        // Arrange
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("truncated content", "max_tokens"));

        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateAsync("system", "user message", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*truncated*max_tokens*");
    }

    [Fact]
    public async Task GenerateStructuredAsync_ThrowsInvalidOperationException_WhenStopReasonIsMaxTokens()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(
            new MacroPlanOutput
            {
                TotalWeeks = 12,
                GoalDescription = "Run a marathon",
                Rationale = "Progressive build",
                Warnings = "None",
                Phases = [],
            },
            ClaudeCoachingLlm.StructuredOutputSerializerOptions);

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse(jsonResponse, "max_tokens"));

        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.GenerateStructuredAsync<MacroPlanOutput>(
                "system", "user message", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*truncated*max_tokens*");
    }

    [Fact]
    public async Task GenerateAsync_DoesNotThrow_WhenStopReasonIsEndTurn()
    {
        // Arrange
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("complete content", "end_turn"));

        var sut = CreateSut();

        // Act
        var actualText = await sut.GenerateAsync("system", "user message", TestContext.Current.CancellationToken);

        // Assert
        actualText.Should().Be("complete content");
    }

    [Fact]
    public void Dispose_DoesNotDisposeClient_WhenNotOwned()
    {
        // Arrange — create a mock that implements both IAnthropicClient and IDisposable
        // so we can verify Dispose is NOT forwarded when _ownsClient is false.
        var disposableClient = Substitute.For<IAnthropicClient, IDisposable>();
        disposableClient.Messages.Returns(_mockMessages);
        var sut = new ClaudeCoachingLlm(disposableClient, DefaultSettings, _logger);

        // Act
        sut.Dispose();

        // Assert — the injected client's Dispose should NOT have been called
        ((IDisposable)disposableClient).DidNotReceive().Dispose();
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenClientIsNotDisposable()
    {
        // Arrange — the default mock only implements IAnthropicClient, not IDisposable.
        // The internal constructor sets _ownsClient = false, so this also exercises
        // the type-check guard (_client is IDisposable).
        var sut = CreateSut();

        // Act & Assert — should complete without throwing
        var act = () => sut.Dispose();
        act.Should().NotThrow();
    }

    /// <summary>
    /// Builds a Message response with a single text content block.
    /// </summary>
    private static Message BuildTextResponse(string text, string stopReason = "end_turn")
    {
        var raw = new Dictionary<string, JsonElement>
        {
            ["id"] = ToJsonElement("msg_test_001"),
            ["type"] = ToJsonElement("message"),
            ["role"] = ToJsonElement("assistant"),
            ["model"] = ToJsonElement("claude-sonnet-4-5-20250514"),
            ["stop_reason"] = ToJsonElement(stopReason),
            ["content"] = ToJsonElement(new[]
            {
                new { type = "text", text },
            }),
            ["usage"] = ToJsonElement(new { input_tokens = 100, output_tokens = 50 }),
        };

        return Message.FromRawUnchecked(raw);
    }

    private static Message BuildMultiTextResponse(params string[] texts)
    {
        var contentArray = texts.Select(t => new { type = "text", text = t }).ToArray();

        var raw = new Dictionary<string, JsonElement>
        {
            ["id"] = ToJsonElement("msg_test_002"),
            ["type"] = ToJsonElement("message"),
            ["role"] = ToJsonElement("assistant"),
            ["model"] = ToJsonElement("claude-sonnet-4-5-20250514"),
            ["stop_reason"] = ToJsonElement("end_turn"),
            ["content"] = ToJsonElement(contentArray),
            ["usage"] = ToJsonElement(new { input_tokens = 100, output_tokens = 50 }),
        };

        return Message.FromRawUnchecked(raw);
    }

    private static Message BuildEmptyResponse()
    {
        var raw = new Dictionary<string, JsonElement>
        {
            ["id"] = ToJsonElement("msg_test_003"),
            ["type"] = ToJsonElement("message"),
            ["role"] = ToJsonElement("assistant"),
            ["model"] = ToJsonElement("claude-sonnet-4-5-20250514"),
            ["stop_reason"] = ToJsonElement("end_turn"),
            ["content"] = ToJsonElement(Array.Empty<object>()),
            ["usage"] = ToJsonElement(new { input_tokens = 100, output_tokens = 0 }),
        };

        return Message.FromRawUnchecked(raw);
    }

    private static JsonElement ToJsonElement(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private ClaudeCoachingLlm CreateSut()
    {
        return new ClaudeCoachingLlm(_mockClient, DefaultSettings, _logger);
    }
}
