using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Options;
using RunCoach.Api.Modules.Coaching.Sanitization;
using RunCoach.Api.Modules.Training.Safety;
using Wolverine;

namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// Default <see cref="IConversationStreamService"/> (Slice 4B PR4). Mirrors the
/// <c>EvaluateAdaptationHandler</c> orchestration discipline for the streaming surface:
/// deterministic safety gate before any LLM call, scripted safety turns routed by
/// category (never LLM prose), the LLM proposing while the deterministic pipeline
/// disposes. Reads run on a per-request tenanted Marten session; every write goes through
/// a tenant-scoped Wolverine handler (<c>InvokeForTenantAsync</c>) so the idempotency
/// markers commit co-transactionally with the events.
/// </summary>
public sealed partial class ConversationStreamService(
    IMessageBus bus,
    IPromptSanitizer sanitizer,
    ISafetyGate safetyGate,
    IMessageIntentClassifier classifier,
    IContextAssembler contextAssembler,
    ICoachingLlm llm,
    IConversationContextLoader contextLoader,
    IOptions<ConversationStreamOptions> options,
    ILogger<ConversationStreamService> logger) : IConversationStreamService
{
    private const string ClassifierFailedMessage =
        "I had trouble reading your message just now. Send it again and I'll pick it up.";

    private const string AnswerTransientMessage =
        "Something cut my reply short. Give it another go in a moment.";

    private const string AnswerPermanentMessage =
        "I couldn't finish that reply. Try rephrasing it and sending again.";

    private const string AnswerIncompleteMessage =
        "That answer got cut off before I finished. Send it again and I'll give you the whole thing.";

    private readonly ConversationStreamOptions _options = options.Value;

    /// <inheritdoc />
    public IAsyncEnumerable<IConversationFrame> StreamReplyAsync(
        Guid userId,
        string message,
        Guid clientMessageId,
        CancellationToken ct)
    {
        // Eager argument validation must fire on the call, not deferred to first
        // enumeration — so it lives here and the iterator body is a separate method (S4456),
        // mirroring ClaudeCoachingLlm.StreamAsync.
        ArgumentNullException.ThrowIfNull(message);
        return StreamReplyCore(userId, message, clientMessageId, ct);
    }

    private async IAsyncEnumerable<IConversationFrame> StreamReplyCore(
        Guid userId,
        string message,
        Guid clientMessageId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // (1) Persist the user turn durably FIRST, before the gate or any stream, so a
        //     crash after this point leaves a recoverable record. Idempotent on the client id.
        await PersistUserTurnAsync(userId, clientMessageId, message, ct).ConfigureAwait(false);

        // (2) Deterministic safety gate on a SEPARATELY-sanitized copy (the DEC-059
        //     boundary). The classifier and answer assembly each sanitize their own copy
        //     from the raw message; this gate copy is normalized (Unicode-stripped) so
        //     keyword detection cannot be evaded by zero-width splitting. The spotlight
        //     wrapper is benign for keyword matching.
        var gateText = (await sanitizer
            .SanitizeAsync(message, PromptSection.CurrentUserMessage, ct)
            .ConfigureAwait(false)).Sanitized;
        var safety = safetyGate.Classify(gateText, metrics: null);

        // (3) Red short-circuits to scripted resources — no classifier, no LLM. Safety is
        //     never left to LLM self-policing (DEC-019/DEC-030/DEC-079).
        if (safety.Tier == SafetyTier.Red)
        {
            var crisis = safety.Category == ReferralCategory.Crisis
                ? CrisisResponseContent.CrisisResponse
                : EmergencyResponseContent.EmergencyResponse;
            yield return new SafetyFrame(crisis, SafetyTier.Red, safety.Category);
            LogRedShortCircuit(logger, userId, safety.Category);
            var crisisTurnId = await PersistCoachTurnAsync(userId, clientMessageId, crisis, isErrored: false, ct)
                .ConfigureAwait(false);
            yield return new DoneFrame(crisisTurnId);
            yield break;
        }

        // (4) Amber surfaces the scripted referral BEFORE the answer (lower event version
        //     ⇒ the timeline renders it no later than the answer), then the coach still
        //     answers — for Q&A "Amber" has no plan load to clamp. Persisted as a coach
        //     turn with a distinct derived id so it never collides with the answer turn.
        if (safety.Tier == SafetyTier.Amber)
        {
            var referral = safety.Category == ReferralCategory.Injury
                ? AmberReferralContent.InjuryReferral
                : AmberReferralContent.RedSReferral;
            yield return new SafetyFrame(referral, SafetyTier.Amber, safety.Category);
            await PersistSafetyReferralAsync(userId, clientMessageId, referral, ct).ConfigureAwait(false);
            LogAmberReferral(logger, userId, safety.Category);
        }

        // (5) Classify intent. A classifier failure surfaces an error frame and never
        //     guesses intent (D3 fail-closed); nothing beyond the durable user turn persists.
        MessageIntentOutput? intent = null;
        ErrorFrame? classifyError = null;
        try
        {
            intent = await classifier.ClassifyAsync(contextLoader.Today(), message, ct).ConfigureAwait(false);
        }
        catch (CoachingLlmException ex)
        {
            LogClassifierFailed(logger, userId, ex);
            classifyError = ToErrorFrame(ex, ClassifierFailedMessage);
        }

        if (classifyError is not null)
        {
            yield return classifyError;
            yield break;
        }

        // (6) Route on intent.
        switch (intent!.Intent)
        {
            case MessageIntent.WorkoutLog:
                // Server-authoritative candidate prescription (never LLM-extracted); the
                // card commits nothing — the plan-mutating write waits for an explicit
                // Confirm (PR5). A null match renders the off-plan card; Confirm still commits.
                var snapshot = await contextLoader
                    .ResolveCandidatePrescriptionAsync(userId, intent.WorkoutLog!.OccurredOn, ct)
                    .ConfigureAwait(false);
                LogWorkoutCard(logger, userId, snapshot is not null);
                yield return new CardFrame(intent.WorkoutLog!, CandidatePrescriptionDto.FromSnapshot(snapshot));
                yield break;

            case MessageIntent.Ambiguous:
                // Ask rather than guess — never silently route to a log.
                yield return new TokenFrame(ConversationScripts.Clarification);
                var clarifyTurnId = await PersistCoachTurnAsync(
                        userId, clientMessageId, ConversationScripts.Clarification, isErrored: false, ct)
                    .ConfigureAwait(false);
                yield return new DoneFrame(clarifyTurnId);
                yield break;

            default:
                await foreach (var frame in StreamAnswerAsync(userId, message, clientMessageId, ct)
                                   .ConfigureAwait(false))
                {
                    yield return frame;
                }

                yield break;
        }
    }

    /// <summary>
    /// The Green/Amber Question path: assemble grounded Q&amp;A context and stream the
    /// answer through the adapter. The SDK enumerator is driven MANUALLY (MoveNextAsync
    /// inside try/catch, yield outside) per the C# yield-in-try constraint and the R-084
    /// taxonomy: a client abort persists nothing and emits no frame; a mid/post-stream
    /// failure discards the partial, persists an errored marker (never a partial as
    /// complete), and emits an error frame; a clean finish persists the full answer and
    /// emits the terminal done frame carrying the reconcile id.
    /// </summary>
    private async IAsyncEnumerable<IConversationFrame> StreamAnswerAsync(
        Guid userId,
        string message,
        Guid clientMessageId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var context = await contextLoader.LoadAnswerContextAsync(userId, clientMessageId, ct).ConfigureAwait(false);
        var composition = await contextAssembler
            .ComposeForConversationAsync(context.Plan, context.RecentLogs, context.RecentTurns, message, ct)
            .ConfigureAwait(false);

        var buffer = new StringBuilder();
        ErrorFrame? streamError = null;
        var aborted = false;

        await using var enumerator = llm
            .StreamAsync(composition.SystemPrompt, composition.UserMessage, ct)
            .GetAsyncEnumerator(ct);

        while (true)
        {
            string delta;
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    break;
                }

                delta = enumerator.Current;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Client disconnected mid-stream: not a fault. Persist nothing beyond the
                // durable user turn, emit no error frame, do not 500.
                aborted = true;
                break;
            }
            catch (IncompleteCoachingLlmException ex)
            {
                // A truncated free-text reply is incomplete, not a complete turn.
                streamError = new ErrorFrame(AnswerIncompleteMessage, ex.Retryable, null);
                break;
            }
            catch (TransientCoachingLlmException ex)
            {
                streamError = new ErrorFrame(
                    AnswerTransientMessage, Retryable: true, ex.RetryAfterSeconds ?? _options.DefaultMidStreamRetryAfterSeconds);
                break;
            }
            catch (PermanentCoachingLlmException)
            {
                streamError = new ErrorFrame(AnswerPermanentMessage, Retryable: false, RetryAfterSeconds: null);
                break;
            }
            catch (CoachingLlmException)
            {
                // Totality backstop for any future subtype — treat as non-retryable.
                streamError = new ErrorFrame(AnswerPermanentMessage, Retryable: false, RetryAfterSeconds: null);
                break;
            }

            buffer.Append(delta);
            yield return new TokenFrame(delta);
        }

        if (aborted)
        {
            yield break;
        }

        if (streamError is not null)
        {
            // Discard the partial; persist an explicit errored marker (never a partial as
            // complete). The client re-sends with a fresh client message id (D5).
            await PersistCoachTurnAsync(userId, clientMessageId, content: string.Empty, isErrored: true, ct)
                .ConfigureAwait(false);
            LogAnswerStreamFailed(logger, userId, streamError.Retryable);
            yield return streamError;
            yield break;
        }

        var answerTurnId = await PersistCoachTurnAsync(userId, clientMessageId, buffer.ToString(), isErrored: false, ct)
            .ConfigureAwait(false);
        yield return new DoneFrame(answerTurnId);
    }

    private async Task PersistUserTurnAsync(Guid userId, Guid clientMessageId, string message, CancellationToken ct) =>
        await bus.InvokeForTenantAsync<ConversationTurnPostedResponse>(
                userId.ToString(),
                new PostUserConversationTurn(userId, clientMessageId, message),
                ct)
            .ConfigureAwait(false);

    private async Task<Guid> PersistCoachTurnAsync(
        Guid userId, Guid clientMessageId, string content, bool isErrored, CancellationToken ct)
    {
        var response = await bus.InvokeForTenantAsync<ConversationTurnPostedResponse>(
                userId.ToString(),
                new PostCoachConversationTurn(userId, clientMessageId, content, isErrored, LoggedRun: null),
                ct)
            .ConfigureAwait(false);
        return response.TurnId;
    }

    private async Task PersistSafetyReferralAsync(Guid userId, Guid clientMessageId, string referral, CancellationToken ct) =>
        await bus.InvokeForTenantAsync<ConversationTurnPostedResponse>(
                userId.ToString(),
                new PostScriptedSafetyTurn(userId, ConversationTurnId.DeriveSafetyTurnId(clientMessageId), referral),
                ct)
            .ConfigureAwait(false);

    private ErrorFrame ToErrorFrame(CoachingLlmException ex, string userMessage) => ex switch
    {
        TransientCoachingLlmException t => new ErrorFrame(
            userMessage, Retryable: true, t.RetryAfterSeconds ?? _options.DefaultMidStreamRetryAfterSeconds),
        _ => new ErrorFrame(userMessage, Retryable: false, RetryAfterSeconds: null),
    };
}
