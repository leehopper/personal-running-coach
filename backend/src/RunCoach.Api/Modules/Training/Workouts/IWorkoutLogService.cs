namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Create/orchestration for workout logs (slice-2b Unit 3). Resolves the
/// server-authoritative prescription snapshot from the runner's active plan
/// (DEC-076) and persists the log idempotently on the client idempotency key
/// (DEC-077). Pure persistence — no LLM call sits on this path (DEC-073).
/// </summary>
public interface IWorkoutLogService
{
    /// <summary>
    /// Builds a <see cref="WorkoutLog"/> from <paramref name="request"/> for
    /// <paramref name="userId"/> — resolving the prescription snapshot from the
    /// runner's active plan server-side (off-plan ⇒ null) — and persists it
    /// idempotently on the request's idempotency key. Returns the new (or, on a
    /// replayed key, the original) log id.
    /// </summary>
    /// <exception cref="ArgumentException">A request value fails a domain invariant
    /// (e.g. a negative distance/duration or an invalid split).</exception>
    Task<Guid> CreateAsync(Guid userId, CreateWorkoutLogRequestDto request, CancellationToken ct);

    /// <summary>
    /// Returns one newest-first keyset page of <paramref name="userId"/>'s logs
    /// (slice-2b Unit 4). <paramref name="cursor"/> is the prior page's tail (null
    /// for the first page); <paramref name="requestedLimit"/> is clamped
    /// server-side to a sane page-size bound. The response carries the DB-ordered,
    /// DB-trimmed page plus the opaque <c>nextCursor</c> for the next (older) page,
    /// or null when the page is the last one.
    /// </summary>
    Task<QueryWorkoutLogsResponseDto> QueryAsync(
        Guid userId, WorkoutLogCursor? cursor, int? requestedLimit, CancellationToken ct);

    /// <summary>
    /// Resolves the server-authoritative candidate prescription a run on
    /// <paramref name="occurredOn"/> would match for <paramref name="userId"/> — the
    /// same plan-slot resolution <see cref="CreateAsync"/> performs (DEC-076), exposed so
    /// the conversational-logging confirmation card (Slice 4B) can surface the candidate
    /// before any commit. Returns <c>null</c> for an off-plan / unscheduled run (no active
    /// plan, no matching slot, or a malformed stored prescription) — Confirm still commits
    /// an off-plan log. Read-only: writes nothing to the database.
    /// </summary>
    Task<WorkoutPrescriptionSnapshot?> ResolveCandidatePrescriptionAsync(
        Guid userId, DateOnly occurredOn, CancellationToken ct);
}
