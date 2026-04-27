namespace RunCoach.Api.Modules.Coaching.Idempotency;

/// <summary>
/// Projection target used by <see cref="IdempotencySweeper"/> when scanning
/// across tenants for expired markers. Carries the minimum identity needed
/// to issue the per-tenant delete (the marker key) plus the tenant id used
/// to open the conjoined-tenancy delete session.
/// </summary>
internal sealed record ExpiredMarker(Guid Key, Guid UserId);
