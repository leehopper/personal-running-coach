using System.Net;
using System.Net.Http.Headers;
using Anthropic;
using Anthropic.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Adaptation;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// DEC-073 (first live in Slice 3): <see cref="ClaudeCoachingLlm"/> translates the Anthropic
/// SDK 12.24.1 failure surface into the adapter-owned <see cref="TransientCoachingLlmException"/>
/// / <see cref="PermanentCoachingLlmException"/> so callers never see SDK types. These tests
/// drive a real <see cref="AnthropicClient"/> through a stub transport so the SDK's actual retry
/// loop, exception factory, and the <see cref="RetryAfterCaptureHandler"/> (which reads the raw
/// <c>Retry-After</c> header — the SDK exposes no accessor) all run end to end.
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

    private static ClaudeCoachingLlm BuildLlm(StubHttpMessageHandler stub, int maxRetries)
    {
        // The capture handler sits outermost in the HttpClient pipeline so it reads the raw
        // Retry-After header off each attempt's response before the SDK consumes it.
        var httpClient = new HttpClient(new RetryAfterCaptureHandler { InnerHandler = stub })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var anthropic = new AnthropicClient(new ClientOptions
        {
            ApiKey = Settings.ApiKey,
            MaxRetries = maxRetries,
            HttpClient = httpClient,
        });

        return new ClaudeCoachingLlm(anthropic, Settings, NullLogger<ClaudeCoachingLlm>.Instance);
    }

    private sealed class StubHttpMessageHandler(
        HttpStatusCode status,
        int? retryAfterSeconds = null,
        bool throwTransport = false) : HttpMessageHandler
    {
        private const string ErrorBody =
            "{\"type\":\"error\",\"error\":{\"type\":\"api_error\",\"message\":\"simulated\"}}";

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;

            if (throwTransport)
            {
                throw new HttpRequestException("simulated transport failure");
            }

            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(ErrorBody),
            };

            if (retryAfterSeconds is { } seconds)
            {
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
            }

            return Task.FromResult(response);
        }
    }
}
