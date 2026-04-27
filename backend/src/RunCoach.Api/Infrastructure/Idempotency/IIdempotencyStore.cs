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
    /// <paramref name="key"/>. Returns <c>null</c> if none was recorded;
    /// otherwise deserializes the stored JSON payload into
    /// <typeparamref name="TResponse"/>.
    /// </summary>
    /// <typeparam name="TResponse">Response shape originally recorded by the
    /// handler. The caller must pass the same type used in <see cref="Record{TResponse}"/>.
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
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    /// <typeparam name="TResponse">Response shape to record. Serialized to a
    /// <see cref="System.Text.Json.JsonDocument"/> on the marker.</typeparam>
    /// <param name="key">Client-supplied idempotency key.</param>
    /// <param name="userId">Owning user id — also the conjoined-tenancy tenant id.</param>
    /// <param name="response">Response payload to memoize.</param>
    void Record<TResponse>(Guid key, Guid userId, TResponse response)
        where TResponse : class;
}
