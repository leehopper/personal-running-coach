using RunCoach.Api.Modules.Training.Safety;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Deterministic escalation classifier (Slice 3 PR2 / Unit 1, DEC-012/DEC-078):
/// resolves L0 absorb / L1 micro-adjust / L2 restructure from a
/// <see cref="DeviationResult"/>, the safety tier, and a prior signal state, applying
/// asymmetric enter/exit hysteresis so a restructure cannot flip-flop. The
/// anti-flip-flop guarantee is L2-only: inside the dead-zone a missed key workout
/// still earns an L1 micro-adjust, and a fresh consecutive-missed-days hard trigger
/// re-escalates once the cooldown has elapsed. Pure and stateless — no LLM, no I/O
/// beyond suppressed-trigger observability logging; the L2 restructure is where
/// <see cref="EvaluateAdaptationHandler"/> hands off to the coaching LLM.
/// </summary>
public sealed partial class EscalationClassifier(ILogger<EscalationClassifier> logger) : IEscalationClassifier
{
    private readonly ILogger<EscalationClassifier> _logger = logger;

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

        // Asymmetric hysteresis: once a restructure has fired, suppress score-driven
        // re-firing until the cooldown has elapsed AND the signal has cleared the
        // (lower) exit threshold. The dead-zone is L2-score-only: hard triggers and
        // L1 key-workout misses punch through per the carve-outs below.
        if (priorState.PlanState == PlanState.NeedsAdjustment)
        {
            var cooldownActive = priorState.LastAdaptationOn is { } last
                && deviation.OccurredOn.DayNumber - last.DayNumber < AdaptationThresholds.RestructureCooldownDays;

            // Hard-trigger re-escalation: a fresh run of consecutive missed days
            // re-fires the restructure once the cooldown has elapsed, even while the
            // rolling score still sits inside the dead-zone — a genuinely
            // deteriorating runner must not be silently absorbed.
            if (!cooldownActive && newStreak >= AdaptationThresholds.ConsecutiveMissedDaysForRestructure)
            {
                return Restructure(newScore, newStreak, deviation.OccurredOn);
            }

            // The anti-flip-flop guarantee is L2-only: a missed key workout still
            // earns a deterministic L1 reschedule mid-recovery. The dead-zone state
            // holds (no plan-state transition, restructure stamp untouched).
            if (missed && deviation.IsKeyWorkout)
            {
                return new EscalationDecision(
                    EscalationLevel.MicroAdjust,
                    AdaptationKind.Nudge,
                    priorState with
                    {
                        RollingDeviationScore = newScore,
                        ConsecutiveMissedDays = newStreak,
                    });
            }

            if (cooldownActive || newScore > AdaptationThresholds.RestructureExitScore)
            {
                // Still inside the dead-zone — absorb without re-firing, hold the
                // state, and surface any suppressed would-be trigger for observability.
                LogIfTriggerSuppressed(deviation, newScore, newStreak, cooldownActive);
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
        // run of consecutive missed days. This is where EvaluateAdaptationHandler hands
        // off to the coaching LLM.
        if (newScore >= AdaptationThresholds.RestructureEnterScore
            || newStreak >= AdaptationThresholds.ConsecutiveMissedDaysForRestructure)
        {
            return Restructure(newScore, newStreak, deviation.OccurredOn);
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
    /// Fire a restructure now and stamp the adaptation date. Both restructure
    /// triggers — the enter-threshold crossing and the dead-zone
    /// consecutive-missed-days hard trigger — resolve to this same decision
    /// (<see cref="PlanState.NeedsAdjustment"/>, <c>LastAdaptationOn</c> = the log's
    /// day), so the two call sites cannot silently diverge.
    /// </summary>
    private static EscalationDecision Restructure(double newScore, int newStreak, DateOnly occurredOn) =>
        new(
            EscalationLevel.Restructure,
            AdaptationKind.Restructure,
            new AdaptationSignalState(PlanState.NeedsAdjustment, newScore, newStreak, occurredOn));

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Escalation trigger suppressed inside restructure dead-zone: would-be {SuppressedLevel} on {OccurredOn} (score {RollingDeviationScore}, missed-day streak {ConsecutiveMissedDays}, cooldown active: {CooldownActive}).")]
    private static partial void LogTriggerSuppressedInDeadZone(
        ILogger logger,
        EscalationLevel suppressedLevel,
        DateOnly occurredOn,
        double rollingDeviationScore,
        int consecutiveMissedDays,
        bool cooldownActive);

    /// <summary>
    /// A log under-performs when it was not completed (skipped or cut short), its pace
    /// was slower than the prescribed band, or it ran meaningfully short of the
    /// prescribed distance. Over-performance and on-target both fall through to decay.
    /// </summary>
    private static bool IsUnderPerformance(DeviationResult deviation) =>
        deviation.CompletionStatus != CompletionStatus.Complete
        || deviation.PaceBand == PaceBandMembership.SlowerThanSlow
        || deviation.DistanceDeviationPercent < -AdaptationThresholds.DistanceTolerancePercent;

    /// <summary>
    /// Emits the suppressed-trigger observability log (no event, no decision change)
    /// when a dead-zone absorb swallowed a signal that would otherwise have escalated:
    /// a score at or above an enter threshold, or a hard missed-day streak still
    /// inside the cooldown. Key-workout misses never reach here — they fire as L1.
    /// </summary>
    private void LogIfTriggerSuppressed(
        DeviationResult deviation, double newScore, int newStreak, bool cooldownActive)
    {
        var wouldRestructure = newScore >= AdaptationThresholds.RestructureEnterScore
            || newStreak >= AdaptationThresholds.ConsecutiveMissedDaysForRestructure;
        if (!wouldRestructure && newScore < AdaptationThresholds.MicroAdjustEnterScore)
        {
            return;
        }

        var suppressedLevel = wouldRestructure ? EscalationLevel.Restructure : EscalationLevel.MicroAdjust;
        LogTriggerSuppressedInDeadZone(
            _logger, suppressedLevel, deviation.OccurredOn, newScore, newStreak, cooldownActive);
    }
}
