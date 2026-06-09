using FluentAssertions;
using Microsoft.Extensions.Logging;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Safety;
using RunCoach.Api.Modules.Training.Workouts;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Training.Adaptation;

/// <summary>
/// Unit tests for <see cref="EscalationClassifier"/> (Slice 3 PR2 / Unit 1): the
/// deterministic L0/L1/L2 resolution, the asymmetric enter/exit hysteresis, and the
/// locked dead-zone carve-outs (hard-trigger re-escalation, in-dead-zone key-miss L1,
/// suppressed-trigger observability logging).
/// </summary>
public sealed class EscalationClassifierTests
{
    private readonly CapturingLogger<EscalationClassifier> _logger = new();
    private readonly EscalationClassifier _sut;

    public EscalationClassifierTests()
    {
        _sut = new EscalationClassifier(_logger);
    }

    [Fact]
    public void Classify_WithinBandOnGreen_ResolvesToAbsorbWithNoPlanChange()
    {
        // Act
        var actual = _sut.Classify(OnTarget(), SafetyTier.Green, AdaptationSignalState.Initial);

        // Assert — Level 0: absorb, no downstream adaptation work.
        actual.EscalationLevel.Should().Be(EscalationLevel.Absorb);
        actual.AdaptationKind.Should().Be(AdaptationKind.Absorb);
        actual.NextState.PlanState.Should().Be(PlanState.OnTrack);
    }

    [Fact]
    public void Classify_SingleMissedKeyWorkoutOnGreen_ResolvesToMicroAdjust()
    {
        // Act
        var actual = _sut.Classify(MissedKey(), SafetyTier.Green, AdaptationSignalState.Initial);

        // Assert — Level 1: a single reschedulable missed key workout.
        actual.EscalationLevel.Should().Be(EscalationLevel.MicroAdjust);
        actual.AdaptationKind.Should().Be(AdaptationKind.Nudge);
    }

    [Fact]
    public void Classify_TwoMinorDeviationsOnGreen_ResolvesToMicroAdjust()
    {
        // Arrange / Act — one minor deviation absorbs; the second reaches the micro-adjust threshold.
        var afterFirst = _sut.Classify(MinorUnder(Day(1)), SafetyTier.Green, AdaptationSignalState.Initial);
        var afterSecond = _sut.Classify(MinorUnder(Day(2)), SafetyTier.Green, afterFirst.NextState);

        // Assert
        afterFirst.EscalationLevel.Should().Be(
            EscalationLevel.Absorb, because: "a single minor band deviation is absorbed");
        afterSecond.EscalationLevel.Should().Be(EscalationLevel.MicroAdjust);
        afterSecond.AdaptationKind.Should().Be(AdaptationKind.Nudge);
    }

    [Fact]
    public void Classify_SustainedUnderPerformanceCrossingEnterThreshold_ResolvesToRestructure()
    {
        // Arrange / Act — three sustained minor deviations cross the L1→L2 enter threshold.
        var d1 = _sut.Classify(MinorUnder(Day(1)), SafetyTier.Green, AdaptationSignalState.Initial);
        var d2 = _sut.Classify(MinorUnder(Day(2)), SafetyTier.Green, d1.NextState);
        var d3 = _sut.Classify(MinorUnder(Day(3)), SafetyTier.Green, d2.NextState);

        // Assert
        d3.EscalationLevel.Should().Be(EscalationLevel.Restructure);
        d3.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        d3.NextState.PlanState.Should().Be(PlanState.NeedsAdjustment);
    }

    [Fact]
    public void Classify_ThreeConsecutiveMissedDays_ResolvesToRestructure()
    {
        // Arrange / Act — three consecutive skipped days.
        var d1 = _sut.Classify(MissedEasy(Day(1)), SafetyTier.Green, AdaptationSignalState.Initial);
        var d2 = _sut.Classify(MissedEasy(Day(2)), SafetyTier.Green, d1.NextState);
        var d3 = _sut.Classify(MissedEasy(Day(3)), SafetyTier.Green, d2.NextState);

        // Assert
        d3.NextState.ConsecutiveMissedDays.Should().Be(3);
        d3.EscalationLevel.Should().Be(EscalationLevel.Restructure);
    }

