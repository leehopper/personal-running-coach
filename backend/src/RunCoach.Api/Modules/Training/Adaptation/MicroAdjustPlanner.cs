using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// The deterministic L1 micro-adjust planner (Slice 3 PR2 / Unit 1): reschedules a
/// missed key workout <em>forward</em> within the current micro week (DEC-012 — never
/// redistribute backward), swapping it with the nearest later easy/recovery day such
/// that no two key workouts land on consecutive days. Pure — it computes the
/// before/after diff only; the PR5 orchestration reads the live micro week, calls this,
/// and appends the resulting <see cref="PlanAdaptationDiff"/>. When no valid
/// non-stacking forward swap exists, it returns <c>false</c> and the orchestration
/// escalates the micro-adjust to an L2 restructure.
/// </summary>
internal static class MicroAdjustPlanner
{
    /// <summary>
    /// Attempts to plan a forward swap for the missed key workout on
    /// <paramref name="missedDayOfWeek"/>.
    /// </summary>
    /// <param name="currentWeek">The current micro week's workouts (one per scheduled day).</param>
    /// <param name="missedDayOfWeek">The day-of-week (0=Sunday..6=Saturday) of the missed workout.</param>
    /// <param name="weekNumber">The 1-based week number the changes target.</param>
    /// <param name="diff">The resulting before/after diff when a swap is found; otherwise empty.</param>
    /// <returns><c>true</c> when a non-stacking forward swap was planned; otherwise <c>false</c>.</returns>
    public static bool TryPlanSwap(
        IReadOnlyList<WorkoutOutput> currentWeek,
        int missedDayOfWeek,
        int weekNumber,
        out PlanAdaptationDiff diff)
    {
        ArgumentNullException.ThrowIfNull(currentWeek);
        diff = PlanAdaptationDiff.Empty;

        var missed = currentWeek.FirstOrDefault(workout => workout.DayOfWeek == missedDayOfWeek);
        if (missed is null || !WorkoutKind.IsKey(missed.WorkoutType))
        {
            // Only a scheduled key workout is rescheduled by a deterministic micro-adjust.
            return false;
        }

        // Forward-only (DEC-012 — never redistribute backward): the nearest later
        // easy/recovery day whose swap does not place two key workouts on consecutive days.
        foreach (var target in currentWeek
            .Where(workout => workout.DayOfWeek > missedDayOfWeek && !WorkoutKind.IsKey(workout.WorkoutType))
            .OrderBy(workout => workout.DayOfWeek))
        {
            if (WouldStackKeyWorkouts(currentWeek, missed, target))
            {
                continue;
            }

            var movedKey = missed with { DayOfWeek = target.DayOfWeek };
            var movedEasy = target with { DayOfWeek = missed.DayOfWeek };
            diff = new PlanAdaptationDiff(
                [
                    new WorkoutChange(weekNumber, missed.DayOfWeek, missed, movedEasy),
                    new WorkoutChange(weekNumber, target.DayOfWeek, target, movedKey),
                ],
                []);
            return true;
        }

        return false;
    }

    /// <summary>
    /// After the swap the key workout sits on the target day and an easy workout on the
    /// missed day, so only the target day's neighbours can newly stack against the key.
    /// </summary>
    private static bool WouldStackKeyWorkouts(
        IReadOnlyList<WorkoutOutput> week,
        WorkoutOutput missed,
        WorkoutOutput target) =>
        NeighbourIsKey(week, missed, target.DayOfWeek - 1)
        || NeighbourIsKey(week, missed, target.DayOfWeek + 1);

    private static bool NeighbourIsKey(IReadOnlyList<WorkoutOutput> week, WorkoutOutput missed, int dayOfWeek)
    {
        // The missed day becomes an easy workout after the swap, so it never stacks.
        if (dayOfWeek == missed.DayOfWeek)
        {
            return false;
        }

        var neighbour = week.FirstOrDefault(workout => workout.DayOfWeek == dayOfWeek);
        return neighbour is not null && WorkoutKind.IsKey(neighbour.WorkoutType);
    }
}
