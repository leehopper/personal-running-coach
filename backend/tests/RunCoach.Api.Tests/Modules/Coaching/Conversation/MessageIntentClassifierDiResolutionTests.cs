using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Regression guard for the DI registration of <see cref="IMessageIntentClassifier"/>.
/// Unlike <c>ContextAssemblerDiResolutionTests</c> (which guards a genuine two-constructor
/// selection hazard), <see cref="MessageIntentClassifier"/> has a single constructor whose
/// dependencies are all registered, so the failure mode is simply a missing/incorrect
/// registration. This resolves the production binding exactly as the streaming endpoint will,
/// applying the registration convention uniformly across the conversation services.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class MessageIntentClassifierDiResolutionTests : IClassFixture<RunCoachAppFactory>
{
    private readonly RunCoachAppFactory _factory;

    public MessageIntentClassifierDiResolutionTests(RunCoachAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void IMessageIntentClassifier_ResolvesFromProductionContainer()
    {
        // Arrange + Act: resolve the production-registered classifier from the SUT's
        // service provider — no substitute, no manual factory.
        using var scope = _factory.Services.CreateScope();
        var classifier = scope.ServiceProvider.GetRequiredService<IMessageIntentClassifier>();

        // Assert: the concrete production type is wired (resolution itself proves every
        // constructor dependency is registered).
        classifier.Should().BeOfType<MessageIntentClassifier>();
    }
}
