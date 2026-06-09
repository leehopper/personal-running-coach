namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// A non-retryable coaching-LLM failure (DEC-073): a client error the request itself caused
/// (400/401/403/404/422) or a malformed response body. Retrying the identical request will
/// fail the same way, so the caller surfaces a terminal error rather than retrying.
/// </summary>
public sealed class PermanentCoachingLlmException : CoachingLlmException
{
    /// <summary>Initializes a new instance of the <see cref="PermanentCoachingLlmException"/> class.</summary>
    /// <param name="message">A user-facing message safe to surface in the adaptation envelope.</param>
    /// <param name="innerException">The originating SDK exception, retained for diagnostics.</param>
    public PermanentCoachingLlmException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
