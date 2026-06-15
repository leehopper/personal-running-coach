namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// The outcome of the F4 restructure weekly-target ↔ resulting-workout-sum check
/// (slice 3B). <see cref="ProposedWeeklyTargetKm"/> and
/// <see cref="ResultingWorkoutSumKm"/> are <see langword="null"/> when the check did
/// not apply (no current-week target edit, or no materialized micro detail for the
/// current week); both are surfaced so the handler can log the contradiction.
/// </summary>
/// <param name="IsConsistent">
/// <see langword="true"/> when the check did not apply, or when the proposed
/// current-week target equals the resulting workout-distance sum exactly.
/// </param>
/// <param name="ProposedWeeklyTargetKm">The proposed current-week weekly target (km), or null when N/A.</param>
/// <param name="ResultingWorkoutSumKm">The summed resulting-week workout distance (km), or null when N/A.</param>
internal readonly record struct RestructureConsistencyResult(
    bool IsConsistent,
    int? ProposedWeeklyTargetKm,
    int? ResultingWorkoutSumKm)
{
    /// <summary>Gets the not-applicable result — the check did not apply to this proposal (treated as consistent).</summary>
    public static RestructureConsistencyResult NotApplicable { get; } = new(true, null, null);
}
