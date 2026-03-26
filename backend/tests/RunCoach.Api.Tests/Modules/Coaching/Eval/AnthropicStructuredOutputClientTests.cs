using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Anthropic.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Unit tests for <see cref="AnthropicStructuredOutputClient"/>.
/// Covers: passthrough without schema, native API delegation with schema,
/// system/user message splitting, finish reason mapping, and max_tokens truncation guard.
/// </summary>
public sealed class AnthropicStructuredOutputClientTests
{
    private const string DefaultModel = "claude-sonnet-4-6";
    private const int DefaultMaxTokens = 4096;

    private readonly IChatClient _mockInner = Substitute.For<IChatClient>();
    private readonly IAnthropicClient _mockNativeClient = Substitute.For<IAnthropicClient>();
    private readonly IMessageService _mockMessages = Substitute.For<IMessageService>();

    public AnthropicStructuredOutputClientTests()
    {
        _mockNativeClient.Messages.Returns(_mockMessages);
    }

    [Fact]
    public async Task GetResponseAsync_NoSchema_DelegatesToInnerClient()
    {
        // Arrange
        var expectedResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello from inner client"));
        _mockInner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedResponse));

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };

        // Act
        var actual = await sut.GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeSameAs(expectedResponse);
        await _mockMessages.DidNotReceive()
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponseAsync_NullOptions_DelegatesToInnerClient()
    {
        // Arrange
        var expectedResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "passthrough"));
        _mockInner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedResponse));

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hi") };

        // Act
        var actual = await sut.GetResponseAsync(messages, options: null, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeSameAs(expectedResponse);
    }

    [Fact]
    public async Task GetResponseAsync_TextFormatOnly_DelegatesToInnerClient()
    {
        // Arrange
        var expectedResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "text response"));
        _mockInner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedResponse));

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };
        var options = new ChatOptions { ResponseFormat = ChatResponseFormat.Text };

        // Act
        var actual = await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeSameAs(expectedResponse);
    }

    [Fact]
    public async Task GetResponseAsync_WithJsonSchema_CallsNativeClient()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("{ \"name\": \"test\" }");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Generate something"),
        };

        // Act
        var actual = await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert -- native client was called, inner client was NOT called
        capturedParams.Should().NotBeNull();
        await _mockInner.DidNotReceive()
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>());
        actual.Messages.Should().ContainSingle();
        actual.Messages[0].Text.Should().Contain("test");
    }

    [Fact]
    public async Task GetResponseAsync_WithJsonSchema_UsesModelFromOptions()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ModelId = "claude-opus-4-20250514",
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("{ \"name\": \"test\" }");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act
        await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.Model.ToString().Should().Contain("claude-opus-4-20250514");
    }

    [Fact]
    public async Task GetResponseAsync_WithJsonSchema_UsesDefaultModelWhenNotInOptions()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("{ \"name\": \"test\" }");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act
        await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.Model.ToString().Should().Contain(DefaultModel);
    }

    [Fact]
    public async Task GetResponseAsync_WithJsonSchema_UsesMaxOutputTokensFromOptions()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            MaxOutputTokens = 8192,
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("{ \"name\": \"test\" }");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act
        await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.MaxTokens.Should().Be(8192);
    }

    [Fact]
    public async Task GetResponseAsync_WithJsonSchema_UsesDefaultMaxTokensWhenNotInOptions()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("{ \"name\": \"test\" }");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act
        await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.MaxTokens.Should().Be(DefaultMaxTokens);
    }

    [Fact]
    public async Task GetResponseAsync_WithJsonSchema_SendsOutputConfigWithSchema()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("{ \"name\": \"test\" }");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act
        await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.OutputConfig.Should().NotBeNull();
        capturedParams.OutputConfig!.Format.Should().BeOfType<JsonOutputFormat>();
    }

    [Fact]
    public async Task GetResponseAsync_WithJsonSchema_PassesTemperatureFromOptions()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            Temperature = 0.5f,
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("{ \"name\": \"test\" }");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act
        await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.Temperature.Should().NotBeNull();
        capturedParams.Temperature!.Value.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_SplitsSystemMessageFromUserMessages()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("{ \"name\": \"test\" }");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a running coach."),
            new(ChatRole.User, "Generate a plan."),
        };

        // Act
        await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert -- system goes to System param, user goes to Messages.
        // Anthropic SDK wraps System in MessageCreateParamsSystem; use .Value for the raw string.
        capturedParams.Should().NotBeNull();
        capturedParams!.System!.ToString().Should().Contain("You are a running coach.");
        capturedParams.Messages.Should().HaveCount(1);
        capturedParams.Messages[0].Role.ToString().Should().Contain("user");
        capturedParams.Messages[0].Content.ToString().Should().Contain("Generate a plan.");
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_ConcatenatesMultipleSystemMessages()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("{ \"name\": \"test\" }");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a running coach."),
            new(ChatRole.System, "Be encouraging and precise."),
            new(ChatRole.User, "Generate a plan."),
        };

        // Act
        await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert -- multiple system messages are joined with double newline
        capturedParams.Should().NotBeNull();
        capturedParams!.System!.ToString().Should().Contain("You are a running coach.").And.Contain("Be encouraging and precise.");
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_NoSystemMessages_SetsSystemToEmptyString()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("{ \"name\": \"test\" }");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Generate a plan without system prompt."),
        };

        // Act
        await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert -- no system messages means System is empty (null coalesced to empty)
        capturedParams.Should().NotBeNull();
        capturedParams!.System!.ToString().Should().Be("\"\"");
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_SkipsWhitespaceOnlySystemMessages()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("{ \"name\": \"test\" }");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "   "),
            new(ChatRole.System, "Actual system prompt."),
            new(ChatRole.User, "Generate."),
        };

        // Act
        await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert -- whitespace-only system message is skipped
        capturedParams.Should().NotBeNull();
        capturedParams!.System!.ToString().Should().Contain("Actual system prompt.");
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_MapsAssistantRoleCorrectly()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        MessageCreateParams? capturedParams = null;
        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedParams = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse("{ \"name\": \"test\" }");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello"),
            new(ChatRole.Assistant, "Hi there!"),
            new(ChatRole.User, "Generate a plan."),
        };

        // Act
        await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.Messages.Should().HaveCount(3);
        capturedParams.Messages[0].Role.ToString().Should().Contain("user");
        capturedParams.Messages[0].Content.ToString().Should().Contain("Hello");
        capturedParams.Messages[1].Role.ToString().Should().Contain("assistant");
        capturedParams.Messages[1].Content.ToString().Should().Contain("Hi there!");
        capturedParams.Messages[2].Role.ToString().Should().Contain("user");
        capturedParams.Messages[2].Content.ToString().Should().Contain("Generate a plan.");
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_EndTurnMapsToStop()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("{ \"name\": \"test\" }", "end_turn"));

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act
        var actual = await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        actual.FinishReason.Should().Be(ChatFinishReason.Stop);
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_StopSequenceMapsToStop()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("{ \"name\": \"test\" }", "stop_sequence"));

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act
        var actual = await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        actual.FinishReason.Should().Be(ChatFinishReason.Stop);
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_ToolUseMapsToToolCalls()
    {
        // Arrange -- tool_use stop reason is not blocked by the max_tokens guard,
        // so it flows through to MapToChatResponse normally.
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("{ \"name\": \"test\" }", "tool_use"));

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act
        var actual = await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        actual.FinishReason.Should().Be(ChatFinishReason.ToolCalls);
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_MapsModelIdFromResponse()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("{ \"name\": \"test\" }"));

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act
        var actual = await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        actual.ModelId.Should().Contain("claude-sonnet-4-5-20250514");
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_MapsUsageFromResponse()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("{ \"name\": \"test\" }"));

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act
        var actual = await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        actual.Usage.Should().NotBeNull();
        actual.Usage!.InputTokenCount.Should().Be(100);
        actual.Usage.OutputTokenCount.Should().Be(50);
        actual.Usage.TotalTokenCount.Should().Be(150);
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_ResponseHasAssistantRole()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("{ \"name\": \"result\" }"));

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act
        var actual = await sut.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        // Assert
        actual.Messages.Should().ContainSingle();
        actual.Messages[0].Role.Should().Be(ChatRole.Assistant);
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_MaxTokensStopReason_ThrowsInvalidOperationException()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("{ \"name\": \"truncated\" }", "max_tokens"));

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act & Assert
        await sut.Invoking(s => s.GetResponseAsync(messages, options, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*truncated*max_tokens*");
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_MaxTokensStopReason_SuggestsIncreasingMaxTokens()
    {
        // Arrange
        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextResponse("{ \"name\": \"truncated\" }", "max_tokens"));

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act & Assert
        await sut.Invoking(s => s.GetResponseAsync(messages, options, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Increase MaxTokens*");
    }

    [Fact]
    public async Task GetResponseAsync_WithSchema_PropagatesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var schema = CreateSimpleSchema();
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "TestSchema"),
        };

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns<Message>(callInfo =>
            {
                callInfo.ArgAt<CancellationToken>(1).ThrowIfCancellationRequested();
                return BuildTextResponse("should not reach");
            });

        using var sut = CreateSut();
        var messages = new List<ChatMessage> { new(ChatRole.User, "Generate") };

        // Act & Assert
        await sut.Invoking(s => s.GetResponseAsync(messages, options, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Creates a minimal JSON schema element for testing purposes.
    /// </summary>
    private static JsonElement CreateSimpleSchema()
    {
        var schemaJson = """
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string" }
                },
                "required": ["name"],
                "additionalProperties": false
            }
            """;
        return JsonDocument.Parse(schemaJson).RootElement.Clone();
    }

    /// <summary>
    /// Builds a Message response with a single text content block using the Anthropic SDK's
    /// raw dictionary factory. Mirrors the pattern from ClaudeCoachingLlmTests.
    /// </summary>
    private static Message BuildTextResponse(string text, string stopReason = "end_turn")
    {
        var raw = new Dictionary<string, JsonElement>
        {
            ["id"] = ToJsonElement("msg_test_structured_001"),
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

    private static JsonElement ToJsonElement(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private AnthropicStructuredOutputClient CreateSut()
    {
        return new AnthropicStructuredOutputClient(
            _mockInner,
            _mockNativeClient,
            DefaultModel,
            DefaultMaxTokens);
    }
}
