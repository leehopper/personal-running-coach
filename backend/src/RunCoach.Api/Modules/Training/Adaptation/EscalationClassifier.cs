using RunCoach.Api.Modules.Training.Safety;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Deterministic escalation classifier (Slice 3 PR2 / Unit 1, DEC-012/DEC-078):
/// resolves L0 absorb / L1 micro-adjust / L2 restructure from a
/// <see cref="DeviationResult"/>, the safety tier, and a prior signal state, applying
/// asymmetric enter/exit hysteresis so a restructure cannot flip-flop. Pure and
/// stateless — no LLM, no I/O; the L2 restructure is where PR5 hands off to the LLM.
/// </summary>
public sealed class EscalationClassifier : IEscalationClassifier
{
    /// <inheritdoc />
    public EscalationDecision Classify(
        DeviationResult deviation,
        SafetyTier safetyTier,
        AdaptationSignalState priorState)
    {
        ArgumentNullException.ThrowIfNull(deviation);
        ArgumentNullException.ThrowIfNull(priorState);

        // Red short-circuits to safety-only in the PR5 orchestration; defensively the
        // classifier yields no plan adaptation and leaves the signal state untouched.
        if (safetyTier == SafetyTier.Red)
        {
            return Absorb(priorState);
        }

        var missed = deviation.CompletionStatus == CompletionStatus.Skipped;
        var newStreak = missed ? priorState.ConsecutiveMissedDays + 1 : 0;
        var newScore = IsUnderPerformance(deviation)
            ? Math.Min(
                priorState.RollingDeviationScore + AdaptationThresholds.MinorDeviationStep,
                AdaptationThresholds.MaxRollingDeviationScore)
            : priorState.RollingDeviationScore * AdaptationThresholds.OnTargetDecayFactor;

        // Asymmetric hysteresis: once a restructure has fired, suppress re-firing until
        // the cooldown has elapsed AND the signal has cleared the (lower) exit threshold.
        if (priorState.PlanState == PlanState.NeedsAdjustment)
        {
            var cooldownActive = priorState.LastAdaptationOn is { } last
                && deviation.OccurredOn.DayNumber - last.DayNumber < AdaptationThresholds.RestructureCooldownDays;
            if (cooldownActive || newScore > AdaptationThresholds.RestructureExitScore)
            {
                // Still inside the dead-zone — absorb without re-firing, hold the state.
                return Absorb(priorState with
                {
                    RollingDeviationScore = newScore,
                    ConsecutiveMissedDays = newStreak,
                });
            }

            // Cleared: cooldown elapsed and the signal settled at or below the exit threshold.
            return Absorb(new AdaptationSignalState(
                PlanState.OnTrack, newScore, newStreak, priorState.LastAdaptationOn));
        }

        // L2 restructure: sustained under-performance crossing the enter threshold, or a
        // run of consecutive missed days. This is where PR5 hands off to the LLM.
        if (newScore >= AdaptationThresholds.RestructureEnterScore
            || newStreak >= AdaptationThresholds.ConsecutiveMissedDaysForRestructure)
        {
            return new EscalationDecision(
                EscalationLevel.Restructure,
                AdaptationKind.Restructure,
                new AdaptationSignalState(PlanState.NeedsAdjustment, newScore, newStreak, deviation.OccurredOn));
        }

        // L1 micro-adjust: a single reschedulable missed/moved key workout, or accumulated
        // minor deviations reaching the micro-adjust threshold.
        if ((missed && deviation.IsKeyWorkout) || newScore >= AdaptationThresholds.MicroAdjustEnterScore)
        {
            return new EscalationDecision(
                EscalationLevel.MicroAdjust,
                AdaptationKind.Nudge,
                new AdaptationSignalState(PlanState.MinorDeviation, newScore, newStreak, priorState.LastAdaptationOn));
        }

        // L0 absorb.
        var nextPlanState = newScore >= AdaptationThresholds.MinorDeviationStep
            ? PlanState.MinorDeviation
            : PlanState.OnTrack;
        return new EscalationDecision(
            EscalationLevel.Absorb,
            AdaptationKind.Absorb,
            new AdaptationSignalState(nextPlanState, newScore, newStreak, priorState.LastAdaptationOn));
    }

    private static EscalationDecision Absorb(AdaptationSignalState state) =>
        new(EscalationLevel.Absorb, AdaptationKind.Absorb, state);

    /// <summary>
    /// A log under-performs when it was not completed (skipped or cut short), its pace
    /// was slower than the prescribed band, or it ran meaningfully short of the
    /// prescribed distance. Over-performance and on-target both fall through to decay.
    /// </summary>
    private static bool IsUnderPerformance(DeviationResult deviation) =>
        deviation.CompletionStatus != CompletionStatus.Complete
        || deviation.PaceBand == PaceBandMembership.SlowerThanSlow
        || deviation.DistanceDeviationPercent < -AdaptationThresholds.DistanceTolerancePercent;
}
