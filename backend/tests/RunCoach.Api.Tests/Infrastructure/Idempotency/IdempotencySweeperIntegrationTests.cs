using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Tests.Infrastructure;
using MartenSessionOptions = Marten.Services.SessionOptions;

namespace RunCoach.Api.Tests.Infrastructure.Idempotency;

/// <summary>
/// Integration coverage for <see cref="IdempotencySweeper"/>. Asserts the
/// host registration, the DI lifetime, and that <c>SweepAsync</c> deletes
/// markers older than the 48h retention window across every tenant.
/// </summary>
[Trait("Category", "Integration")]
public class IdempotencySweeperIntegrationTests(RunCoachAppFactory factory) : DbBackedIntegrationTestBase(factory)
{
    [Fact]
    public void Sweeper_Is_Registered_As_HostedService_By_Production_Wiring()
    {
        // Arrange — exercise AddApplicationModules directly against a fresh
        // ServiceCollection. The SUT factory cannot witness this because it
        // removes the hosted-service descriptor in ConfigureWebHost (the
        // production wall-clock TimeProvider would race the FakeTimeProvider
        // tests use). Verifying against a separate collection isolates the
        // production-registration assertion from that test-fixture override.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddApplicationModules(configuration);

        // Assert
        var sweeperDescriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(IHostedService)
                && d.ImplementationType == typeof(IdempotencySweeper));

