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
    /// lost race). The log is saved; the LLM is NOT called again — the review just failed. The copy
    /// is deliberately neutral and does NOT instruct a resend: on the lost-race branch a re-confirm
    /// re-derives the same <c>WorkoutLog</c>/adaptation marker and is a no-op, so "send it again"
    /// would loop the runner uselessly (DEC-080/081 posture).
    /// </summary>
    public const string SavedReviewRetrying =
        "Saved your run. I couldn't finish reviewing it against your plan just now. Check your plan again in a moment.";

    /// <summary>
    /// The ack when the plan adapted cleanly but the LLM ack generation itself failed. The log is
    /// saved and the plan is updated, so this points the runner at their plan.
    /// </summary>
    public const string AckUnavailable =
        "Logged your run. Your plan's updated — take a look.";
}
