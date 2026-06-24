namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Thrown by <see cref="ICoachingLlm.StreamAsync"/> when a stream ends without a
/// usable, complete reply (R-084 errored-turn path): the output was truncated at
/// <c>max_tokens</c>, the context window was exceeded, or the model refused. The
/// SDK reports these as a clean enumeration end (not an exception), so the adapter
/// detects the terminal finish reason and raises this type explicitly.
///
/// It is deliberately distinct from
/// <see cref="TransientCoachingLlmException"/> / <see cref="PermanentCoachingLlmException"/>,
/// which answer "should the caller retry the call?" — this answers "this turn's
/// content is unusable: discard the partial and persist an errored marker, never a
/// complete turn." <see cref="Retryable"/> reflects whether re-sending the turn (with
/// a fresh idempotency GUID) could plausibly succeed: <see langword="true"/> for
/// <see cref="IncompleteReason.MaxTokens"/>; <see langword="false"/> for context
/// overflow or a refusal (re-sending the same input fails the same way).
/// </summary>
public sealed class IncompleteCoachingLlmException : CoachingLlmException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IncompleteCoachingLlmException"/> class.
    /// </summary>
    /// <param name="message">A user-safe description of the incomplete reply.</param>
    /// <param name="reason">Why the reply did not complete normally.</param>
    /// <param name="retryable">Whether re-sending the turn could plausibly succeed.</param>
    /// <param name="innerException">The optional underlying cause (usually none — the SDK ends cleanly).</param>
    public IncompleteCoachingLlmException(
        string message,
        IncompleteReason reason,
        bool retryable,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
        Retryable = retryable;
    }

    /// <summary>Gets the reason the reply did not complete normally.</summary>
    public IncompleteReason Reason { get; }

    /// <summary>Gets a value indicating whether re-sending the turn could plausibly succeed.</summary>
    public bool Retryable { get; }
}
