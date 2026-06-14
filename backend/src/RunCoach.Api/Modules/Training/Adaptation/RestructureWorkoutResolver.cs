using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Shared sparse-edit merge semantics for a restructure's current-week workout
/// revisions, so the diff calculator (<see cref="RestructureDiffCalculator"/>) and
/// the F4 weekly-target consistency check (<see cref="RestructureConsistencyCheck"/>)
/// resolve the "resulting week" identically and can never drift (slice 3B F4).
/// </summary>
/// <remarks>
/// <see cref="RestructurePlan.RevisedCurrentWeekWorkouts"/> is a sparse per-day edit
/// set: a present day (0..6) is an upsert that replaces the existing day, an absent
/// day is left untouched, and a day outside 0..6 is dropped. Duplicate days collapse
/// last-wins, mirroring the projection's sequential upsert semantics.
/// </remarks>
internal static class RestructureWorkoutResolver
{
    /// <summary>
    /// Indexes the revised workouts by day-of-week, dropping days outside 0..6 and
    /// collapsing duplicate days last-wins.
    /// </summary>
    /// <param name="revisedWorkouts">The proposal's sparse per-day workout revisions.</param>
    /// <returns>The revised workouts keyed by their 0..6 day-of-week.</returns>
    public static Dictionary<int, WorkoutOutput> IndexRevisedByDay(WorkoutOutput[] revisedWorkouts)
    {
        ArgumentNullException.ThrowIfNull(revisedWorkouts);

        var revisedByDay = new Dictionary<int, WorkoutOutput>();
        foreach (var workout in revisedWorkouts.Where(workout => workout.DayOfWeek is >= 0 and <= 6))
        {
            revisedByDay[workout.DayOfWeek] = workout;
        }

        return revisedByDay;
    }

    /// <summary>
    /// Resolves the current week's workouts after applying the sparse revisions over
    /// the existing week: a revised day replaces the existing one, an existing day with
    /// no revision is carried over untouched, and a revised day the week never scheduled
    /// is added. This is the "resulting week" the F4 consistency check sums distances over.
    /// </summary>
    /// <param name="revisedWorkouts">The proposal's sparse per-day workout revisions.</param>
    /// <param name="existingWorkouts">The current micro week's existing workouts.</param>
    /// <returns>The resulting week's workouts after the upserts (order is unspecified).</returns>
    public static IReadOnlyList<WorkoutOutput> ResolveResultingWorkouts(
        WorkoutOutput[] revisedWorkouts,
        IReadOnlyList<WorkoutOutput> existingWorkouts)
    {
        ArgumentNullException.ThrowIfNull(revisedWorkouts);
        ArgumentNullException.ThrowIfNull(existingWorkouts);

        var revisedByDay = IndexRevisedByDay(revisedWorkouts);

        var resulting = new List<WorkoutOutput>(existingWorkouts.Count + revisedByDay.Count);

        // Existing days the revision does not touch carry over verbatim.
        resulting.AddRange(existingWorkouts.Where(workout => !revisedByDay.ContainsKey(workout.DayOfWeek)));

        // Revised days (upserts + additions) replace or extend the week.
        resulting.AddRange(revisedByDay.Values);

        return resulting;
    }
}
