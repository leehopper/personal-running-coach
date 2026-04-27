using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Regression guard for the DI registration of <see cref="IContextAssembler"/>.
/// <see cref="ContextAssembler"/> exposes two public constructors — a 3-arg legacy
/// form and a 6-arg onboarding-aware form. The default DI container's
/// "most-resolvable parameters" heuristic silently picked the 3-arg constructor at
/// runtime in this project's service graph, leaving <c>_sanitizer</c> null and
/// breaking <see cref="IContextAssembler.ComposeForOnboardingAsync"/> with an
/// InvalidOperationException on every onboarding turn. Existing integration tests
/// did not catch the regression because they stub <see cref="IContextAssembler"/>
/// via NSubstitute and never resolve the production binding.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ContextAssemblerDiResolutionTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    [Fact]
    public async Task ComposeForOnboardingAsync_DoesNotThrowMissingOnboardingDependencies()
    {
        // Arrange: resolve the production-registered IContextAssembler from the
        // SUT's service provider. No substitute, no manual factory — exactly what
        // OnboardingTurnHandler resolves at request time. The factory is the
        // assembly-scoped fixture (constructor-injected via the AssemblyFixture
        // attribute on AssemblyInfo.cs), so this test reuses the single
        // Testcontainers Postgres + WebApplicationFactory host that every other
        // integration test in the assembly already shares. Using IClassFixture
        // here would spin up a second `RunCoachAppFactory` (separate Postgres
        // container, separate SUT host) whose env-var-based connection-string
        // override races with the assembly fixture's, intermittently leaving
        // both SUTs pointed at the same database and producing
        // "Unable to attain a global lock in time order to apply database
        // changes" failures on `ApplyAllDatabaseChangesOnStartup`.
        using var scope = Factory.Services.CreateScope();
        var assembler = scope.ServiceProvider.GetRequiredService<IContextAssembler>();
        var view = new OnboardingView
        {
            UserId = Guid.NewGuid(),
            Status = OnboardingStatus.InProgress,
        };

        // Act + Assert: ComposeForOnboardingAsync must not throw the sentinel
        // exception that fires when the 3-arg constructor was used. Any other
        // exception (e.g., prompts file IO) is acceptable here; we only fail on
        // the precise DI-misconfiguration signature.
        Func<Task> act = () =>
            assembler.ComposeForOnboardingAsync(view, OnboardingTopic.PrimaryGoal, "race-training", default);

        await act.Should()
            .NotThrowAsync<InvalidOperationException>(
                because: "the 6-arg ContextAssembler constructor must win DI resolution so " +
                         "_sanitizer + _onboardingSystemPromptCache are both non-null per Slice 1 § Unit 1");
    }
}
