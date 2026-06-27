namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// System-authored, non-LLM scripted copy for the streaming Q&amp;A endpoint (Slice 4B
/// PR4) that is not safety content. Deterministic — it never passes through the LLM, so
/// it carries no voice/trademark risk and needs no <c>VoiceProseGuard</c> gate, but it is
/// written in the DEC-084 gruff-direct register for consistency with the coach's voice.
/// </summary>
public static class ConversationScripts
{
    /// <summary>
    /// The clarification streamed when the intent classifier returns
    /// <see cref="MessageIntent.Ambiguous"/> — the coach asks rather than guessing,
    /// keeping a workout out of the plan until the runner says so (confirm-then-commit).
    /// </summary>
    public const string Clarification =
        "I can't tell if that's a question or a workout you're logging. Which is it? Ask me "
        + "what you want to know, or give me the run with its distance and time and I'll set "
        + "it up for you to confirm.";
}
