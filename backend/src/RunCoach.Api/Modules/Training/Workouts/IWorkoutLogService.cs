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
}
