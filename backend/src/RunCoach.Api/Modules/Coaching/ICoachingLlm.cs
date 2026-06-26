using System.Text.Json;
using RunCoach.Api.Modules.Coaching.Models;

namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Thin abstraction over an LLM API call for coaching plan generation.
/// Implementations handle model-specific details (API client, authentication,
/// retry logic, response extraction).
/// </summary>
public interface ICoachingLlm
{
    /// <summary>
    /// Sends a system prompt and user message to the LLM and returns the
    /// generated text response.
    /// </summary>
    /// <param name="systemPrompt">The coaching system prompt with persona, safety rules, and context.</param>
    /// <param name="userMessage">The assembled user message with profile data and request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated text response from the LLM.</returns>
    Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct);

    /// <summary>
    /// Sends a system prompt and user message to the LLM and returns a
    /// structured response deserialized to <typeparamref name="T"/> alongside
    /// the call's <see cref="AnthropicUsage"/> token counters.
    /// Uses Anthropic constrained decoding to guarantee schema-compliant JSON.
    /// </summary>
    /// <typeparam name="T">The structured output record type.</typeparam>
    /// <param name="systemPrompt">The coaching system prompt.</param>
    /// <param name="userMessage">The assembled user message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A tuple of <c>(Result, Usage)</c>: the deserialized structured response and
    /// the per-call <see cref="AnthropicUsage"/> breakdown so callers can roll
    /// cache-hit-rate / token totals into orchestration-layer telemetry per
    /// Slice 1 § Unit 2 R02.8.
    /// </returns>
    Task<(T Result, AnthropicUsage Usage)> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userMessage,
        CancellationToken ct);

    /// <summary>
    /// Sends a system prompt and user message to the LLM with optional
    /// pre-built JSON schema and Anthropic prompt-cache breakpoint, and
    /// returns a structured response deserialized to <typeparamref name="T"/>
    /// alongside the call's <see cref="AnthropicUsage"/> token counters.
    /// </summary>
    /// <typeparam name="T">The structured output record type.</typeparam>
    /// <param name="systemPrompt">The coaching system prompt.</param>
    /// <param name="userMessage">The assembled user message.</param>
    /// <param name="schema">
    /// Optional pre-built JSON schema dictionary used as the
    /// <c>output_config.format.schema</c> payload. Pass the byte-stable
    /// <c>OnboardingSchema.Frozen</c> to keep the Anthropic grammar cache + prompt-prefix cache hot;
    /// pass <see langword="null"/> to fall back to runtime generation via
    /// <c>JsonSchemaHelper.GenerateSchema&lt;T&gt;()</c>.
    /// </param>
    /// <param name="cacheControl">
    /// Optional Anthropic <c>cache_control</c> breakpoint to attach to the
    /// system prompt block. When non-null the system prompt is sent as a
    /// content-block array carrying this marker; when null the system prompt
    /// is sent as a plain string and Anthropic does not cache it. Per DEC-047
    /// the onboarding flow sets <see cref="CacheControl.Ephemeral1h"/>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A tuple of <c>(Result, Usage)</c>: the deserialized structured response and
    /// the per-call <see cref="AnthropicUsage"/> breakdown so callers can roll
    /// cache-hit-rate / token totals into orchestration-layer telemetry per
    /// Slice 1 § Unit 2 R02.8.
    /// </returns>
    Task<(T Result, AnthropicUsage Usage)> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userMessage,
        IReadOnlyDictionary<string, JsonElement>? schema,
        CacheControl? cacheControl,
        CancellationToken ct);

    /// <summary>
    /// As <see cref="GenerateStructuredAsync{T}(string, string, IReadOnlyDictionary{string, JsonElement}?, CacheControl?, CancellationToken)"/>,
    /// but targets a per-call model binding instead of the configured default.
    /// </summary>
    /// <remarks>
    /// Slice 4B introduced the first production call against a second (Haiku)
    /// model — the intent classifier — so this overload lets a caller point one
    /// structured call at a cheaper/faster model (a floating alias only, DEC-037)
    /// while leaving the shared default untouched for plan-generation, onboarding,
    /// and adaptation. There is deliberately no temperature parameter: the
    /// Anthropic SDK marks <c>temperature</c>/<c>top_p</c>/<c>top_k</c> obsolete and
    /// rejects any non-default value with HTTP 400 on current Claude models, so
    /// classifier determinism comes from constrained decoding (a byte-stable
    /// frozen schema), not sampling control.
    /// </remarks>
    /// <typeparam name="T">The structured output record type.</typeparam>
    /// <param name="systemPrompt">The coaching system prompt.</param>
    /// <param name="userMessage">The assembled user message.</param>
    /// <param name="schema">Optional pre-built JSON schema dictionary (pass the byte-stable Frozen schema).</param>
    /// <param name="cacheControl">Optional Anthropic <c>cache_control</c> breakpoint for the system block.</param>
    /// <param name="modelOverride">
    /// The model id to target for this call (e.g. a Haiku floating alias). When
    /// <see langword="null"/> the configured default model is used.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of <c>(Result, Usage)</c> as the sibling overloads.</returns>
    Task<(T Result, AnthropicUsage Usage)> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userMessage,
        IReadOnlyDictionary<string, JsonElement>? schema,
        CacheControl? cacheControl,
        string? modelOverride,
        CancellationToken ct);

    /// <summary>
    /// Streams the LLM's free-text reply as text deltas for interactive
    /// coaching conversations. Yields each text delta as it arrives.
    /// </summary>
    /// <remarks>
    /// Totality contract (DEC-073, extended for streaming): the only exceptions
    /// that escape this method are
    /// <see cref="TransientCoachingLlmException"/> /
    /// <see cref="PermanentCoachingLlmException"/> (the call failed — retryable
    /// or not), <see cref="IncompleteCoachingLlmException"/> (the stream ended
    /// without a usable, complete reply — <c>max_tokens</c> truncation, context
    /// overflow, or a model refusal; the caller must discard the partial and
    /// persist an errored marker, never a complete turn), and an unwrapped
    /// <see cref="OperationCanceledException"/> when the caller's own token is
    /// cancelled (a client abort — <em>not</em> a service fault). A clean
    /// completion simply ends the enumeration after the final delta.
    /// </remarks>
    /// <param name="systemPrompt">The coaching system prompt.</param>
    /// <param name="userMessage">The assembled conversation user message.</param>
    /// <param name="ct">Cancellation token (propagate the request-aborted token).</param>
    /// <returns>An async stream of text deltas in arrival order.</returns>
    IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct);
}
