using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Domain event appended to the existing per-user Plan stream when the deterministic
/// SafetyGate raises a non-Green signal for a logged workout (Slice 3 Unit 2,
/// DEC-079). Decoupled from <see cref="PlanAdaptedFromLog"/>: a Red crisis
/// short-circuits the flow and appends only this event (no plan change), while an
/// Amber signal may accompany a load-reducing adaptation. Consumed by the
/// conversation projection to emit a system-safety turn carrying the scripted
/// (crisis) or coaching (referral) <see cref="Content"/>. Appended via
/// <c>session.Events.Append</c> to the existing stream — never <c>StartStream</c>.
/// </summary>
/// <param name="TriggeringWorkoutLogId">The <c>WorkoutLog</c> whose notes/metrics raised the signal.</param>
/// <param name="SafetyTier">The tier the deterministic gate resolved (Amber or Red).</param>
/// <param name="ReferralCategory">The referral category the gate matched.</param>
/// <param name="Content">The user-facing safety message (scripted for crisis, coaching for referral).</param>
public sealed record SafetySignalRaised(
    Guid TriggeringWorkoutLogId,
    SafetyTier SafetyTier,
    ReferralCategory ReferralCategory,
    string Content);
