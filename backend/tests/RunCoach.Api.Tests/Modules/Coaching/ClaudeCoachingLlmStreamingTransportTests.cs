using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Sanitization;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Slice 4B Unit 1 (R-084): <see cref="ClaudeCoachingLlm.StreamAsync"/> driven through the
/// <em>real</em> Anthropic SDK 12.29.1 + Microsoft.Extensions.AI streaming bridge over a stub
/// HTTP transport that emits a hand-crafted <c>text/event-stream</c> body. This pins the wire
/// truth the unit-level translator tests cannot: that a mid-stream <c>error</c> SSE event
/// surfaces as <c>AnthropicSseException</c> whose message carries the structured error
/// <c>type</c> (resolving R-084's lone open caveat), that <c>max_tokens</c> /
/// <c>model_context_window_exceeded</c> / <c>refusal</c> end the enumeration <em>cleanly</em>
/// and must be detected from the raw <c>stop_reason</c>, and that pre-first-byte HTTP failures
/// reuse the request/response status classifier. The pipeline is built through
/// <see cref="ClaudeCoachingLlm.CreateClientPipeline"/> — the production seam — with the default
/// streaming factory, so the audit-wrapped bridge under test cannot drift from production.
/// </summary>
public sealed class ClaudeCoachingLlmStreamingTransportTests
{
    private static readonly CoachingLlmSettings Settings = new()
    {
        ApiKey = "test-key",
        ModelId = "claude-sonnet-4-6",
        MaxTokens = 1024,
    };

    // R-084 mid-stream SSE error-type → DEC-073 classification. The SDK reports a post-200 error
    // as AnthropicSseException whose message carries the structured error `type` verbatim; the
    // adapter classifies on that token. Retryable service-side errors are Transient; client/auth
    // errors are Permanent; an unknown type defaults to Transient (recall-over-precision).
    [Theory]
    [InlineData("overloaded_error", true)]
    [InlineData("rate_limit_error", true)]
    [InlineData("api_error", true)]
    [InlineData("timeout_error", true)]
    [InlineData("some_unmapped_error", true)]
    [InlineData("invalid_request_error", false)]
    [InlineData("authentication_error", false)]
    [InlineData("permission_error", false)]
    public async Task StreamAsync_MidStreamError_ClassifiesByErrorType(string errorType, bool expectTransient)
    {
        // Arrange — a 200 that streams two text deltas then an `error` SSE event of the given type,
        // which the SDK rethrows mid-enumeration as AnthropicSseException.
        using var llm = BuildLlm(SseBuilder.WithMidStreamError(errorType));

        // Act
        var act = async () => await CollectAsync(llm);

        // Assert — the SDK type never escapes; it maps to the DEC-073 Transient/Permanent type.
        if (expectTransient)
        {
            await act.Should().ThrowAsync<TransientCoachingLlmException>();
        }
        else
        {
            await act.Should().ThrowAsync<PermanentCoachingLlmException>();
        }
    }

    [Theory]
    [InlineData("end_turn")]
    [InlineData("stop_sequence")]
    public async Task StreamAsync_CleanCompletion_YieldsFullTextAndDoesNotThrow(string stopReason)
    {
        // Arrange — a normal stream that ends on a clean stop reason.
        using var llm = BuildLlm(SseBuilder.WithStopReason(stopReason));

        // Act
        var deltas = await CollectAsync(llm);

        // Assert — every text delta is yielded in order; the enumeration ends without throwing.
        deltas.Should().Equal("Easy ", "does it.");
    }

    // R-084: max_tokens / model_context_window_exceeded / refusal end the enumeration *cleanly*
    // (no exception). A truncated free-text reply is incomplete, not unparseable, so the adapter
    // detects the terminal stop_reason and raises the errored-turn signal. model_context_window_
    // exceeded is the trap: the M.E.AI bridge maps it to ChatFinishReason.Stop (identical to a
    // clean end_turn), so detection must read the raw Anthropic stop_reason, not the finish reason.
    [Theory]
    [InlineData("max_tokens", IncompleteReason.MaxTokens, true)]
    [InlineData("model_context_window_exceeded", IncompleteReason.ContextWindowExceeded, false)]
    [InlineData("refusal", IncompleteReason.Refusal, false)]
    public async Task StreamAsync_IncompleteFinish_RaisesIncompleteWithReason(
        string stopReason, IncompleteReason expectedReason, bool expectedRetryable)
    {
        // Arrange — a stream that delivers text then ends on an incomplete-finish stop reason.
        using var llm = BuildLlm(SseBuilder.WithStopReason(stopReason));

        // Act
        var act = async () => await CollectAsync(llm);

        // Assert — the partial is unusable: an IncompleteCoachingLlmException carrying the precise
        // reason and retryability, never a clean completion.
        var thrown = await act.Should().ThrowAsync<IncompleteCoachingLlmException>();
        thrown.Which.Reason.Should().Be(expectedReason);
        thrown.Which.Retryable.Should().Be(expectedRetryable);
    }

