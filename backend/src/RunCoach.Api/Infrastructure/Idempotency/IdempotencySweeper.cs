using Marten;
using MartenSessionOptions = Marten.Services.SessionOptions;

namespace RunCoach.Api.Infrastructure.Idempotency;

/// <summary>
/// Background sweeper that deletes <see cref="IdempotencyMarker"/> documents
/// older than the 48h retention window. Runs every hour while the host is
/// alive. Uses an <c>AllowAnyTenant</c> session because conjoined-tenancy
/// scopes ordinary sessions to a single tenant — the sweeper has to span
/// every tenant on the database.
/// </summary>
/// <remarks>
/// Construction takes the singleton <see cref="IDocumentStore"/> rather than
/// an injected <c>IDocumentSession</c> because <see cref="IHostedService"/>
/// instances are singletons and Marten sessions are scoped. A scoped session
/// resolved from the root provider would leak the underlying connection
/// across iterations; opening a fresh session per sweep is the documented
/// pattern.
/// </remarks>
internal sealed partial class IdempotencySweeper(
    IDocumentStore store,
    TimeProvider timeProvider,
    ILogger<IdempotencySweeper> logger) : BackgroundService
{
    /// <summary>Markers older than this are eligible for deletion.</summary>
    internal static readonly TimeSpan RetentionWindow = TimeSpan.FromHours(48);

    /// <summary>Interval between sweeps.</summary>
    internal static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    private readonly IDocumentStore _store = store;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<IdempotencySweeper> _logger = logger;

    /// <summary>
    /// Deletes <see cref="IdempotencyMarker"/> documents whose
    /// <see cref="IdempotencyMarker.RecordedAt"/> is older than
    /// <see cref="RetentionWindow"/>. Internal so test harnesses can drive
    /// a sweep deterministically without waiting for the timer.
    /// </summary>
    /// <remarks>
    /// Conjoined tenancy makes <c>DeleteWhere</c> tenant-scoped and offers no
    /// <c>AnyTenant</c> overload. The cross-tenant sweep therefore (a) queries
    /// expired markers across every tenant via <see cref="LinqExtensions.AnyTenant{T}"/>,
    /// (b) groups them by tenant id, and (c) issues one tenant-scoped delete
    /// session per group. The number of expired markers per sweep is bounded
    /// (idempotency keys are per-request and the window is 48h), so the
    /// per-tenant fan-out is acceptable.
    /// </remarks>
    internal async Task SweepAsync(CancellationToken ct)
    {
        var cutoff = _timeProvider.GetUtcNow() - RetentionWindow;

        // Cross-tenant scan to find all expired markers along with their owning tenant id.
        IReadOnlyList<ExpiredMarker> expired;
        await using (var scanSession = _store.QuerySession(new MartenSessionOptions
        {
            AllowAnyTenant = true,
        }))
        {
            expired = await scanSession.Query<IdempotencyMarker>()
                .Where(m => m.AnyTenant() && m.RecordedAt < cutoff)
                .Select(m => new ExpiredMarker(m.Key, m.UserId))
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        if (expired.Count == 0)
        {
            LogSweepCompleted(_logger, cutoff, 0);
            return;
        }

        // Per-tenant deletes — conjoined tenancy requires the delete session to
        // know which tenant it's writing against.
        foreach (var tenantGroup in expired.GroupBy(m => m.UserId))
        {
            await using var deleteSession = _store.LightweightSession(tenantGroup.Key.ToString());
            foreach (var marker in tenantGroup)
            {
                deleteSession.Delete<IdempotencyMarker>(marker.Key);
            }

            await deleteSession.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        LogSweepCompleted(_logger, cutoff, expired.Count);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // First sweep runs immediately on host boot rather than waiting
        // SweepInterval before the first cleanup pass.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host is shutting down — exit cleanly.
                return;
            }
#pragma warning disable CA1031 // Sweeper must keep running across transient DB errors.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogSweepFailed(_logger, ex);
            }

            try
            {
                await Task.Delay(SweepInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "IdempotencyMarker sweep completed; deleted {DeletedCount} markers older than {Cutoff:O}")]
    private static partial void LogSweepCompleted(ILogger logger, DateTimeOffset cutoff, int deletedCount);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "IdempotencyMarker sweep failed; will retry on next interval")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}
