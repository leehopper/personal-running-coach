using Marten;
using Microsoft.Extensions.Logging;
using RunCoach.Api.Modules.Coaching.Idempotency;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Training.Plan.Models;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Wolverine command handler for <see cref="RegeneratePlanCommand"/> per Slice 1
/// § Unit 5 R05.1 / DEC-057 / DEC-060. The handler runs as a regular Wolverine
/// handler (NOT an <c>[AggregateHandler]</c> — there is no aggregate to fetch
/// since regeneration creates a NEW Plan stream) and performs every side-effect
/// atomically through the single Marten <see cref="IDocumentSession"/>
/// Wolverine's transactional middleware brackets around the handler body —
/// there is no <c>RunCoachDbContext</c> injection, no <c>IMessageBus.InvokeAsync</c>
/// call, no second Postgres transaction.
/// </summary>
/// <remarks>
/// <para>
/// Per-call flow:
/// <list type="number">
/// <item>Idempotency check via <see cref="IIdempotencyStore.SeenAsync"/>; on hit
///   return the byte-identical prior response, append nothing.</item>
/// <item>Load the inline-projected <see cref="OnboardingView"/> directly off the
///   document session — it carries the prior <c>CurrentPlanId</c> set by the
///   most recent <see cref="PlanLinkedToUser"/> event during initial generation.</item>
/// <item>Invoke <see cref="IPlanGenerationService.GeneratePlanAsync"/> with the
///   prior plan id threaded through as <c>previousPlanId</c> so the new
///   <see cref="PlanGenerated"/> event carries the audit-link via its
///   <c>PreviousPlanId</c> slot.</item>
/// <item>Stage the returned events on a new
///   <c>session.Events.StartStream&lt;PlanProjectionDto&gt;(planId, ...)</c>.</item>
/// <item>Append a fresh <see cref="PlanLinkedToUser"/> event to the onboarding
///   stream — <see cref="UserProfileFromOnboardingProjection"/> consumes it
///   and updates <c>UserProfile.CurrentPlanId</c> atomically with the Marten
///   event append per DEC-060 (no direct EF write from the handler).</item>
/// <item>Record the response on <see cref="IIdempotencyStore.Record{TResponse}"/>.</item>
/// </list>
/// </para>
/// <para>
/// Failure semantics: any uncaught exception (LLM rejection on any of the six
/// macro/meso/micro calls, transient infrastructure error) propagates out of
/// the handler. Wolverine's transactional middleware aborts the Marten
/// transaction — no new Plan stream, no <c>PlanLinkedToUser</c>, the EF
/// projection's <c>UserProfile.CurrentPlanId</c> stays at its prior value.
/// </para>
/// </remarks>
public sealed partial class RegeneratePlanHandler
{
    // The type is a stateless logical handler container: Wolverine codegen
    // emits a non-static handler stub that calls the static `Handle` method,
    // and `ILogger<RegeneratePlanHandler>` needs a non-static type argument.
    // The private constructor prevents instantiation in test code.
    private RegeneratePlanHandler()
    {
    }

