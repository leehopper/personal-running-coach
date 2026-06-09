using RunCoach.Api.Modules.Coaching.Adaptation;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Response body for <c>POST /api/v1/workouts/logs</c> (slice-2b Unit 3, extended by
/// Slice 3 § Unit 5). Carries the newly-created log id so the frontend can correlate
/// the pessimistic create and invalidate its <c>WorkoutLog</c> RTK-query tag, plus the
/// flat DEC-073 adaptation envelope produced by the synchronous post-commit
/// evaluation. On a replayed idempotency key the prior id is re-read straight from
/// the <c>WorkoutLog</c> row via its unique <c>(UserId, IdempotencyKey)</c> index
/// (DEC-077) — there is no separate idempotency marker on this path.
/// </summary>
/// <param name="WorkoutLogId">The id of the newly-created (or replayed) workout log.</param>
/// <param name="Adaptation">
/// The adaptation envelope: <c>Kind=Adapted</c> with the resolved plan-change kind, or
/// <c>Kind=Error</c> when the evaluation terminally failed. The envelope rides the 201
/// body in every case — an adaptation failure never fails the create, because the log
/// row has already committed by the time the evaluation runs.
/// </param>
public sealed record CreateWorkoutLogResponseDto(Guid WorkoutLogId, AdaptationResponseDto Adaptation);
