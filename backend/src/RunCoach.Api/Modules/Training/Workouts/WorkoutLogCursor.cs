namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Keyset cursor for newest-first workout-log paging: the last seen
/// <c>(OccurredOn, WorkoutLogId)</c> pair. The next page is everything strictly
/// older than this anchor under the <c>OccurredOn DESC, WorkoutLogId DESC</c>
/// ordering, so there are no skips or duplicates across a page boundary.
/// </summary>
/// <param name="OccurredOn">The last seen run date.</param>
/// <param name="WorkoutLogId">The last seen log id (tiebreak within a date).</param>
public readonly record struct WorkoutLogCursor(DateOnly OccurredOn, Guid WorkoutLogId);