    /// <summary>
    /// Wolverine command handler. Wolverine's <c>AutoApplyTransactions</c>
    /// policy brackets every Marten <c>IDocumentSession</c> resolution with a
    /// single transaction-scoped <c>SaveChangesAsync</c> call after the handler
    /// returns (DEC-048 / batch 22a research). The handler stages every event +
    /// idempotency marker on this session; the framework commits them
    /// atomically. The handler itself never calls <c>SaveChangesAsync</c>.
    /// </summary>
    /// <param name="cmd">The regenerate-plan command.</param>
    /// <param name="session">Marten session bracketed by Wolverine's transactional middleware.</param>
    /// <param name="planGen">Plan generation orchestrator (six-call macro/meso/micro chain).</param>
    /// <param name="idempotency">Idempotency store backed by the same <paramref name="session"/>.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response DTO surfaced by the controller.</returns>
    public static async Task<RegeneratePlanResponse> Handle(
        RegeneratePlanCommand cmd,
        IDocumentSession session,
        IPlanGenerationService planGen,
        IIdempotencyStore idempotency,
        ILogger<RegeneratePlanHandler> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(planGen);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(logger);

        // (a) idempotency short-circuit: on hit return the byte-identical prior
        //     response, append nothing, write nothing.
        var prior = await idempotency
            .SeenAsync<RegeneratePlanResponse>(cmd.IdempotencyKey, ct)
            .ConfigureAwait(false);
        if (prior is not null)
        {
            LogIdempotentReplay(logger, cmd.IdempotencyKey, cmd.UserId);
            return prior;
        }

        var onboardingStreamId = cmd.UserId;

        // (b) load the inline-projected onboarding view directly off the
        //     document session. `OnboardingProjection` is registered with
        //     `ProjectionLifecycle.Inline` so the document is materialized on
        //     the same NpgsqlConnection the handler is about to write through.
        //     The controller already verified the runner has completed
        //     onboarding (UserProfile.OnboardingCompletedAt non-null), so the
        //     view is guaranteed to exist and carry a non-null `CurrentPlanId`
        //     by the time this branch runs.
        var view = await session
            .LoadAsync<OnboardingView>(onboardingStreamId, ct)
            .ConfigureAwait(false);

        if (view is null || view.CurrentPlanId is null)
        {
            // Defense-in-depth: the controller's UserProfile.OnboardingCompletedAt
            // gate should make this unreachable, but if a stale projection
            // somehow carries a missing view we surface the protocol violation
            // as an exception so Wolverine aborts the transaction.
            throw new InvalidOperationException(
                $"Cannot regenerate plan for user {cmd.UserId}: onboarding view missing or no prior plan linked.");
        }

        var priorPlanId = view.CurrentPlanId.Value;

        // (c) + (d) generate the new Plan stream events. The plan-generation
        //     service does NOT touch the session; it returns the event list
        //     for us to stage. PreviousPlanId is threaded onto the returned
        //     PlanGenerated event so the projection retains audit linkage to
        //     the prior plan without a schema bump.
        var planId = Guid.NewGuid();
        LogRegenerateStart(logger, cmd.UserId, planId, priorPlanId, cmd.Intent is not null);

        var planEvents = await planGen
            .GeneratePlanAsync(view, cmd.UserId, planId, cmd.Intent, previousPlanId: priorPlanId, ct)
            .ConfigureAwait(false);

        // (e) stage the new Plan stream — the inline `PlanProjection`
        //     materializes the `PlanProjectionDto` document on the same
        //     transaction so `GET /api/v1/plan/current` is consistent the
        //     instant the response returns.
        session.Events.StartStream<PlanProjectionDto>(planId, planEvents.ToArray());

        // (f) append PlanLinkedToUser to the onboarding stream — this is what
        //     `UserProfileFromOnboardingProjection` consumes to flip
        //     `UserProfile.CurrentPlanId` to the new plan id atomically with
        //     the Marten event append per DEC-060 / R-069. NO direct EF write.
        session.Events.Append(onboardingStreamId, new PlanLinkedToUser(cmd.UserId, planId));

        // (g) + (h) record the idempotency marker LAST so the response we
        //     recorded matches what the caller is about to receive.
        var response = new RegeneratePlanResponse(planId, "generated");
        idempotency.Record(cmd.IdempotencyKey, cmd.UserId, response);

        return response;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Plan regeneration idempotent replay key={IdempotencyKey} user={UserId}")]
    private static partial void LogIdempotentReplay(ILogger logger, Guid idempotencyKey, Guid userId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Plan regeneration starting user={UserId} newPlanId={NewPlanId} previousPlanId={PreviousPlanId} hasIntent={HasIntent}")]
    private static partial void LogRegenerateStart(
        ILogger logger,
        Guid userId,
        Guid newPlanId,
        Guid previousPlanId,
        bool hasIntent);
}
