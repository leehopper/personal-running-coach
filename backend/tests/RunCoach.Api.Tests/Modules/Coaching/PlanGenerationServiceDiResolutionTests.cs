using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Regression guard for the DI registration of <see cref="IPlanGenerationService"/>.
/// The production binding is <see cref="PlanGenerationService"/> registered
/// scoped in <c>ServiceCollectionExtensions.AddApplicationModules</c>. It
/// orchestrates the six-call macro/meso/micro structured-output chain per
/// spec § Unit 2 R02.4-R02.6 / DEC-057 / R-066. The service is intentionally
/// NOT a Wolverine handler and NOT a Wolverine command — the caller's static
/// handler body invokes it inline so the returned events commit on the
/// caller's <c>IDocumentSession</c> inside one Marten transaction. A silent
/// swap (e.g. a stub registered later in the pipeline, a duplicate
/// registration with a different concrete type winning, or the registration
/// being removed altogether) would break the dual-write atomicity contract
/// from DEC-060 / R-069. Existing handler tests stub
/// <see cref="IPlanGenerationService"/> via NSubstitute so they would never
/// touch the production binding.
/// </summary>
/// <remarks>
/// The test exercises the production <c>AddApplicationModules</c> extension on
/// a fresh <see cref="ServiceCollection"/> rather than booting the SUT's
/// <see cref="RunCoachAppFactory"/>. The factory deliberately swaps
/// <see cref="IPlanGenerationService"/> for <c>StubPlanGenerationService</c>
/// so integration tests don't pay six Anthropic LLM calls per run — that swap
/// is correct for those tests but means a resolution check against
/// <c>factory.Services</c> would assert the stub shape rather than the
/// production wiring this guard is meant to lock in. The
/// <see cref="ServiceLifetime"/> assertion is the load-bearing invariant —
/// the service captures per-request <c>IContextAssembler</c> and
/// <c>ICoachingLlm</c> dependencies that must NOT leak across handler
/// invocations.
/// </remarks>
public sealed class PlanGenerationServiceDiResolutionTests
{
    [Fact]
    public void IPlanGenerationService_Is_Registered_As_PlanGenerationService_Scoped()
    {
        // Arrange: invoke the production registration extension on a fresh
        // service collection — same call site as Program.cs.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddApplicationModules(configuration);

        // Act: find the IPlanGenerationService descriptor.
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPlanGenerationService));

        // Assert: the descriptor MUST exist, MUST point at the production
        // implementation, and MUST be scoped so per-request dependencies stay
        // request-bound.
        descriptor.Should().NotBeNull(
            because: "AddApplicationModules must register IPlanGenerationService for the static handlers' inline invocation");
        descriptor!.ImplementationType.Should().Be<PlanGenerationService>(
            because: "the production six-call macro/meso/micro chain is the only implementation registered in the host's service graph per Slice 1 § Unit 2 R02.4-R02.6");
        descriptor.Lifetime.Should().Be(
            ServiceLifetime.Scoped,
            because: "the service captures per-request IContextAssembler + ICoachingLlm dependencies; a singleton would leak per-tenant state and a transient would create distinct instances across the handler body");
    }
}
