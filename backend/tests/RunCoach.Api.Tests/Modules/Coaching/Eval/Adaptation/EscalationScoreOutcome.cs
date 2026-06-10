namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// The direction of a scored mismatch between a scenario's declared ground-truth
/// escalation level and the level the deterministic engine actually resolved.
/// Scoring is deliberately asymmetric (DEC-079 recall-over-precision): an
/// <see cref="UnderReaction"/> is a hard failure (the system did too little for a
/// runner who needed more), while an <see cref="OverReaction"/> scores low but
/// does not fail the suite (doing slightly more than needed is the lesser evil).
/// </summary>
internal enum EscalationScoreOutcome
{
    /// <summary>The resolved level equals the ground-truth level.</summary>
    Match = 0,

    /// <summary>The resolved level is below ground truth — under-reaction (hard fail).</summary>
    UnderReaction = 1,

    /// <summary>The resolved level is above ground truth — over-reaction (low score, not a penalty).</summary>
    OverReaction = 2,
}
