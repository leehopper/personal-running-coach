using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Plan.Models;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// F4 (slice 3B): a validated L2 restructure must be internally consistent — when it
/// proposes a revised weekly target for the CURRENT week, that target must equal the
/// total distance of the week's RESULTING workouts (the existing micro week with the
/// sparse revisions applied, including the workouts the proposal leaves untouched).
/// </summary>
/// <remarks>
/// <para>
/// The live pass produced a restructure whose week-1 target (24 km) matched only its
/// three edited workouts while an untouched fourth workout pushed the real week to
/// 30 km. The pure <see cref="PlanAdaptationOutputValidator"/> cannot catch this — it
/// has no access to the projection's untouched days — so this is the projection-aware
/// companion the L2 handler runs as a post-validation gate. A mismatch is terminal
/// (DEC-080: nothing staged, <c>Kind=Error</c>), never a re-prompt.
/// </para>
/// <para>
/// The match is EXACT: weekly targets and workout distances are whole-km integers, so
/// a consistent restructure sums precisely with no tolerance band (DEC-083 ratifies
/// this over the spec's earlier tolerance-band wording). The check applies only when
/// the proposal revises the current week's target and that week carries materialized
/// micro detail; otherwise it is a no-op pass (a restructure that touches only upcoming
/// weeks has no current-week target to contradict, and a pre-existing meso/micro
/// mismatch the proposal does not touch is out of scope — deferred).
/// </para>
/// <para>
/// The resulting-week sum counts running distance only: <c>WeeklyTargetKm</c> is a
/// running-volume figure, so a carried-over cross-training day (which the resolver
/// preserves verbatim) is excluded from the sum, matching how the target is defined
/// and how the prompt frames it ("the exact arithmetic sum of those distances" over runs).
/// </para>
/// </remarks>
internal static class RestructureConsistencyCheck
{
    /// <summary>
    /// Evaluates the current-week weekly-target ↔ resulting-workout-sum consistency.
    /// </summary>
    /// <param name="proposal">The validated restructure proposal.</param>
    /// <param name="plan">The current plan projection the proposal is applied against.</param>
    /// <param name="currentWeekNumber">The 1-based week the triggering log belongs to.</param>
    /// <returns>The evaluation; <see cref="RestructureConsistencyResult.IsConsistent"/> drives the gate.</returns>
    public static RestructureConsistencyResult Evaluate(
        RestructurePlan proposal,
        PlanProjectionDto plan,
        int currentWeekNumber)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(plan);

        // Only the current week's PROPOSED target can be checked against summed
        // workouts; upcoming weeks carry no materialized micro detail. Collapse
        // duplicate current-week edits last-wins, mirroring the diff calculator.
        int? proposedTargetKm = null;
        foreach (var edit in proposal.RevisedWeeklyTargets.Where(edit => edit.WeekNumber == currentWeekNumber))
        {
            proposedTargetKm = edit.WeeklyTargetKm;
        }

        if (proposedTargetKm is null
            || !plan.MicroWorkoutsByWeek.TryGetValue(currentWeekNumber, out var currentWeek))
        {
            return RestructureConsistencyResult.NotApplicable;
        }

        var resultingSumKm = RestructureWorkoutResolver
            .ResolveResultingWorkouts(proposal.RevisedCurrentWeekWorkouts, currentWeek.Workouts)
            .Where(workout => workout.WorkoutType != WorkoutType.CrossTrain)
            .Sum(workout => workout.TargetDistanceKm);

        return new RestructureConsistencyResult(
            proposedTargetKm.Value == resultingSumKm,
            proposedTargetKm,
            resultingSumKm);
    }
}
