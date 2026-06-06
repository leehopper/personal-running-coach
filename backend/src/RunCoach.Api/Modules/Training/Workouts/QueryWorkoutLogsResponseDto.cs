namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Response for <c>POST /api/v1/workouts/logs/query</c>: one newest-first page of
/// the runner's logs plus the opaque <paramref name="NextCursor"/> for the next
/// (older) page. <paramref name="NextCursor"/> is null when the page is the last
/// one — i.e. fewer rows than the requested page size were available.
/// </summary>
/// <param name="Logs">The page of logs, newest-first (<c>OccurredOn</c> desc, id tiebreak).</param>
/// <param name="NextCursor">Opaque cursor to fetch the next older page, or null when there are no more.</param>
public sealed record QueryWorkoutLogsResponseDto(
    IReadOnlyList<WorkoutLogDto> Logs,
    string? NextCursor);
