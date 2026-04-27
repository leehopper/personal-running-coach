using System.Text.Json;
using Marten.Schema;

namespace RunCoach.Api.Infrastructure.Idempotency;

/// <summary>
/// Marten document storing the response of a previously processed idempotent
/// command, keyed by the client-supplied <see cref="Key"/>. Per DEC-060 / R-069
/// this is a Marten document — not an EF table — so the idempotency check and
/// record commit atomically with the events on the handler's injected
/// <c>IDocumentSession</c>. Tenant scoping is handled by Marten's conjoined
/// tenancy (<c>Policies.AllDocumentsAreMultiTenanted()</c> in
/// <c>MartenConfiguration</c>); GDPR delete sweeps the marker via
/// <c>store.Advanced.DeleteAllTenantDataAsync(userId.ToString())</c>
/// (DEC-047 pattern).
/// </summary>
/// <param name="Key">Client-supplied idempotency key. Marten document identity.</param>
/// <param name="UserId">Owning user id — also the tenant id.</param>
/// <param name="Response">The original response payload, serialized as a
/// <see cref="JsonDocument"/> so any caller-defined response shape round-trips
/// without coupling this primitive to a single response type.</param>
/// <param name="RecordedAt">UTC timestamp the response was first recorded;
/// drives the 48h expiry sweep run by <see cref="IdempotencySweeper"/>.</param>
internal sealed record IdempotencyMarker(
    [property: Identity] Guid Key,
    Guid UserId,
    JsonDocument Response,
    DateTimeOffset RecordedAt);
