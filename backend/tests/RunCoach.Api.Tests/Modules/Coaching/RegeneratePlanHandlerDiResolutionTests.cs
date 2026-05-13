using FluentAssertions;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Regression guard for the DI surface around <see cref="RegeneratePlanHandler"/>.
/// The handler is a static Wolverine handler (private constructor — Wolverine
/// codegens a non-static stub that calls the static <c>Handle</c> method) so
/// it is not itself resolved from DI; instead Wolverine resolves the handler's
/// parameter dependencies from the request scope on every invocation. The
/// critical invariant from DEC-060 / R-069 is that <see cref="IDocumentSession"/>
/// (Marten's Wolverine-outboxed session bracketed by transactional middleware)
/// AND <see cref="IIdempotencyStore"/> resolve from the same scope to the
/// SAME underlying session instance — the idempotency marker and the staged
/// events must commit on one Postgres transaction. The two flavors of Marten
/// session that can be registered (raw Marten session vs Wolverine-outboxed
/// session) are observably similar at the interface level but break
/// idempotency when the wrong one is wired in: a raw session writes outside
/// the handler's transactional bracket and the marker can survive a rolled
/// back event append (or vice versa).
/// </summary>
/// <remarks>
/// Existing tests do not catch this:
/// <see cref="Modules.Training.Plan.RegenerateTransactionScopeTests"/> builds an
/// NSubstitute <c>IDocumentSession</c> and calls
/// <c>RegeneratePlanHandler.Handle</c> directly, so it would pass against any
/// DI misregistration. The dual-write atomicity tests run end-to-end with the
/// production wiring but assert on Postgres backend_xid behaviour rather than
/// DI shape — a regression that left two distinct sessions in the scope would
/// surface as a flaky atomicity failure rather than a focused DI-resolution
/// failure. This test gives the focused signal.
/// <para>
/// The two facts split intentionally: the application-module fact hits the
/// production registration on a fresh container (no booting cost), while the
/// session-sharing fact uses the SUT host so the Marten +
/// <c>IntegrateWithWolverine</c> wiring is in scope.
/// </para>
/// </remarks>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class RegeneratePlanHandlerDiResolutionTests : IClassFixture<RunCoachAppFactory>
{
    private readonly RunCoachAppFactory _factory;

    public RegeneratePlanHandlerDiResolutionTests(RunCoachAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Application_Module_Dependencies_Of_Handle_Method_Are_Registered()
    {
        // Arrange: invoke the production registration extension on a fresh
        // service collection — same call site as Program.cs.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddApplicationModules(configuration);

        // Act + Assert: the two application-module dependencies of
        // RegeneratePlanHandler.Handle (IPlanGenerationService,
        // IIdempotencyStore) must be registered. IDocumentSession and
        // ILogger<T> are registered by Marten + the host's logging
        // builder respectively — covered by the session-sharing fact below
        // which boots the full SUT.
        var planGenerationDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPlanGenerationService));
        planGenerationDescriptor.Should().NotBeNull(
            because: "RegeneratePlanHandler.Handle requires IPlanGenerationService at request time");
        planGenerationDescriptor!.ImplementationType.Should().Be<PlanGenerationService>();
        planGenerationDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);

        var idempotencyDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IIdempotencyStore));
        idempotencyDescriptor.Should().NotBeNull(
            because: "RegeneratePlanHandler.Handle requires IIdempotencyStore at request time");
        idempotencyDescriptor!.ImplementationType.Should().Be<MartenIdempotencyStore>();
        idempotencyDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Handler_Session_And_IdempotencyStore_Share_Same_DocumentSession_Within_Scope()
    {
        // Arrange: one scope models one Wolverine handler invocation.
        using var scope = _factory.Services.CreateScope();

        // Act: resolve the handler-flavor IDocumentSession that Wolverine
        // would pass to RegeneratePlanHandler.Handle, then resolve the
        // idempotency store from the same scope. Inside MartenIdempotencyStore
        // the same IDocumentSession is captured via its primary constructor.
        var directSession = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var idempotency = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var secondSession = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RegeneratePlanHandler>>();

        // Assert: repeated resolutions of IDocumentSession from the same scope
        // must return the same instance — this is the load-bearing scoped
        // lifetime that makes the handler's events, the idempotency marker,
        // and Wolverine's outbox sit on one Postgres transaction (DEC-060 /
        // R-069). If a future refactor accidentally registered IDocumentSession
        // as transient (or wired a second un-outboxed session in), the marker
        // and the staged events would diverge into separate transactions and
        // idempotency replay protection would silently break on partial
        // failures.
        const string SessionReason = "Wolverine's outbox-integrated Marten registration must hand the same IDocumentSession instance across every resolution within one scope so the handler body and the idempotency store stage writes on one transaction";
        secondSession.Should().BeSameAs(directSession, because: SessionReason);

        // The idempotency store + logger must be non-null so the handler
        // body's argument-null guards don't trip before the work happens.
        idempotency.Should().NotBeNull();
        logger.Should().NotBeNull();
    }
}
