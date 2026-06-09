using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// DEC-073 (first live in Slice 3): <see cref="ClaudeCoachingLlm"/> translates the Anthropic
/// SDK 12.24.1 failure surface into the adapter-owned <see cref="TransientCoachingLlmException"/>
/// / <see cref="PermanentCoachingLlmException"/> so callers never see SDK types. These tests
/// drive a real <see cref="Anthropic.AnthropicClient"/> through a stub transport so the SDK's actual retry
/// loop, exception factory, and the <see cref="RetryAfterCaptureHandler"/> (which reads the raw
/// <c>Retry-After</c> header — the SDK exposes no accessor) all run end to end. The pipeline is
/// built through <see cref="ClaudeCoachingLlm.CreateClientPipeline"/> — the same seam the
/// production constructor uses — so the wiring under test cannot drift from production.
/// </summary>
public sealed class ClaudeCoachingLlmFailureTests
{
    private static readonly CoachingLlmSettings Settings = new()
    {
        ApiKey = "test-key",
        ModelId = "claude-sonnet-4-6",
        MaxTokens = 1024,
    };

    [Fact]
    public async Task GenerateStructuredAsync_Translates429_ToTransientWithRetryAfterFromRawHeaders()
    {
        // Arrange — 429 once (no SDK retry) carrying Retry-After: 30.
        var stub = new StubHttpMessageHandler(HttpStatusCode.TooManyRequests, retryAfterSeconds: 30);
        using var llm = BuildLlm(stub, maxRetries: 0);

        // Act
        var act = () => llm.GenerateStructuredAsync<PlanAdaptationOutput>(
            "system", "user", AdaptationSchema.Frozen, cacheControl: null, TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<TransientCoachingLlmException>();
        thrown.Which.RetryAfterSeconds.Should().Be(30, "the retry-after value must be read from the raw response headers");
    }

    [Fact]
    public async Task GenerateStructuredAsync_Translates400_ToPermanent()
    {
        // Arrange
        var stub = new StubHttpMessageHandler(HttpStatusCode.BadRequest);
        using var llm = BuildLlm(stub, maxRetries: 0);

        // Act
        var act = () => llm.GenerateStructuredAsync<PlanAdaptationOutput>(
            "system", "user", AdaptationSchema.Frozen, cacheControl: null, TestContext.Current.CancellationToken);

        // Assert — a 400 is terminal; no further attempt and no transient classification.
        await act.Should().ThrowAsync<PermanentCoachingLlmException>();
        stub.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GenerateStructuredAsync_Translates503_ToTransient()
    {
        // Arrange
        var stub = new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        using var llm = BuildLlm(stub, maxRetries: 0);

        // Act
        var act = () => llm.GenerateStructuredAsync<PlanAdaptationOutput>(
            "system", "user", AdaptationSchema.Frozen, cacheControl: null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<TransientCoachingLlmException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task GenerateStructuredAsync_TranslatesRetryable4xx_ToTransient(HttpStatusCode status)
    {
        // Arrange — 408/409 land on the non-leaf Anthropic4xxException, so the adapter must
        // classify them transient by status code rather than by leaf exception type.
        var stub = new StubHttpMessageHandler(status);
        using var llm = BuildLlm(stub, maxRetries: 0);

        // Act
        var act = () => llm.GenerateStructuredAsync<PlanAdaptationOutput>(
            "system", "user", AdaptationSchema.Frozen, cacheControl: null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<TransientCoachingLlmException>();
    }

    [Fact]
    public async Task GenerateStructuredAsync_TranslatesNetworkFailure_ToTransient()
    {
        // Arrange — transport failure (no HTTP response at all).
        var stub = new StubHttpMessageHandler(HttpStatusCode.OK, throwTransport: true);
        using var llm = BuildLlm(stub, maxRetries: 0);

        // Act
        var act = () => llm.GenerateStructuredAsync<PlanAdaptationOutput>(
            "system", "user", AdaptationSchema.Frozen, cacheControl: null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<TransientCoachingLlmException>();
    }

    [Fact]
    public async Task GenerateStructuredAsync_TranslatesSdkPerAttemptTimeout_ToTransient()
    {
        // Arrange — a hanging transport. The SDK's per-attempt timeout (ClientOptions.Timeout)
        // fires as a raw TaskCanceledException from a linked CTS with the caller's token NOT
        // cancelled; the adapter must translate it, never surface it as a cancellation.
        var stub = new StubHttpMessageHandler(HttpStatusCode.OK, hangUntilCancelled: true);
        using var llm = BuildLlm(stub, maxRetries: 0, timeoutSeconds: 1);

        // Act
        var act = () => llm.GenerateStructuredAsync<PlanAdaptationOutput>(
            "system", "user", AdaptationSchema.Frozen, cacheControl: null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<TransientCoachingLlmException>();
    }

    [Fact]
    public async Task GenerateStructuredAsync_WithMaxRetriesTwo_MakesExactlyThreeAttempts()
    {
        // Arrange — every attempt 429; MaxRetries=2 ⇒ 1 initial + 2 retries = 3 attempts.
        var stub = new StubHttpMessageHandler(HttpStatusCode.TooManyRequests, retryAfterSeconds: 0);
        using var llm = BuildLlm(stub, maxRetries: 2);

        // Act
        var act = () => llm.GenerateStructuredAsync<PlanAdaptationOutput>(
            "system", "user", AdaptationSchema.Frozen, cacheControl: null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<TransientCoachingLlmException>();
        stub.CallCount.Should().Be(3, "MaxRetries of 2 means at most three attempts");
    }

    [Fact]
    public async Task GenerateStructuredAsync_AttachesLastAttemptsRetryAfter_AcrossSdkRetries()
    {
        // Arrange — Retry-After differs per attempt; the value attached to the thrown exception
        // must be the final attempt's (last write wins within one capture scope).
        var stub = new StubHttpMessageHandler(
            HttpStatusCode.TooManyRequests, retryAfterSchedule: [0, 0, 7]);
        using var llm = BuildLlm(stub, maxRetries: 2);

        // Act
        var act = () => llm.GenerateStructuredAsync<PlanAdaptationOutput>(
            "system", "user", AdaptationSchema.Frozen, cacheControl: null, TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<TransientCoachingLlmException>();
        thrown.Which.RetryAfterSeconds.Should().Be(7, "the hint must reflect the final attempt's Retry-After header");
    }

    [Fact]
    public async Task GenerateStructuredAsync_HonorsCancellation_WithoutReclassifying()
    {
        // Arrange — a pre-cancelled token must surface as OperationCanceledException, never
        // reclassified as Transient/Permanent.
        var stub = new StubHttpMessageHandler(HttpStatusCode.TooManyRequests, retryAfterSeconds: 0);
        using var llm = BuildLlm(stub, maxRetries: 2);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => llm.GenerateStructuredAsync<PlanAdaptationOutput>(
            "system", "user", AdaptationSchema.Frozen, cacheControl: null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GenerateAsync_TextPath_AlsoTranslatesFailures()
    {
        // Arrange — DEC-073 translation is shared across the text + structured paths.
        var stub = new StubHttpMessageHandler(HttpStatusCode.TooManyRequests, retryAfterSeconds: 12);
        using var llm = BuildLlm(stub, maxRetries: 0);

        // Act
        var act = () => llm.GenerateAsync("system", "user", TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<TransientCoachingLlmException>();
        thrown.Which.RetryAfterSeconds.Should().Be(12);
    }

    [Fact]
    public async Task GenerateStructuredAsync_DeserializesSchemaConformantAdaptationJson_OnSuccess()
    {
        // Arrange — a 200 whose payload is shaped exactly as constrained decoding emits against
        // AdaptationSchema.Frozen: snake_case keys, string enums, all seven properties present,
        // the non-matching slot an explicit JSON null, and a nullable referral category.
        const string adaptationJson = """
            {
              "adaptation_kind": "Restructure",
              "safety_tier": "Amber",
              "nudge_patch": null,
              "restructure_plan": {
                "revised_weekly_targets": [{ "week_number": 2, "weekly_target_km": 30 }],
                "revised_current_week_workouts": [{
                  "day_of_week": 2,
                  "workout_type": "Easy",
                  "title": "Easy Aerobic Run",
                  "target_distance_km": 8,
                  "target_duration_minutes": 45,
                  "target_pace_easy_sec_per_km": 360,
                  "target_pace_fast_sec_per_km": 330,
                  "segments": [],
                  "warmup_notes": "five minutes of easy jogging",
                  "cooldown_notes": "five minutes of easy jogging",
                  "coaching_notes": "keep it conversational the whole way",
                  "perceived_effort": 3
                }],
                "forward_path": "Hold this volume for one week, then add about ten percent back."
              },
              "net_load_delta": -8,
              "rationale": "You have had a heavy stretch, so I trimmed this week and kept the path back visible.",
              "referral_category": "Injury"
            }
            """;
        var stub = new StubHttpMessageHandler(HttpStatusCode.OK, successText: adaptationJson);
        using var llm = BuildLlm(stub, maxRetries: 0);

        // Act
        var (actualOutput, actualUsage) = await llm.GenerateStructuredAsync<PlanAdaptationOutput>(
            "system", "user", AdaptationSchema.Frozen, cacheControl: null, TestContext.Current.CancellationToken);

        // Assert — the schema↔deserializer loop closes: enums, the explicit-null slot, and the
        // nullable referral category all materialize, and the validator accepts the shape.
        actualOutput.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        actualOutput.SafetyTier.Should().Be(SafetyTier.Amber);
        actualOutput.NudgePatch.Should().BeNull();
        actualOutput.RestructurePlan.Should().NotBeNull();
        actualOutput.RestructurePlan!.RevisedWeeklyTargets.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = 30 });
        actualOutput.RestructurePlan.RevisedCurrentWeekWorkouts.Should().ContainSingle()
            .Which.Title.Should().Be("Easy Aerobic Run");
        actualOutput.NetLoadDelta.Should().Be(-8);
        actualOutput.ReferralCategory.Should().Be(ReferralCategory.Injury);
        actualUsage.InputTokens.Should().Be(10);
        actualUsage.OutputTokens.Should().Be(20);
        PlanAdaptationOutputValidator.Validate(actualOutput).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Dispose_ThroughPublicConstructor_DisposesOwnedPipelineIdempotently()
    {
        // Arrange — the public constructor owns its HttpClient + AnthropicClient pipeline
        // (no network call is made until a message is sent).
        var llm = new ClaudeCoachingLlm(
            Options.Create(Settings),
            NullLogger<ClaudeCoachingLlm>.Instance);

        // Act
        var act = () =>
        {
            llm.Dispose();
            llm.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    private static ClaudeCoachingLlm BuildLlm(StubHttpMessageHandler stub, int maxRetries, int timeoutSeconds = 120)
    {
        var settings = Settings with { MaxRetries = maxRetries, TimeoutSeconds = timeoutSeconds };
        var (anthropic, _) = ClaudeCoachingLlm.CreateClientPipeline(settings, stub);

        return new ClaudeCoachingLlm(anthropic, settings, NullLogger<ClaudeCoachingLlm>.Instance);
    }

    private sealed class StubHttpMessageHandler(
        HttpStatusCode status,
        int? retryAfterSeconds = null,
        bool throwTransport = false,
        bool hangUntilCancelled = false,
        string? successText = null,
        int?[]? retryAfterSchedule = null) : HttpMessageHandler
    {
        private const string ErrorBody =
            "{\"type\":\"error\",\"error\":{\"type\":\"api_error\",\"message\":\"simulated\"}}";

        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;

            if (hangUntilCancelled)
            {
                // Hangs until the SDK's per-attempt timeout (or the caller) cancels the linked token.
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (throwTransport)
            {
                throw new HttpRequestException("simulated transport failure");
            }

            var body = status == HttpStatusCode.OK && successText is not null
                ? BuildSuccessBody(successText)
                : ErrorBody;
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

            var attemptRetryAfter = retryAfterSchedule is { Length: > 0 } schedule
                ? schedule[Math.Min(CallCount - 1, schedule.Length - 1)]
                : retryAfterSeconds;
            if (attemptRetryAfter is { } seconds)
            {
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
            }

            return response;
        }

        /// <summary>
        /// Wraps the structured-output text in a minimal valid Anthropic messages.create
        /// response body so the SDK's own deserialization path runs end to end.
        /// </summary>
        private static string BuildSuccessBody(string text) =>
            JsonSerializer.Serialize(new
            {
                id = "msg_test",
                type = "message",
                role = "assistant",
                model = "claude-sonnet-4-6",
                content = new[] { new { type = "text", text } },
                stop_reason = "end_turn",
                stop_sequence = (string?)null,
                usage = new { input_tokens = 10, output_tokens = 20 },
            });
    }
}