    [Fact]
    public void Classify_OverPerformanceOnGreen_DoesNotUpgradeEscalation()
    {
        // Act
        var actual = _sut.Classify(OverPerformance(), SafetyTier.Green, AdaptationSignalState.Initial);

        // Assert — over-performance never upgrades the plan (DEC-012).
        actual.EscalationLevel.Should().Be(EscalationLevel.Absorb);
    }

    [Fact]
    public void Classify_LargeOverPerformance_StillAbsorbsBoundedByCap()
    {
        // Arrange — an over-performance well beyond the over-performance cap.
        var huge = Dev(
            isKey: true,
            distancePct: 25.0,
            durationPct: -15.0,
            paceBand: PaceBandMembership.FasterThanFast,
            paceDev: -45.0);

        // Act
        var actual = _sut.Classify(huge, SafetyTier.Green, AdaptationSignalState.Initial);

        // Assert — still bounded: no escalation from over-performance.
        actual.EscalationLevel.Should().Be(EscalationLevel.Absorb);
    }

    [Fact]
    public void Classify_SecondLogInsideHysteresisDeadZone_DoesNotRefireRestructure()
    {
        // Arrange — a restructure has just fired: NeedsAdjustment, score at the cap, fired Day 1.
        var afterRestructure = new AdaptationSignalState(
            PlanState.NeedsAdjustment, AdaptationThresholds.MaxRollingDeviationScore, 0, Day(1));

        // Act — a second on-target log two days later, still inside the cooldown.
        var actual = _sut.Classify(OnTarget(Day(3)), SafetyTier.Green, afterRestructure);

        // Assert — no re-fire; the dead-zone holds NeedsAdjustment.
        actual.EscalationLevel.Should().Be(EscalationLevel.Absorb);
        actual.NextState.PlanState.Should().Be(PlanState.NeedsAdjustment);
    }

    [Fact]
    public void Classify_SignalFallsButStaysAboveExitThreshold_HoldsNeedsAdjustmentNoRefire()
    {
        // Arrange — past the cooldown, but the rolling signal still sits above the exit threshold.
        var afterRestructure = new AdaptationSignalState(PlanState.NeedsAdjustment, 1.5, 0, Day(1));

        // Act — a fresh under-performance long after the cooldown elapsed.
        var actual = _sut.Classify(MinorUnder(Day(20)), SafetyTier.Green, afterRestructure);

        // Assert — asymmetric hysteresis: the state does not return to on-track and no restructure re-fires.
        actual.NextState.PlanState.Should().Be(
            PlanState.NeedsAdjustment, because: "the signal has not cleared the exit threshold");
        actual.EscalationLevel.Should().NotBe(
            EscalationLevel.Restructure, because: "a restructure cannot re-fire inside the dead-zone");
    }

    [Fact]
    public void Classify_CooldownElapsedAndSignalBelowExit_ClearsToOnTrack()
    {
        // Arrange — past the cooldown and the signal will decay below the exit threshold.
        var afterRestructure = new AdaptationSignalState(PlanState.NeedsAdjustment, 1.5, 0, Day(1));

        // Act — an on-target log long after the cooldown drops the score below exit (1.5 * 0.5 = 0.75).
        var actual = _sut.Classify(OnTarget(Day(20)), SafetyTier.Green, afterRestructure);

        // Assert
        actual.NextState.PlanState.Should().Be(PlanState.OnTrack);
        actual.EscalationLevel.Should().Be(EscalationLevel.Absorb);
    }

