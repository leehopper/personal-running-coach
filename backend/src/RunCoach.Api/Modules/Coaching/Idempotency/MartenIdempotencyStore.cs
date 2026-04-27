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
    /// <remarks>
    /// Uses <c>Insert</c> rather than <c>Store</c>: the marker is the
    /// canonical "first response wins" record, so a duplicate key must
    /// surface a Marten <c>DocumentAlreadyExistsException</c> instead of
    /// silently upserting the second writer's payload over the first.
    /// </remarks>
    public void Record<TResponse>(Guid key, Guid userId, TResponse response)
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(response);

        var payload = JsonSerializer.SerializeToDocument(response);
        var marker = new IdempotencyMarker(key, userId, payload, DateTimeOffset.UtcNow);
        _session.Insert(marker);
    }
}
