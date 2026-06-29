using JasperFx;
using JasperFx.Events;
using RunCoach.Api.Modules.Coaching.Adaptation;
using Wolverine;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Default <see cref="IAdaptationEvaluationDispatcher"/>. <c>InvokeForTenantAsync</c> sets the
/// user id as Marten's conjoined tenant on the handler's auto-applied <c>IDocumentSession</c> —
/// without it the handler's session has no TenantId and its multi-tenant document loads fail to
/// resolve.
/// </summary>
public sealed partial class AdaptationEvaluationDispatcher(
    IMessageBus bus,
    ILogger<AdaptationEvaluationDispatcher> logger) : IAdaptationEvaluationDispatcher
{
    /// <inheritdoc />
    public async Task<AdaptationResponseDto> EvaluateAsync(Guid workoutLogId, Guid userId, CancellationToken ct)
    {
        try
        {
            return await bus.InvokeForTenantAsync<AdaptationResponseDto>(
                userId.ToString(),
                new EvaluateAdaptationCommand(workoutLogId, userId),
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is EventStreamUnexpectedMaxEventIdException or DocumentAlreadyExistsException)
        {
            // A lost adaptation race a bounded number of retries could not resolve escapes here.
            // The log row is already committed, so the caller must still answer success — the
            // conflict maps to a generic retryable Kind=Error envelope instead of a 5xx, and the
            // client's "try again" replays the winner's committed adaptation via the idempotency
            // marker.
            //
            // Two surfaces are caught, both meaning "the other evaluation won":
            // - `EventStreamUnexpectedMaxEventIdException` (a `JasperFx.ConcurrencyException`):
            //   the event-appending paths' lost Rich-append-mode race — Marten transforms the
            //   loser's (stream id, version) unique violation into this type. The chain-scoped
            //   retry normally replays the winner's marker instead, so this only fires when the
            //   conflict outlives the bounded retries.
            // - `DocumentAlreadyExistsException`: the marker-only paths (off-plan no-op, L0
            //   absorb, Red short-circuit) stage just the WorkoutLogId-keyed marker; a concurrent
            //   duplicate's `Insert` loses the race and surfaces this. It is NOT retried by the
            //   chain rule (keyed solely on the stream-version exception), so it would otherwise
            //   5xx the caller.
            //
            // `Marten.Exceptions.ConcurrentUpdateException` is deliberately NOT caught: the
            // signal-state document has no optimistic concurrency (its Store is last-write-wins),
            // so that surface is unreachable for this command — a catch for it would be dead code.
            LogAdaptationDispatchConflicted(logger, workoutLogId, userId, ex);
            return new AdaptationResponseDto
            {
                Kind = AdaptationResponseKind.Error,
                ErrorMessage = "Your workout was saved, but your coach could not review it just now. Submitting the same log again will retry the review.",
                Retryable = true,
            };
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Adaptation evaluation conflicted after bounded retries for workout log {WorkoutLogId}, user {UserId}; the committed log still succeeds with a retryable error envelope.")]
    private static partial void LogAdaptationDispatchConflicted(
        ILogger logger, Guid workoutLogId, Guid userId, Exception exception);
}
