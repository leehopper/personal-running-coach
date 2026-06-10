using System.Globalization;
using JasperFx.Events;
using Marten;
using Microsoft.Extensions.Logging;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Sanitization;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Modules.Training.Safety;
using RunCoach.Api.Modules.Training.Workouts;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Wolverine command handler for <see cref="EvaluateAdaptationCommand"/> (Slice 3
/// § Unit 5, DEC-012/DEC-060/DEC-073). Orchestrates the deterministic adaptation
/// paths — safety gate, deviation engine, escalation classifier, micro-adjust
/// planner — and emits events ONLY: every side-effect (event appends, the
/// signal-state document, the idempotency marker) stages on the single Marten
/// <see cref="IDocumentSession"/> that Wolverine's transactional middleware
/// commits after the handler returns. The handler runs as a regular Wolverine
/// handler (NOT an <c>[AggregateHandler]</c>) and never touches
/// <c>RunCoachDbContext</c> for writes; its only relational access is the
/// sanctioned read-only <see cref="IWorkoutLogRepository.GetByIdAsync"/> of the
/// already-committed log.
/// </summary>
/// <remarks>
/// <para>
/// Per-call flow (DEC-012 ladder, L0–L1 deterministic; L2 is the first LLM level):
/// <list type="number">
/// <item>Idempotency check keyed off <c>WorkoutLogId</c>; on hit return the
///   byte-identical prior response, stage nothing.</item>
/// <item>Read the committed log (user-scoped, read-only).</item>
/// <item>Off-plan short-circuit: a null prescription snapshot yields a null
///   <see cref="DeviationResult"/> — record the no-op response so replays
///   short-circuit, append nothing.</item>
/// <item>Sanitize the log's free-text (DEC-059 boundary) and run
///   <see cref="ISafetyGate.Classify"/> on the sanitized note + metrics.</item>
/// <item>Red short-circuits: append ONLY <see cref="SafetySignalRaised"/> with
///   the scripted content for the matched category — no LLM, no plan change,
///   no signal-state advance.</item>
/// <item>Rehydrate <see cref="AdaptationSignalState"/> from its per-plan Marten
///   document (validated via the <see cref="AdaptationSignalStateDocument.ToState"/>
///   factory boundary; <see cref="AdaptationSignalState.Initial"/> when absent),
///   classify, then route: L0 absorb appends nothing (the state document still
///   advances); L1 plans a forward swap via <see cref="MicroAdjustPlanner"/> and
///   appends exactly one <see cref="PlanAdaptedFromLog"/> nudge; an unswappable
///   L1 escalates into the L2 restructure path.</item>
/// <item>L2 Green/Amber restructure: compose the adaptation prompt, make exactly
///   ONE structured-output LLM call (DEC-073 — SDK-side retries only), validate via
///   <see cref="PlanAdaptationOutputValidator"/>, compute the deterministic diff via
///   <see cref="RestructureDiffCalculator"/>, then append one
///   <see cref="PlanAdaptedFromLog"/> restructure (plus the scripted Amber
///   <see cref="SafetySignalRaised"/> referral when the tier is Amber) and advance
///   the signal state.</item>
/// <item>The marker records LAST on every committing path so it persists
///   atomically with the appends it memoizes.</item>
/// </list>
/// </para>
/// <para>
/// Failure semantics: nothing is staged on failure paths. Any uncaught exception
/// aborts the Wolverine-bracketed Marten transaction, rolling back marker, events,
/// and state document together, so a retried run re-evaluates against fresh state.
/// A <em>caught</em> terminal <see cref="CoachingLlmException"/> on the L2 path is
/// different: the handler returns the <c>Kind=Error</c> envelope normally, so
/// Wolverine COMMITS the session — which is safe only because the L2 path stages
/// strictly nothing (no event, no state document, no marker) before the LLM call,
/// validation, and diff have all succeeded. The committed transaction is therefore
/// empty: the plan stream is unchanged and the marker stays released for
/// re-evaluation.
/// </para>
/// </remarks>
public sealed partial class EvaluateAdaptationHandler
{
    /// <summary>
    /// Bounded inline retries for a lost stream-version race before the message
    /// dead-letters. Each retry re-runs <see cref="Handle"/> in full: the marker
    /// rolled back with the failed transaction so <c>SeenAsync</c> misses, and the
    /// signal state reloads fresh — which IS the retry-against-fresh-state
    /// semantics the optimistic-concurrency contract asks for.
    /// </summary>
    internal const int MaxConcurrencyRetries = 3;

    /// <summary>
    /// User-facing message for the non-retryable envelope when the LLM's proposal
    /// fails post-decode validation (or is not the restructure the deterministic
    /// ladder asked for). Mirrors <see cref="PermanentCoachingLlmException"/>
    /// semantics: retrying the identical request is unlikely to help, the log is
    /// already saved, and no plan change was applied.
    /// </summary>
    private const string RejectedProposalMessage =
        "The coach proposed a plan change that did not pass safety validation, so your "
        + "plan was left unchanged. Your workout log is saved.";

    // The type is a stateless logical handler container: Wolverine codegen
    // emits a non-static handler stub that calls the static `Handle` method,
    // and `ILogger<EvaluateAdaptationHandler>` needs a non-static type argument.
    // The private constructor prevents instantiation in test code.
    private EvaluateAdaptationHandler()
    {
    }

    /// <summary>
    /// Wolverine chain-configuration hook, discovered by convention and applied
    /// once at startup before code generation. Scopes the adaptation-specific
    /// concurrency policy to THIS message type only: an
    /// <see cref="EventStreamUnexpectedMaxEventIdException"/> (two adaptation
    /// evaluations racing appends on the same plan stream under Rich append mode)
    /// retries inline a bounded number of times, then dead-letters. Chain rules
    /// are evaluated before the globally registered rules, so the global
    /// first-write-wins dead-letter routing (DEC-057) keeps governing the
    /// onboarding and regenerate chains untouched.
    /// </summary>
    /// <param name="chain">The handler chain for <see cref="EvaluateAdaptationCommand"/>.</param>
    public static void Configure(HandlerChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ConfigureFailureRules(chain);
    }

    /// <summary>
    /// Wolverine command handler. Wolverine's <c>AutoApplyTransactions</c> policy
    /// brackets the Marten <c>IDocumentSession</c> with a single transaction-scoped
    /// <c>SaveChangesAsync</c> after the handler returns; the handler itself never
    /// calls <c>SaveChangesAsync</c>.
    /// </summary>
    /// <param name="cmd">The evaluate-adaptation command.</param>
    /// <param name="session">Marten session bracketed by Wolverine's transactional middleware.</param>
    /// <param name="workoutLogs">Read-only access to the committed log (writes prohibited per DEC-060).</param>
    /// <param name="deviationEngine">Deterministic prescribed-vs-actual comparison.</param>
    /// <param name="recentLogSanitizer">DEC-059 free-text sanitizer for the log's note + metric values.</param>
    /// <param name="safetyGate">Deterministic keyword safety classifier; runs before any LLM.</param>
    /// <param name="escalationClassifier">Deterministic DEC-012 escalation ladder.</param>
    /// <param name="assembler">Context assembler composing the L2 adaptation prompt (sanitized, nonce-spotlighted).</param>
    /// <param name="llm">Coaching LLM adapter — invoked on the L2 restructure path only.</param>
    /// <param name="idempotency">Idempotency store backed by the same <paramref name="session"/>.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The flat DEC-073 response envelope surfaced by the create flow.</returns>
    public static async Task<AdaptationResponseDto> Handle(
        EvaluateAdaptationCommand cmd,
        IDocumentSession session,
        IWorkoutLogRepository workoutLogs,
        IDeviationEngine deviationEngine,
        IRecentLogSanitizer recentLogSanitizer,
        ISafetyGate safetyGate,
        IEscalationClassifier escalationClassifier,
        IContextAssembler assembler,
        ICoachingLlm llm,
        IIdempotencyStore idempotency,
        ILogger<EvaluateAdaptationHandler> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(workoutLogs);
        ArgumentNullException.ThrowIfNull(deviationEngine);
        ArgumentNullException.ThrowIfNull(recentLogSanitizer);
        ArgumentNullException.ThrowIfNull(safetyGate);
        ArgumentNullException.ThrowIfNull(escalationClassifier);
        ArgumentNullException.ThrowIfNull(assembler);
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(logger);

        // (1) idempotency short-circuit: one adaptation evaluation commits per
        //     log, ever. On hit return the byte-identical prior response.
        var prior = await idempotency
            .SeenAsync<AdaptationResponseDto>(cmd.WorkoutLogId, ct)
            .ConfigureAwait(false);
        if (prior is not null)
        {
            LogIdempotentReplay(logger, cmd.WorkoutLogId, cmd.UserId);
            return prior;
        }

        // (2) read the committed log. SANCTIONED read: DEC-060 prohibits the
        //     write surface (no `DbContext` injection, no `SaveChangesAsync`,
        //     no `CreateAsync`) — a user-scoped read-only repository read is fine.
        //     The create flow only dispatches after its EF commit, so a missing
        //     row is a protocol violation; throwing aborts the transaction with
        //     nothing staged and the marker un-recorded.
        var log = await workoutLogs
            .GetByIdAsync(cmd.UserId, cmd.WorkoutLogId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Cannot evaluate adaptation: workout log {cmd.WorkoutLogId} not found for user {cmd.UserId}.");

        // (3) off-plan short-circuit: a null prescription snapshot yields a null
        //     deviation — no plan stream to adapt, no event, no LLM, no turn.
        //     The no-op response IS recorded so a replayed submission
        //     short-circuits at step (1); the marker rides this same transaction.
        var deviation = deviationEngine.Evaluate(log);
        if (deviation is null)
        {
            LogOffPlanNoOp(logger, cmd.WorkoutLogId);
            return RecordAndReturn(idempotency, cmd.WorkoutLogId, AdaptationKind.Absorb);
        }

        // A non-null deviation implies an on-plan log per the engine's contract,
        // and the snapshot's `SourcePlanId` carries the server-authoritative
        // plan stream id — no EF profile read is needed to resolve the stream.
        var snapshot = log.Prescription!;
        var planId = snapshot.SourcePlanId;

        // (4) sanitize the user-authored free-text at the DEC-059 boundary, then
        //     gate. The gate expects sanitized input and does not re-sanitize.
        var detail = await SanitizeDetailAsync(log, snapshot, recentLogSanitizer, ct).ConfigureAwait(false);
        var safety = safetyGate.Classify(detail.Notes, detail.Metrics);

        // (5) Red short-circuits the whole flow: append ONLY the scripted safety
        //     turn — no LLM, no plan change, no signal-state advance. Safety is
        //     never left to LLM self-policing (DEC-019/DEC-030/DEC-079).
        if (safety.Tier == SafetyTier.Red)
        {
            var content = safety.Category == ReferralCategory.Crisis
                ? CrisisResponseContent.CrisisResponse
                : EmergencyResponseContent.EmergencyResponse;
            session.Events.Append(
                planId,
                new SafetySignalRaised(cmd.WorkoutLogId, SafetyTier.Red, safety.Category, content));
            LogRedShortCircuit(logger, cmd.WorkoutLogId, safety.Category);
            return RecordAndReturn(idempotency, cmd.WorkoutLogId, AdaptationKind.Absorb);
        }

        // (6) rehydrate the prior signal state through the validating factory
        //     boundary; a plan with no adaptation history starts from Initial.
        var stateDocument = await session
            .LoadAsync<AdaptationSignalStateDocument>(planId, ct)
            .ConfigureAwait(false);
        var priorState = stateDocument?.ToState() ?? AdaptationSignalState.Initial;

        // (7) deterministic DEC-012 escalation.
        var decision = escalationClassifier.Classify(deviation, safety.Tier, priorState);

        // (8) L0 absorb: an absorb never produces an event, but the signal-state
        //     document still advances so accumulated minor deviations can cross
        //     a threshold on a later log.
        if (decision.EscalationLevel == EscalationLevel.Absorb)
        {
            session.Store(AdaptationSignalStateDocument.From(planId, decision.NextState));
            return RecordAndReturn(idempotency, cmd.WorkoutLogId, AdaptationKind.Absorb);
        }

        // (9) L1 micro-adjust: swap the missed key workout forward within the
        //     live micro week — deterministic, no LLM. No live micro detail for
        //     the prescribed week, or no valid non-stacking forward swap,
        //     escalates L1 -> L2.
        // Both the L1 swap and the L2 prompt composition read the live plan
        // projection; load it once for whichever branch follows.
        var plan = await session.LoadAsync<PlanProjectionDto>(planId, ct).ConfigureAwait(false);

        if (decision.EscalationLevel == EscalationLevel.MicroAdjust)
        {
            var currentWeek = LiveMicroWeek(plan, snapshot.WeekNumber);
            if (currentWeek is not null
                && MicroAdjustPlanner.TryPlanSwap(currentWeek, snapshot.DayOfWeek, snapshot.WeekNumber, out var diff))
            {
                session.Events.Append(
                    planId,
                    new PlanAdaptedFromLog(
                        cmd.WorkoutLogId,
                        AdaptationKind.Nudge,
                        EscalationLevel.MicroAdjust,
                        safety.Tier,
                        BuildNudgeRationale(diff),
                        diff));
                session.Store(AdaptationSignalStateDocument.From(planId, decision.NextState));
                LogNudgeApplied(logger, cmd.WorkoutLogId, planId, snapshot.WeekNumber);
                return RecordAndReturn(idempotency, cmd.WorkoutLogId, AdaptationKind.Nudge);
            }

            LogMicroAdjustEscalated(logger, cmd.WorkoutLogId, planId, snapshot.WeekNumber);
        }

        // (10) L2 restructure: the first level that invokes the coaching LLM.
        return await RestructureAsync(
            cmd,
            planId,
            plan,
            snapshot,
            deviation,
            detail,
            decision,
            safety,
            session,
            assembler,
            llm,
            idempotency,
            logger,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers the bounded-retry-then-dead-letter rule for stream-version
    /// conflicts. Extracted from <see cref="Configure"/> so unit tests can
    /// exercise the registration against a substitutable policy surface
    /// (constructing a real <c>HandlerChain</c> requires Wolverine's internal
    /// handler graph).
    /// </summary>
    /// <remarks>
    /// The rule targets <see cref="EventStreamUnexpectedMaxEventIdException"/>
    /// because that is the exception a lost Rich-append-mode race on an EXISTING
    /// plan stream actually surfaces as: the loser's commit violates the events
    /// table's (stream id, version) uniqueness and Marten transforms that Postgres
    /// error into this type (a <see cref="JasperFx.ConcurrencyException"/>
    /// subclass). <c>Marten.Exceptions.ConcurrentUpdateException</c> deliberately
    /// does NOT appear here: it sits outside the <c>JasperFx.ConcurrencyException</c>
    /// hierarchy and is raised for session-level write collisions, not for this
    /// append conflict — a rule keyed on it never fires for the adaptation race.
    /// </remarks>
    /// <param name="policies">The failure-policy surface to register on.</param>
    internal static void ConfigureFailureRules(IWithFailurePolicies policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        policies.OnException<EventStreamUnexpectedMaxEventIdException>()
            .RetryTimes(MaxConcurrencyRetries)
            .Then.MoveToErrorQueue();
    }

    /// <summary>
    /// The L2 LLM restructure path (Slice 3 Unit 4/5, DEC-012/DEC-073/DEC-079):
    /// compose the adaptation prompt, make exactly ONE structured-output call
    /// against the byte-stable <see cref="AdaptationSchema.Frozen"/> schema
    /// (no handler-side retry loop — DEC-073 puts bounded retries inside the
    /// adapter's SDK), validate via <see cref="PlanAdaptationOutputValidator"/>,
    /// compute the deterministic diff via <see cref="RestructureDiffCalculator"/>,
    /// and only then stage the event(s) + state + marker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage-nothing-before-success is the load-bearing invariant: a caught
    /// <see cref="CoachingLlmException"/> (and a post-decode validation reject,
    /// which the handler maps to the same non-retryable envelope — the validator
    /// is pure, the handler owns the policy) returns normally, so Wolverine
    /// commits the session. Because every <c>session.Events.Append</c> /
    /// <c>session.Store</c> / <c>idempotency.Record</c> sits strictly AFTER the
    /// LLM call, validation, and diff have succeeded, that commit is empty:
    /// stream unchanged, marker released, the log re-evaluates on retry.
    /// </para>
    /// <para>
    /// An Amber tier additionally appends <see cref="SafetySignalRaised"/> with
    /// the versioned scripted <see cref="AmberReferralContent"/> for the matched
    /// category. The referral content source was unpinned by design; the scripted
    /// per-category route (mirroring the Red-tier
    /// <see cref="CrisisResponseContent"/> / <see cref="EmergencyResponseContent"/>
    /// routing) is chosen deliberately over LLM prose so the safety turn stays
    /// deterministic and safe (DEC-019/DEC-030/DEC-079 — safety is never left to
    /// LLM self-policing).
    /// </para>
    /// </remarks>
    private static async Task<AdaptationResponseDto> RestructureAsync(
        EvaluateAdaptationCommand cmd,
        Guid planId,
        PlanProjectionDto? plan,
        WorkoutPrescriptionSnapshot snapshot,
        DeviationResult deviation,
        LoggedWorkoutDetail detail,
        EscalationDecision decision,
        SafetyClassification safety,
        IDocumentSession session,
        IContextAssembler assembler,
        ICoachingLlm llm,
        IIdempotencyStore idempotency,
        ILogger logger,
        CancellationToken ct)
    {
        // An on-plan log whose plan stream has no materialized projection is a
        // protocol violation; throwing aborts the transaction with nothing staged.
        if (plan is null)
        {
            throw new InvalidOperationException(
                $"Cannot restructure: no plan projection found for plan {planId}.");
        }

        var composition = await assembler
            .ComposeForAdaptationAsync(plan, decision.EscalationLevel, safety.Tier, deviation, detail, ct)
            .ConfigureAwait(false);

        // Exactly one call, with no handler-side retry loop — DEC-073 places the
        // bounded retries inside the adapter's SDK. Only the adapter's terminal
        // failure shapes are caught here; everything else, including
        // cancellation, propagates and aborts the transaction.
        PlanAdaptationOutput output;
        AnthropicUsage usage;
        try
        {
            (output, usage) = await llm
                .GenerateStructuredAsync<PlanAdaptationOutput>(
                    composition.SystemPrompt,
                    composition.UserMessage,
                    AdaptationSchema.Frozen,
                    CacheControl.Ephemeral1h,
                    ct)
                .ConfigureAwait(false);
        }
        catch (CoachingLlmException ex)
        {
            LogAdaptationLlmFailed(logger, cmd.WorkoutLogId, planId, ex);
            return AdaptationResponseDto.FromError(ex);
        }

        // Post-decode policy (handler-owned): a structurally/safety-invalid
        // proposal — or a proposal that contradicts the deterministic ladder by
        // not being a restructure (DEC-079: the deterministic layer decides the
        // level; the LLM never absorbs a sustained deviation away) — maps to the
        // same non-retryable Kind=Error envelope with nothing staged.
        var validation = PlanAdaptationOutputValidator.Validate(output);
        if (!validation.IsValid || output.AdaptationKind != AdaptationKind.Restructure)
        {
            LogRestructureProposalRejected(
                logger, cmd.WorkoutLogId, planId, validation.Violation, output.AdaptationKind);
            return AdaptationResponseDto.FromError(
                new PermanentCoachingLlmException(RejectedProposalMessage, null));
        }

        // DEC-079: the diff is structured data computed deterministically from
        // the validated proposal against the projection — never parsed prose.
        var diff = RestructureDiffCalculator.Calculate(output.RestructurePlan!, plan, snapshot.WeekNumber);

        // Staging begins only now that LLM + validate + diff have all succeeded.
        session.Events.Append(
            planId,
            new PlanAdaptedFromLog(
                cmd.WorkoutLogId,
                AdaptationKind.Restructure,
                EscalationLevel.Restructure,
                safety.Tier,
                output.Rationale,
                diff));

        if (safety.Tier == SafetyTier.Amber)
        {
            // Scripted-by-category referral content (see remarks) — GATE-BEFORE-
            // INCREASE itself is already validator-enforced; the handler trusts it.
            var content = safety.Category == ReferralCategory.Injury
                ? AmberReferralContent.InjuryReferral
                : AmberReferralContent.RedSReferral;
            session.Events.Append(
                planId,
                new SafetySignalRaised(cmd.WorkoutLogId, SafetyTier.Amber, safety.Category, content));
        }

        session.Store(AdaptationSignalStateDocument.From(planId, decision.NextState));
        LogRestructureApplied(
            logger,
            cmd.WorkoutLogId,
            planId,
            usage.InputTokens,
            usage.OutputTokens,
            usage.CacheCreationInputTokens,
            usage.CacheReadInputTokens);
        return RecordAndReturn(idempotency, cmd.WorkoutLogId, AdaptationKind.Restructure);
    }

    /// <summary>
    /// Records the no-plan-change response under the log's id and returns it —
    /// the shared tail of every committing deterministic path. The marker is
    /// always the LAST thing staged so it commits atomically with whatever the
    /// path appended before it.
    /// </summary>
    private static AdaptationResponseDto RecordAndReturn(
        IIdempotencyStore idempotency,
        Guid workoutLogId,
        AdaptationKind kind)
    {
        var response = AdaptationResponseDto.Adapted(kind);
        idempotency.Record(workoutLogId, response);
        return response;
    }

    /// <summary>
    /// Maps the committed entity into the <see cref="LoggedWorkoutDetail"/> view
    /// (the WorkoutLog → detail mapping deferred to this Slice 3 consumer) and
    /// routes its note + free-text metric values through the DEC-059 sanitizer.
    /// The sanitized detail feeds the safety gate here and the adaptation prompt
    /// at the L2 seam.
    /// </summary>
    private static ValueTask<LoggedWorkoutDetail> SanitizeDetailAsync(
        WorkoutLog log,
        WorkoutPrescriptionSnapshot snapshot,
        IRecentLogSanitizer sanitizer,
        CancellationToken ct)
    {
        var detail = new LoggedWorkoutDetail(
            log.OccurredOn,
            snapshot.WorkoutType.ToString(),
            log.Distance,
            log.Duration,
            WorkoutMetricsProjection.ToDisplayMetrics(log.Metrics),
            log.Notes);
        return sanitizer.SanitizeAsync(detail, ct);
    }

    /// <summary>
    /// Resolves the live micro week for the prescribed week number off the
    /// already-loaded plan projection. Null when the projection is missing or
    /// carries no micro detail for that week (only week 1 is materialized at
    /// MVP-0) — the caller escalates rather than swapping blind.
    /// </summary>
    private static WorkoutOutput[]? LiveMicroWeek(PlanProjectionDto? plan, int weekNumber) =>
        plan is not null && plan.MicroWorkoutsByWeek.TryGetValue(weekNumber, out var micro)
            ? micro.Workouts
            : null;

    /// <summary>
    /// Renders the deterministic, user-facing one-liner for a nudge from the
    /// structured diff — never LLM-authored, never parsed back. The diff a
    /// successful <see cref="MicroAdjustPlanner.TryPlanSwap"/> emits always
    /// carries exactly two workout changes: the missed key day and the later
    /// easy day it swapped with.
    /// </summary>
    private static string BuildNudgeRationale(PlanAdaptationDiff diff)
    {
        // A successful TryPlanSwap diff always carries the swapped pair with
        // both Before slots populated: [0] the missed key day, [1] the later
        // easy day. Every interpolated value is already a plain string, so the
        // concatenation is culture-free.
        var keyChange = diff.WorkoutChanges[0];
        var easyChange = diff.WorkoutChanges[1];
        return $"You missed {keyChange.Before!.Title} on {DayName(keyChange.DayOfWeek)}, so I moved it "
            + $"forward to {DayName(easyChange.DayOfWeek)} and slotted {easyChange.Before!.Title} into "
            + $"{DayName(keyChange.DayOfWeek)} instead. The week keeps its quality work without "
            + "stacking hard days back to back.";
    }

    private static string DayName(int dayOfWeek) =>
        CultureInfo.InvariantCulture.DateTimeFormat.GetDayName((DayOfWeek)dayOfWeek);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Adaptation evaluation idempotent replay workoutLogId={WorkoutLogId} user={UserId}")]
    private static partial void LogIdempotentReplay(ILogger logger, Guid workoutLogId, Guid userId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Adaptation evaluation no-op for off-plan log workoutLogId={WorkoutLogId}")]
    private static partial void LogOffPlanNoOp(ILogger logger, Guid workoutLogId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Safety gate Red short-circuit workoutLogId={WorkoutLogId} category={Category}: scripted turn appended, no LLM, no plan change")]
    private static partial void LogRedShortCircuit(ILogger logger, Guid workoutLogId, ReferralCategory category);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Deterministic nudge applied workoutLogId={WorkoutLogId} planId={PlanId} week={WeekNumber}")]
    private static partial void LogNudgeApplied(ILogger logger, Guid workoutLogId, Guid planId, int weekNumber);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Micro-adjust not plannable workoutLogId={WorkoutLogId} planId={PlanId} week={WeekNumber}: escalating L1 -> L2")]
    private static partial void LogMicroAdjustEscalated(ILogger logger, Guid workoutLogId, Guid planId, int weekNumber);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "LLM restructure applied workoutLogId={WorkoutLogId} planId={PlanId} inputTokens={InputTokens} outputTokens={OutputTokens} cacheCreationInputTokens={CacheCreationInputTokens} cacheReadInputTokens={CacheReadInputTokens}")]
    private static partial void LogRestructureApplied(
        ILogger logger,
        Guid workoutLogId,
        Guid planId,
        long inputTokens,
        long outputTokens,
        long cacheCreationInputTokens,
        long cacheReadInputTokens);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "Adaptation LLM call failed terminally workoutLogId={WorkoutLogId} planId={PlanId}; returning the Kind=Error envelope with nothing staged")]
    private static partial void LogAdaptationLlmFailed(
        ILogger logger,
        Guid workoutLogId,
        Guid planId,
        Exception ex);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "Adaptation LLM proposal rejected workoutLogId={WorkoutLogId} planId={PlanId} violation={Violation} proposedKind={ProposedKind}; returning the Kind=Error envelope with nothing staged")]
    private static partial void LogRestructureProposalRejected(
        ILogger logger,
        Guid workoutLogId,
        Guid planId,
        PlanAdaptationOutputValidationViolation violation,
        AdaptationKind proposedKind);
}
