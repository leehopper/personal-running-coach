using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    public async Task<Guid> CreateIdempotentAsync(WorkoutLog log, CancellationToken ct)
    {
        _db.WorkoutLogs.Add(log);
        try
        {
            await _db.SaveChangesAsync(ct);
            LogCreated(_logger, log.WorkoutLogId, log.UserId);
            return log.WorkoutLogId;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: WorkoutLogConfiguration.IdempotencyIndexName,
            })
        {
            // Replay: this (user, idempotency key) already produced a log. The failed
            // SaveChanges auto-rolled-back its own implicit transaction, so the
            // context's connection is clean for a fresh read; AsNoTracking bypasses
            // the lingering Added entity (DEC-077 / R-081).
            var existingId = await _db.WorkoutLogs
                .AsNoTracking()
                .Where(w => w.UserId == log.UserId && w.IdempotencyKey == log.IdempotencyKey)
                .Select(w => w.WorkoutLogId)
                .SingleAsync(ct);
            LogIdempotentReplay(_logger, existingId, log.UserId);
            return existingId;
        }
    }

    /// <inheritdoc />
    public Task<WorkoutLog?> GetByIdAsync(Guid userId, Guid workoutLogId, CancellationToken ct) =>
        _db.WorkoutLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId && w.WorkoutLogId == workoutLogId, ct);

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
        Guid userId, Guid sourcePlanId, int weekNumber, int dayOfWeek, CancellationToken ct)
    {
        // User-scoped first as defense-in-depth (a plan id is already per-user, but
        // the repository must not depend on that). Comparing the complex-type
        // coordinate columns naturally excludes off-plan rows: their Prescription
        // columns are NULL, so the equality is never true (SQL three-valued logic).
        return await _db.WorkoutLogs
            .AsNoTracking()
            .Where(w =>
                w.UserId == userId &&
                w.Prescription!.SourcePlanId == sourcePlanId &&
                w.Prescription.WeekNumber == weekNumber &&
                w.Prescription.DayOfWeek == dayOfWeek)
            .ToListAsync(ct);
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Created WorkoutLog {WorkoutLogId} for user {UserId}.")]
    private static partial void LogCreated(ILogger logger, Guid workoutLogId, Guid userId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Idempotent replay returned existing WorkoutLog {WorkoutLogId} for user {UserId}.")]
    private static partial void LogIdempotentReplay(ILogger logger, Guid workoutLogId, Guid userId);
}
