using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Domain event appended to the existing per-user Plan stream when a logged workout
/// drives a deterministic (nudge) or LLM-authored (restructure) plan adaptation
/// (Slice 3 Unit 2, DEC-079). Carries the resolved DEC-012
/// <see cref="Adaptation.EscalationLevel"/>, the <see cref="Adaptation.AdaptationKind"/>
/// discriminator, the <see cref="Safety.SafetyTier"/> the gate resolved, the
/// user-facing <see cref="Rationale"/>, and the structured before/after
/// <see cref="Diff"/> the panel renders. Consumed by <see cref="PlanProjection"/>
/// (mutates the plan read model) and the conversation projection (emits an
/// assistant-adaptation turn). Appended via <c>session.Events.Append</c> to the
/// existing stream — never <c>StartStream</c>.
/// </summary>
/// <param name="TriggeringWorkoutLogId">The <c>WorkoutLog</c> whose logging triggered this adaptation.</param>
/// <param name="AdaptationKind">Whether the change was a nudge or a restructure.</param>
/// <param name="EscalationLevel">The resolved DEC-012 escalation level.</param>
/// <param name="SafetyTier">The safety tier the deterministic gate resolved for the log.</param>
/// <param name="Rationale">The user-facing explanation rendered in the panel.</param>
/// <param name="Diff">The structured before/after changes applied to the plan.</param>
public sealed record PlanAdaptedFromLog(
    Guid TriggeringWorkoutLogId,
    AdaptationKind AdaptationKind,
    EscalationLevel EscalationLevel,
    SafetyTier SafetyTier,
    string Rationale,
    PlanAdaptationDiff Diff);
