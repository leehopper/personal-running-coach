using System.Text.Json;
using Marten;

namespace RunCoach.Api.Infrastructure.Idempotency;

/// <summary>
/// Marten-document-backed <see cref="IIdempotencyStore"/>. Both
/// <see cref="SeenAsync{TResponse}"/> and <see cref="Record{TResponse}"/>
/// operate on the caller's scoped <see cref="IDocumentSession"/> so the
/// lookup + record participate in Marten's single transaction alongside any
/// event appends performed in the same Wolverine handler body
/// (DEC-060 / R-069).
/// </summary>
public sealed partial class MartenIdempotencyStore(
    IDocumentSession session,
    TimeProvider timeProvider,
    ILogger<MartenIdempotencyStore> logger) : IIdempotencyStore
{
    private readonly IDocumentSession _session = session;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<MartenIdempotencyStore> _logger = logger;

    /// <inheritdoc />
    public async Task<TResponse?> SeenAsync<TResponse>(Guid key, CancellationToken ct)
        where TResponse : class
    {
        var marker = await _session.LoadAsync<IdempotencyMarker>(key, ct).ConfigureAwait(false);
        if (marker is null)
        {
            return null;
        }

        // Cross-version replay guard: if the recorded payload type no longer
        // matches what the caller asked for, the JSON shapes are not
        // guaranteed to agree. Treat the marker as a miss and let the
        // caller re-execute against the new contract; the stale marker is
        // overwritten on the next Record (or aged out by the sweeper).
        var requestedTypeName = typeof(TResponse).FullName;
        if (!string.Equals(marker.PayloadTypeName, requestedTypeName, StringComparison.Ordinal))
        {
            LogPayloadTypeMismatch(_logger, key, marker.PayloadTypeName, requestedTypeName ?? "<unknown>");
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
    public void Record<TResponse>(Guid key, TResponse response)
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(response);

        // Marten's conjoined tenancy populates the row's tenant_id column
        // from the session, not from the document body. Reading TenantId
        // here keeps the marker's UserId field in lockstep with the actual
        // stored tenant id so the cross-tenant sweeper's GroupBy(m.UserId)
        // never opens a delete session against a tenant the marker is not
        // physically tenanted to. A non-tenanted session ("*DEFAULT*" or
        // empty) is rejected because the idempotency primitive is only
        // meaningful in a multi-tenant context.
        if (!Guid.TryParse(_session.TenantId, out var sessionTenant))
        {
            throw new InvalidOperationException(
                $"IIdempotencyStore.Record requires a tenant-scoped IDocumentSession; got TenantId='{_session.TenantId}'.");
        }

        var typeName = typeof(TResponse).FullName
            ?? throw new InvalidOperationException(
                "IIdempotencyStore.Record cannot record an anonymous or unbounded generic response type.");

        var payload = JsonSerializer.SerializeToDocument(response);
        var marker = new IdempotencyMarker(key, sessionTenant, typeName, payload, _timeProvider.GetUtcNow());
        _session.Insert(marker);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "IdempotencyMarker {Key} payload type '{Recorded}' does not match requested '{Requested}'; treating as miss.")]
    private static partial void LogPayloadTypeMismatch(
        ILogger logger,
        Guid key,
        string recorded,
        string requested);
}
