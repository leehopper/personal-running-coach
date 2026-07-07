using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Infrastructure;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Regression guard (DEC-071) for Wolverine 6 message-handler code generation.
/// Wolverine 6 defaults to <c>ServiceLocationPolicy.NotAllowed</c>: when a handler
/// depends on a service registered as an "opaque" lambda factory with a Scoped or
/// Transient lifetime, Wolverine cannot construct it statically, falls back to
/// service location, and throws <c>InvalidServiceLocationException</c> while
/// compiling the handler chain — surfacing as an HTTP 500 on the affected endpoint
/// (e.g. the <c>SubmitStructuredAnswersHandler</c> / POST <c>/api/v1/onboarding/answers</c> chain).
///
/// This blind spot existed because no green test compiled the production handler
/// graph: the <c>*DiResolutionTests</c> only assert <c>GetRequiredService</c>
/// succeeds (which an opaque lambda factory satisfies), handler unit tests call the
/// static <c>Handle</c> method directly with mocks (bypassing Wolverine codegen),
/// and the HTTP/bus integration host swaps <c>IPlanGenerationService</c> for a stub
/// that severs the failing dependency edge.
///
/// This test inspects the production registrations directly (no host boot, no DB,
/// no LLM) and fails if any module service uses an opaque Scoped/Transient lambda
/// factory, which would re-break handler codegen. If a future registration genuinely
/// needs a factory, allow-list the service via
/// <c>opts.CodeGeneration.AlwaysUseServiceLocationFor&lt;T&gt;()</c> in the Wolverine
/// configuration AND add it to <see cref="KnownServiceLocationAllowList"/> in the
/// same change.
/// </summary>
public sealed class WolverineCodegenCompositionTests
{
    /// <summary>
    /// Service types intentionally permitted to use Wolverine service location via
    /// <c>CodeGeneration.AlwaysUseServiceLocationFor&lt;T&gt;()</c>. Empty by design —
    /// the module convention is concrete implementation-type registrations so handler
    /// codegen stays static.
    /// </summary>
    private static readonly HashSet<Type> KnownServiceLocationAllowList = new();

    [Fact]
    public void AddApplicationModules_RegistersNoOpaqueScopedOrTransientLambdaFactories()
    {
        // Arrange: build the exact production registration graph Program.cs assembles
        // (see Program.cs `builder.Services.AddApplicationModules(builder.Configuration)`).
        var services = new ServiceCollection();
        services.AddApplicationModules(new ConfigurationBuilder().Build());

        // Act: find Scoped/Transient registrations that use an opaque implementation
        // factory (lambda) rather than a concrete implementation type. Singletons are
        // exempt — Wolverine resolves them as cached instances, not via per-handler
        // constructor codegen.
        var opaqueLambdaFactories = services
            .Where(descriptor =>
                descriptor.ImplementationFactory is not null
                && descriptor.Lifetime != ServiceLifetime.Singleton
                && !KnownServiceLocationAllowList.Contains(descriptor.ServiceType))
            .Select(descriptor => descriptor.ServiceType.FullName)
            .ToList();

        // Assert.
        opaqueLambdaFactories.Should()
            .BeEmpty(
                because: "Wolverine 6 handler codegen (ServiceLocationPolicy.NotAllowed) cannot " +
                         "statically construct an opaque Scoped/Transient lambda factory and throws " +
                         "InvalidServiceLocationException while compiling any handler that depends on " +
                         "it (DEC-071). Register a concrete implementation type, or allow-list the " +
                         "service via CodeGeneration.AlwaysUseServiceLocationFor<T>().");
    }
}
