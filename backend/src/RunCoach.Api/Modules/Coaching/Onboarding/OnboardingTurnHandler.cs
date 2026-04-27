using System.Text.Json;
using Marten;
using Marten.Events;
using Microsoft.Extensions.Logging;
using RunCoach.Api.Modules.Coaching.Idempotency;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Wolverine <c>[AggregateHandler]</c> for <see cref="SubmitUserTurn"/> per
/// Slice 1 § Unit 1 R01.6 / DEC-057 / DEC-060. The handler performs every
/// per-turn side-effect atomically through the single Marten
/// <see cref="IDocumentSession"/> Wolverine's transactional middleware brackets
/// around the handler body — there is no <c>RunCoachDbContext</c> injection,
/// no <c>IMessageBus.InvokeAsync</c> call, no second Postgres transaction.
/// </summary>
/// <remarks>
/// <para>
/// Per-turn flow:
/// <list type="number">
/// <item>Idempotency check via <see cref="IIdempotencyStore.SeenAsync"/>; on hit
///   return the byte-identical prior response, append nothing.</item>
/// <item>Resolve the next topic via the deterministic
///   <see cref="OnboardingCompletionGate.NextTopic"/> selector.</item>
/// <item>Compose the Pattern-B prompt via
///   <see cref="IContextAssembler.ComposeForOnboardingAsync"/> (sanitizer-applied).</item>
/// <item>Call <see cref="ICoachingLlm.GenerateStructuredAsync{T}(string, string,
///   System.Collections.Generic.IReadOnlyDictionary{string, JsonElement}?,
///   Models.CacheControl?, CancellationToken)"/> with the byte-stable schema +
///   1h ephemeral cache marker.</item>
/// <item>Validate the Pattern-B-Invariant via
///   <see cref="OnboardingTurnOutputValidator.Validate"/> with one-retry on
///   first failure (stronger discriminator instruction); on second failure,
///   emit <see cref="ClarificationRequested"/> and short-circuit to "ask".</item>
/// <item>Stage <see cref="UserTurnRecorded"/>, <see cref="TopicAsked"/>,
///   <see cref="AssistantTurnRecorded"/>, plus any
///   <see cref="AnswerCaptured"/> / <see cref="ClarificationRequested"/> the
///   LLM produced.</item>
/// <item>If the deterministic completion gate is now satisfied (and the LLM
///   agreed via <c>ReadyForPlan</c>) — invoke
///   <see cref="IPlanGenerationService.GeneratePlanAsync"/>, stage the returned
///   plan events on a new <c>StartStream&lt;Plan&gt;(planId, ...)</c>, append
///   <see cref="PlanLinkedToUser"/> + <see cref="OnboardingCompleted"/> to the
///   onboarding stream — all on the same session.</item>
/// <item>Record the response on <see cref="IIdempotencyStore.Record{T}"/>.</item>
/// </list>
/// </para>
/// <para>
/// Failure semantics: any uncaught exception (LLM rejection, Pattern-B
/// validation failure, plan-generation failure) propagates out of the handler.
/// Wolverine's transactional middleware aborts the Marten transaction —
/// nothing committed. Empirically guarded by
/// <c>InvokeAsyncTransactionScopeTests</c>.
/// </para>
/// </remarks>
public sealed partial class OnboardingTurnHandler
{
    private const string PromptPrefix = "[Pattern-B-Invariant]";
    private const double ExtractionConfidenceFloor = 0.6;

    // Wire-format JsonDocuments embedded inside the response DTO + Marten
    // event payloads must be camelCase so they round-trip cleanly to the
    // frontend Zod schemas (the controller's default formatter handles the
    // outer DTO via camelCase, but JsonDocument payloads are opaque to it).
    private static readonly JsonSerializerOptions WireSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // The type is a stateless logical handler container: Wolverine codegen
    // emits a non-static handler stub that calls the static `Handle` method,
    // and `ILogger<OnboardingTurnHandler>` needs a non-static type argument.
    // The private constructor prevents instantiation in test code.
    private OnboardingTurnHandler()
    {
    }

