namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Why a streamed coaching reply ended without a usable, complete turn
/// (Slice 4B / R-084). The SDK reports each of these as a clean enumeration end
/// rather than an exception, so <see cref="ICoachingLlm.StreamAsync"/> detects the
/// terminal finish reason and raises <see cref="IncompleteCoachingLlmException"/>.
/// Explicitly numbered for stable wire/telemetry encoding.
/// </summary>
public enum IncompleteReason
{
    /// <summary>The reply hit the output token cap (<c>stop_reason=max_tokens</c>) and is truncated.</summary>
    MaxTokens = 0,

    /// <summary>The conversation exceeded the model context window (<c>model_context_window_exceeded</c>).</summary>
    ContextWindowExceeded = 1,

    /// <summary>The model declined to answer (<c>stop_reason=refusal</c>).</summary>
    Refusal = 2,
}
