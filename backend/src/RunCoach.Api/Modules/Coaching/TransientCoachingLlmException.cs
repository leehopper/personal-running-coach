namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// A retryable coaching-LLM failure (DEC-073): rate limiting (429), server errors (5xx incl.
/// 529), request timeout/conflict (408/409), or a transport failure. The Anthropic SDK has
/// already exhausted its own bounded retries by the time this surfaces, so a retry is the
/// caller's decision; at MVP-0 the adaptation flow does not auto-retry.
/// </summary>
public sealed class TransientCoachingLlmException : CoachingLlmException
{
    /// <summary>Initializes a new instance of the <see cref="TransientCoachingLlmException"/> class.</summary>
    /// <param name="message">A user-facing message safe to surface in the adaptation envelope.</param>
    /// <param name="retryAfterSeconds">
    /// The server-advised retry delay in seconds, read from the raw <c>Retry-After</c> response
    /// header (the SDK exposes no accessor), or <see langword="null"/> when none was advertised.
    /// </param>
    /// <param name="innerException">The originating SDK exception, retained for diagnostics.</param>
    public TransientCoachingLlmException(string message, int? retryAfterSeconds, Exception? innerException)
        : base(message, innerException)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    /// <summary>
    /// Gets the server-advised retry delay in seconds, or <see langword="null"/> when the
    /// response carried no <c>Retry-After</c> header.
    /// </summary>
    public int? RetryAfterSeconds { get; }
}