    [Fact]
    public void Classify_HardTriggerInDeadZonePostCooldown_ReEscalatesToRestructure()
    {
        // Arrange — in the dead-zone: restructure fired Day 1, score above exit but
        // below enter (1.2 + 1.0 step = 2.2), two missed days already on the streak.
        var inDeadZone = new AdaptationSignalState(PlanState.NeedsAdjustment, 1.2, 2, Day(1));

        // Act — a third consecutive missed day lands after the 7-day cooldown elapsed.
        var actual = _sut.Classify(MissedEasy(Day(10)), SafetyTier.Green, inDeadZone);

        // Assert — the hard trigger punches through: a genuinely deteriorating runner
        // must not be silently absorbed even though the score has not cleared exit.
        actual.EscalationLevel.Should().Be(EscalationLevel.Restructure);
        actual.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        actual.NextState.PlanState.Should().Be(PlanState.NeedsAdjustment);
        actual.NextState.ConsecutiveMissedDays.Should().Be(3);
        actual.NextState.LastAdaptationOn.Should().Be(
            Day(10), because: "the re-fired restructure restarts the cooldown window");
    }

    [Fact]
    public void Classify_HardTriggerInDeadZoneDuringCooldown_AbsorbsAndLogsSuppression()
    {
        // Arrange — same deteriorating streak, but the restructure fired only two days ago.
        var inDeadZone = new AdaptationSignalState(PlanState.NeedsAdjustment, 1.2, 2, Day(1));

        // Act — the third consecutive missed day lands inside the cooldown.
        var actual = _sut.Classify(MissedEasy(Day(3)), SafetyTier.Green, inDeadZone);

        // Assert — absorbed (cooldown holds), but the suppression is observable in the log.
        actual.EscalationLevel.Should().Be(EscalationLevel.Absorb);
        actual.NextState.PlanState.Should().Be(PlanState.NeedsAdjustment);
        actual.NextState.ConsecutiveMissedDays.Should().Be(3);
        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain("suppressed").And.Contain(nameof(EscalationLevel.Restructure));
    }

    [Fact]
    public void Classify_MissedKeyWorkoutInsideDeadZone_FiresMicroAdjust()
    {
        // Arrange — deep inside the dead-zone: restructure fired Day 1, cooldown active.
        var inDeadZone = new AdaptationSignalState(PlanState.NeedsAdjustment, 2.0, 0, Day(1));

        // Act — a key workout is missed two days later.
        var actual = _sut.Classify(MissedKey(Day(3)), SafetyTier.Green, inDeadZone);

        // Assert — the anti-flip-flop guarantee is L2-only: the deterministic L1
        // reschedule still fires, while the dead-zone state holds for L2 purposes.
        actual.EscalationLevel.Should().Be(EscalationLevel.MicroAdjust);
        actual.AdaptationKind.Should().Be(AdaptationKind.Nudge);
        actual.NextState.PlanState.Should().Be(
            PlanState.NeedsAdjustment, because: "an L1 nudge must not release the L2 hysteresis dead-zone");
        actual.NextState.LastAdaptationOn.Should().Be(
            Day(1), because: "only a restructure restarts the cooldown window");
    }

    [Fact]
    public void Classify_ScoreTriggerSuppressedInDeadZone_EmitsObservabilityLog()
    {
        // Arrange — score pinned at the cap, restructure fired Day 1 (cooldown active).
        var inDeadZone = new AdaptationSignalState(
            PlanState.NeedsAdjustment, AdaptationThresholds.MaxRollingDeviationScore, 0, Day(1));

        // Act — a fresh under-performance keeps the score at the restructure-enter level.
        var actual = _sut.Classify(MinorUnder(Day(3)), SafetyTier.Green, inDeadZone);

        // Assert — absorbed (no event), with a single source-generated suppression log.
        actual.EscalationLevel.Should().Be(EscalationLevel.Absorb);
        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain("suppressed").And.Contain(nameof(EscalationLevel.Restructure));
    }

