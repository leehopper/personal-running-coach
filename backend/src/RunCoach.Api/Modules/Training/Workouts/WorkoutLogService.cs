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
/// </summary>
public sealed partial class WorkoutLogService(
    RunCoachDbContext db,
    IWorkoutLogRepository repository,
    IDocumentStore documentStore,
    TimeProvider timeProvider,
    ILogger<WorkoutLogService> logger) : IWorkoutLogService
{
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

    private static string? SerializeMetrics(IReadOnlyDictionary<string, JsonElement>? metrics) =>
        metrics is { Count: > 0 } ? JsonSerializer.Serialize(metrics) : null;

    private static List<WorkoutSplit>? MapSplits(IReadOnlyList<WorkoutLogSplitDto>? splits) =>
        splits is { Count: > 0 }
            ? splits
                .Select(s => new WorkoutSplit(
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
}
