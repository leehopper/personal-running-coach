using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Resolves a DEC-012 escalation decision from a <see cref="DeviationResult"/>, the
/// pre-LLM <see cref="SafetyTier"/>, and the prior signal state (Slice 3 PR2 / Unit 1).
/// Pure and stateless — a total transition function, no LLM, no I/O.
/// </summary>
public interface IEscalationClassifier
{
    /// <summary>
    /// Resolves the escalation level/kind for one on-plan logged workout and returns
    /// the advanced signal state. L0 absorb and L1 micro-adjust are handled here with
    /// no LLM; L2 restructure is the level the PR5 orchestration escalates to the LLM.
    /// </summary>
    /// <param name="deviation">The deterministic deviation of an on-plan log (never null/off-plan).</param>
    /// <param name="safetyTier">The pre-LLM safety tier from the SafetyGate.</param>
    /// <param name="priorState">The signal state carried from the previous evaluation.</param>
    /// <returns>The resolved decision plus the next signal state.</returns>
    EscalationDecision Classify(DeviationResult deviation, SafetyTier safetyTier, AdaptationSignalState priorState);
}
