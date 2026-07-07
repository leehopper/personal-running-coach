using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Regression guard for the DI registration of <see cref="IContextAssembler"/>.
/// <see cref="ContextAssembler"/> has two constructors — a 3-arg legacy form (now
/// <c>internal</c>, test-only via InternalsVisibleTo) and the public sanitizer-bearing
/// form (4 required dependencies + an optional IRecentLogSanitizer for the adaptation
/// flow). The default DI container's "most-resolvable parameters" heuristic once silently
/// picked the 3-arg constructor in this project's service graph, leaving <c>_sanitizer</c>
/// null and breaking every sanitizer-dependent compose entry point with an
/// InvalidOperationException. Existing integration tests did not catch the regression
/// because they stub <see cref="IContextAssembler"/> via NSubstitute and never resolve the
/// production binding. Retargeted onto <see cref="IContextAssembler.ComposeForClassificationAsync"/>
/// after the onboarding compose path was retired (Slice 4C-onboarding) — the guard applies to
/// every surviving sanitizer-dependent entry point (classification / conversation / adaptation / ack).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class ContextAssemblerDiResolutionTests : IClassFixture<RunCoachAppFactory>
{
    private readonly RunCoachAppFactory _factory;

    public ContextAssemblerDiResolutionTests(RunCoachAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ComposeForClassificationAsync_DoesNotThrowMissingSanitizerDependency()
    {
        // Arrange: resolve the production-registered IContextAssembler from the
        // SUT's service provider. No substitute, no manual factory — exactly what
        // the conversation intent classifier resolves at request time.
        using var scope = _factory.Services.CreateScope();
        var assembler = scope.ServiceProvider.GetRequiredService<IContextAssembler>();

        // Act + Assert: a sanitizer-dependent compose entry point must not throw the
        // sentinel InvalidOperationException that fires when the 3-arg constructor was
        // used (it leaves _sanitizer null). Any other exception (e.g., prompts file IO)
        // is acceptable here; we only fail on the precise DI-misconfiguration signature.
        Func<Task> act = () =>
            assembler.ComposeForClassificationAsync(
                new DateOnly(2026, 7, 6),
                "I ran 5k easy today",
                TestContext.Current.CancellationToken);

        await act.Should()
            .NotThrowAsync<InvalidOperationException>(
                because: "the public sanitizer-bearing ContextAssembler constructor must win DI " +
                         "resolution so _sanitizer is non-null for every compose entry point");
    }
}
