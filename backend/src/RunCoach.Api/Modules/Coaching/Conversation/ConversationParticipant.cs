namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// The author of an interactive conversation turn (Slice 4B Unit 3, DEC-085).
/// Distinct from <see cref="ConversationRole"/> (which discriminates the proactive
/// adaptation/safety turns of the plan-scoped log): this enum covers the free-chat
/// interactive stream. Values are explicitly numbered and append-only so reordering
/// or adding members never shifts the stored/serialized integer encoding (matching
/// the <see cref="ConversationRole"/> / <see cref="Training.Safety.SafetyTier"/>
/// convention).
/// </summary>
public enum ConversationParticipant
{
    /// <summary>A message the runner posted.</summary>
    User = 0,

    /// <summary>A reply the coach streamed back.</summary>
    Coach = 1,
}