    // Pre-first-byte HTTP failures (the SDK throws an AnthropicApiException before any SSE byte) reuse
    // the request/response status classifier unchanged (R-084 carry-over): 408/409/429/5xx → Transient,
    // other 4xx → Permanent.
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.Conflict, true)]
    public async Task StreamAsync_PreFirstByteHttpError_ClassifiesByStatus(HttpStatusCode status, bool expectTransient)
    {
        // Arrange — a non-200 before any SSE event opens.
        using var llm = BuildLlm(new StatusStubHttpMessageHandler(status));

        // Act
        var act = async () => await CollectAsync(llm);

        // Assert
        if (expectTransient)
        {
            await act.Should().ThrowAsync<TransientCoachingLlmException>();
        }
        else
        {
            await act.Should().ThrowAsync<PermanentCoachingLlmException>();
        }
    }

    [Fact]
    public async Task StreamAsync_PreFirstByte429_CapturesRetryAfterFromRawHeader()
    {
        // Arrange — a pre-stream 429 carrying Retry-After: 30. Before headers are flushed the owned
        // pipeline can still read the raw header (the SDK exposes no accessor).
        using var llm = BuildLlm(new StatusStubHttpMessageHandler(HttpStatusCode.TooManyRequests, retryAfterSeconds: 30));

        // Act
        var act = async () => await CollectAsync(llm);

        // Assert
        var thrown = await act.Should().ThrowAsync<TransientCoachingLlmException>();
        thrown.Which.RetryAfterSeconds.Should().Be(30);
    }

    [Fact]
    public async Task StreamAsync_RoutesThroughSanitizationAuditClient_OpeningOneGuardrailSpan()
    {
        // Arrange — the production streaming factory wraps the M.E.AI bridge in
        // SanitizationAuditChatClient, so a stream opens exactly one GUARDRAIL rollup span at the
        // LLM-call boundary. Listening on the RunCoach.Llm source proves the production wiring.
        var captured = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "RunCoach.Llm",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                lock (captured)
                {
                    captured.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        using var llm = BuildLlm(SseBuilder.WithStopReason("end_turn"));

        // Act
        var deltas = await CollectAsync(llm);

        // Assert
        deltas.Should().Equal("Easy ", "does it.");
        var auditSpans = captured.Where(a => a.OperationName == SanitizationAuditChatClient.AuditSpanName).ToList();
        auditSpans.Should().ContainSingle("the production streaming factory wraps the bridge in the audit client");
        var tags = auditSpans[0].TagObjects.ToDictionary(kv => kv.Key, kv => kv.Value);
        tags.Should().ContainKey("openinference.span.kind").WhoseValue.Should().Be("GUARDRAIL");
    }

    private static async Task<List<string>> CollectAsync(ClaudeCoachingLlm llm)
    {
        var deltas = new List<string>();
        await foreach (var delta in llm.StreamAsync("system", "user", TestContext.Current.CancellationToken))
        {
            deltas.Add(delta);
        }

        return deltas;
    }

    /// <summary>
    /// Builds an adapter whose real SDK client talks to the supplied SSE transport, using the
    /// production streaming factory (audit-wrapped M.E.AI bridge) so the path under test matches
    /// production.
    /// </summary>
    private static ClaudeCoachingLlm BuildLlm(string sseBody) =>
        BuildLlm(new SseStubHttpMessageHandler(sseBody));

    private static ClaudeCoachingLlm BuildLlm(HttpMessageHandler transport)
    {
        var settings = Settings with { MaxRetries = 0 };
        var (anthropic, _) = ClaudeCoachingLlm.CreateClientPipeline(settings, transport);
        return new ClaudeCoachingLlm(anthropic, settings, NullLogger<ClaudeCoachingLlm>.Instance);
    }

    /// <summary>
    /// Assembles minimal Anthropic Messages streaming bodies in the documented SSE event order
    /// (<c>message_start</c> → <c>content_block_*</c> → <c>message_delta</c> → <c>message_stop</c>),
    /// or a truncated stream ending in an <c>error</c> event.
    /// </summary>
    private static class SseBuilder
    {
        public static string WithStopReason(string stopReason)
        {
            var sb = new StringBuilder();
            AppendPreamble(sb);
            Event(sb, "content_block_stop", """{"type":"content_block_stop","index":0}""");
            Event(sb, "message_delta", "{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"" + stopReason + "\",\"stop_sequence\":null},\"usage\":{\"output_tokens\":15}}");
            Event(sb, "message_stop", """{"type":"message_stop"}""");
            return sb.ToString();
        }

        public static string WithMidStreamError(string errorType)
        {
            var sb = new StringBuilder();
            AppendPreamble(sb);
            Event(sb, "error", "{\"type\":\"error\",\"error\":{\"type\":\"" + errorType + "\",\"message\":\"simulated\"}}");
            return sb.ToString();
        }

        private static void AppendPreamble(StringBuilder sb)
        {
            Event(sb, "message_start", """{"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"claude-sonnet-4-6","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":10,"output_tokens":1}}}""");
            Event(sb, "content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""");
            Event(sb, "content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Easy "}}""");
            Event(sb, "content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"does it."}}""");
        }

        private static void Event(StringBuilder sb, string name, string data) =>
            sb.Append("event: ").Append(name).Append('\n').Append("data: ").Append(data).Append("\n\n");
    }

    private sealed class SseStubHttpMessageHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/event-stream"),
            });
    }

    private sealed class StatusStubHttpMessageHandler(HttpStatusCode status, int? retryAfterSeconds = null) : HttpMessageHandler
    {
        private const string ErrorBody =
            "{\"type\":\"error\",\"error\":{\"type\":\"api_error\",\"message\":\"simulated\"}}";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(ErrorBody, Encoding.UTF8, "application/json"),
            };

            if (retryAfterSeconds is { } seconds)
            {
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
            }

            return Task.FromResult(response);
        }
    }
}