    [Fact]
    public void Classify_DeadZoneAbsorbWithoutWouldBeTrigger_EmitsNoSuppressionLog()
    {
        // Arrange — in the dead-zone with the score at the cap.
        var inDeadZone = new AdaptationSignalState(
            PlanState.NeedsAdjustment, AdaptationThresholds.MaxRollingDeviationScore, 0, Day(1));

        // Act — an on-target log decays the score below every enter threshold (3.0 → 1.5).
        var actual = _sut.Classify(OnTarget(Day(3)), SafetyTier.Green, inDeadZone);

        // Assert — plain dead-zone absorb: nothing was suppressed, so nothing is logged.
        actual.EscalationLevel.Should().Be(EscalationLevel.Absorb);
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Classify_DeviationWithinTolerance_TreatedAsOnTargetAbsorb()
    {
        // Arrange — sub-tolerance distance/duration drift with the pace inside the band.
        var withinTolerance = Dev(distancePct: 2.0, durationPct: -3.0);

        // Act
        var actual = _sut.Classify(withinTolerance, SafetyTier.Green, AdaptationSignalState.Initial);

        // Assert — percentage-based tolerance treats this as on target.
        actual.EscalationLevel.Should().Be(EscalationLevel.Absorb);
        actual.NextState.PlanState.Should().Be(PlanState.OnTrack);
    }

    [Fact]
    public void Classify_RedSafetyTier_ResolvesToAbsorbWithNoPlanChange()
    {
        // Act — Red short-circuits to safety-only in PR5; the classifier yields no plan adaptation.
        var actual = _sut.Classify(MissedKey(), SafetyTier.Red, AdaptationSignalState.Initial);

        // Assert
        actual.EscalationLevel.Should().Be(EscalationLevel.Absorb);
        actual.AdaptationKind.Should().Be(AdaptationKind.Absorb);
    }

    [Fact]
    public void Classify_AmberSafetyTier_ResolvesLevelLikeGreen()
    {
        // Act
        var actual = _sut.Classify(MissedKey(), SafetyTier.Amber, AdaptationSignalState.Initial);

        // Assert — Amber does not change the level; the non-increase clamp is PR4's post-LLM validator.
        actual.EscalationLevel.Should().Be(EscalationLevel.MicroAdjust);
    }

    [Fact]
    public void Classify_NonSkippedLog_ResetsConsecutiveMissedStreak()
    {
        // Arrange / Act
        var afterMiss = _sut.Classify(MissedEasy(Day(1)), SafetyTier.Green, AdaptationSignalState.Initial);
        var afterComplete = _sut.Classify(OnTarget(Day(2)), SafetyTier.Green, afterMiss.NextState);

        // Assert
        afterMiss.NextState.ConsecutiveMissedDays.Should().Be(1);
        afterComplete.NextState.ConsecutiveMissedDays.Should().Be(
            0, because: "a non-skipped log resets the missed-day streak");
    }

    [Fact]
    public void Classify_PartialLogInBand_UnderPerformsButDoesNotCountAsMissedDay()
    {
        // Arrange — a Partial (cut-short) log with in-band pace and on-target distance:
        // the not-Complete clause of IsUnderPerformance alone drives under-performance,
        // but a Partial is not a Skipped (missed) day.
        var partial = Dev(status: CompletionStatus.Partial);

        // Act
        var actual = _sut.Classify(partial, SafetyTier.Green, AdaptationSignalState.Initial);

        // Assert — scores like a Skipped log, but never advances the consecutive-missed streak.
        actual.NextState.RollingDeviationScore.Should().BeApproximately(
            AdaptationThresholds.MinorDeviationStep,
            1e-6,
            because: "a Partial log under-performs via the not-Complete clause, like a Skipped one");
        actual.NextState.ConsecutiveMissedDays.Should().Be(
            0, because: "only a Skipped log counts toward the consecutive-missed-day streak");
        actual.EscalationLevel.Should().Be(EscalationLevel.Absorb);
    }

    [Fact]
    public void Classify_TwoPartialLogs_AccumulateToMicroAdjust()
    {
        // Arrange / Act — two Partial logs accumulate the score exactly like minor deviations,
        // reaching the micro-adjust threshold without ever touching the missed-day streak.
        var afterFirst = _sut.Classify(
            Dev(Day(1), status: CompletionStatus.Partial), SafetyTier.Green, AdaptationSignalState.Initial);
        var afterSecond = _sut.Classify(
            Dev(Day(2), status: CompletionStatus.Partial), SafetyTier.Green, afterFirst.NextState);

        // Assert
        afterFirst.EscalationLevel.Should().Be(
            EscalationLevel.Absorb, because: "a single Partial log is absorbed below the micro-adjust threshold");
        afterSecond.EscalationLevel.Should().Be(EscalationLevel.MicroAdjust);
        afterSecond.NextState.ConsecutiveMissedDays.Should().Be(
            0, because: "Partial logs never accrue a consecutive-missed-day streak");
    }

    [Theory]
    [InlineData(-6.0, 1.0, PlanState.MinorDeviation)] // just beyond the -5% tolerance: under-performs
    [InlineData(-4.0, 0.0, PlanState.OnTrack)] // just inside the -5% tolerance: absorbed as on target
    public void Classify_DistanceShortfallAloneAtToleranceBoundary_DrivesUnderPerformance(
        double distancePct, double expectedScore, PlanState expectedPlanState)
    {
        // Arrange — a Complete, in-band log whose only deviation is running short of the prescribed
        // distance, isolating the distance clause of IsUnderPerformance at the ±tolerance boundary.
        var log = Dev(distancePct: distancePct);

        // Act
        var actual = _sut.Classify(log, SafetyTier.Green, AdaptationSignalState.Initial);

        // Assert — a single deviation never reaches micro-adjust; the score and plan-state pin the boundary.
        actual.EscalationLevel.Should().Be(EscalationLevel.Absorb);
        actual.NextState.RollingDeviationScore.Should().BeApproximately(expectedScore, 1e-6);
        actual.NextState.PlanState.Should().Be(expectedPlanState);
    }

    [Fact]
    public void Classify_NullDeviation_Throws()
    {
        // Act
        var act = () => _sut.Classify(null!, SafetyTier.Green, AdaptationSignalState.Initial);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static DateOnly Day(int day) => new(2026, 6, day);

    /// <summary>Builds a <see cref="DeviationResult"/>; an on-target Complete log by default.</summary>
    private static DeviationResult Dev(
        DateOnly? on = null,
        CompletionStatus status = CompletionStatus.Complete,
        bool isKey = false,
        double distancePct = 0.0,
        double durationPct = 0.0,
        PaceBandMembership paceBand = PaceBandMembership.InsideBand,
        double paceDev = 0.0) =>
        new(on ?? Day(1), status, isKey, distancePct, durationPct, paceBand, paceDev);

    private static DeviationResult OnTarget(DateOnly? on = null) => Dev(on);

    private static DeviationResult MinorUnder(DateOnly? on = null) =>
        Dev(
            on,
            durationPct: 12.0,
            paceBand: PaceBandMembership.SlowerThanSlow,
            paceDev: 15.0);

    private static DeviationResult MissedKey(DateOnly? on = null) =>
        Dev(
            on,
            status: CompletionStatus.Skipped,
            isKey: true,
            distancePct: -100.0,
            durationPct: -100.0,
            paceBand: PaceBandMembership.Unknown);

    private static DeviationResult MissedEasy(DateOnly? on = null) =>
        Dev(
            on,
            status: CompletionStatus.Skipped,
            distancePct: -100.0,
            durationPct: -100.0,
            paceBand: PaceBandMembership.Unknown);

    private static DeviationResult OverPerformance(DateOnly? on = null) =>
        Dev(
            on,
            isKey: true,
            distancePct: 8.0,
            durationPct: -5.0,
            paceBand: PaceBandMembership.FasterThanFast,
            paceDev: -20.0);
}
