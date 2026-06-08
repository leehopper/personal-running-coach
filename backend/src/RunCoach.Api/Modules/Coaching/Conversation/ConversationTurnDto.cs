using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Wire DTO for one turn in the read-only "Explain-the-change" panel (Slice 3
/// Unit 2, DEC-079), projected from a <see cref="ConversationTurnView"/>. The
/// frontend (PR7) switches on <see cref="Role"/> + <see cref="SafetyTier"/> for
/// the render style and severity accent, and renders the structured
/// <see cref="Diff"/> in the "Show what changed" expander. Nullable members
/// (<see cref="EscalationLevel"/>, <see cref="AdaptationKind"/>, <see cref="Diff"/>)
/// are absent on safety-only turns.
/// </summary>
/// <param name="TriggeringPlanEventId">The Marten event id of the source event.</param>
/// <param name="Role">Whether this is an adaptation explanation or a safety message.</param>
/// <param name="Content">The user-facing copy.</param>
/// <param name="EscalationLevel">The DEC-012 level for an adaptation turn; null for a safety turn.</param>
/// <param name="SafetyTier">The safety tier resolved for the triggering log.</param>
/// <param name="ReferralCategory">The referral category for a safety turn; None otherwise.</param>
/// <param name="AdaptationKind">The adaptation kind for an adaptation turn; null for a safety turn.</param>
/// <param name="Diff">The structured before/after diff for an adaptation turn; null for a safety turn.</param>
/// <param name="TriggeringWorkoutLogId">The workout log whose logging triggered the source event.</param>
/// <param name="CreatedAt">The time the source event was appended (Marten event timestamp).</param>
public sealed record ConversationTurnDto(
    Guid TriggeringPlanEventId,
    ConversationRole Role,
    string Content,
    EscalationLevel? EscalationLevel,
    SafetyTier SafetyTier,
    ReferralCategory ReferralCategory,
    AdaptationKind? AdaptationKind,
    PlanAdaptationDiff? Diff,
    Guid TriggeringWorkoutLogId,
    DateTimeOffset CreatedAt);
