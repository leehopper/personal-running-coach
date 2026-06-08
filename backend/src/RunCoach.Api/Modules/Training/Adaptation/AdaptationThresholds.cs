namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Named, first-pass threshold constants for the deterministic deviation/escalation
/// layer (Slice 3 PR2 / Unit 1, DEC-078). Centralised here so the escalation policy
/// lives in one place rather than scattered as call-site magic numbers — mirrors the
/// <c>SafetyKeywordCatalog</c> resource convention.
/// </summary>
/// <remarks>
/// These are deliberately simple first-pass values pending calibration against the
/// five <c>TestProfiles</c> during eval authoring (Unit 6); the full
/// EWMA/ACWR/CTL/ATL/TSB load model is deferred (DEC-078). Population-volume clamps
/// (DEC-010/DEC-028) are enforced at the LLM/validator boundary (PR4), not re-derived
/// here. Bump <see cref="PolicyVersion"/> on any change so audit replay can pin the
/// active set.
/// </remarks>
internal static class AdaptationThresholds
{
    /// <summary>Version stamp for this threshold set; bump on any change for audit-replay pinning.</summary>
    public const string PolicyVersion = "v1.0.0";

    /// <summary>
    /// Distance band tolerance (percent). Actuals within ±this of the prescribed
    /// distance are treated as on target, absorbing value-object precision. Duration is
    /// not checked directly against this: a slower-than-prescribed effort already
    /// surfaces through the pace band (sec/km), so a separate duration percentage would
    /// double-count it.
    /// </summary>
    public const double DistanceTolerancePercent = 5.0;

    /// <summary>The rolling deviation score added for each under-performing log.</summary>
    public const double MinorDeviationStep = 1.0;

    /// <summary>
    /// Multiplicative decay applied to the rolling deviation score on an on-target
    /// (or over-performing) log, so a recovered week walks the signal back down.
    /// </summary>
    public const double OnTargetDecayFactor = 0.5;

    /// <summary>
    /// Upper bound on the rolling deviation score, keeping the hysteresis dead-zone
    /// math bounded (a runaway streak cannot push the score arbitrarily high).
    /// </summary>
    public const double MaxRollingDeviationScore = RestructureEnterScore;

    /// <summary>
    /// Rolling deviation score at or above which accumulated minor deviations warrant
    /// an L1 micro-adjust (the "2–3 minor deviations" trigger).
    /// </summary>
    public const double MicroAdjustEnterScore = 2.0;

    /// <summary>
    /// Rolling deviation score at or above which sustained under-performance crosses
    /// the L1→L2 threshold into a restructure (enter threshold).
    /// </summary>
    public const double RestructureEnterScore = 3.0;

    /// <summary>
    /// Rolling deviation score at or below which the signal clears a restructure state
    /// back to on-track (the dead-zone guard holds while the score is strictly above
    /// this). Strictly less than <see cref="RestructureEnterScore"/> so enter/exit are
    /// asymmetric (the hysteresis dead-zone that prevents flip-flop).
    /// </summary>
    public const double RestructureExitScore = 1.0;

    /// <summary>Consecutive missed (skipped) days that force an L2 restructure regardless of score.</summary>
    public const int ConsecutiveMissedDaysForRestructure = 3;

    /// <summary>
    /// Cooldown window (days) after a restructure during which another restructure
    /// cannot fire — the time half of the asymmetric hysteresis.
    /// </summary>
    public const int RestructureCooldownDays = 7;

    /// <summary>
    /// First-pass cap (percent) on key-workout over-performance: over-performance
    /// never upgrades the escalation level here, and any future target increase the
    /// LLM proposes is bound by this (enforced at the PR4 validator, DEC-012 key rule).
    /// </summary>
    public const double OverPerformanceCapPercent = 3.0;
}
