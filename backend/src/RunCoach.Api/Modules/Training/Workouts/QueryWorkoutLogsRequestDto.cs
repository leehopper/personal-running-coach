namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Request body for <c>POST /api/v1/workouts/logs/query</c> (slice-2b Unit 4).
/// Keyset paging only at MVP-0: an optional opaque <paramref name="Cursor"/> (a
/// prior page's <c>nextCursor</c>; null/absent starts at the most recent log) and
/// an optional <paramref name="Limit"/> page size (clamped server-side). Ordering
/// is fixed newest-first (<c>OccurredOn</c> desc, id tiebreak). A filter block is
/// intentionally deferred — the record grows additively when filter UI arrives, so
/// adding it later is not a wire break.
/// </summary>
/// <param name="Limit">Requested page size; null applies the server default. Clamped to the server's [1, max] bound.</param>
/// <param name="Cursor">Opaque keyset cursor from a prior page's <c>nextCursor</c>; null/empty starts at the newest log.</param>
public sealed record QueryWorkoutLogsRequestDto(int? Limit, string? Cursor);
