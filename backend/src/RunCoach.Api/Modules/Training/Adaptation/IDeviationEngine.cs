using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Computes the deterministic <see cref="DeviationResult"/> for a logged workout
/// versus its frozen prescription snapshot (Slice 3 PR2 / Unit 1). Pure and
/// stateless — no LLM, no I/O.
/// </summary>
public interface IDeviationEngine
{
    /// <summary>
    /// Compares the logged workout's actuals against its
    /// <see cref="WorkoutLog.Prescription"/> snapshot. Returns <c>null</c> when the
    /// log is off-plan (a null prescription snapshot) — a no-op for downstream
    /// adaptation.
    /// </summary>
    /// <param name="log">The committed workout log to evaluate.</param>
    /// <returns>The deviation signals, or <c>null</c> for an off-plan log.</returns>
    DeviationResult? Evaluate(WorkoutLog log);
}
