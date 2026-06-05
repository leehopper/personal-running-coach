namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Data access for <see cref="WorkoutLog"/> rows (slice-2b Unit 2). Pure
/// persistence — no endpoints, no LLM. Reads are user-scoped; the prescription
/// coordinate query backs Slice 3's prescribed-vs-actual lookup.
/// </summary>
public interface IWorkoutLogRepository
{
    /// <summary>
    /// Persists a new log. Calls <c>SaveChangesAsync</c> directly, so it is safe
    /// for a controller/service caller but MUST NOT be called from inside a
    /// Wolverine handler body — that would commit the EF write in a separate
    /// transaction from the handler's Marten session and break dual-write
    /// atomicity (DEC-060). A future plan-adaptation handler (Slice 3) should
    /// stage the row via the projection pattern, not this method.
    /// </summary>
    Task CreateAsync(WorkoutLog log, CancellationToken ct);

    /// <summary>
    /// Loads a single log owned by <paramref name="userId"/>, or null if absent
    /// or owned by another user. User-scoped at the repository boundary so a row
    /// can never leak across users even if a caller-side ownership check drifts.
    /// </summary>
    Task<WorkoutLog?> GetByIdAsync(Guid userId, Guid workoutLogId, CancellationToken ct);

    /// <summary>
    /// Returns a user's logs newest-first (by <c>OccurredOn</c> then id) as a
    /// keyset page. Pass the last page's tail as <paramref name="cursor"/> to get
    /// the next page; pass null for the first page. <paramref name="limit"/> must
    /// be positive (the caller is responsible for clamping an upper page-size
    /// bound on any client-supplied value).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    Task<IReadOnlyList<WorkoutLog>> GetByUserAsync(
        Guid userId, WorkoutLogCursor? cursor, int limit, CancellationToken ct);

    /// <summary>
    /// Returns <paramref name="userId"/>'s logs whose prescription snapshot matches
    /// the given plan-slot coordinate. Other users' logs and off-plan logs (null
    /// prescription) never match (DEC-076).
    /// </summary>
    Task<IReadOnlyList<WorkoutLog>> GetByPlannedWorkoutAsync(
        Guid userId, Guid sourcePlanId, int weekNumber, int dayOfWeek, CancellationToken ct);
}
