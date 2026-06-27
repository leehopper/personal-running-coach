using Marten;
using Microsoft.EntityFrameworkCore;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// Default <see cref="IConversationContextLoader"/> (Slice 4B PR4). Groups the read-side
/// data access the streaming orchestrator needs — the app-local date, the candidate
/// prescription resolution (delegated to the unchanged <see cref="IWorkoutLogService"/>),
/// and the composed answer context — behind one collaborator. Reads run on a per-request
/// tenanted Marten session keyed by the authenticated user id.
/// </summary>
public sealed class ConversationContextLoader(
    IWorkoutLogService workoutLogService,
    IWorkoutLogRepository workoutLogs,
    IDocumentStore documentStore,
    RunCoachDbContext db,
    ILocalDateProvider localDate) : IConversationContextLoader
{
    private const int RecentLogLimit = 5;
    private const int RecentTurnLimit = 10;

    /// <inheritdoc />
    public DateOnly Today() => localDate.Today();

    /// <inheritdoc />
    public Task<WorkoutPrescriptionSnapshot?> ResolveCandidatePrescriptionAsync(
        Guid userId, DateOnly occurredOn, CancellationToken ct) =>
        workoutLogService.ResolveCandidatePrescriptionAsync(userId, occurredOn, ct);

    /// <inheritdoc />
    public async Task<ConversationAnswerContext> LoadAnswerContextAsync(
        Guid userId, Guid clientMessageId, CancellationToken ct)
    {
        // SingleOrDefaultAsync fully qualified: both Marten's and EF Core's extension are
        // in scope. This is the EF query against the RunnerOnboardingProfile projection.
        var currentPlanId = await EntityFrameworkQueryableExtensions
            .SingleOrDefaultAsync(
                db.RunnerOnboardingProfiles
                    .AsNoTracking()
                    .Where(p => p.UserId == userId)
                    .Select(p => p.CurrentPlanId),
                ct)
            .ConfigureAwait(false);

        await using var session = documentStore.LightweightSession(userId.ToString());

        PlanProjectionDto? plan = currentPlanId is { } planId
            ? await session.LoadAsync<PlanProjectionDto>(planId, ct).ConfigureAwait(false)
            : null;

        var conversation = await session.LoadAsync<ConversationView>(userId, ct).ConfigureAwait(false);
        IReadOnlyList<ConversationContextTurn> recentTurns = (conversation?.Turns ?? [])
            .Where(t => !t.IsErrored && t.TurnId != clientMessageId)
            .TakeLast(RecentTurnLimit)
            .Select(t => new ConversationContextTurn(t.Participant, t.Content))
            .ToArray();

        var logEntities = await workoutLogs
            .GetByUserAsync(userId, cursor: null, limit: RecentLogLimit, ct)
            .ConfigureAwait(false);
        IReadOnlyList<LoggedWorkoutDetail> recentLogs = logEntities
            .Select(LoggedWorkoutDetailMapper.ToLoggedWorkoutDetail)
            .ToArray();

        return new ConversationAnswerContext(plan, recentLogs, recentTurns);
    }
}
