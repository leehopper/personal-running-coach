using System.Collections.Immutable;
using System.Text.Json;
using FluentAssertions;
using Marten;
using Marten.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using NSubstitute;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Conversation;
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
/// consistency so that N first-turn submissions racing on the same stream result in
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
/// The test runs in two phases to make the collision deterministic. Phase 1 invokes
/// <see cref="OnboardingTurnHandler.Handle"/> for all N submissions WITHOUT committing:
/// every invocation reads <c>view == null</c> (nothing has committed yet, so ordering
/// is irrelevant) and stages a <c>StartStream</c> on its own session. Phase 2 races
/// all N <c>SaveChangesAsync</c> commits. Exactly one stream-row insert can satisfy
/// the <c>mt_streams (tenant_id, id)</c> primary key, so exactly one commit wins
/// regardless of scheduling — the property holds on any machine at any speed.
/// </para>
/// <para>
/// The two-phase shape exists because the obvious alternative — N end-to-end
/// handler+commit tasks fired via <c>Task.WhenAll</c> — encodes a hidden timing
/// assumption: that every handler reads "no stream yet" before any task commits.
/// On a slow, coverage-instrumented runner the tasks can serialize, and a task that
/// starts after the winner's commit reads the existing view, takes the handler's
/// legitimate second-turn Append path, and succeeds — failing the exactly-one-winner
/// assertion (observed once in CI as <c>successCount == 2</c>) without any actual
/// violation of the stream guarantee. Staging everything before any commit removes
/// the timing dependence while still exercising the same database-level guard.
/// </para>
/// <para>
/// The test calls <see cref="OnboardingTurnHandler.Handle"/> directly (bypassing the
/// Wolverine message bus) so the <c>ExistingStreamIdCollisionException</c> propagates
/// from <c>SaveChangesAsync</c> to the test harness rather than being routed to the
/// dead-letter queue. This proves the underlying database-level guard fires, which is
/// the precondition for Wolverine's <c>MoveToErrorQueue</c> policy to be meaningful.
/// </para>
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class OnboardingTurnConcurrencyIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private const int ConcurrentSubmitCount = 5;
    private const string StrongPassword = "Str0ngTestPassw0rd!";

    /// <summary>
    /// Five first-turn submissions, all staged before any commit, then committed
    /// concurrently, must result in exactly one successful <c>SaveChangesAsync</c>
    /// (the "winner") and four stream-collision failures (the "losers"). Each
    /// loser's exception must be either <see cref="ExistingStreamIdCollisionException"/>
    /// or a <see cref="MartenCommandException"/> wrapping a PostgreSQL
    /// unique-constraint violation — both indicate Marten's Rich-mode version
    /// gate fired.
    /// </summary>
    [Fact]
    public async Task ConcurrentFirstTurnSubmits_ExactlyOneSucceeds_RestThrowStreamCollision()
    {
        // Arrange — provision a real Identity row so Marten's conjoined-tenancy
        // projection wiring has a valid FK target when the winner commits.
        var userId = await SeedUserAsync();

        var prepared = new List<(IServiceScope Scope, IDocumentSession Session)>(ConcurrentSubmitCount);
        try
        {
            // Act, phase 1 — run the handler for all N submissions without
            // committing. Each invocation gets its own DI scope + session
            // (mirroring the per-request scope Wolverine creates in production),
            // reads view == null (nothing has committed), and stages
            // StartStream<OnboardingView> for the same userId. Each uses a
            // distinct idempotencyKey so the idempotency short-circuit does NOT
            // mask the race — the collision must happen at the DB layer, not
            // inside the handler.
            for (var i = 0; i < ConcurrentSubmitCount; i++)
            {
                prepared.Add(await PrepareFirstTurnAsync(userId, idempotencyKey: Guid.NewGuid()));
            }

            // Act, phase 2 — race all N commits. This is where Marten's
            // Rich-mode stream-version gate must let exactly one StartStream
            // insert through.
            var outcomes = await Task.WhenAll(prepared.Select(async p =>
            {
                try
                {
                    await p.Session.SaveChangesAsync(TestContext.Current.CancellationToken);
                    return (Exception?)null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }));

            // Assert — exactly one winner; all losers carry a stream-collision signal.
            var successCount = outcomes.Count(o => o is null);
            var failures = outcomes.Where(o => o is not null).Cast<Exception>().ToList();

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
            //
            // Per CodeRabbit feedback: a bare `is MartenCommandException` would also
            // accept unrelated SQL errors (foreign-key, syntax, etc.). Verify the
            // wrapped Postgres exception carries SqlState 23505 (unique_violation),
            // which is the exact signal that Marten's Rich-mode version gate fired.
            foreach (var ex in failures)
            {
                ex.Should().Match<Exception>(
                    e => e is ExistingStreamIdCollisionException || IsUniqueViolation(e),
                    because: "Rich-mode concurrent StartStream must throw ExistingStreamIdCollisionException or a MartenCommandException wrapping a Postgres unique_violation (SqlState 23505) — not silently succeed");
            }
        }
        finally
        {
            foreach (var (scope, session) in prepared)
            {
                await session.DisposeAsync();
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Regression guard for the shared per-user event stream: onboarding and the interactive
    /// conversation both materialize from the bare-user-id stream. A runner who chats before
    /// onboarding creates that stream via the conversation handler; their first onboarding turn
    /// must then APPEND <c>OnboardingStarted</c>, not <c>StartStream</c> — a <c>StartStream</c>
    /// over the already-existing stream throws <see cref="ExistingStreamIdCollisionException"/>,
    /// which <c>OnboardingController</c> does not catch, so it would surface as an unhandled 500
    /// that permanently blocks the account from ever completing onboarding.
    /// </summary>
    [Fact]
    public async Task FirstOnboardingTurn_WhenConversationStreamAlreadyExists_AppendsWithoutCollision()
    {
        // Arrange — a runner who has chatted (their per-user stream exists, tagged
        // ConversationView) but has never onboarded (no OnboardingView document yet).
        var userId = await SeedUserAsync();
        await PreseedConversationStreamAsync(userId);

        // Act — stage the first onboarding turn, then commit (standing in for Wolverine's
        // transactional middleware). The commit must not throw a stream-collision.
        var (scope, session) = await PrepareFirstTurnAsync(userId, idempotencyKey: Guid.NewGuid());
        try
        {
            Func<Task> commit = () => session.SaveChangesAsync(TestContext.Current.CancellationToken);
            await commit.Should().NotThrowAsync(
                because: "the first onboarding turn appends OnboardingStarted to the existing shared per-user stream instead of StartStream, which would collide");
        }
        finally
        {
            await session.DisposeAsync();
            scope.Dispose();
        }

        // Assert — the OnboardingView materialized from the appended OnboardingStarted, proving
        // the projection's Create ran on the bootstrap event even though it was not the stream's
        // first event.
        using var readScope = Factory.Services.CreateScope();
        var store = readScope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var readSession = store.LightweightSession(userId.ToString());
        var view = await readSession.LoadAsync<OnboardingView>(userId, TestContext.Current.CancellationToken);
        view.Should().NotBeNull(
            because: "OnboardingProjection.Create runs on the appended OnboardingStarted mid-stream");
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        // Marten event data lives in runcoach_events (not public schema).
        // Reset both so no stream data leaks between tests. Base type already
        // calls GC.SuppressFinalize.
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Walks an exception chain looking for a Postgres unique-constraint
    /// violation (SqlState 23505). Marten typically wraps the Npgsql exception
    /// inside a MartenCommandException, so we drill through InnerException
    /// rather than relying on the outer type alone.
    /// </summary>
    private static bool IsUniqueViolation(Exception ex)
    {
        if (ex is not MartenCommandException)
        {
            return false;
        }

        var current = ex.InnerException;
        while (current is not null)
        {
            if (current is PostgresException pg && string.Equals(pg.SqlState, "23505", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
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
    /// Creates the runner's per-user event stream via a conversation turn (as if they chatted
    /// before onboarding), tagging the physical stream <c>ConversationView</c> with no
    /// onboarding events on it yet.
    /// </summary>
    private async Task PreseedConversationStreamAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Events.StartStream<ConversationView>(
            userId, new UserMessagePosted(userId, Guid.NewGuid(), "hey coach"));
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Invokes <see cref="OnboardingTurnHandler.Handle"/> in its own DI scope with a
    /// real <see cref="IDocumentSession"/> WITHOUT committing — the handler stages
    /// the StartStream + Append operations and the caller owns the commit (standing
    /// in for Wolverine's transactional middleware) plus disposal of the returned
    /// scope and session. All LLM-dependent collaborators are stubbed to return a
    /// minimal valid <see cref="OnboardingTurnOutput"/> so the handler proceeds past
    /// the LLM call and reaches the <c>StartStream</c> + <c>Append</c> path.
    /// </summary>
    private async Task<(IServiceScope Scope, IDocumentSession Session)> PrepareFirstTurnAsync(Guid userId, Guid idempotencyKey)
    {
        var scope = Factory.Services.CreateScope();

        // Marten is configured with TenancyStyle.Conjoined; the default DI
        // session has no tenant assigned (in production, Wolverine middleware
        // sets it from the message envelope). LightweightSession with the
        // user id as tenant matches what the runtime would do for this
        // command and lets MartenIdempotencyStore's tenant-scope guard pass.
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var session = store.LightweightSession(userId.ToString());

        var llm = Substitute.For<ICoachingLlm>();
        llm.GenerateStructuredAsync<OnboardingTurnOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => (BuildValidLlmOutput(), AnthropicUsage.Zero));

        var assembler = Substitute.For<IContextAssembler>();
        assembler.ComposeForOnboardingAsync(
                Arg.Any<OnboardingView>(),
                Arg.Any<OnboardingTopic>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new OnboardingPromptComposition(
                SystemPrompt: "system",
                UserMessage: "user",
                Findings: ImmutableArray<SanitizationFinding>.Empty));

        var sanitizer = Substitute.For<IPromptSanitizer>();
        var planGen = Substitute.For<IPlanGenerationService>();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        var idempotency = new MartenIdempotencyStore(
            session,
            time,
            NullLogger<MartenIdempotencyStore>.Instance);

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

        return (scope, session);
    }
}
