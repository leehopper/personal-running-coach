using System.ComponentModel;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Modules.Coaching.Adaptation;

/// <summary>
/// Pattern-B (DEC-058) structured-output schema for one plan adaptation, returned by
/// the coaching LLM at DEC-012 Level 2 (restructure). A single byte-stable schema with
/// an <see cref="AdaptationKind"/> discriminator plus two nullable typed slots
/// (<see cref="NudgePatch"/> / <see cref="RestructurePlan"/>): exactly the slot matching
/// the discriminator is non-null (absorb fills neither). Anthropic constrained decoding
/// cannot express "exactly one slot matches the discriminator" (it rejects <c>oneOf</c>),
/// so <see cref="PlanAdaptationOutputValidator"/> enforces that — and the
/// GATE-BEFORE-INCREASE safety invariant — at the .NET boundary after deserialization.
/// </summary>
/// <remarks>
/// <para>
/// Distinct from the <c>PlanAdaptedFromLog</c> domain event and the deterministic
/// <c>PlanAdaptationDiff</c>: this is the raw LLM proposal. The orchestration layer
/// (Slice 3 Unit 5) validates it, applies it to the plan projection, computes the
/// before/after diff deterministically, and appends the event.
/// </para>
/// <para>
/// Numerical bounds (e.g. weekly-mileage-jump ceilings, the over-performance cap) live
/// in the prompt and the property <see cref="DescriptionAttribute"/> text, NOT as
/// <c>minimum</c>/<c>maximum</c> schema keywords (Anthropic rejects those with HTTP 400).
/// </para>
/// </remarks>
public sealed record PlanAdaptationOutput
{
    /// <summary>
    /// Gets the kind of adaptation the coach is proposing. The single non-null slot must
    /// match this discriminator: <see cref="AdaptationKind.Absorb"/> fills neither slot,
    /// <see cref="AdaptationKind.Nudge"/> fills <see cref="NudgePatch"/>, and
    /// <see cref="AdaptationKind.Restructure"/> fills <see cref="RestructurePlan"/>.
    /// </summary>
    [Description("The kind of adaptation: Absorb (no change), Nudge (a small 1-2 workout reschedule), or Restructure (a week/load restructure). Exactly the slot matching this kind is filled; Absorb fills neither.")]
    public required AdaptationKind AdaptationKind { get; init; }

    /// <summary>
    /// Gets the deterministic safety tier the SafetyGate resolved for the triggering log,
    /// echoed back so the validator can enforce GATE-BEFORE-INCREASE against
    /// <see cref="NetLoadDelta"/>.
    /// </summary>
    [Description("The safety tier the deterministic safety gate resolved: Green (no constraint), Amber (must not increase load), or Red (handled deterministically without an LLM plan change). For Amber or Red the net load delta must be zero or negative.")]
    public required SafetyTier SafetyTier { get; init; }

    /// <summary>
    /// Gets the micro-cycle reschedule when <see cref="AdaptationKind"/> is
    /// <see cref="AdaptationKind.Nudge"/>; null otherwise.
    /// </summary>
    [Description("The small reschedule for a Nudge: revised workouts for the current micro week. Non-null only when the kind is Nudge.")]
    public required NudgePatch? NudgePatch { get; init; }

    /// <summary>
    /// Gets the week/load restructure when <see cref="AdaptationKind"/> is
    /// <see cref="AdaptationKind.Restructure"/>; null otherwise.
    /// </summary>
    [Description("The week/load restructure for a Restructure: revised upcoming weekly targets, revised current-week workouts, and the return-to-progression path. Non-null only when the kind is Restructure.")]
    public required RestructurePlan? RestructurePlan { get; init; }

    /// <summary>
    /// Gets the net change in training load this adaptation introduces, in kilometers of
    /// weekly volume (positive increases load, negative reduces it). For any non-Green
    /// <see cref="SafetyTier"/> this must be zero or negative (GATE-BEFORE-INCREASE).
    /// </summary>
    [Description("Net change in weekly training volume this adaptation introduces, in kilometers (positive = more load, negative = less). Must be zero or negative whenever the safety tier is not Green.")]
    public required int NetLoadDelta { get; init; }

    /// <summary>
    /// Gets the user-facing coaching rationale shown in the explain-the-change panel:
    /// validate what happened, name the data pattern, state the change, and show the path
    /// forward — in the coach's voice, with no controlling/clinical language.
    /// </summary>
    [Description("User-facing coaching rationale for the explain-the-change panel: acknowledge what happened, name the data pattern you saw, state what you changed, and show the path forward. Warm and direct; never controlling, never count misses, never claim to have physically observed the runner.")]
    public required string Rationale { get; init; }

    /// <summary>
    /// Gets the professional-referral category when the adaptation carries a safety
    /// referral (Amber injury / RED-S); null when no referral applies.
    /// </summary>
    [Description("The professional-referral category when a safety referral applies (Injury or RedS for Amber). Null when no referral applies.")]
    public required ReferralCategory? ReferralCategory { get; init; }
}