        sweeperDescriptor.Should().NotBeNull(
            because: "AddApplicationModules must register IdempotencySweeper as an IHostedService for production hosts to run it");
    }

    [Fact]
    public async Task SweepAsync_Deletes_Markers_Older_Than_RetentionWindow()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero));
        var tenantId = Guid.NewGuid();

        var oldKey = Guid.NewGuid();
        var freshKey = Guid.NewGuid();
        var oldRecordedAt = time.GetUtcNow() - TimeSpan.FromHours(49); // older than 48h
        var freshRecordedAt = time.GetUtcNow() - TimeSpan.FromHours(2);

        await using (var seedSession = store.LightweightSession(tenantId.ToString()))
        {
            seedSession.Store(new IdempotencyMarker(
                oldKey,
                tenantId,
                "sweeper-test",
                JsonSerializer.SerializeToDocument(new { kind = "old" }),
                oldRecordedAt));
            seedSession.Store(new IdempotencyMarker(
                freshKey,
                tenantId,
                "sweeper-test",
                JsonSerializer.SerializeToDocument(new { kind = "fresh" }),
                freshRecordedAt));
            await seedSession.SaveChangesAsync(ct);
        }

        var sweeper = new IdempotencySweeper(store, time, NullLogger<IdempotencySweeper>.Instance);

        // Act
        await sweeper.SweepAsync(ct);

        // Assert — fresh marker remains, expired marker is gone.
        await using var verify = store.LightweightSession(tenantId.ToString());
        var actualOld = await verify.LoadAsync<IdempotencyMarker>(oldKey, ct);
        var actualFresh = await verify.LoadAsync<IdempotencyMarker>(freshKey, ct);

        actualOld.Should().BeNull(because: "marker recorded 49h ago is past the 48h retention window");
        actualFresh.Should().NotBeNull(because: "marker recorded 2h ago is inside the retention window");
    }

    [Fact]
    public async Task SweepAsync_Deletes_Expired_Markers_Across_All_Tenants()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero));
        var expiredAt = time.GetUtcNow() - TimeSpan.FromHours(50);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var keyA = Guid.NewGuid();
        var keyB = Guid.NewGuid();

        await using (var sessionA = store.LightweightSession(tenantA.ToString()))
        {
            sessionA.Store(new IdempotencyMarker(
                keyA,
                tenantA,
                "sweeper-test",
                JsonSerializer.SerializeToDocument(new { tenant = "A" }),
                expiredAt));
            await sessionA.SaveChangesAsync(ct);
        }

        await using (var sessionB = store.LightweightSession(tenantB.ToString()))
        {
            sessionB.Store(new IdempotencyMarker(
                keyB,
                tenantB,
                "sweeper-test",
                JsonSerializer.SerializeToDocument(new { tenant = "B" }),
                expiredAt));
            await sessionB.SaveChangesAsync(ct);
        }

        var sweeper = new IdempotencySweeper(store, time, NullLogger<IdempotencySweeper>.Instance);

        // Act
        await sweeper.SweepAsync(ct);

        // Assert — both tenants' expired markers are gone.
        await using (var verifyA = store.LightweightSession(tenantA.ToString()))
        {
            var actual = await verifyA.LoadAsync<IdempotencyMarker>(keyA, ct);
            actual.Should().BeNull(because: "sweep must run cross-tenant via AllowAnyTenant");
        }

        await using (var verifyB = store.LightweightSession(tenantB.ToString()))
        {
            var actual = await verifyB.LoadAsync<IdempotencyMarker>(keyB, ct);
            actual.Should().BeNull(because: "sweep must run cross-tenant via AllowAnyTenant");
        }
    }

    [Fact]
    public async Task SweepAsync_Marker_Recorded_Exactly_At_Cutoff_Is_Not_Deleted()
    {
        // Arrange — a marker whose RecordedAt equals the sweep cutoff exactly
        // pins the strict-less-than predicate (`m.RecordedAt < cutoff`) used by
        // IdempotencySweeper. A `<=` regression would silently delete the
        // boundary marker; this fact catches that.
        var ct = TestContext.Current.CancellationToken;
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero));
        var tenantId = Guid.NewGuid();
        var key = Guid.NewGuid();
        var cutoff = time.GetUtcNow() - IdempotencySweeper.RetentionWindow;

        await using (var seedSession = store.LightweightSession(tenantId.ToString()))
        {
            seedSession.Store(new IdempotencyMarker(
                key,
                tenantId,
                "sweeper-test",
                JsonSerializer.SerializeToDocument(new { kind = "boundary" }),
                cutoff));
            await seedSession.SaveChangesAsync(ct);
        }

        var sweeper = new IdempotencySweeper(store, time, NullLogger<IdempotencySweeper>.Instance);

        // Act
        await sweeper.SweepAsync(ct);

        // Assert — boundary marker survives.
        await using var verify = store.LightweightSession(tenantId.ToString());
        var actual = await verify.LoadAsync<IdempotencyMarker>(key, ct);
        actual.Should().NotBeNull(
            because: "the sweep predicate is strictly `RecordedAt < cutoff`; a marker recorded exactly at the cutoff must survive");
    }

    [Fact]
    public async Task SweepAsync_With_No_Expired_Markers_Is_NoOp()
    {
        // Arrange — empty store. Exercises the early-return path in
        // IdempotencySweeper.SweepAsync where an empty expired-list short-
        // circuits the per-tenant delete fan-out.
        var ct = TestContext.Current.CancellationToken;
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero));
        var sweeper = new IdempotencySweeper(store, time, NullLogger<IdempotencySweeper>.Instance);

        // Act
        var act = async () => await sweeper.SweepAsync(ct);

        // Assert — no exceptions and no markers exist.
        await act.Should().NotThrowAsync(
            because: "an empty store must complete the sweep cleanly via the early-return path");

        await using var verify = store.QuerySession(new MartenSessionOptions { AllowAnyTenant = true });
        var remaining = await verify.Query<IdempotencyMarker>()
            .Where(m => m.AnyTenant())
            .CountAsync(ct);
        remaining.Should().Be(0, because: "no markers were ever seeded");
    }

    [Fact]
    public async Task SweepAsync_Twice_In_A_Row_Is_Idempotent()
    {
        // Arrange — one expired marker. After the first sweep it is gone; a
        // second sweep must be a clean no-op (no exceptions, store unchanged).
        var ct = TestContext.Current.CancellationToken;
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero));
        var tenantId = Guid.NewGuid();
        var key = Guid.NewGuid();
        var expiredAt = time.GetUtcNow() - TimeSpan.FromHours(50);

        await using (var seedSession = store.LightweightSession(tenantId.ToString()))
        {
            seedSession.Store(new IdempotencyMarker(
                key,
                tenantId,
                "sweeper-test",
                JsonSerializer.SerializeToDocument(new { kind = "expired" }),
                expiredAt));
            await seedSession.SaveChangesAsync(ct);
        }

        var sweeper = new IdempotencySweeper(store, time, NullLogger<IdempotencySweeper>.Instance);

        // Act — first sweep deletes; second sweep must be a clean no-op.
        await sweeper.SweepAsync(ct);
        var secondSweep = async () => await sweeper.SweepAsync(ct);

        // Assert
        await secondSweep.Should().NotThrowAsync(
            because: "back-to-back sweeps must be safe; the second call hits the early-return path");

        await using var verify = store.LightweightSession(tenantId.ToString());
        var actual = await verify.LoadAsync<IdempotencyMarker>(key, ct);
        actual.Should().BeNull(because: "the first sweep deleted the marker; the second left state untouched");
    }

    [Fact]
    public async Task ExecuteAsync_FiresPeriodically_DrivenByFakeTimeProvider()
    {
        // Arrange — drive the BackgroundService loop directly. The first sweep
        // fires on entry (empty store, no-op); we then seed an expired marker,
        // advance fake time past `SweepInterval`, and assert the loop fires a
        // second sweep that deletes the marker. Validates the
        // `Task.Delay(SweepInterval, _timeProvider, ct)` periodic wiring.
        var ct = TestContext.Current.CancellationToken;
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero));
        var tenantId = Guid.NewGuid();
        var key = Guid.NewGuid();

        var logger = new CapturingLogger<IdempotencySweeper>();
        var sweeper = new IdempotencySweeper(store, time, logger);

        // Act
        await sweeper.StartAsync(ct);
        try
        {
            // Wait for the loop's first sweep to log "completed" — that's the
            // observable signal the loop has parked on `Task.Delay(SweepInterval,
            // fakeTime, ct)` and is ready to react to a fake-time Advance.
            await AsyncWait.UntilAsync(
                () => logger.Entries.Any(e => e.Level == LogLevel.Debug),
                TimeSpan.FromSeconds(5),
                "the BackgroundService loop must run its first sweep and emit LogSweepCompleted before the test seeds and advances time",
                ct);

            var firstSweepLogCount = logger.Entries.Count(e => e.Level == LogLevel.Debug);

            var expiredAt = time.GetUtcNow() - TimeSpan.FromHours(50);
            await using (var seedSession = store.LightweightSession(tenantId.ToString()))
            {
                seedSession.Store(new IdempotencyMarker(
                    key,
                    tenantId,
                    "sweeper-test",
                    JsonSerializer.SerializeToDocument(new { kind = "expired" }),
                    expiredAt));
                await seedSession.SaveChangesAsync(ct);
            }

            // Advance past the sweep interval to release the loop's `Task.Delay`
            // and trigger the second sweep iteration.
            time.Advance(IdempotencySweeper.SweepInterval + TimeSpan.FromSeconds(1));

            // Wait for the second sweep to delete the seeded marker. Two
            // observable signals confirm the iteration ran: a new
            // LogSweepCompleted entry, and the marker being gone from the DB.
            // Poll on the latter since it's the load-bearing assertion.
            await AsyncWait.UntilAsync(
                async () =>
                {
                    await using var poll = store.LightweightSession(tenantId.ToString());
                    return await poll.LoadAsync<IdempotencyMarker>(key, ct) is null;
                },
                TimeSpan.FromSeconds(10),
                "advancing the FakeTimeProvider past SweepInterval must release the loop's Task.Delay and trigger a fresh sweep that deletes the seeded marker",
                ct);

            // Assert — the second sweep also produced a completed-log entry.
            var totalSweepLogs = logger.Entries.Count(e => e.Level == LogLevel.Debug);
            totalSweepLogs.Should().BeGreaterThan(
                firstSweepLogCount,
                because: "the second sweep must run a full iteration and emit its own LogSweepCompleted");
        }
        finally
        {
            // Always stop the BackgroundService so a failed assertion does not
            // leak the loop into the next test, where it would race the shared
            // RunCoachAppFactory's Marten state.
            await sweeper.StopAsync(ct);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SwallowsTransientStoreErrorsAndContinues()
    {
        // Arrange — substitute IDocumentStore that throws on QuerySession to
        // simulate a transient backend failure. The CA1031 catch-all in
        // IdempotencySweeper.ExecuteAsync must keep the loop alive and only
        // log a warning. Uses an in-memory CapturingLogger so assertions are
        // structural (recorded log entries) rather than reflection-based.
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero));
        var store = Substitute.For<IDocumentStore>();
        store.QuerySession(Arg.Any<MartenSessionOptions>())
            .Throws(new InvalidOperationException("transient backend failure"));

        var logger = new CapturingLogger<IdempotencySweeper>();

        var sweeper = new IdempotencySweeper(store, time, logger);

        // Act
        await sweeper.StartAsync(ct);
        try
        {
            // Wait for the first iteration's SweepAsync to throw and the
            // CA1031 catch-all to record a warning — that's the signal the
            // loop has parked on the fake-time delay.
            await AsyncWait.UntilAsync(
                () => logger.Entries.Any(e => e.Level == LogLevel.Warning),
                TimeSpan.FromSeconds(5),
                "the first SweepAsync iteration must throw and the catch-all must record a warning before the test advances fake time",
                ct);

            // Advance past SweepInterval to trigger a second iteration, which
            // also throws. The loop must still be alive — wait for a second
            // warning rather than sleeping a fixed interval.
            time.Advance(IdempotencySweeper.SweepInterval + TimeSpan.FromSeconds(1));
            await AsyncWait.UntilAsync(
                () => logger.Entries.Count(e => e.Level == LogLevel.Warning) >= 2,
                TimeSpan.FromSeconds(5),
                "advancing past SweepInterval must trigger a second iteration; the catch-all must record a second warning, proving the loop is still alive",
                ct);

            // Assert — loop survived the failure window and the warning was emitted.
            var executeTask = sweeper.ExecuteTask;
            var stillRunning = executeTask is not null && !executeTask.IsCompleted;
            stillRunning.Should().BeTrue(
                because: "CA1031 catch-all in ExecuteAsync must keep the BackgroundService alive across transient store errors");

            logger.Entries.Should().Contain(
                e => e.Level == LogLevel.Warning && e.Exception is InvalidOperationException,
                because: "LogSweepFailed must surface the transient store failure as a warning carrying the original exception");
        }
        finally
        {
            // Always stop the BackgroundService so a failed assertion does not
            // leak the loop. Particularly important here because the substitute
            // IDocumentStore keeps throwing on every iteration; a leaked loop
            // would spam the test runner with warnings until the host disposes.
            await sweeper.StopAsync(ct);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        // Marten state lives in `runcoach_events`, which Respawn skips. Reset
        // it explicitly so seeded markers don't leak between tests.
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
