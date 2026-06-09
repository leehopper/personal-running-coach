namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Base type for the only exceptions <see cref="ICoachingLlm"/> surfaces to callers under
/// DEC-073 (first live in Slice 3). The adapter translates every Anthropic SDK failure into
/// one of the two concrete subtypes so callers never take a dependency on SDK exception types
/// and can branch on retryability alone.
/// </summary>
public abstract class CoachingLlmException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CoachingLlmException"/> class.</summary>
    /// <param name="message">A user-facing message safe to surface in the adaptation envelope.</param>
    /// <param name="innerException">The originating SDK exception, retained for diagnostics.</param>
    protected CoachingLlmException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
