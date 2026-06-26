using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Anthropic.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Covers the Slice 4B PR3a per-call model-override capability on
/// <see cref="ClaudeCoachingLlm.GenerateStructuredAsync{T}"/> — the first
/// production path that targets a second (Haiku) model binding, used by the
/// intent classifier. (The spec's "temperature 0" requirement is not
/// implementable: the Anthropic SDK marks <c>Temperature</c>/<c>TopP</c>/<c>TopK</c>
/// obsolete and rejects any non-default value with HTTP 400 on current models,
/// so classifier determinism comes from constrained decoding, not sampling.)
/// These assert the outgoing <see cref="MessageCreateParams"/> carries the
/// overridden model, and that the pre-existing overloads stay on the default.
/// </summary>
public sealed class ClaudeCoachingLlmModelBindingTests
{
    private static readonly CoachingLlmSettings DefaultSettings = new()
    {
        ApiKey = "sk-test-key-for-unit-tests",
        ModelId = "claude-sonnet-4-6",
        MaxTokens = 4096,
        MaxRetries = 3,
        TimeoutSeconds = 120,
    };

    private readonly IAnthropicClient _mockClient = Substitute.For<IAnthropicClient>();
    private readonly IMessageService _mockMessages = Substitute.For<IMessageService>();
    private readonly ILogger<ClaudeCoachingLlm> _logger = NullLogger<ClaudeCoachingLlm>.Instance;

    public ClaudeCoachingLlmModelBindingTests()
    {
        _mockClient.Messages.Returns(_mockMessages);
    }

    [Fact]
    public async Task GenerateStructuredAsync_WithModelOverride_TargetsTheOverriddenModel()
    {
        // Arrange
        var captured = CaptureParams();
        var sut = CreateSut();

        // Act
        await sut.GenerateStructuredAsync<MacroPlanOutput>(
            "system",
            "user",
            schema: null,
            cacheControl: null,
            modelOverride: "claude-haiku-4-5",
            TestContext.Current.CancellationToken);

        // Assert
        captured.Value.Should().NotBeNull();
        captured.Value!.Model.ToString().Should().Contain("claude-haiku-4-5");
        captured.Value.Model.ToString().Should().NotContain("sonnet");
    }

    [Fact]
    public async Task GenerateStructuredAsync_WithNullModelOverride_FallsBackToSettingsModel()
    {
        // Arrange
        var captured = CaptureParams();
        var sut = CreateSut();

        // Act
        await sut.GenerateStructuredAsync<MacroPlanOutput>(
            "system",
            "user",
            schema: null,
            cacheControl: null,
            modelOverride: null,
            TestContext.Current.CancellationToken);

        // Assert
        captured.Value.Should().NotBeNull();
        captured.Value!.Model.ToString().Should().Contain(DefaultSettings.ModelId);
    }

    [Fact]
    public async Task GenerateStructuredAsync_ExistingOverload_StaysOnTheSettingsModel()
    {
        // Arrange — the pre-existing 5-arg overload (plan-gen / onboarding /
        // adaptation) must keep targeting the configured default model.
        var captured = CaptureParams();
        var sut = CreateSut();

        // Act
        await sut.GenerateStructuredAsync<MacroPlanOutput>(
            "system",
            "user",
            schema: null,
            cacheControl: null,
            TestContext.Current.CancellationToken);

        // Assert
        captured.Value.Should().NotBeNull();
        captured.Value!.Model.ToString().Should().Contain(DefaultSettings.ModelId);
    }

    private static Message BuildTextResponse(string text)
    {
        var raw = new Dictionary<string, JsonElement>
        {
            ["id"] = ToJsonElement("msg_test_001"),
            ["type"] = ToJsonElement("message"),
            ["role"] = ToJsonElement("assistant"),
            ["model"] = ToJsonElement("claude-sonnet-4-6"),
            ["stop_reason"] = ToJsonElement("end_turn"),
            ["content"] = ToJsonElement(new[] { new { type = "text", text } }),
            ["usage"] = ToJsonElement(new { input_tokens = 100, output_tokens = 50 }),
        };

        return Message.FromRawUnchecked(raw);
    }

    private static JsonElement ToJsonElement(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private ParamsBox CaptureParams()
    {
        var box = new ParamsBox();
        var jsonResponse = JsonSerializer.Serialize(
            new MacroPlanOutput
            {
                TotalWeeks = 8,
                GoalDescription = "5K race",
                Rationale = "Short cycle",
                Warnings = "None",
                Phases = [],
            },
            ClaudeCoachingLlm.StructuredOutputSerializerOptions);

        _mockMessages
            .Create(Arg.Any<MessageCreateParams>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                box.Value = callInfo.ArgAt<MessageCreateParams>(0);
                return BuildTextResponse(jsonResponse);
            });

        return box;
    }

    private ClaudeCoachingLlm CreateSut() => new(_mockClient, DefaultSettings, _logger);

    private sealed class ParamsBox
    {
        public MessageCreateParams? Value { get; set; }
    }
}
