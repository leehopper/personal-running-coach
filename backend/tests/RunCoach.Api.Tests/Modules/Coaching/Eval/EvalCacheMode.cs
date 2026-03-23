namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Controls how eval tests interact with the LLM API response cache.
/// Set via the EVAL_CACHE_MODE environment variable.
/// </summary>
public enum EvalCacheMode
{
    /// <summary>
    /// Default mode. Behaves as Record when an API key is available,
    /// Replay when no API key is configured.
    /// </summary>
    Auto,

    /// <summary>
    /// Live API calls with response caching enabled.
    /// Requires a valid Anthropic API key.
    /// </summary>
    Record,

    /// <summary>
    /// Cache-only mode. No API calls are made. Cache misses throw
    /// a descriptive exception indicating which scenario needs re-recording.
    /// Used in CI for deterministic, zero-cost test runs.
    /// </summary>
    Replay,
}
