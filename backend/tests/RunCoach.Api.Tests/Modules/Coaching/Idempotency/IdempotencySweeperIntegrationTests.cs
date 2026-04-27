using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using RunCoach.Api.Modules.Coaching.Idempotency;
using RunCoach.Api.Tests.Infrastructure;

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
            using var oldDoc = JsonSerializer.SerializeToDocument(new { kind = "old" });
            using var freshDoc = JsonSerializer.SerializeToDocument(new { kind = "fresh" });
            seedSession.Store(new IdempotencyMarker(oldKey, tenantId, oldDoc.RootElement.Clone(), oldRecordedAt));
            seedSession.Store(new IdempotencyMarker(freshKey, tenantId, freshDoc.RootElement.Clone(), freshRecordedAt));
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
            using var docA = JsonSerializer.SerializeToDocument(new { tenant = "A" });
            sessionA.Store(new IdempotencyMarker(keyA, tenantA, docA.RootElement.Clone(), expiredAt));
            await sessionA.SaveChangesAsync(ct);
        }

        await using (var sessionB = store.LightweightSession(tenantB.ToString()))
        {
            using var docB = JsonSerializer.SerializeToDocument(new { tenant = "B" });
            sessionB.Store(new IdempotencyMarker(keyB, tenantB, docB.RootElement.Clone(), expiredAt));
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
    public async Task SweepAsync_PagedScan_Drains_All_Markers_Across_Two_Sweeps()
    {
        // Arrange — seed 2*pageSize markers under one tenant, all expired.
        // A sweeper with pageSize=3 keeps the test fast; 6 markers require
        // exactly 2 sweeps to drain, proving the bounded scan hands off
        // overflow to the next interval rather than silently skipping rows.
        const int pageSize = 3;
        const int total = pageSize * 2; // 6 markers
        var ct = TestContext.Current.CancellationToken;
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero));
        var expiredAt = time.GetUtcNow() - TimeSpan.FromHours(49);
        var tenantId = Guid.NewGuid();
        var keys = Enumerable.Range(0, total).Select(_ => Guid.NewGuid()).ToList();

        await using (var seed = store.LightweightSession(tenantId.ToString()))
        {
            foreach (var key in keys)
            {
                using var doc = JsonSerializer.SerializeToDocument(new { id = key });
                seed.Store(new IdempotencyMarker(key, tenantId, doc.RootElement.Clone(), expiredAt));
            }

            await seed.SaveChangesAsync(ct);
        }

        var sweeper = new IdempotencySweeper(store, time, NullLogger<IdempotencySweeper>.Instance, pageSize);

        // Act — sweep 1 deletes up to pageSize markers; sweep 2 drains the rest.
        await sweeper.SweepAsync(ct);
        await sweeper.SweepAsync(ct);

        // Assert — all 6 markers gone after 2 sweeps of pageSize=3.
        await using var verify = store.LightweightSession(tenantId.ToString());
        foreach (var key in keys)
        {
            var actual = await verify.LoadAsync<IdempotencyMarker>(key, ct);
            actual.Should().BeNull(
                because: $"marker {key} should be deleted within 2 sweeps of pageSize={pageSize}");
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
