namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// The minimal per-user signal + plan-state the deterministic escalation classifier
/// threads between evaluations (Slice 3 PR2 / Unit 1, DEC-078). Pure data: the
/// classifier consumes a prior state and returns the next one. <strong>How this state
/// is persisted (a small Marten read-model vs. recomputed per evaluation from the
/// recent-log window) is deliberately left to the PR5 orchestration</strong> — the
/// spec's open question — so the deterministic layer stays a pure, total transition.
/// No EWMA/ACWR/CTL/ATL/TSB load model is computed at MVP-0.
/// </summary>
/// <param name="PlanState">The current plan-state in the hysteresis machine.</param>
/// <param name="RollingDeviationScore">
/// A simple accumulator of recent under-performance: it steps up on an under-performing
/// log and decays toward zero on an on-target log. Not a calibrated load metric.
/// </param>
/// <param name="ConsecutiveMissedDays">Run of consecutive skipped days; resets on any non-skipped log.</param>
/// <param name="LastAdaptationOn">
/// The day the most recent restructure fired, used for the cooldown half of the
/// hysteresis; <c>null</c> until the first restructure.
/// </param>
public sealed record AdaptationSignalState(
    PlanState PlanState,
    double RollingDeviationScore,
    int ConsecutiveMissedDays,
    DateOnly? LastAdaptationOn)
{
    /// <summary>Gets the starting state for a user with no adaptation history.</summary>
    public static AdaptationSignalState Initial { get; } =
        new(PlanState.OnTrack, 0.0, 0, null);
}
