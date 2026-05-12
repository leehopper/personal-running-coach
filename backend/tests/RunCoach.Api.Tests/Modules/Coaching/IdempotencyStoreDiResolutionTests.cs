using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Regression guard for the DI registration of <see cref="IIdempotencyStore"/>.
/// The production binding is <see cref="MartenIdempotencyStore"/> registered
/// scoped in <c>ServiceCollectionExtensions.AddApplicationModules</c>; both
/// the lookup and write paths must operate on the caller's injected
/// <c>IDocumentSession</c> so the idempotency marker commits atomically with
/// the events appended in the same Wolverine handler body per DEC-060 / R-069.
/// A silent swap to a different implementation (e.g. an in-memory stub left
/// over from a refactor, or a no-op fallback registration in a downstream
/// module) would break the dual-write atomicity claim the regenerate and
/// onboarding-turn handlers depend on. The existing integration tests
/// (<see cref="Infrastructure.Idempotency.MartenIdempotencyStoreIntegrationTests"/>)
/// construct the store manually around a <c>LightweightSession</c> so they
/// would not catch a misregistration on the SUT's DI surface.
/// </summary>
/// <remarks>
/// The test exercises the production <c>AddApplicationModules</c> extension on
/// a fresh <see cref="ServiceCollection"/> rather than booting the SUT's
/// <see cref="RunCoachAppFactory"/>. The factory replaces a subset of
/// production bindings (e.g. <see cref="Modules.Training.Plan.IPlanGenerationService"/>
/// → <c>StubPlanGenerationService</c>) for test ergonomics, so resolving from
/// <c>factory.Services</c> would assert the test-time stub shape rather than
/// the production registration this guard is meant to lock in. The
/// <see cref="ServiceLifetime"/> assertion is the load-bearing invariant —
/// a singleton store would break per-request <c>IDocumentSession</c> scoping
/// and a transient store would break the "same session across handler body"
/// guarantee that ties the marker write to the staged events.
/// </remarks>
public sealed class IdempotencyStoreDiResolutionTests
{
    [Fact]
    public void IIdempotencyStore_Is_Registered_As_MartenIdempotencyStore_Scoped()
    {
        // Arrange: invoke the production registration extension on a fresh
        // service collection — same call site as Program.cs.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddApplicationModules(configuration);

        // Act: find the IIdempotencyStore descriptor.
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IIdempotencyStore));

        // Assert: the descriptor MUST exist, MUST point at the production
        // Marten-backed implementation, and MUST be scoped.
        descriptor.Should().NotBeNull(
            because: "AddApplicationModules must register IIdempotencyStore for Wolverine handlers to resolve at request time");
        descriptor!.ImplementationType.Should().Be<MartenIdempotencyStore>(
            because: "the Marten-backed store is the only implementation that participates in the handler's transaction so the idempotency marker + events commit atomically");
        descriptor.Lifetime.Should().Be(
            ServiceLifetime.Scoped,
            because: "the store must share the handler's request-scoped IDocumentSession; a singleton would leak sessions across requests and a transient would break the marker/event same-session guarantee");
    }
}
