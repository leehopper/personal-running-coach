using System.Collections.Immutable;
using System.Text.Json;
using FluentAssertions;
using Marten;
using Marten.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Idempotency;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Coaching.Sanitization;
using RunCoach.Api.Modules.Identity.Entities;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Regression guard for DEC-057's "single-handler / single-session / single-transaction"
/// concurrency guarantee. Verifies that <c>EventAppendMode.Rich</c> enforces stream-version
/// consistency so that N concurrent first-turn submissions for the same user result in
/// exactly one successful commit and (N-1) stream-collision failures.
///
/// <para>
/// Without <c>EventAppendMode.Rich</c> (i.e. under <c>Quick</c> mode), Marten skips
/// the per-stream version check. All N concurrent sessions can race past
/// <c>session.Events.StartStream&lt;OnboardingView&gt;(userId, …)</c> and succeed at
/// <c>SaveChangesAsync</c>, producing N copies of the onboarding stream's first-event
/// sequence — the exact silent data-corruption the <c>Rich</c> flip closes.
/// </para>
/// <para>
/// The test calls <see cref="OnboardingTurnHandler.Handle"/> directly (bypassing the
/// Wolverine message bus) so the <c>ExistingStreamIdCollisionException</c> propagates
/// from <c>SaveChangesAsync</c> to the test harness rather than being routed to the
/// dead-letter queue. This proves the underlying database-level guard fires, which is
/// the precondition for Wolverine's <c>MoveToErrorQueue</c> policy to be meaningful.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class OnboardingTurnConcurrencyTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private const int ConcurrentSubmitCount = 5;
    private const string StrongPassword = "Str0ngTestPassw0rd!";

    /// <summary>
    /// Five concurrent first-turn submissions for the same user stream must result in
    /// exactly one successful <c>SaveChangesAsync</c> commit (the "winner") and four
    /// <c>ExistingStreamIdCollisionException</c> failures (the "losers"). Each loser's
    /// exception must be either <see cref="ExistingStreamIdCollisionException"/> or a
    /// <see cref="MartenCommandException"/> wrapping a PostgreSQL unique-constraint
    /// violation — both indicate Marten's Rich-mode version gate fired.
    /// </summary>
    [Fact]
    public async Task ConcurrentFirstTurnSubmits_ExactlyOneSucceeds_RestThrowStreamCollision()
    {
        // Arrange — provision a real Identity row so Marten's conjoined-tenancy
        // projection wiring has a valid FK target when the winner commits.
        var userId = await SeedUserAsync();

        var successCount = 0;
        var failures = new List<Exception>();
        var lockObj = new Lock();

        // Act — fire N concurrent handler invocations, each in its own DI scope
        // (mirroring the per-request scope Wolverine creates in production). All
        // share the same userId so their StartStream calls target the same Marten
        // stream. Each uses a distinct idempotencyKey so the idempotency short-
        // circuit does NOT mask the race — the collision must happen at the DB
        // layer, not inside the handler.
        var tasks = Enumerable.Range(0, ConcurrentSubmitCount).Select(async _ =>
        {
            try
            {
                await InvokeFirstTurnAsync(userId, idempotencyKey: Guid.NewGuid());
                lock (lockObj)
                {
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                lock (lockObj)
                {
                    failures.Add(ex);
                }
            }
        });

        await Task.WhenAll(tasks);

        // Assert — exactly one winner; all losers carry a stream-collision signal.
        successCount.Should().Be(
            1,
            because: "exactly one concurrent StartStream<OnboardingView> commit must win; all others must hit the stream-id unique constraint");

        var expectedFailureCount = ConcurrentSubmitCount - 1;
        failures.Should().HaveCount(
            expectedFailureCount,
            because: $"the remaining {expectedFailureCount} submits must fail with a stream-collision exception from Marten's Rich-mode version check");

        // Each failure must be a Marten stream-collision indicator: either the typed
        // ExistingStreamIdCollisionException (emitted directly by Marten when it
        // detects the collision before issuing the SQL) or a MartenCommandException
        // wrapping the PostgreSQL unique-constraint violation (emitted when the
        // collision surfaces at the DB level inside SaveChangesAsync).
        foreach (var ex in failures)
        {
            ex.Should().Match<Exception>(
                e => e is ExistingStreamIdCollisionException || e is MartenCommandException,
                because: "Rich-mode concurrent StartStream must throw ExistingStreamIdCollisionException or MartenCommandException — not silently succeed");
        }
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        // Marten event data lives in runcoach_events (not public schema).
        // Reset both so no stream data leaks between tests.
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static OnboardingTurnOutput BuildValidLlmOutput() => new()
    {
        Reply = [new AnthropicContentBlock { Type = AnthropicContentBlockType.Text, Text = "tell me about your goals" }],
        Extracted = new ExtractedAnswer
        {
            Topic = OnboardingTopic.PrimaryGoal,
            Confidence = 0.5,
            NormalizedPrimaryGoal = null,
            NormalizedTargetEvent = null,
            NormalizedCurrentFitness = null,
            NormalizedWeeklySchedule = null,
            NormalizedInjuryHistory = null,
            NormalizedPreferences = null,
        },
        NeedsClarification = true,
        ClarificationReason = "Need more detail",
        ReadyForPlan = false,
    };

    private async Task<Guid> SeedUserAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"concurrency-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { Email = email, UserName = email };
        var result = await users.CreateAsync(user, StrongPassword);
        result.Succeeded.Should()
            .BeTrue(because: $"seed must succeed — got [{string.Join(", ", result.Errors.Select(e => e.Code))}]");
        return user.Id;
    }

    /// <summary>
    /// Invokes <see cref="OnboardingTurnHandler.Handle"/> in its own DI scope with a
    /// real <see cref="IDocumentSession"/> and then commits via
    /// <c>SaveChangesAsync</c> (standing in for Wolverine's transactional middleware).
    /// All LLM-dependent collaborators are stubbed to return a minimal valid
    /// <see cref="OnboardingTurnOutput"/> so the handler proceeds past the LLM call
    /// and reaches the <c>StartStream</c> + <c>Append</c> path before commit.
    /// </summary>
    private async Task InvokeFirstTurnAsync(Guid userId, Guid idempotencyKey)
    {
        using var scope = Factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

        var llm = Substitute.For<ICoachingLlm>();
        llm.GenerateStructuredAsync<OnboardingTurnOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(BuildValidLlmOutput());

        var assembler = Substitute.For<IContextAssembler>();
        assembler.ComposeForOnboardingAsync(
                Arg.Any<OnboardingView>(),
                Arg.Any<OnboardingTopic>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new OnboardingPromptComposition(
                SystemPrompt: "system",
                UserMessage: "user",
                Findings: ImmutableArray<SanitizationFinding>.Empty,
                Neutralized: false));

        var sanitizer = Substitute.For<IPromptSanitizer>();
        var planGen = Substitute.For<IPlanGenerationService>();
        var idempotency = new MartenIdempotencyStore(session);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));

        await OnboardingTurnHandler.Handle(
            new SubmitUserTurn(userId, idempotencyKey, "hello"),
            session,
            llm,
            assembler,
            sanitizer,
            idempotency,
            planGen,
            time,
            NullLogger<OnboardingTurnHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Stand in for Wolverine's transactional middleware. This is the commit
        // where the stream-collision exception surfaces for the losing sessions.
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
