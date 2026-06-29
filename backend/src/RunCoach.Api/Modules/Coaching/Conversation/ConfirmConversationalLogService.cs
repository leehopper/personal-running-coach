using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Workouts;
using Wolverine;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Default <see cref="IConfirmConversationalLogService"/> (Slice 4B PR5). Reuses three shipped
/// seams verbatim — <see cref="StructuredLogDraftMapper"/>, <see cref="IWorkoutLogService.CreateAsync"/>
/// (EF-native idempotent, DEC-077), and the shared <see cref="IAdaptationEvaluationDispatcher"/> —
/// then composes a short gruff-direct ack via <see cref="ICoachingLlm.GenerateAsync"/> (DEC-084
/// voice via <c>coaching-system.v1</c>, trademark-scrubbed by the adapter) and persists it as a
/// coach turn AFTER the adaptation has committed, so the ack never preempts a safety referral. The
/// EF-row idempotency key, the conversation client message id, and the adaptation
/// <c>WorkoutLogId</c> marker are three deliberately distinct idempotency mechanisms.
/// </summary>
public sealed partial class ConfirmConversationalLogService(
    IWorkoutLogService workoutLogService,
    IAdaptationEvaluationDispatcher adaptationDispatcher,
    IContextAssembler contextAssembler,
    ICoachingLlm llm,
    IMessageBus bus,
    ILogger<ConfirmConversationalLogService> logger) : IConfirmConversationalLogService
{
    /// <inheritdoc />
    public async Task<ConfirmConversationalLogResponseDto> ConfirmAsync(
        Guid userId,
        ConfirmConversationalLogRequestDto request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The EF-row idempotency key is DERIVED from the card's client message id (DEC-077): a
        // double-confirm replays the same key and commits exactly one log. It is deliberately
        // distinct from the conversation client id and the adaptation WorkoutLogId marker.
        var efIdempotencyKey = ConversationTurnId.DeriveWorkoutLogIdempotencyKey(request.ClientMessageId);
        var createRequest = StructuredLogDraftMapper.ToCreateWorkoutLogRequest(request.Draft, efIdempotencyKey);

        // (1) Commit the log through the unchanged create path (resolves the prescription
        //     server-side; off-plan -> null). (2) Run the identical post-create adaptation seam.
        var workoutLogId = await workoutLogService.CreateAsync(userId, createRequest, ct).ConfigureAwait(false);
        var adaptation = await adaptationDispatcher.EvaluateAsync(workoutLogId, userId, ct).ConfigureAwait(false);

        // (3) Persist the ack AFTER the adaptation has committed its proactive turns (incl. any
        //     Amber referral, DEC-081), so it never preempts or contradicts a safety referral.
        await PersistAckAsync(userId, request, adaptation, ct).ConfigureAwait(false);

        return new ConfirmConversationalLogResponseDto(workoutLogId, adaptation);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Acknowledgment generation failed after a confirmed conversational log committed and adapted; a scripted ack was persisted instead.")]
    private static partial void LogAckGenerationFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Persisting the acknowledgment turn failed for user {UserId} after the log committed and adapted; the confirm still succeeds and the ack recovers on a re-confirm.")]
    private static partial void LogAckPersistenceFailed(ILogger logger, Guid userId, Exception exception);

    private async Task PersistAckAsync(
        Guid userId,
        ConfirmConversationalLogRequestDto request,
        AdaptationResponseDto adaptation,
        CancellationToken ct)
    {
        // The log committed and the adaptation already ran; the ack is best-effort. A failure
        // composing or appending it (a prompt-store I/O blip, a transient Marten/Npgsql append
        // error) must NOT fail the confirm — the committed log always wins (the contract). It is
        // recovered on a re-confirm: the idempotent create + adaptation replay, then a fresh ack.
        // A genuine client abort (OperationCanceledException) is excluded so it still surfaces.
        try
        {
            var ackContent = await ComposeAckContentAsync(request.Draft, adaptation, ct).ConfigureAwait(false);

            // Idempotent on the server-derived coach turn id (DeriveCoachTurnId(clientMessageId)) —
            // a re-confirm re-derives the same id and the append short-circuits.
            await bus.InvokeForTenantAsync<ConversationTurnPostedResponse>(
                    userId.ToString(),
                    new PostCoachConversationTurn(userId, request.ClientMessageId, ackContent, IsErrored: false),
                    ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAckPersistenceFailed(logger, userId, ex);
        }
    }

    private async Task<string> ComposeAckContentAsync(
        StructuredLogDraft draft,
        AdaptationResponseDto adaptation,
        CancellationToken ct)
    {
        // A terminal review failure (or lost race) rode back as Kind=Error: the review already
        // failed, so do NOT call the LLM again — surface the scripted "saved; retrying" ack.
        if (adaptation.Kind != AdaptationResponseKind.Adapted || adaptation.AdaptationKind is not { } kind)
        {
            return ConversationAckScripts.SavedReviewRetrying;
        }

        try
        {
            var composition = await contextAssembler.ComposeForAckAsync(draft, kind, ct).ConfigureAwait(false);
            return await llm.GenerateAsync(composition.SystemPrompt, composition.UserMessage, ct).ConfigureAwait(false);
        }
        catch (CoachingLlmException ex)
        {
            // The log committed and the plan adapted; only the ack generation failed. Fall back to
            // a scripted ack rather than failing the confirm (the save already succeeded).
            LogAckGenerationFailed(logger, ex);
            return ConversationAckScripts.AckUnavailable;
        }
    }
}
