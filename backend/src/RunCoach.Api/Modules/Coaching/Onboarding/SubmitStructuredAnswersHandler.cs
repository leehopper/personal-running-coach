using System.Text.Json;
using Marten;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Wolverine static command handler for <see cref="SubmitStructuredAnswers"/> — the deterministic,
/// form-first onboarding intake (DEC-086 D1 / DP-2). Like <see cref="OnboardingTurnHandler"/> it is
/// dispatched via Wolverine's plain static-handler convention (no <c>[AggregateHandler]</c>
/// attribute): it injects <see cref="IDocumentSession"/> directly, loads
/// <see cref="OnboardingView"/> via <c>session.LoadAsync</c>, and stages every event on that one
/// session, which Wolverine's transactional middleware commits atomically — no
/// <c>SaveChangesAsync</c> in the handler body.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the turn handler there is <b>no LLM call</b>. The already-validated answer records are
/// appended straight to the stream as whole-record <see cref="AnswerCaptured"/> events
/// (<c>Confidence = 1.0</c>), exactly as the deterministic <c>ReviseAnswer</c> escape hatch does.
/// The completion gate is the sole plan-generation authority (there is no LLM <c>ReadyForPlan</c>
/// signal to AND against).
/// </para>
/// <para>
/// Flow: (1) idempotency short-circuit; (2) bootstrap the stream via
/// <see cref="MartenEventStreamExtensions.StartStreamOrAppendAsync{TAggregate}"/> keyed on physical
/// stream existence (onboarding and conversation share one per-user stream — a raw
/// <c>StartStream</c> would collide); (3) append one <see cref="AnswerCaptured"/> per submitted
/// topic and mirror each onto a working view; (4) if
/// <see cref="OnboardingCompletionGate.IsSatisfied(OnboardingView)"/>, run the existing inline
/// plan-generation terminal branch and append <see cref="PlanLinkedToUser"/> +
/// <see cref="OnboardingCompleted"/>; (5) record the idempotency marker last. Any failure (e.g. a
/// rejected generated plan) propagates so Wolverine aborts the transaction — nothing staged, the
/// form is re-submittable.
/// </para>
/// </remarks>
public sealed partial class SubmitStructuredAnswersHandler
{
    // The type is a stateless logical handler container: Wolverine codegen emits a non-static
    // handler stub that calls the static Handle method, and `ILogger<SubmitStructuredAnswersHandler>`
    // needs a non-static type argument. The private constructor prevents instantiation in test code.
    private SubmitStructuredAnswersHandler()
    {
    }

