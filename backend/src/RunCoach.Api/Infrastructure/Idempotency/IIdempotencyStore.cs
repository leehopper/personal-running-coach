namespace RunCoach.Api.Infrastructure.Idempotency;

/// <summary>
/// Idempotency primitive used by Wolverine handlers (per-turn, regenerate, and
/// Slice 4's open-conversation endpoint) to short-circuit duplicate command
/// submissions. Implementations operate on the caller's injected Marten
/// <c>IDocumentSession</c> so the lookup + write commit atomically with the
/// events being appended in the same handler — see DEC-060 / R-069.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Looks up a previously recorded response for the given idempotency
    /// <paramref name="key"/>. Returns <c>null</c> if no marker is recorded
    /// OR if the recorded marker's payload type no longer matches
    /// <typeparamref name="TResponse"/> (cross-version replay protection);
    /// otherwise deserializes the stored JSON payload.
    /// </summary>
    /// <typeparam name="TResponse">Response shape originally recorded by the
    /// handler. Must match the type used in <see cref="Record{TResponse}"/>;
    /// implementations validate this against the marker's recorded payload
    /// type and treat a mismatch as a cache miss.
    /// </typeparam>
    /// <param name="key">Client-supplied idempotency key.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<TResponse?> SeenAsync<TResponse>(Guid key, CancellationToken ct)
        where TResponse : class;

    /// <summary>
    /// Stages an <c>IdempotencyMarker</c> document on the active Marten
    /// session. The session is committed by Wolverine's transactional
    /// middleware on handler exit, so the marker persists atomically with
    /// any events appended in the same handler body. Does not call
    /// <c>SaveChangesAsync</c>. The marker's owning tenant id is sourced
    /// from the session's <c>TenantId</c> rather than a caller parameter,
    /// so the body field cannot disagree with Marten's tenant column.
    /// </summary>
    /// <typeparam name="TResponse">Response shape to record. Serialized to a
    /// <see cref="System.Text.Json.JsonDocument"/> on the marker; the
    /// type's assembly-qualified name is also stored so subsequent
    /// <see cref="SeenAsync{TResponse}"/> calls can refuse cross-version
    /// replays.</typeparam>
    /// <param name="key">Client-supplied idempotency key.</param>
    /// <param name="response">Response payload to memoize.</param>
    void Record<TResponse>(Guid key, TResponse response)
        where TResponse : class;
}
