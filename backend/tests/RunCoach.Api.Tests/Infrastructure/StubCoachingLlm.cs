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
    private static Func<CancellationToken, IAsyncEnumerable<string>>? _streamBehavior;
    private static int _streamCallCount;
    private static Func<string>? _generateBehavior;
    private static int _generateCallCount;

    /// <summary>
    /// Gets the number of structured-output calls made since the last
    /// <see cref="Reset"/>. Tests assert <c>0</c> on the deterministic
    /// (absorb/nudge/Red) paths and exactly <c>1</c> on the L2 restructure path.
    /// </summary>
    public static int StructuredCallCount => Volatile.Read(ref _structuredCallCount);

    /// <summary>
    /// Gets the number of streaming calls made since the last <see cref="Reset"/>.
    /// The Slice 4B SSE tests assert <c>0</c> on the non-streaming paths (Red
    /// short-circuit, WorkoutLog card) and <c>1</c> on the Question answer path.
    /// </summary>
    public static int StreamCallCount => Volatile.Read(ref _streamCallCount);

    /// <summary>
    /// Gets the number of free-text <see cref="GenerateAsync"/> calls made since the last
    /// <see cref="Reset"/>. The Slice 4B confirm-then-commit ack is the first integration flow to
    /// use it; tests assert exactly <c>1</c> on the LLM-ack path and <c>0</c> on the scripted-ack
    /// (Kind=Error) path.
    /// </summary>
    public static int GenerateCallCount => Volatile.Read(ref _generateCallCount);

    /// <summary>
    /// Clears the scripted behavior and zeroes the call counter. Call from test
    /// setup/dispose so one test's script never leaks into the next.
    /// </summary>
    public static void Reset()
    {
        Volatile.Write(ref _structuredBehavior, null);
        Interlocked.Exchange(ref _structuredCallCount, 0);
        Volatile.Write(ref _streamBehavior, null);
        Interlocked.Exchange(ref _streamCallCount, 0);
        Volatile.Write(ref _generateBehavior, null);
        Interlocked.Exchange(ref _generateCallCount, 0);
    }

    /// <summary>
    /// Scripts the next free-text <see cref="GenerateAsync"/> call (Slice 4B confirm-then-commit
    /// ack): the delegate runs once per call and either returns the ack text or throws a
    /// <see cref="CoachingLlmException"/> to exercise the scripted-fallback path.
    /// </summary>
    /// <param name="behavior">The per-call free-text behavior delegate.</param>
    public static void UseGenerateBehavior(Func<string> behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        Volatile.Write(ref _generateBehavior, behavior);
    }

    /// <summary>
    /// Scripts the next streaming call (Slice 4B SSE endpoint): the delegate is invoked
    /// once per <see cref="StreamAsync"/> with the request-cancellation token and returns
    /// the token sequence to yield. A scripted iterator may yield then throw a
    /// <see cref="CoachingLlmException"/> (mid-stream failure) or an
    /// <see cref="IncompleteCoachingLlmException"/> (free-text-incomplete finish), or
    /// await on the supplied token to simulate a client abort.
    /// </summary>
    /// <param name="behavior">The per-call streaming behavior delegate.</param>
    public static void UseStreamBehavior(Func<CancellationToken, IAsyncEnumerable<string>> behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        Volatile.Write(ref _streamBehavior, behavior);
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
    public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct)
    {
        Interlocked.Increment(ref _generateCallCount);

        var behavior = Volatile.Read(ref _generateBehavior)
            ?? throw new InvalidOperationException(
                "StubCoachingLlm received an unscripted free-text GenerateAsync call. The Slice 4B "
                + "confirm ack is the first integration flow to use it — script the text via "
                + "StubCoachingLlm.UseGenerateBehavior(...) in the test arrange, or fix the flow under "
                + "test if no free-text generation was expected. Voice coverage lives in the eval tier.");

        // Honor the cancellation token so the free-text ack path can model a client abort: the
        // production adapter surfaces OperationCanceledException on a canceled call, and
        // ConfirmConversationalLogService treats that as the one ack failure that must propagate
        // rather than fall back to a scripted ack — a test passing a canceled token exercises it.
        ct.ThrowIfCancellationRequested();

        // The delegate may throw a CoachingLlmException to exercise the ack's scripted-fallback
        // path; anything it throws propagates exactly as the production adapter's failures would.
        return Task.FromResult(behavior());
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamAsync(string systemPrompt, string userMessage, CancellationToken ct)
    {
        Interlocked.Increment(ref _streamCallCount);

        var behavior = Volatile.Read(ref _streamBehavior)
            ?? throw new InvalidOperationException(
                "StubCoachingLlm received an unscripted streaming call. Script the token sequence "
                + "via StubCoachingLlm.UseStreamBehavior(...) in the test arrange, or fix the flow "
                + "under test if no streaming call was expected.");

        return behavior(ct);
    }

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
