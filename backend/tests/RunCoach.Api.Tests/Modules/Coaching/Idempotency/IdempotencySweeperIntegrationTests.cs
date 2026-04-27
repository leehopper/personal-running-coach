using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Tests.Infrastructure;
using MartenSessionOptions = Marten.Services.SessionOptions;

namespace RunCoach.Api.Tests.Modules.Coaching.Idempotency;

/// <summary>
/// Integration coverage for <see cref="IdempotencySweeper"/>. Asserts the
/// host registration, the DI lifetime, and that <c>SweepAsync</c> deletes
/// markers older than the 48h retention window across every tenant.
/// </summary>
[Trait("Category", "Integration")]
public class IdempotencySweeperIntegrationTests(RunCoachAppFactory factory) : DbBackedIntegrationTestBase(factory)
{
    [Fact]
    public void Sweeper_Is_Registered_As_HostedService()
    {
        // Arrange — the production registration must surface the sweeper as
        // an IHostedService so the generic host runs it on StartAsync.
        var hosted = Factory.Services.GetServices<IHostedService>().ToList();

        // Act
        var actual = hosted.OfType<IdempotencySweeper>().ToList();

        // Assert
        actual.Should().ContainSingle(
            because: "ServiceCollectionExtensions registers IdempotencySweeper via AddHostedService<T>()");
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
                JsonSerializer.SerializeToDocument(new { kind = "old" }),
                oldRecordedAt));
            seedSession.Store(new IdempotencyMarker(
                freshKey,
                tenantId,
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
                JsonSerializer.SerializeToDocument(new { tenant = "A" }),
                expiredAt));
            await sessionA.SaveChangesAsync(ct);
        }

        await using (var sessionB = store.LightweightSession(tenantB.ToString()))
        {
            sessionB.Store(new IdempotencyMarker(
                keyB,
                tenantB,
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

        var sweeper = new IdempotencySweeper(store, time, NullLogger<IdempotencySweeper>.Instance);

        // Act
        await sweeper.StartAsync(ct);

        // Give the loop a moment to run its first (no-op) sweep and park on
        // `Task.Delay(SweepInterval, fakeTime, ct)`. Real-clock pump only —
        // the fake-time delay won't progress until we Advance below.
        await Task.Delay(200, ct);

        var expiredAt = time.GetUtcNow() - TimeSpan.FromHours(50);
        await using (var seedSession = store.LightweightSession(tenantId.ToString()))
        {
            seedSession.Store(new IdempotencyMarker(
                key,
                tenantId,
                JsonSerializer.SerializeToDocument(new { kind = "expired" }),
                expiredAt));
            await seedSession.SaveChangesAsync(ct);
        }

        // Advance past the sweep interval to release the loop's `Task.Delay`
        // and trigger the second sweep iteration.
        time.Advance(IdempotencySweeper.SweepInterval + TimeSpan.FromSeconds(1));

        // Poll the DB up to ~10s waiting for the second sweep to delete the
        // marker. Polling uses real time; the sweep iteration itself uses fake.
        var deleted = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var poll = store.LightweightSession(tenantId.ToString());
            if (await poll.LoadAsync<IdempotencyMarker>(key, ct) is null)
            {
                deleted = true;
                break;
            }

            await Task.Delay(50, ct);
        }

        await sweeper.StopAsync(ct);

        // Assert
        deleted.Should().BeTrue(
            because: "advancing the FakeTimeProvider past SweepInterval must release the loop's Task.Delay and trigger a fresh sweep that deletes the seeded marker");
    }

    [Fact]
    public async Task ExecuteAsync_SwallowsTransientStoreErrorsAndContinues()
    {
        // Arrange — substitute IDocumentStore that throws on QuerySession to
        // simulate a transient backend failure. The CA1031 catch-all in
        // IdempotencySweeper.ExecuteAsync must keep the loop alive and only
        // log a warning. Uses NSubstitute throughout — no DB needed.
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero));
        var store = Substitute.For<IDocumentStore>();
        store.QuerySession(Arg.Any<MartenSessionOptions>())
            .Throws(new InvalidOperationException("transient backend failure"));

        var logger = Substitute.For<ILogger<IdempotencySweeper>>();
        logger.IsEnabled(LogLevel.Warning).Returns(true);

        var sweeper = new IdempotencySweeper(store, time, logger);

        // Act
        await sweeper.StartAsync(ct);

        // Pump real time so the first iteration runs SweepAsync (which throws),
        // the catch-all swallows it, and the loop parks on the fake-time delay.
        await Task.Delay(200, ct);

        // Advance past SweepInterval to trigger a second iteration, which also
        // throws. The loop must still be alive.
        time.Advance(IdempotencySweeper.SweepInterval + TimeSpan.FromSeconds(1));
        await Task.Delay(200, ct);

        var executeTask = sweeper.ExecuteTask;
        var stillRunning = executeTask is not null && !executeTask.IsCompleted;

        await sweeper.StopAsync(ct);

        // Assert — loop survived the failure window and the warning was emitted.
        stillRunning.Should().BeTrue(
            because: "CA1031 catch-all in ExecuteAsync must keep the BackgroundService alive across transient store errors");

        // Count `LogSweepFailed` invocations directly. NSubstitute's
        // Received() overload requires an exact count; the loop fires SweepAsync
        // on entry plus once per fake-time advance, so the call count is
        // timing-dependent. We only need to prove "at least one warning fired".
        var warningCount = logger.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == nameof(ILogger.Log)
                && c.GetArguments() is { Length: >= 1 } args
                && args[0] is LogLevel level
                && level == LogLevel.Warning);
        warningCount.Should().BeGreaterThan(
            0,
            because: "the catch-all in ExecuteAsync must surface the transient failure via LogSweepFailed");

        var querySessionCount = store.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == nameof(IDocumentStore.QuerySession));
        querySessionCount.Should().BeGreaterThan(
            0,
            because: "the loop must have invoked QuerySession at least once before the failure was caught");
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
