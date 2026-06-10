namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// The asymmetric score for one classification scenario: the mismatch direction,
/// a 0–1 numeric score, and whether the mismatch is a hard failure that must fail
/// the suite (an under-reaction).
/// </summary>
/// <param name="Outcome">The direction of any mismatch (or <see cref="EscalationScoreOutcome.Match"/>).</param>
/// <param name="Score">The numeric score in [0, 1]: 1.0 for an exact match, low for an over-reaction, 0 for an under-reaction.</param>
/// <param name="IsHardFail">True when the scenario under-reacted — the suite-failing case.</param>
internal sealed record EscalationScore(
    EscalationScoreOutcome Outcome,
    double Score,
    bool IsHardFail);
