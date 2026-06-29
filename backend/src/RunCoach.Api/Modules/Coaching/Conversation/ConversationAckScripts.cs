namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// System-authored, non-LLM scripted acknowledgments for the conversational-logging
/// confirm-then-commit path (Slice 4B PR5). Deterministic — they never pass through the LLM, so
/// they carry no voice/trademark risk and need no <c>VoiceProseGuard</c> gate, but they are
/// written in the DEC-084 gruff-direct register. Used when the LLM ack cannot or should not be
/// generated.
/// </summary>
public static class ConversationAckScripts
{
    /// <summary>
    /// The ack when the adaptation rode back <c>Kind=Error</c> (a terminal review failure or a
    /// lost race). The log is saved; the review retries on a re-confirm (DEC-080/081 posture). The
    /// LLM is NOT called again — the review just failed.
    /// </summary>
    public const string SavedReviewRetrying =
        "Saved your run. I couldn't review it against your plan just now — send it again and I'll pick it up.";

    /// <summary>
    /// The ack when the plan adapted cleanly but the LLM ack generation itself failed. The log is
    /// saved and the plan is updated, so this points the runner at their plan.
    /// </summary>
    public const string AckUnavailable =
        "Logged your run. Your plan's updated — take a look.";
}
