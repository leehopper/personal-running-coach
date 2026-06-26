using System.Text.Json;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Deterministic <see cref="ICoachingLlm"/> stub registered by
/// <see cref="RunCoachAppFactory"/> in place of the production
/// <c>ClaudeCoachingLlm</c>, so integration tests that drive Wolverine handler
/// chains injecting <see cref="ICoachingLlm"/> (the Slice 3
/// <c>EvaluateAdaptationHandler</c>, the onboarding turn handler) never construct
/// the real Anthropic client. The test host carries no <c>Anthropic:ApiKey</c>,
/// and the integration tier makes ZERO real API calls by contract — eval-cached
/// LLM coverage lives in the eval tier, not here.
/// </summary>
/// <remarks>
/// <para>
/// The class must be <c>public</c>: Wolverine's codegen service-locates internal
/// types and silently falls back to scope-based DI for the whole chain, which
/// resolves a different <c>IDocumentSession</c> for the idempotency store than
/// the one Wolverine commits and breaks same-session idempotency.
/// </para>
/// <para>
/// Wolverine bakes the concrete registration into its generated handler code at
/// host boot, so per-test behavior cannot be swapped via DI. Instead, tests
/// script the next structured-output result through the static
/// <see cref="UseStructuredBehavior"/> surface (return a canned output, throw a
/// <see cref="CoachingLlmException"/>, or rendezvous on a barrier for
/// concurrency tests) and reset it via <see cref="Reset"/> in test setup. With
/// no scripted behavior the stub throws — an unscripted LLM call in the
/// integration tier is a test bug and must fail loudly rather than silently
/// reaching the network.
/// </para>
/// <para>
/// The static control surface is thread-safe (<see cref="Interlocked"/> counter,
/// volatile delegate slot): concurrency tests invoke the stub from parallel
/// handler executions.
/// </para>
/// </remarks>
public sealed class StubCoachingLlm : ICoachingLlm
{
    private static Func<object>? _structuredBehavior;
    private static int _structuredCallCount;

    /// <summary>
    /// Gets the number of structured-output calls made since the last
    /// <see cref="Reset"/>. Tests assert <c>0</c> on the deterministic
    /// (absorb/nudge/Red) paths and exactly <c>1</c> on the L2 restructure path.
    /// </summary>
    public static int StructuredCallCount => Volatile.Read(ref _structuredCallCount);

    /// <summary>
    /// Clears the scripted behavior and zeroes the call counter. Call from test
    /// setup/dispose so one test's script never leaks into the next.
    /// </summary>
    public static void Reset()
    {
        Volatile.Write(ref _structuredBehavior, null);
        Interlocked.Exchange(ref _structuredCallCount, 0);
    }

    /// <summary>
    /// Scripts the next structured-output calls: the delegate runs once per call
    /// and either returns the structured output instance for the requested type
    /// or throws (e.g. a <see cref="TransientCoachingLlmException"/> /
    /// <see cref="PermanentCoachingLlmException"/> to exercise the DEC-073
    /// failure envelopes).
    /// </summary>
    /// <param name="behavior">The per-call behavior delegate.</param>
    public static void UseStructuredBehavior(Func<object> behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        Volatile.Write(ref _structuredBehavior, behavior);
    }

    /// <inheritdoc />
    public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct) =>
        throw new InvalidOperationException(
            "StubCoachingLlm.GenerateAsync was called: no integration-tier flow scripts free-text "
            + "generation. Script the structured surface instead, or move the coverage to the eval tier.");

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamAsync(string systemPrompt, string userMessage, CancellationToken ct) =>
        throw new InvalidOperationException(
            "StubCoachingLlm.StreamAsync was called: no integration-tier flow scripts streaming "
            + "generation. Streaming coverage lives in the ClaudeCoachingLlm streaming tests; add a "
            + "dedicated streaming test seam if an integration flow needs it.");

    /// <inheritdoc />
    public Task<(T Result, AnthropicUsage Usage)> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userMessage,
        CancellationToken ct) =>
        GenerateStructuredAsync<T>(systemPrompt, userMessage, schema: null, cacheControl: null, ct);

    /// <inheritdoc />
    public Task<(T Result, AnthropicUsage Usage)> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userMessage,
        IReadOnlyDictionary<string, JsonElement>? schema,
        CacheControl? cacheControl,
        CancellationToken ct) =>
        GenerateStructuredAsync<T>(systemPrompt, userMessage, schema, cacheControl, modelOverride: null, ct);

    /// <inheritdoc />
    public Task<(T Result, AnthropicUsage Usage)> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userMessage,
        IReadOnlyDictionary<string, JsonElement>? schema,
        CacheControl? cacheControl,
        string? modelOverride,
        CancellationToken ct)
    {
        // The model override is irrelevant to the stub's scripted behavior — it
        // records the call and returns the scripted output regardless of model.
        Interlocked.Increment(ref _structuredCallCount);

        var behavior = Volatile.Read(ref _structuredBehavior)
            ?? throw new InvalidOperationException(
                "StubCoachingLlm received an unscripted structured-output call for "
                + $"{typeof(T).Name}. The integration tier makes zero real LLM calls; script the "
                + "outcome via StubCoachingLlm.UseStructuredBehavior(...) in the test arrange, or "
                + "fix the flow under test if no LLM call was expected.");

        // The delegate may throw a CoachingLlmException to exercise the
        // handler's terminal-failure envelope paths; anything it throws
        // propagates exactly as the production adapter's failures would.
        var outcome = behavior();
        if (outcome is not T typed)
        {
            // A null-returning script lands here too; report it as "null" instead
            // of tripping over outcome.GetType() while building the diagnostic.
            throw new InvalidOperationException(
                $"StubCoachingLlm behavior produced {outcome?.GetType().Name ?? "null"} but the caller "
                + $"requested {typeof(T).Name}; script a matching structured output.");
        }

        return Task.FromResult((typed, AnthropicUsage.Zero));
    }
}
