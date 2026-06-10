namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// One scored scenario row in the adaptation eval report: which category it
/// counts toward, whether it passed (exact match / correct classification),
/// whether it was a hard fail (an under-reaction or a missed safety signal), the
/// numeric score, the mismatch direction, and a human-readable detail line for
/// the proof artifact.
/// </summary>
/// <param name="ScenarioId">Stable scenario identifier (e.g. "nudge.lee.missed-tempo").</param>
/// <param name="Category">The reporting category this scenario counts toward.</param>
/// <param name="Outcome">The scored mismatch direction (or a match).</param>
/// <param name="Passed">True when the scenario matched its ground truth exactly.</param>
/// <param name="IsHardFail">True when the scenario under-reacted / missed a safety signal.</param>
/// <param name="Score">The 0–1 numeric score.</param>
/// <param name="Detail">A short human-readable explanation (expected vs actual).</param>
internal sealed record AdaptationEvalScenarioResult(
    string ScenarioId,
    AdaptationEvalCategory Category,
    EscalationScoreOutcome Outcome,
    bool Passed,
    bool IsHardFail,
    double Score,
    string Detail);
