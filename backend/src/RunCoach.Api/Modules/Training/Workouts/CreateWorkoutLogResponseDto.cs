namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Response body for <c>POST /api/v1/workouts/logs</c> (slice-2b Unit 3). Carries
/// the newly-created log id so the frontend can correlate the pessimistic create
/// and invalidate its <c>WorkoutLog</c> RTK-query tag. Recorded on the
/// idempotency marker so a replayed key returns the byte-identical id without
/// creating a second row.
/// </summary>
/// <param name="WorkoutLogId">The id of the newly-created (or replayed) workout log.</param>
public sealed record CreateWorkoutLogResponseDto(Guid WorkoutLogId);
