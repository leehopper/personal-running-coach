namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Orchestrates the Slice 4B confirm-then-commit flow (DEC-085 D4): map the confirmed draft onto
/// the unchanged Slice 2b create path, run the identical post-create adaptation seam, and persist
/// a single coach acknowledgment turn afterward.
/// </summary>
public interface IConfirmConversationalLogService
{
    /// <summary>
    /// Commits the confirmed draft as a workout log, evaluates it for plan adaptation, and
    /// persists the coach acknowledgment turn (an LLM ack on an adapted outcome; a scripted ack on
    /// a terminal review failure). Idempotent on the request's client message id — a double-confirm
    /// commits one log and appends one ack. Never throws on an adaptation or ack failure: the
    /// committed log always wins and the failure rides the returned envelope.
    /// </summary>
    /// <param name="userId">The authenticated runner.</param>
    /// <param name="request">The confirmed draft + the card's client message id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The committed log id plus the adaptation envelope.</returns>
    /// <exception cref="System.ArgumentException">A draft value fails a domain invariant (e.g. a negative distance/duration).</exception>
    Task<ConfirmConversationalLogResponseDto> ConfirmAsync(
        Guid userId,
        ConfirmConversationalLogRequestDto request,
        CancellationToken ct);
}
