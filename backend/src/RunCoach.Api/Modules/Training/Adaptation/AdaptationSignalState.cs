namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// The minimal per-user signal + plan-state the deterministic escalation classifier
/// threads between evaluations (Slice 3 PR2 / Unit 1, DEC-078). Pure data: the
/// classifier consumes a prior state and returns the next one. The state persists
/// per plan as an <see cref="AdaptationSignalStateDocument"/> Marten document stored
/// on the same session as the evaluation's event appends, so signal-state advancement
/// commits (or rolls back) atomically with the events it justified — resolving the
/// PR2-era open question of read-model vs. recompute-per-evaluation in favor of the
/// small persisted document. Rehydration from that document flows through
/// <see cref="Create"/>, the validating deserialization boundary. No
/// EWMA/ACWR/CTL/ATL/TSB load model is computed at MVP-0.
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

    /// <summary>
    /// Creates a signal state, enforcing the invariants the classifier's transition
    /// maintains internally but a persisted document cannot guarantee on its own.
    /// This factory is THE deserialization boundary: every rehydration from
    /// <see cref="AdaptationSignalStateDocument"/> flows through it, so a hand-edited
    /// or schema-drifted row fails loudly (or is clamped back into band) at load time
    /// instead of silently corrupting the hysteresis math downstream.
    /// </summary>
    /// <param name="planState">
    /// The plan-state in the hysteresis machine. Must be a defined
    /// <see cref="PlanState"/> member: the enum persists as its numeric encoding,
    /// so a drifted document can carry a value no member maps to.
    /// </param>
    /// <param name="rollingDeviationScore">
    /// The rolling deviation accumulator; clamped into
    /// [0, <see cref="AdaptationThresholds.MaxRollingDeviationScore"/>] — the same
    /// bound the classifier applies on every transition — so an out-of-band stored
    /// value cannot widen the hysteresis dead-zone. Must not be NaN (NaN cannot be
    /// clamped and would poison every subsequent threshold comparison).
    /// </param>
    /// <param name="consecutiveMissedDays">
    /// Run of consecutive skipped days; negative values are rejected because the
    /// forced-restructure trigger counts up from zero.
    /// </param>
    /// <param name="lastAdaptationOn">
    /// The day the most recent restructure fired. Required when
    /// <paramref name="planState"/> is <see cref="PlanState.NeedsAdjustment"/>:
    /// a restructure is the only transition into that state and it always stamps
    /// this date — a null here would silently disable the cooldown half of the
    /// asymmetric hysteresis.
    /// </param>
    /// <returns>A validated <see cref="AdaptationSignalState"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="planState"/> is not a defined
    /// <see cref="PlanState"/> member, <paramref name="rollingDeviationScore"/>
    /// is NaN, or <paramref name="consecutiveMissedDays"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="planState"/> is
    /// <see cref="PlanState.NeedsAdjustment"/> and
    /// <paramref name="lastAdaptationOn"/> is <see langword="null"/>.
    /// </exception>
    public static AdaptationSignalState Create(
        PlanState planState,
        double rollingDeviationScore,
        int consecutiveMissedDays,
        DateOnly? lastAdaptationOn)
    {
        // The enum persists as its numeric encoding, so a drifted or hand-edited
        // document can carry a value no PlanState member maps to; reject it here
        // rather than hand the classifier a state outside its transition table.
        if (!Enum.IsDefined(planState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(planState),
                planState,
                "PlanState must be a defined plan-state value.");
        }

        // NaN slips through Math.Clamp unchanged (every IEEE comparison against it
        // is false), so reject it explicitly rather than let it poison the
        // threshold comparisons the classifier runs on every evaluation.
        if (double.IsNaN(rollingDeviationScore))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rollingDeviationScore),
                rollingDeviationScore,
                "RollingDeviationScore must be a number; NaN cannot be clamped into the score band.");
        }

        if (consecutiveMissedDays < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(consecutiveMissedDays),
                consecutiveMissedDays,
                "ConsecutiveMissedDays must be non-negative; the forced-restructure trigger counts up from zero.");
        }

        if (planState == PlanState.NeedsAdjustment && lastAdaptationOn is null)
        {
            throw new ArgumentException(
                "PlanState.NeedsAdjustment requires LastAdaptationOn: a restructure is the only " +
                "transition into that state and always stamps the date. A null here would silently " +
                "disable the cooldown half of the asymmetric hysteresis.",
                nameof(lastAdaptationOn));
        }

        var clampedScore = Math.Clamp(
            rollingDeviationScore,
            0.0,
            AdaptationThresholds.MaxRollingDeviationScore);

        return new AdaptationSignalState(
            planState,
            clampedScore,
            consecutiveMissedDays,
            lastAdaptationOn);
    }
}
