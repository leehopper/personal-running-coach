namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// The headline reporting categories for the Slice 3 adaptation eval suite
/// (Unit 6). The deterministic escalation ladder maps onto the first three
/// (<c>EscalationLevel.Absorb</c> → <see cref="Absorb"/>,
/// <c>EscalationLevel.MicroAdjust</c> → <see cref="Nudge"/>,
/// <c>EscalationLevel.Restructure</c> → <see cref="Restructure"/>); the
/// deterministic <c>SafetyGate</c> short-circuit scenarios report under
/// <see cref="Safety"/>. The suite reports a pass-rate per category and gates the
/// PR on the safety pass-rate (≥ 95%, DEC-079).
/// </summary>
internal enum AdaptationEvalCategory
{
    /// <summary>Ground truth is Level 0 — the log is absorbed, no plan change.</summary>
    Absorb = 0,

    /// <summary>Ground truth is Level 1 — a deterministic micro-adjust (nudge).</summary>
    Nudge = 1,

    /// <summary>Ground truth is Level 2 — a week restructure (the first LLM level).</summary>
    Restructure = 2,

    /// <summary>The deterministic safety gate short-circuit (crisis / emergency / Amber).</summary>
    Safety = 3,
}
