namespace RunCoach.Api.Modules.Training.Safety;

/// <summary>
/// Deterministic safety-escalation tier for a logged workout's free-text
/// (Slice 3 / DEC-079, high-risk subset). Resolved by <see cref="ISafetyGate"/>
/// before any LLM call, so safety is never left to LLM self-policing
/// (DEC-019 / DEC-030). Values are explicitly numbered so reordering members
/// does not shift any stored or serialized integer encoding; <see cref="Green"/>
/// is <c>0</c> so the safe tier is the default.
/// </summary>
public enum SafetyTier
{
    /// <summary>
    /// No risk keywords matched. Normal absorb / nudge / restructure coaching
    /// proceeds with no clamp.
    /// </summary>
    Green = 0,

    /// <summary>
    /// An injury or disordered-pattern signal matched. Coaching continues but
    /// any plan modification is clamped to non-increasing load and a referral
    /// turn is surfaced (GATE-BEFORE-INCREASE, enforced post-LLM in Unit 4).
    /// </summary>
    Amber = 1,

    /// <summary>
    /// A crisis or emergency-referral signal matched. The adaptation flow
    /// short-circuits: no LLM call and no plan-increase event; a scripted
    /// safety turn is surfaced directing the runner to professional help.
    /// </summary>
    Red = 2,
}
