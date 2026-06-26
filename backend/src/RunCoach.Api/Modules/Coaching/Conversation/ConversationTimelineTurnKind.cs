namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Discriminates the four turn variants the composed conversation timeline unions
/// (Slice 4B Unit 3, DEC-085): the interactive user/coach turns from the user-scoped
/// <c>Conversation</c> stream, and the proactive adaptation/safety turns from the
/// current plan's <see cref="ConversationLogView"/>. The frontend switches on this to
/// pick a renderer — chat bubbles for interactive turns, the existing
/// adaptation/safety components for proactive turns. Explicitly numbered and
/// append-only for wire stability.
/// </summary>
public enum ConversationTimelineTurnKind
{
    /// <summary>An interactive message the runner posted.</summary>
    User = 0,

    /// <summary>An interactive reply the coach streamed back.</summary>
    Coach = 1,

    /// <summary>A proactive adaptation explanation from the current plan's log.</summary>
    Adaptation = 2,

    /// <summary>A proactive deterministic safety message from the current plan's log.</summary>
    Safety = 3,
}
