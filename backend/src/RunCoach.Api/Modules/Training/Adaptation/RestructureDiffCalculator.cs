using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Plan.Models;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Maps a validated LLM <see cref="RestructurePlan"/> proposal against the current
/// <see cref="PlanProjectionDto"/> into the structured <see cref="PlanAdaptationDiff"/>
/// the <c>PlanAdaptedFromLog</c> event carries (Slice 3 PR5 / Unit 5, DEC-079): the
/// diff is computed deterministically from structured data, never parsed from prose.
/// Pure — the L2 orchestration calls this after validation and appends the result.
/// </summary>
/// <remarks>
/// <para>
/// The calculator works entirely in projection space (int-km/min
/// <see cref="WorkoutOutput"/> values, meso <c>WeeklyTargetKm</c> ints) — it never
/// converts back into the double-meters/ticks snapshot value objects, so no unit
/// mismatch can creep in.
/// </para>
/// <para>
/// It only ever emits changes the projection's <c>Apply(PlanAdaptedFromLog)</c> will
/// actually honor, keeping the persisted diff truthful:
/// <list type="bullet">
/// <item>Weekly-target edits for weeks absent from <see cref="PlanProjectionDto.MesoWeeks"/>,
///   or that leave the target unchanged, are dropped.</item>
/// <item>Workout edits apply to the CURRENT micro week only; when that week carries no
///   micro detail (only week 1 is materialized at MVP-0) every workout edit is dropped
///   rather than synthesizing a future week.</item>
/// <item>A revised day whose prescription (type/title/distance/duration/paces/effort)
///   equals the existing day is dropped — the F4 prompt has the LLM restate kept runs
///   to total the week, and a restated-but-unchanged day must not show as a no-op
///   <c>X km -&gt; X km</c> change.</item>
/// <item>No change ever carries a week number below 1 — the projection throws on a
///   non-1-based week and fails the whole transaction, so malformed LLM week indices
///   are dropped here instead. Day-of-week values outside 0..6 are dropped for the
///   same reason.</item>
/// <item>A removal (null <c>After</c>) is never produced — the revised workout list is
///   a sparse per-day edit set, so a day absent from it is left untouched.</item>
/// </list>
/// Duplicate entries targeting the same week or day are collapsed last-wins, mirroring
/// the projection's sequential upsert semantics.
/// </para>
/// </remarks>
internal static class RestructureDiffCalculator
{
    /// <summary>
    /// Computes the deterministic before/after diff for a validated restructure proposal.
    /// </summary>
    /// <param name="proposal">The validated LLM restructure proposal.</param>
    /// <param name="plan">The current plan projection the proposal is diffed against.</param>
    /// <param name="currentWeekNumber">The 1-based plan week the triggering log belongs to — the only week whose micro workouts may be edited.</param>
    /// <returns>The structured diff; possibly empty when nothing the proposal targets is applicable.</returns>
    public static PlanAdaptationDiff Calculate(
        RestructurePlan proposal,
        PlanProjectionDto plan,
        int currentWeekNumber)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(plan);

        return new PlanAdaptationDiff(
            CalculateWorkoutChanges(proposal.RevisedCurrentWeekWorkouts, plan, currentWeekNumber),
            CalculateWeeklyTargetChanges(proposal.RevisedWeeklyTargets, plan));
    }

    private static List<WeeklyTargetChange> CalculateWeeklyTargetChanges(
        WeeklyTargetEdit[] edits,
        PlanProjectionDto plan)
    {
        // Collapse duplicate week entries last-wins before diffing, and drop any
        // non-1-based week up front so the guard can never be violated downstream.
        var revisedByWeek = new Dictionary<int, int>();
        foreach (var edit in edits.Where(edit => edit.WeekNumber >= 1))
        {
            revisedByWeek[edit.WeekNumber] = edit.WeeklyTargetKm;
        }

        var changes = new List<WeeklyTargetChange>();
        foreach (var (weekNumber, afterTargetKm) in revisedByWeek.OrderBy(pair => pair.Key))
        {
            // Weeks the meso tier never emitted are skipped — the projection
            // would drop them anyway, and the diff must stay truthful.
            var before = plan.MesoWeeks.FirstOrDefault(week => week.WeekNumber == weekNumber);
            if (before is not null && before.WeeklyTargetKm != afterTargetKm)
            {
                changes.Add(new WeeklyTargetChange(weekNumber, before.WeeklyTargetKm, afterTargetKm));
            }
        }

        return changes;
    }

    private static List<WorkoutChange> CalculateWorkoutChanges(
        WorkoutOutput[] revisedWorkouts,
        PlanProjectionDto plan,
        int currentWeekNumber)
    {
        // Micro edits apply to the current week only, and only when it carries
        // live micro detail — the adaptation never synthesizes future micro weeks.
        if (currentWeekNumber < 1
            || !plan.MicroWorkoutsByWeek.TryGetValue(currentWeekNumber, out var currentWeek))
        {
            return [];
        }

        // Collapse duplicate day entries last-wins; drop days outside 0..6. Shared
        // with the F4 consistency check via RestructureWorkoutResolver so both resolve
        // the sparse per-day edit set identically.
        var revisedByDay = RestructureWorkoutResolver.IndexRevisedByDay(revisedWorkouts);

        var changes = new List<WorkoutChange>();
        foreach (var (dayOfWeek, after) in revisedByDay.OrderBy(pair => pair.Key))
        {
            // The revised list is a sparse per-day edit set: a present day is an
            // upsert (Before null when the day had no workout), an absent day is
            // untouched — so a removal (null After) is never emitted.
            var before = currentWeek.Workouts.FirstOrDefault(workout => workout.DayOfWeek == dayOfWeek);

            // The F4 prompt has the LLM restate EVERY current-week run, including the
            // ones it keeps unchanged, so it can total the week. A restated-but-unchanged
            // day must not surface as a "X km -> X km" no-op in the persisted diff — mirror
            // the weekly-target path's unchanged guard. Equality is on the runner-facing
            // prescription (type/title/distance/duration/paces/effort), not the regenerated
            // prose or segments, which the model rewrites freely even for a kept session.
            if (before is not null && RepresentsNoChange(before, after))
            {
                continue;
            }

            changes.Add(new WorkoutChange(currentWeekNumber, dayOfWeek, before, after));
        }

        return changes;
    }

    private static bool RepresentsNoChange(WorkoutOutput before, WorkoutOutput after) =>
        before.WorkoutType == after.WorkoutType
        && before.Title == after.Title
        && before.TargetDistanceKm == after.TargetDistanceKm
        && before.TargetDurationMinutes == after.TargetDurationMinutes
        && before.TargetPaceEasySecPerKm == after.TargetPaceEasySecPerKm
        && before.TargetPaceFastSecPerKm == after.TargetPaceFastSecPerKm
        && before.PerceivedEffort == after.PerceivedEffort;
}
