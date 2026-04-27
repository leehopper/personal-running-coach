using System.Text.Json;
using Marten;

namespace RunCoach.Api.Modules.Coaching.Idempotency;

/// <summary>
/// Marten-document-backed <see cref="IIdempotencyStore"/>. Both
/// <see cref="SeenAsync{TResponse}"/> and <see cref="Record{TResponse}"/>
/// operate on the caller's scoped <see cref="IDocumentSession"/> so the
/// lookup + record participate in Marten's single transaction alongside any
/// event appends performed in the same Wolverine handler body
/// (DEC-060 / R-069).
/// </summary>
public sealed class MartenIdempotencyStore(IDocumentSession session) : IIdempotencyStore
{
    private readonly IDocumentSession _session = session;

    /// <inheritdoc />
    public async Task<TResponse?> SeenAsync<TResponse>(Guid key, CancellationToken ct)
        where TResponse : class
    {
        var marker = await _session.LoadAsync<IdempotencyMarker>(key, ct).ConfigureAwait(false);
        if (marker is null)
        {
            return null;
        }

        return marker.Response.Deserialize<TResponse>();
    }

    /// <inheritdoc />
    public void Record<TResponse>(Guid key, Guid userId, TResponse response)
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(response);

        // Serialize to a JsonDocument then extract the root element as a JsonElement.
        // JsonElement is a value type that does not own pooled buffers; the owning
        // JsonDocument is disposed immediately after extraction, returning its
        // PooledByteBufferWriter to the pool. The cloned element remains valid
        // because Clone() copies the bytes into an independent allocation.
        using var doc = JsonSerializer.SerializeToDocument(response);
        var payload = doc.RootElement.Clone();
        var marker = new IdempotencyMarker(key, userId, payload, DateTimeOffset.UtcNow);
        _session.Store(marker);
    }
}
