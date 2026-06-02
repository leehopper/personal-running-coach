using Microsoft.EntityFrameworkCore;
using RunCoach.Api.Infrastructure;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// EF Core implementation of <see cref="IWorkoutLogRepository"/> over
/// <see cref="RunCoachDbContext"/>. Sorting and keyset paging execute as SQL;
/// reads are no-tracking (a log is an immutable historical fact).
/// </summary>
public sealed partial class WorkoutLogRepository(
    RunCoachDbContext db,
    ILogger<WorkoutLogRepository> logger) : IWorkoutLogRepository
{
    private readonly RunCoachDbContext _db = db;
    private readonly ILogger<WorkoutLogRepository> _logger = logger;

    /// <inheritdoc />
    public async Task CreateAsync(WorkoutLog log, CancellationToken ct)
    {
        _db.WorkoutLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        LogCreated(_logger, log.WorkoutLogId, log.UserId);
    }

    /// <inheritdoc />
    public Task<WorkoutLog?> GetByIdAsync(Guid workoutLogId, CancellationToken ct) =>
        _db.WorkoutLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.WorkoutLogId == workoutLogId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkoutLog>> GetByUserAsync(
        Guid userId, WorkoutLogCursor? cursor, int limit, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var query = _db.WorkoutLogs.AsNoTracking().Where(w => w.UserId == userId);

        if (cursor is { } anchor)
        {
            // Strictly older than the anchor under OccurredOn DESC, Id DESC.
            query = query.Where(w =>
                w.OccurredOn < anchor.OccurredOn ||
                (w.OccurredOn == anchor.OccurredOn && w.WorkoutLogId < anchor.WorkoutLogId));
        }

        return await query
            .OrderByDescending(w => w.OccurredOn)
            .ThenByDescending(w => w.WorkoutLogId)
            .Take(limit)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkoutLog>> GetByPlannedWorkoutAsync(
        Guid sourcePlanId, int weekNumber, int dayOfWeek, CancellationToken ct)
    {
        // Comparing the complex-type coordinate columns naturally excludes
        // off-plan rows: their Prescription columns are NULL, so the equality is
        // never true (SQL three-valued logic).
        return await _db.WorkoutLogs
            .AsNoTracking()
            .Where(w =>
                w.Prescription!.SourcePlanId == sourcePlanId &&
                w.Prescription.WeekNumber == weekNumber &&
                w.Prescription.DayOfWeek == dayOfWeek)
            .ToListAsync(ct);
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Created WorkoutLog {WorkoutLogId} for user {UserId}.")]
    private static partial void LogCreated(ILogger logger, Guid workoutLogId, Guid userId);
}