    /// <summary>
    /// Wolverine command handler. Stages every event + idempotency marker on <paramref name="session"/>;
    /// Wolverine's transactional middleware commits them atomically after the handler returns.
    /// </summary>
    /// <param name="cmd">The submit-structured-answers command (validated by the controller).</param>
    /// <param name="session">Marten session bracketed by Wolverine's transactional middleware.</param>
    /// <param name="idempotency">Idempotency store backed by the same <paramref name="session"/>.</param>
    /// <param name="planGen">Plan generation orchestrator (six-call macro/meso/micro chain).</param>
    /// <param name="time">Time provider for event timestamps.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler's terminal decision (whether a plan was generated + its id), memoized for idempotent replay.</returns>
    public static async Task<SubmitStructuredAnswersResult> Handle(
        SubmitStructuredAnswers cmd,
        IDocumentSession session,
        IIdempotencyStore idempotency,
        IPlanGenerationService planGen,
        TimeProvider time,
        ILogger<SubmitStructuredAnswersHandler> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(planGen);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        // (1) idempotency short-circuit: on hit return the byte-identical prior result, append nothing.
        var prior = await idempotency
            .SeenAsync<SubmitStructuredAnswersResult>(cmd.IdempotencyKey, ct)
            .ConfigureAwait(false);
        if (prior is not null)
        {
            LogIdempotentReplay(logger, cmd.IdempotencyKey, cmd.UserId);
            return prior;
        }

        var streamId = cmd.UserId;
        var now = time.GetUtcNow();

        var view = await session
            .LoadAsync<OnboardingView>(streamId, ct)
            .ConfigureAwait(false);

        if (view is null)
        {
            // Bootstrap on physical stream existence (a runner who chatted before onboarding already
            // has the shared per-user stream, so a raw StartStream would collide).
            await session
                .StartStreamOrAppendAsync<OnboardingView>(streamId, new OnboardingStarted(streamId, now), ct)
                .ConfigureAwait(false);
        }
        else if (view.Status == OnboardingStatus.Completed)
        {
            // Re-submitting the form after completion would generate a second plan. Reject it as a
            // protocol violation the controller maps to a 409 (regeneration goes through Settings).
            throw new OnboardingAlreadyCompleteException(cmd.UserId);
        }

        // Working copy so the completion gate sees the in-flight state without waiting for Marten to
        // materialize the projection mid-handler.
        var working = view ?? CreateBootstrapView(streamId, now);

        var topicsCaptured = 0;

        if (cmd.PrimaryGoal is not null)
        {
            AppendAnswer(session, streamId, OnboardingTopic.PrimaryGoal, cmd.PrimaryGoal, now);
            working.PrimaryGoal = cmd.PrimaryGoal;

            // Mirror both projections: a non-racing goal clears any stale target event so the working
            // view and the materialized projections agree.
            if (cmd.PrimaryGoal.Goal != Models.PrimaryGoal.RaceTraining)
            {
                working.TargetEvent = null;
            }

            ClearClarification(working, OnboardingTopic.PrimaryGoal);
            topicsCaptured++;
        }

        if (cmd.TargetEvent is not null)
        {
            AppendAnswer(session, streamId, OnboardingTopic.TargetEvent, cmd.TargetEvent, now);
            working.TargetEvent = cmd.TargetEvent;
            ClearClarification(working, OnboardingTopic.TargetEvent);
            topicsCaptured++;
        }

        if (cmd.CurrentFitness is not null)
        {
            AppendAnswer(session, streamId, OnboardingTopic.CurrentFitness, cmd.CurrentFitness, now);
            working.CurrentFitness = cmd.CurrentFitness;
            ClearClarification(working, OnboardingTopic.CurrentFitness);
            topicsCaptured++;
        }

        if (cmd.WeeklySchedule is not null)
        {
            AppendAnswer(session, streamId, OnboardingTopic.WeeklySchedule, cmd.WeeklySchedule, now);
            working.WeeklySchedule = cmd.WeeklySchedule;
            ClearClarification(working, OnboardingTopic.WeeklySchedule);
            topicsCaptured++;
        }

        if (cmd.InjuryHistory is not null)
        {
            AppendAnswer(session, streamId, OnboardingTopic.InjuryHistory, cmd.InjuryHistory, now);
            working.InjuryHistory = cmd.InjuryHistory;
            ClearClarification(working, OnboardingTopic.InjuryHistory);
            topicsCaptured++;
        }

        if (cmd.Preferences is not null)
        {
            AppendAnswer(session, streamId, OnboardingTopic.Preferences, cmd.Preferences, now);
            working.Preferences = cmd.Preferences;
            ClearClarification(working, OnboardingTopic.Preferences);
            topicsCaptured++;
        }

        // Terminal branch — the deterministic gate is the sole plan-generation authority (no LLM
        // ReadyForPlan signal in the form flow). Plan generation runs INLINE on this same session.
        Guid? planId = null;
        if (OnboardingCompletionGate.IsSatisfied(working))
        {
            var newPlanId = Guid.NewGuid();
            LogTerminalBranch(logger, cmd.UserId, newPlanId);

            var planEvents = await planGen
                .GeneratePlanAsync(working, cmd.UserId, newPlanId, intent: null, previousPlanId: null, ct)
                .ConfigureAwait(false);

            session.Events.StartStream<RunCoach.Api.Modules.Training.Plan.Models.PlanProjectionDto>(
                newPlanId,
                planEvents.ToEvents().ToArray());
            session.Events.Append(streamId, new PlanLinkedToUser(cmd.UserId, newPlanId));
            session.Events.Append(streamId, new OnboardingCompleted(newPlanId, now));
            planId = newPlanId;
        }

        var result = new SubmitStructuredAnswersResult(planId is not null, planId, topicsCaptured);

        // Record the idempotency marker LAST so the recorded result matches what the caller receives.
        idempotency.Record(cmd.IdempotencyKey, result);

        return result;
    }

    private static void AppendAnswer<T>(
        IDocumentSession session, Guid streamId, OnboardingTopic topic, T record, DateTimeOffset now)
        where T : class
    {
        // Captured-answer payloads stay default-cased (PascalCase) because both inline projections
        // read them back via `JsonDocument.Deserialize<T>()` with the server-default casing; they never
        // reach the wire. This is the same construction ReviseAnswer / ExtractAnswer use.
        var payload = JsonSerializer.SerializeToDocument(record);
        session.Events.Append(streamId, new AnswerCaptured(topic, payload, Confidence: 1.0, CapturedAt: now));
    }

    private static void ClearClarification(OnboardingView working, OnboardingTopic topic)
    {
        // Capturing an answer clears any outstanding clarification on that topic, mirroring the
        // projection's AnswerCaptured apply so the working gate check agrees with the materialized view.
        if (working.OutstandingClarifications.Contains(topic))
        {
            working.OutstandingClarifications = working.OutstandingClarifications
                .Where(t => t != topic)
                .ToArray();
        }
    }

    private static OnboardingView CreateBootstrapView(Guid streamId, DateTimeOffset now) => new()
    {
        Id = streamId,
        UserId = streamId,
        Status = OnboardingStatus.InProgress,
        OnboardingStartedAt = now,
        OutstandingClarifications = Array.Empty<OnboardingTopic>(),
        Version = 1,
    };

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Structured onboarding answers idempotent replay key={IdempotencyKey} user={UserId}")]
    private static partial void LogIdempotentReplay(ILogger logger, Guid idempotencyKey, Guid userId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Structured onboarding answers terminal branch user={UserId} planId={PlanId}")]
    private static partial void LogTerminalBranch(ILogger logger, Guid userId, Guid planId);
}
