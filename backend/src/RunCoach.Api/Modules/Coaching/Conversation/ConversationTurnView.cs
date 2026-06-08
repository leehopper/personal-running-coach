using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// One persisted turn in a runner's conversation read-model (Slice 3 Unit 2,
/// DEC-079). NET-NEW and intentionally distinct from the legacy two-string
/// <see cref="Models.ConversationTurn"/> DTO it supersedes. Each turn is projected
/// from exactly one Plan-stream event; <see cref="TriggeringPlanEventId"/> is the
/// Marten event id (stable across replay) so the conversation projection is
/// idempotent — replaying a stream yields exactly one turn per event.
/// </summary>
public sealed record ConversationTurnView
{
    /// <summary>Gets the Marten event id of the source event — the per-turn replay-stable key.</summary>
    public required Guid TriggeringPlanEventId { get; init; }

    /// <summary>Gets whether this is an adaptation explanation or a safety message.</summary>
    public required ConversationRole Role { get; init; }

    /// <summary>Gets the user-facing copy rendered in the panel.</summary>
    public required string Content { get; init; }

    /// <summary>Gets the DEC-012 escalation level for an adaptation turn; null for a safety turn.</summary>
    public EscalationLevel? EscalationLevel { get; init; }

    /// <summary>Gets the safety tier the deterministic gate resolved for the triggering log.</summary>
    public required SafetyTier SafetyTier { get; init; }

    /// <summary>Gets the referral category for a safety turn; <see cref="Safety.ReferralCategory.None"/> otherwise.</summary>
    public ReferralCategory ReferralCategory { get; init; }

    /// <summary>Gets the adaptation kind for an adaptation turn; null for a safety turn.</summary>
    public AdaptationKind? AdaptationKind { get; init; }

    /// <summary>Gets the structured before/after diff for an adaptation turn; null for a safety turn.</summary>
    public PlanAdaptationDiff? Diff { get; init; }

    /// <summary>Gets the <c>WorkoutLog</c> whose logging triggered the source event.</summary>
    public required Guid TriggeringWorkoutLogId { get; init; }

    /// <summary>Gets the wall-clock time the source event was appended (Marten event timestamp).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Builds an assistant-adaptation turn from a <see cref="PlanAdaptedFromLog"/>
    /// event and its Marten metadata.
    /// </summary>
    /// <param name="eventId">The Marten event id of the source event.</param>
    /// <param name="createdAt">The Marten event timestamp.</param>
    /// <param name="data">The adaptation event payload.</param>
    /// <returns>The projected adaptation turn.</returns>
    public static ConversationTurnView FromAdaptation(
        Guid eventId,
        DateTimeOffset createdAt,
        PlanAdaptedFromLog data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new ConversationTurnView
        {
            TriggeringPlanEventId = eventId,
            Role = ConversationRole.AssistantAdaptation,
            Content = data.Rationale,
            EscalationLevel = data.EscalationLevel,
            SafetyTier = data.SafetyTier,
            ReferralCategory = ReferralCategory.None,
            AdaptationKind = data.AdaptationKind,
            Diff = data.Diff,
            TriggeringWorkoutLogId = data.TriggeringWorkoutLogId,
            CreatedAt = createdAt,
        };
    }

    /// <summary>
    /// Builds a system-safety turn from a <see cref="SafetySignalRaised"/> event and
    /// its Marten metadata.
    /// </summary>
    /// <param name="eventId">The Marten event id of the source event.</param>
    /// <param name="createdAt">The Marten event timestamp.</param>
    /// <param name="data">The safety event payload.</param>
    /// <returns>The projected safety turn.</returns>
    public static ConversationTurnView FromSafety(
        Guid eventId,
        DateTimeOffset createdAt,
        SafetySignalRaised data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new ConversationTurnView
        {
            TriggeringPlanEventId = eventId,
            Role = ConversationRole.SystemSafety,
            Content = data.Content,
            EscalationLevel = null,
            SafetyTier = data.SafetyTier,
            ReferralCategory = data.ReferralCategory,
            AdaptationKind = null,
            Diff = null,
            TriggeringWorkoutLogId = data.TriggeringWorkoutLogId,
            CreatedAt = createdAt,
        };
    }
}
