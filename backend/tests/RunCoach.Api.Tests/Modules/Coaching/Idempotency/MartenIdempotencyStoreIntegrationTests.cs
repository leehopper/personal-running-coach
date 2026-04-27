using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RunCoach.Api.Infrastructure.Idempotency;
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
        var idempotency = Build(session);

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
            Build(write).Record(key, expected);
            await write.SaveChangesAsync(ct);
        }

        TestResponse? actual;
        await using (var read = store.LightweightSession(userId.ToString()))
        {
            actual = await Build(read).SeenAsync<TestResponse>(key, ct);
        }

        // Assert
        actual.Should().NotBeNull();
        actual!.Status.Should().Be(expected.Status);
        actual.Counter.Should().Be(expected.Counter);
    }

    [Fact]
    public async Task Record_Persists_RecordedAt_TenantId_And_PayloadType_On_Marker()
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
            Build(write).Record(key, new TestResponse("noted", 1));
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
        actual.UserId.Should().Be(userId, because: "Record sources UserId from the active session's TenantId");
        actual.PayloadTypeName.Should().Be(typeof(TestResponse).FullName);
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
            Build(ownerSession).Record(sharedKey, new TestResponse("owner-only", 42));
            await ownerSession.SaveChangesAsync(ct);
        }

        // Read from a session scoped to the OTHER tenant — conjoined tenancy must hide the row.
        TestResponse? actualOtherTenant;
        TestResponse? actualOwnerTenant;
        await using (var otherSession = store.LightweightSession(otherUserId.ToString()))
        {
            actualOtherTenant = await Build(otherSession).SeenAsync<TestResponse>(sharedKey, ct);
        }

        await using (var ownerSession = store.LightweightSession(ownerUserId.ToString()))
        {
            actualOwnerTenant = await Build(ownerSession).SeenAsync<TestResponse>(sharedKey, ct);
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
        var idempotency = Build(session);

        // Act
        var act = () => idempotency.Record<TestResponse>(Guid.NewGuid(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Record_Throws_When_Session_Is_Not_Tenanted()
    {
        // Arrange — open a session without a tenant id; Marten reports "*DEFAULT*".
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        using var session = store.LightweightSession();
        var idempotency = Build(session);

        // Act
        var act = () => idempotency.Record(Guid.NewGuid(), new TestResponse("x", 0));

        // Assert
        act.Should().Throw<InvalidOperationException>(
            because: "Idempotency markers must inherit tenant id from the active session, not a default tenant");
    }

    [Fact]
    public async Task SeenAsync_Returns_Null_When_PayloadType_Mismatches()
    {
        // Arrange — record with TestResponse, attempt to read as OtherResponse.
        var ct = TestContext.Current.CancellationToken;
        var key = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var store = Factory.Services.GetRequiredService<IDocumentStore>();

        await using (var write = store.LightweightSession(userId.ToString()))
        {
            Build(write).Record(key, new TestResponse("v1", 99));
            await write.SaveChangesAsync(ct);
        }

        OtherResponse? actual;
        await using (var read = store.LightweightSession(userId.ToString()))
        {
            actual = await Build(read).SeenAsync<OtherResponse>(key, ct);
        }

        // Assert — cross-version replay protection: mismatched type returns miss.
        actual.Should().BeNull(
            because: "the recorded PayloadTypeName does not match the requested TResponse, so SeenAsync must treat the marker as a miss");
    }

    private static MartenIdempotencyStore Build(IDocumentSession session) =>
        new(session, NullLogger<MartenIdempotencyStore>.Instance);

    private sealed record TestResponse(string Status, int Counter);

    private sealed record OtherResponse(string Other);
}