    /// <summary>
    /// Wolverine command handler. Wolverine's <c>AutoApplyTransactions</c> policy
    /// brackets every Marten <c>IDocumentSession</c> resolution with a single
    /// transaction-scoped <c>SaveChangesAsync</c> call after the handler returns
    /// (DEC-048 / batch 22a research). The handler stages every event +
    /// idempotency marker on this session; the framework commits them
    /// atomically. The handler itself never calls <c>SaveChangesAsync</c>.
    /// </summary>
    /// <param name="cmd">The submit-user-turn command.</param>
    /// <param name="session">Marten session bracketed by Wolverine's transactional middleware.</param>
    /// <param name="llm">Coaching LLM adapter.</param>
    /// <param name="assembler">Context assembler. Holds its own <c>IPromptSanitizer</c> reference and applies sanitization per-section inside <c>ComposeForOnboardingAsync</c> / <c>ComposeForPlanGenerationAsync</c> per DEC-059.</param>
    /// <param name="idempotency">Idempotency store backed by the same <paramref name="session"/>.</param>
    /// <param name="planGen">Plan generation orchestrator (six-call macro/meso/micro chain).</param>
    /// <param name="time">Time provider for event timestamps.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response DTO surfaced by the controller.</returns>
    public static async Task<OnboardingTurnResponseDto> Handle(
        SubmitUserTurn cmd,
        IDocumentSession session,
        ICoachingLlm llm,
        IContextAssembler assembler,
        IIdempotencyStore idempotency,
        IPlanGenerationService planGen,
        TimeProvider time,
        ILogger<OnboardingTurnHandler> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(assembler);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(planGen);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        // (1) idempotency short-circuit: on hit return the byte-identical prior
        //     response, append nothing, write nothing.
        var prior = await idempotency
            .SeenAsync<OnboardingTurnResponseDto>(cmd.IdempotencyKey, ct)
            .ConfigureAwait(false);
        if (prior is not null)
        {
            LogIdempotentReplay(logger, cmd.IdempotencyKey, cmd.UserId);
            return prior;
        }

        var streamId = cmd.UserId;
        var now = time.GetUtcNow();

        // Load the inline-projected onboarding view directly off the document
        // session — `OnboardingProjection` is registered with
        // `ProjectionLifecycle.Inline` so the document is materialized on the
        // same NpgsqlConnection the handler is about to write through. Returns
        // null for the very first turn (before any events exist on the stream).
        var view = await session
            .LoadAsync<OnboardingView>(streamId, ct)
            .ConfigureAwait(false);

        // (2) bootstrap the stream on the very first turn — `OnboardingStarted`
        //     is the create event the Marten projection's `Create` overload
        //     keys off. The session call stages the stream creation; commit
        //     happens after the handler returns under Wolverine's transaction.
        var isFirstTurn = view is null;
        if (isFirstTurn)
        {
            session.Events.StartStream<OnboardingView>(streamId, new OnboardingStarted(streamId, now));
        }
        else if (view!.Status == OnboardingStatus.Completed)
        {
            // Submitting a new turn after onboarding is complete is a 409 per
            // the feature spec. Surface the protocol violation as an
            // exception the controller maps to ProblemDetails.
            throw new OnboardingAlreadyCompleteException(cmd.UserId);
        }

        // The handler maintains a working copy of the view so the deterministic
        // selector + completion gate see the in-flight state without waiting
        // for Marten to materialize the projection mid-handler.
        var working = view ?? CreateBootstrapView(streamId, now);

        // (3) deterministic next-topic selection.
        var currentTopic = OnboardingCompletionGate.NextTopic(working) ?? OnboardingTopic.Preferences;

        // (4) compose the Pattern-B prompt.
        var composition = await assembler
            .ComposeForOnboardingAsync(working, currentTopic, cmd.Text, ct)
            .ConfigureAwait(false);

        // (5) call the LLM with the byte-stable schema + 1h ephemeral cache marker.
        OnboardingTurnOutput output;
        try
        {
            output = await llm
                .GenerateStructuredAsync<OnboardingTurnOutput>(
                    composition.SystemPrompt,
                    composition.UserMessage,
                    OnboardingSchema.Frozen,
                    Coaching.Models.CacheControl.Ephemeral1h,
                    ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogLlmCallFailed(logger, cmd.UserId, currentTopic, ex);
            throw;
        }

        // (6) validate Pattern-B-Invariant with one-retry on first failure.
        var validation = OnboardingTurnOutputValidator.Validate(output, currentTopic);
        if (!validation.IsValid)
        {
            LogValidationFailedFirstAttempt(logger, cmd.UserId, currentTopic, validation.Violation);
            var retried = await llm
                .GenerateStructuredAsync<OnboardingTurnOutput>(
                    composition.SystemPrompt,
                    AppendDiscriminatorReinforcement(composition.UserMessage, currentTopic),
                    OnboardingSchema.Frozen,
                    Coaching.Models.CacheControl.Ephemeral1h,
                    ct)
                .ConfigureAwait(false);

            var retryValidation = OnboardingTurnOutputValidator.Validate(retried, currentTopic);
            if (retryValidation.IsValid)
            {
                output = retried;
            }
            else
            {
                // Second failure — emit ClarificationRequested for the
                // current topic and short-circuit the rest of the flow.
                LogValidationFailedSecondAttempt(logger, cmd.UserId, currentTopic, retryValidation.Violation);
                output = BuildDiscriminatorMismatchClarification(currentTopic);
            }
        }

        // (7) stage the per-turn events on the onboarding stream.
        var assistantBlocks = JsonSerializer.SerializeToDocument(output.Reply, WireSerializerOptions);
        var userBlocks = JsonSerializer.SerializeToDocument(BuildUserTextBlocks(cmd.Text), WireSerializerOptions);

        session.Events.Append(streamId, new TopicAsked(currentTopic, now));
        session.Events.Append(streamId, new UserTurnRecorded(userBlocks, now));
        session.Events.Append(streamId, new AssistantTurnRecorded(assistantBlocks, now));

        // (7a) capture the answer when the LLM produced one with sufficient
        //      confidence AND the topic discriminator matches.
        if (output.Extracted is not null
            && output.Extracted.Confidence >= ExtractionConfidenceFloor
            && !output.NeedsClarification)
        {
            var (capturedTopic, payload) = ExtractAnswer(output.Extracted);
            if (payload is not null)
            {
                session.Events.Append(streamId, new AnswerCaptured(capturedTopic, payload, output.Extracted.Confidence, now));
                ApplyAnswerToWorking(working, capturedTopic, output.Extracted);
            }
        }

        if (output.NeedsClarification)
        {
            var reason = string.IsNullOrWhiteSpace(output.ClarificationReason)
                ? "Clarification required"
                : output.ClarificationReason!;
            session.Events.Append(streamId, new ClarificationRequested(currentTopic, reason, now));
            working.OutstandingClarifications = working.OutstandingClarifications
                .Append(currentTopic)
                .Distinct()
                .ToArray();
        }

        // (8) terminal-branch — when the deterministic gate is satisfied AND
        //     the LLM agreed, run plan generation INLINE on this same session.
        var gateSatisfied = OnboardingCompletionGate.IsSatisfied(working);
        OnboardingTurnResponseDto response;
        if (gateSatisfied && output.ReadyForPlan)
        {
            var planId = Guid.NewGuid();
            LogTerminalBranch(logger, cmd.UserId, planId);

            var planEvents = await planGen
                .GeneratePlanAsync(working, cmd.UserId, planId, intent: null, previousPlanId: null, ct)
                .ConfigureAwait(false);

            session.Events.StartStream<RunCoach.Api.Modules.Training.Plan.Models.PlanProjectionDto>(planId, planEvents.ToArray());
            session.Events.Append(streamId, new PlanLinkedToUser(cmd.UserId, planId));
            session.Events.Append(streamId, new OnboardingCompleted(planId, now));

            var (completed, total) = OnboardingCompletionGate.Progress(working);
            response = new OnboardingTurnResponseDto(
                Kind: OnboardingTurnKind.Complete,
                AssistantBlocks: assistantBlocks,
                Topic: null,
                SuggestedInputType: null,
                Progress: new OnboardingProgressDto(completed, total),
                PlanId: planId);
        }
        else
        {
            var nextTopic = OnboardingCompletionGate.NextTopic(working) ?? currentTopic;
            var (completed, total) = OnboardingCompletionGate.Progress(working);

            // When the LLM still needs clarification on the next topic, the
            // canned single/multi/numeric/date control can't carry the
            // free-form follow-up the runner needs to provide (e.g. session
            // minutes for WeeklySchedule, time goal for TargetEvent). Fall
            // back to Text so the runner can answer the assistant's
            // clarifying question; once captured, the gate clears the topic
            // and the next turn re-issues the canonical control.
            var hasOutstandingClarification = working.OutstandingClarifications.Contains(nextTopic);
            var nextInputType = hasOutstandingClarification
                ? SuggestedInputType.Text
                : SuggestInputType(nextTopic);
            response = new OnboardingTurnResponseDto(
                Kind: OnboardingTurnKind.Ask,
                AssistantBlocks: assistantBlocks,
                Topic: nextTopic,
                SuggestedInputType: nextInputType,
                Progress: new OnboardingProgressDto(completed, total),
                PlanId: null);
        }

        // (9) record the idempotency marker LAST so the response we recorded
        //     matches what the caller is about to receive.
        idempotency.Record(cmd.IdempotencyKey, cmd.UserId, response);

        return response;
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

    private static SuggestedInputType SuggestInputType(OnboardingTopic topic) => topic switch
    {
        OnboardingTopic.PrimaryGoal => SuggestedInputType.SingleSelect,
        OnboardingTopic.TargetEvent => SuggestedInputType.Text,
        OnboardingTopic.CurrentFitness => SuggestedInputType.Text,
        OnboardingTopic.WeeklySchedule => SuggestedInputType.MultiSelect,
        OnboardingTopic.InjuryHistory => SuggestedInputType.Text,
        OnboardingTopic.Preferences => SuggestedInputType.Text,
        _ => SuggestedInputType.Text,
    };

    private static (OnboardingTopic Topic, JsonDocument? Payload) ExtractAnswer(ExtractedAnswer extracted)
    {
        return extracted.Topic switch
        {
            // Captured-answer payloads stay PascalCase (default STJ) because they
            // are read back inside the inline OnboardingProjection via
            // `JsonDocument.Deserialize<T>()` whose property matching is the
            // server-default casing. These payloads never reach the wire.
            OnboardingTopic.PrimaryGoal when extracted.NormalizedPrimaryGoal is not null
                => (OnboardingTopic.PrimaryGoal, JsonSerializer.SerializeToDocument(extracted.NormalizedPrimaryGoal)),
            OnboardingTopic.TargetEvent when extracted.NormalizedTargetEvent is not null
                => (OnboardingTopic.TargetEvent, JsonSerializer.SerializeToDocument(extracted.NormalizedTargetEvent)),
            OnboardingTopic.CurrentFitness when extracted.NormalizedCurrentFitness is not null
                => (OnboardingTopic.CurrentFitness, JsonSerializer.SerializeToDocument(extracted.NormalizedCurrentFitness)),
            OnboardingTopic.WeeklySchedule when extracted.NormalizedWeeklySchedule is not null
                => (OnboardingTopic.WeeklySchedule, JsonSerializer.SerializeToDocument(extracted.NormalizedWeeklySchedule)),
            OnboardingTopic.InjuryHistory when extracted.NormalizedInjuryHistory is not null
                => (OnboardingTopic.InjuryHistory, JsonSerializer.SerializeToDocument(extracted.NormalizedInjuryHistory)),
            OnboardingTopic.Preferences when extracted.NormalizedPreferences is not null
                => (OnboardingTopic.Preferences, JsonSerializer.SerializeToDocument(extracted.NormalizedPreferences)),
            _ => (extracted.Topic, null),
        };
    }

    private static void ApplyAnswerToWorking(OnboardingView working, OnboardingTopic topic, ExtractedAnswer extracted)
    {
        switch (topic)
        {
            case OnboardingTopic.PrimaryGoal:
                working.PrimaryGoal = extracted.NormalizedPrimaryGoal;
                break;
            case OnboardingTopic.TargetEvent:
                working.TargetEvent = extracted.NormalizedTargetEvent;
                break;
            case OnboardingTopic.CurrentFitness:
                working.CurrentFitness = extracted.NormalizedCurrentFitness;
                break;
            case OnboardingTopic.WeeklySchedule:
                working.WeeklySchedule = extracted.NormalizedWeeklySchedule;
                break;
            case OnboardingTopic.InjuryHistory:
                working.InjuryHistory = extracted.NormalizedInjuryHistory;
                break;
            case OnboardingTopic.Preferences:
                working.Preferences = extracted.NormalizedPreferences;
                break;
        }

        // Capturing an answer clears any outstanding clarification on that topic.
        if (working.OutstandingClarifications.Contains(topic))
        {
            working.OutstandingClarifications = working.OutstandingClarifications
                .Where(t => t != topic)
                .ToArray();
        }
    }

    private static AnthropicContentBlock[] BuildUserTextBlocks(string text) =>
    [
        new AnthropicContentBlock { Type = AnthropicContentBlockType.Text, Text = text ?? string.Empty },
    ];

    private static string AppendDiscriminatorReinforcement(string baseUserMessage, OnboardingTopic topic)
    {
        // Stronger discriminator instruction appended on the second attempt so
        // the prompt-prefix cache prefix above stays byte-stable. Anthropic's
        // cache hashes everything BEFORE the breakpoint; this tail variation
        // is irrelevant to cache-hit rate.
        return baseUserMessage + System.Environment.NewLine + System.Environment.NewLine
            + PromptPrefix + " RETRY: ensure exactly one Normalized* slot is non-null AND it matches the discriminator. "
            + $"Current topic is {topic}. Set Normalized{topic} (and only Normalized{topic}); leave every other Normalized* slot null. "
            + "Set Extracted.Topic to the same value.";
    }

    private static OnboardingTurnOutput BuildDiscriminatorMismatchClarification(OnboardingTopic topic)
    {
        var reply = new[]
        {
            new AnthropicContentBlock
            {
                Type = AnthropicContentBlockType.Text,
                Text = "I'm having trouble understanding your last reply. Could you rephrase it?",
            },
        };

        _ = topic;
        return new OnboardingTurnOutput
        {
            Reply = reply,
            Extracted = null,
            NeedsClarification = true,
            ClarificationReason = "Discriminator mismatch",
            ReadyForPlan = false,
        };
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Onboarding turn idempotent replay key={IdempotencyKey} user={UserId}")]
    private static partial void LogIdempotentReplay(ILogger logger, Guid idempotencyKey, Guid userId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Onboarding turn terminal branch user={UserId} planId={PlanId}")]
    private static partial void LogTerminalBranch(ILogger logger, Guid userId, Guid planId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Onboarding LLM call failed user={UserId} topic={Topic}")]
    private static partial void LogLlmCallFailed(ILogger logger, Guid userId, OnboardingTopic topic, Exception ex);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Onboarding turn validation failed first attempt user={UserId} topic={Topic} violation={Violation}")]
    private static partial void LogValidationFailedFirstAttempt(
        ILogger logger,
        Guid userId,
        OnboardingTopic topic,
        OnboardingTurnOutputValidationViolation violation);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Onboarding turn validation failed second attempt user={UserId} topic={Topic} violation={Violation}")]
    private static partial void LogValidationFailedSecondAttempt(
        ILogger logger,
        Guid userId,
        OnboardingTopic topic,
        OnboardingTurnOutputValidationViolation violation);
}
