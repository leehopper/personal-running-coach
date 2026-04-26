using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Modules.Coaching.Idempotency;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Idempotency;

/// <summary>
/// Integration tests that round-trip <see cref="IdempotencyMarker"/> documents
/// through the live Testcontainers Postgres + production Marten configuration.
/// Asserts the document survives a real serialize/deserialize cycle and that
/// conjoined tenancy isolates markers across tenants.
/// </summary>
[Trait("Category", "Integration")]
public class MartenIdempotencyStoreIntegrationTests(RunCoachAppFactory factory) : DbBackedIntegrationTestBase(factory)
{
    [Fact]
    public async Task SeenAsync_NoPriorMarker_Returns_Null()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var key = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var store = Factory.Services.GetRequiredService<IDocumentStore>();

        await using var session = store.LightweightSession(userId.ToString());
        var idempotency = new MartenIdempotencyStore(session);

        // Act
        var actual = await idempotency.SeenAsync<TestResponse>(key, ct);

        // Assert
        actual.Should().BeNull();
    }

    [Fact]
    public async Task Record_Then_SeenAsync_RoundTrips_Response_On_Same_Session()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var key = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        var expected = new TestResponse("ok", 7);

        // Act — Record stages, SaveChangesAsync commits within the same session.
        await using (var write = store.LightweightSession(userId.ToString()))
        {
            new MartenIdempotencyStore(write).Record(key, userId, expected);
            await write.SaveChangesAsync(ct);
        }

        TestResponse? actual;
        await using (var read = store.LightweightSession(userId.ToString()))
        {
            actual = await new MartenIdempotencyStore(read).SeenAsync<TestResponse>(key, ct);
        }

        // Assert
        actual.Should().NotBeNull();
        actual!.Status.Should().Be(expected.Status);
        actual.Counter.Should().Be(expected.Counter);
    }

    [Fact]
    public async Task Record_Persists_RecordedAt_And_UserId_On_Marker()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var key = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        var beforeWrite = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        await using (var write = store.LightweightSession(userId.ToString()))
        {
            new MartenIdempotencyStore(write).Record(key, userId, new TestResponse("noted", 1));
            await write.SaveChangesAsync(ct);
        }

        IdempotencyMarker? actual;
        await using (var read = store.LightweightSession(userId.ToString()))
        {
            actual = await read.LoadAsync<IdempotencyMarker>(key, ct);
        }

        // Assert — direct document inspection proves we wrote the canonical fields.
        actual.Should().NotBeNull();
        actual!.Key.Should().Be(key);
        actual.UserId.Should().Be(userId);
        actual.RecordedAt.Should().BeAfter(beforeWrite);
        actual.Response.Should().NotBeNull();
    }

    [Fact]
    public async Task SeenAsync_Marker_From_Other_Tenant_Is_Not_Visible()
    {
        // Arrange — two distinct users / tenants.
        var ct = TestContext.Current.CancellationToken;
        var sharedKey = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var store = Factory.Services.GetRequiredService<IDocumentStore>();

        // Act — record under owner tenant.
        await using (var ownerSession = store.LightweightSession(ownerUserId.ToString()))
        {
            new MartenIdempotencyStore(ownerSession).Record(
                sharedKey,
                ownerUserId,
                new TestResponse("owner-only", 42));
            await ownerSession.SaveChangesAsync(ct);
        }

        // Read from a session scoped to the OTHER tenant — conjoined tenancy must hide the row.
        TestResponse? actualOtherTenant;
        TestResponse? actualOwnerTenant;
        await using (var otherSession = store.LightweightSession(otherUserId.ToString()))
        {
            actualOtherTenant = await new MartenIdempotencyStore(otherSession)
                .SeenAsync<TestResponse>(sharedKey, ct);
        }

        await using (var ownerSession = store.LightweightSession(ownerUserId.ToString()))
        {
            actualOwnerTenant = await new MartenIdempotencyStore(ownerSession)
                .SeenAsync<TestResponse>(sharedKey, ct);
        }

        // Assert
        actualOtherTenant.Should().BeNull(
            because: "Marten's conjoined tenancy must prevent cross-tenant reads of IdempotencyMarker");
        actualOwnerTenant.Should().NotBeNull();
        actualOwnerTenant!.Counter.Should().Be(42);
    }

    [Fact]
    public void Record_Throws_On_Null_Response()
    {
        // Arrange
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        using var session = store.LightweightSession(Guid.NewGuid().ToString());
        var idempotency = new MartenIdempotencyStore(session);

        // Act
        var act = () => idempotency.Record<TestResponse>(Guid.NewGuid(), Guid.NewGuid(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private sealed record TestResponse(string Status, int Counter);
}
