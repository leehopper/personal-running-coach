using System.Text.Json;
using Marten;
using Microsoft.EntityFrameworkCore;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Default <see cref="IWorkoutLogService"/>. Reads the runner's active plan id from
/// the EF <c>RunnerOnboardingProfile</c> (DEC-076 "load via CurrentPlanId"), loads
/// the tenant-scoped Marten <see cref="PlanProjectionDto"/>, maps the run's date to a
/// plan slot via <see cref="PlanCalendar"/>, snapshots the matched workout
/// server-side, and persists through the idempotent repository write (DEC-077).
/// Pure persistence: no LLM call sits on this path (DEC-073) — the synchronous
/// adaptation dispatch happens in <see cref="WorkoutLogsController.CreateLog"/>
/// AFTER <see cref="CreateAsync"/> has committed, so the relational write is
/// never inside the Wolverine handler's Marten transaction.
/// </summary>
public sealed partial class WorkoutLogService(
    RunCoachDbContext db,
    IWorkoutLogRepository repository,
    IDocumentStore documentStore,
    TimeProvider timeProvider,
    ILogger<WorkoutLogService> logger) : IWorkoutLogService
{
    /// <summary>Default page size applied when a query omits a limit.</summary>
    private const int DefaultPageSize = 20;

    /// <summary>Maximum page size; larger requested limits are clamped to this.</summary>
    private const int MaxPageSize = 100;

    private readonly RunCoachDbContext _db = db;
    private readonly IWorkoutLogRepository _repository = repository;
    private readonly IDocumentStore _documentStore = documentStore;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<WorkoutLogService> _logger = logger;

    /// <inheritdoc />
    public async Task<Guid> CreateAsync(Guid userId, CreateWorkoutLogRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prescription = await ResolvePrescriptionAsync(userId, request.OccurredOn, ct).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();

        var log = new WorkoutLog
        {
            WorkoutLogId = Guid.NewGuid(),
            UserId = userId,
            TenantId = userId.ToString(),
            IdempotencyKey = request.IdempotencyKey,
            OccurredOn = request.OccurredOn,
            Distance = Distance.FromMeters(request.DistanceMeters),
            Duration = Duration.FromSeconds(request.DurationSeconds),
            CompletionStatus = request.CompletionStatus,
            Notes = request.Notes,
            Metrics = SerializeMetrics(request.Metrics),
            Splits = MapSplits(request.Splits),
            Prescription = prescription,
            CreatedOn = now,
            ModifiedOn = now,
        };

        var workoutLogId = await _repository.CreateIdempotentAsync(log, ct).ConfigureAwait(false);
        LogWorkoutLogPersisted(_logger, workoutLogId, userId, prescription is not null);
        return workoutLogId;
    }

    /// <inheritdoc />
    public async Task<QueryWorkoutLogsResponseDto> QueryAsync(
        Guid userId, WorkoutLogCursor? cursor, int? requestedLimit, CancellationToken ct)
    {
        var limit = Math.Clamp(requestedLimit ?? DefaultPageSize, 1, MaxPageSize);

        var logs = await _repository.GetByUserAsync(userId, cursor, limit, ct).ConfigureAwait(false);

        // A full page means there may be more, so hand back the tail as the next
        // cursor; a short or empty page is the last one. Detecting "more" by row
        // count — rather than fetching limit+1 and trimming — keeps every returned
        // row exactly as the DB ordered and trimmed it (no app-layer slicing), at
        // the cost of one extra empty fetch when the total is an exact multiple of
        // the page size.
        var nextCursor = logs.Count == limit
            ? WorkoutLogCursorCodec.Encode(new WorkoutLogCursor(logs[^1].OccurredOn, logs[^1].WorkoutLogId))
            : null;

        var dtos = logs.Select(MapToDto).ToList();
        return new QueryWorkoutLogsResponseDto(dtos, nextCursor);
    }

    /// <inheritdoc />
    public Task<WorkoutPrescriptionSnapshot?> ResolveCandidatePrescriptionAsync(
        Guid userId, DateOnly occurredOn, CancellationToken ct) =>
        ResolvePrescriptionAsync(userId, occurredOn, ct);

    private static string? SerializeMetrics(IReadOnlyDictionary<string, JsonElement>? metrics) =>
        metrics is { Count: > 0 } ? JsonSerializer.Serialize(metrics) : null;

    private static List<WorkoutSplit>? MapSplits(IReadOnlyList<WorkoutLogSplitDto>? splits) =>
        splits is { Count: > 0 }
            ? splits
                .Select(s => new WorkoutSplit(
                    s.Index, s.DistanceMeters, s.DurationSeconds, s.PaceSecPerKm, s.AverageHeartRate))
                .ToList()
            : null;

    private static WorkoutLogDto MapToDto(WorkoutLog log) =>
        new(
            WorkoutLogId: log.WorkoutLogId,
            OccurredOn: log.OccurredOn,
            DistanceMeters: log.Distance.Meters,
            DurationSeconds: log.Duration.TotalSeconds,
            CompletionStatus: log.CompletionStatus,
            Notes: log.Notes,
            Metrics: DeserializeMetrics(log.Metrics),
            Splits: MapSplitsToDto(log.Splits));

    private static Dictionary<string, JsonElement>? DeserializeMetrics(string? metrics) =>
        string.IsNullOrEmpty(metrics)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metrics);

    private static List<WorkoutLogSplitDto>? MapSplitsToDto(IReadOnlyList<WorkoutSplit>? splits) =>
        splits is { Count: > 0 }
            ? splits
                .Select(s => new WorkoutLogSplitDto(
                    s.Index, s.DistanceMeters, s.DurationSeconds, s.PaceSecPerKm, s.AverageHeartRate))
                .ToList()
            : null;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Persisted workout log {WorkoutLogId} for user {UserId} (on-plan: {OnPlan}).")]
    private static partial void LogWorkoutLogPersisted(
        ILogger logger, Guid workoutLogId, Guid userId, bool onPlan);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "No active plan for user {UserId}; run on {OccurredOn} resolves off-plan.")]
    private static partial void LogOffPlanNoActivePlan(ILogger logger, Guid userId, DateOnly occurredOn);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Plan {PlanId} for user {UserId} has a malformed prescription; logging the run off-plan.")]
    private static partial void LogMalformedPlanOffPlan(
        ILogger logger, Guid userId, Guid planId, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Resolved prescription for user {UserId} from plan {PlanId} at week {WeekNumber} day {DayOfWeek}.")]
    private static partial void LogPrescriptionResolved(
        ILogger logger, Guid userId, Guid planId, int weekNumber, int dayOfWeek);

    private async Task<WorkoutPrescriptionSnapshot?> ResolvePrescriptionAsync(
        Guid userId, DateOnly occurredOn, CancellationToken ct)
    {
        // SingleOrDefaultAsync is fully qualified: both Marten's and EF Core's
        // extension are in scope here and would otherwise collide. This is an EF
        // query against the RunnerOnboardingProfile relational projection.
        var currentPlanQuery = _db.RunnerOnboardingProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.CurrentPlanId);
        var currentPlanId = await EntityFrameworkQueryableExtensions
            .SingleOrDefaultAsync(currentPlanQuery, ct)
            .ConfigureAwait(false);
        if (currentPlanId is not { } planId)
        {
            LogOffPlanNoActivePlan(_logger, userId, occurredOn);
            return null;
        }

        await using var session = _documentStore.LightweightSession(userId.ToString());
        var plan = await session.LoadAsync<PlanProjectionDto>(planId, ct).ConfigureAwait(false);
        if (plan?.Macro is null)
        {
            return null;
        }

        try
        {
            if (PlanCalendar.ResolveSlot(plan.PlanStartDate, occurredOn, plan.Macro.TotalWeeks) is not { } slot)
            {
                return null;
            }

            if (!plan.MicroWorkoutsByWeek.TryGetValue(slot.WeekNumber, out var micro))
            {
                return null;
            }

            var workout = micro.Workouts.FirstOrDefault(w => w.DayOfWeek == slot.DayOfWeek);
            if (workout is null)
            {
                return null;
            }

            // Server-authoritative: prescribed values come from the plan slot, never the
            // client. Fast = harder (lower sec/km) pace, Slow = easy pace (DEC-076).
            LogPrescriptionResolved(_logger, userId, plan.PlanId, slot.WeekNumber, slot.DayOfWeek);
            return WorkoutPrescriptionSnapshot.Create(
                sourcePlanId: plan.PlanId,
                weekNumber: slot.WeekNumber,
                dayOfWeek: slot.DayOfWeek,
                workoutType: workout.WorkoutType,
                prescribedDistance: Distance.FromKilometers(workout.TargetDistanceKm),
                prescribedDuration: Duration.FromMinutes(workout.TargetDurationMinutes),
                prescribedPaceFast: Pace.FromSecondsPerKm(workout.TargetPaceFastSecPerKm),
                prescribedPaceSlow: Pace.FromSecondsPerKm(workout.TargetPaceEasySecPerKm));
        }
        catch (ArgumentException ex)
        {
            // The plan slot was located, but its stored prescription is malformed:
            // a non-Sunday PlanStartDate (ResolveSlot), inverted pace bounds
            // (WorkoutPrescriptionSnapshot.Create), or a non-positive target pace
            // (Pace.FromSecondsPerKm) — all from unvalidated LLM plan output, i.e.
            // server-side data, never the client's request. Treat the run as
            // off-plan so a legitimate log still persists, and warn for
            // investigation. Crucially this keeps the fault off the controller's
            // client-input 400 path, which would otherwise return the wrong status
            // and leak internal plan state in ex.Message (DEC-076 / DEC-077).
            LogMalformedPlanOffPlan(_logger, userId, planId, ex);
            return null;
        }
    }
}
