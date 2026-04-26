using FluentAssertions;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RunCoach.Api.Modules.Coaching.Idempotency;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;

namespace RunCoach.Api.Tests.Modules.Training.Plan;

/// <summary>
/// Unit-level R-066 + R-069 atomicity regression test for the regenerate
/// handler (spec 13 § Unit 5 Verification; DEC-060). Models the same shape as
/// <see cref="Modules.Coaching.Onboarding.InvokeAsyncTransactionScopeTests"/>:
/// stub <see cref="IPlanGenerationService"/> so it throws on the simulated
/// 4th meso call, drive the handler, then assert the handler propagates the
/// exception AND every staged side-effect that would have produced an
/// observable post-condition (new Plan stream, fresh
/// <see cref="PlanLinkedToUser"/> event, recorded idempotency response) was
/// never reached.
/// </summary>
/// <remarks>
/// <para>
/// The handler itself never calls <c>SaveChangesAsync</c>; Wolverine's
/// transactional middleware owns the commit. When
/// <see cref="IPlanGenerationService.GeneratePlanAsync"/> throws, control
/// unwinds before the handler reaches <c>StartStream</c>, the
/// <c>PlanLinkedToUser</c> append, or <c>IIdempotencyStore.Record</c>. The
/// negative assertions on those three call sites prove the handler leaves
/// the session with no pending writes related to the regeneration — Wolverine
/// then aborts the transaction so the prior plan stream is unaffected, no
/// new Plan stream exists, and <c>UserProfile.CurrentPlanId</c> stays at its
/// prior value (per the <see cref="UserProfileFromOnboardingProjection"/>
/// inline projection wiring established in
/// <see cref="Infrastructure.MartenConfiguration"/>).
/// </para>
/// <para>
/// Per DEC-060 / R-069 the dual-write atomicity claim — that exactly one
/// Postgres transaction (and one <c>backend_xid</c>) covers the entire
/// handler — is upheld by Marten's
/// <c>UseEntityFrameworkCoreTransactionParticipant</c> wiring. That
/// single-transaction property is asserted via the framework-level test
/// <c>UserProfileFromOnboardingProjection</c> integration coverage rather
/// than a separate <c>pg_stat_activity.backend_xid</c> observer probe (the
/// observer probe is deferred per the same rationale recorded in
/// <c>InvokeAsyncTransactionScopeTests</c>).
/// </para>
/// </remarks>
public class RegenerateTransactionScopeTests
{
    [Fact]
    public async Task Handle_When_PlanGeneration_Throws_Handler_Throws_And_No_Writes_Are_Staged()
    {
        // Arrange — set up the runner with a prior plan linked via the
        //   onboarding view, then make plan generation explode mid-call.
        var session = Substitute.For<IDocumentSession>();
        var planGen = Substitute.For<IPlanGenerationService>();
        var idempotency = Substitute.For<IIdempotencyStore>();
        var userId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        var priorPlanId = Guid.NewGuid();

        // Idempotency miss — the request is brand new, so the handler must
        //   proceed past the short-circuit branch.
        idempotency
            .SeenAsync<RegeneratePlanResponse>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((RegeneratePlanResponse?)null);

        // The onboarding view must carry a non-null CurrentPlanId so the
        //   handler reaches the plan-generation call. Without this slot the
        //   handler would throw a different InvalidOperationException
        //   (defense-in-depth path) before the simulated 4th-meso failure.
        var view = BuildViewWithPriorPlan(userId, priorPlanId);
        session.LoadAsync<OnboardingView>(userId, Arg.Any<CancellationToken>()).Returns(view);

        // Stub plan generation to throw — this stands in for "the 4th meso
        //   call rejected" in the deeper LLM chain. The handler does not
        //   distinguish which call inside `GeneratePlanAsync` failed, only
        //   that the call did.
        planGen
            .GeneratePlanAsync(
                Arg.Any<OnboardingView>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<RegenerationIntent?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("simulated plan-generation failure on the 4th meso call"));

        // Act
        var act = async () => await RegeneratePlanHandler.Handle(
            new RegeneratePlanCommand(userId, Intent: null, idempotencyKey),
            session,
            planGen,
            idempotency,
            NullLogger<RegeneratePlanHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Assert — handler propagates the exception so Wolverine's
        //   transactional middleware rolls back the session.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated plan-generation failure*");

        // The handler MUST NOT have staged the new Plan stream — that call
        //   sits AFTER the plan-generation await on the happy path. If it
        //   ever fires before the await, the generation failure would still
        //   leave staged writes on the session for the framework to
        //   accidentally commit on a parallel happy path. Negative coverage
        //   ensures the call sites stay below the failing await.
        session.Events.DidNotReceiveWithAnyArgs()
            .StartStream<PlanProjectionDto>(Arg.Any<Guid>(), Arg.Any<object[]>());

        // The fresh PlanLinkedToUser event (which would flip
        //   `UserProfile.CurrentPlanId` to a new value via the EF projection)
        //   must never have been appended. This is the load-bearing
        //   atomicity guarantee from DEC-060 / R-069: failed regeneration
        //   leaves the prior plan link intact.
        session.Events.DidNotReceiveWithAnyArgs()
            .Append(Arg.Any<Guid>(), Arg.Any<PlanLinkedToUser>());

        // Idempotency record never lands — so on retry the runner gets a
        //   fresh attempt instead of a memoized half-state response. This is
        //   the same negative invariant the onboarding-side test asserts.
        idempotency.DidNotReceiveWithAnyArgs()
            .Record<RegeneratePlanResponse>(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<RegeneratePlanResponse>());

        // The handler never calls SaveChangesAsync directly — the framework's
        //   transactional middleware owns that call. Asserting the negative
        //   here keeps the contract explicit: a future refactor that
        //   accidentally commits mid-handler would break atomicity and this
        //   test is the trip wire.
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_When_Idempotency_Hit_Short_Circuits_Without_Touching_PlanGen_Or_Session()
    {
        // Arrange — the idempotency store already holds a recorded response
        //   under this key (e.g. the runner re-submitted after a network
        //   blip). The handler must return the cached payload byte-for-byte
        //   and append nothing.
        var session = Substitute.For<IDocumentSession>();
        var planGen = Substitute.For<IPlanGenerationService>();
        var idempotency = Substitute.For<IIdempotencyStore>();
        var userId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        var cachedPlanId = Guid.NewGuid();
        var cachedResponse = new RegeneratePlanResponse(cachedPlanId, "generated");

        idempotency
            .SeenAsync<RegeneratePlanResponse>(idempotencyKey, Arg.Any<CancellationToken>())
            .Returns(cachedResponse);

        // Act
        var actual = await RegeneratePlanHandler.Handle(
            new RegeneratePlanCommand(userId, Intent: null, idempotencyKey),
            session,
            planGen,
            idempotency,
            NullLogger<RegeneratePlanHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Assert — byte-identical replay.
        actual.Should().Be(cachedResponse);

        // Plan generation MUST NOT have been invoked on the cache-hit branch.
        await planGen.DidNotReceiveWithAnyArgs()
            .GeneratePlanAsync(
                Arg.Any<OnboardingView>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<RegenerationIntent?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>());

        // No reads of the onboarding view either — the cache hit short-
        //   circuits before the LoadAsync call.
        await session.DidNotReceiveWithAnyArgs()
            .LoadAsync<OnboardingView>(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        // No writes were staged on the session.
        session.Events.DidNotReceiveWithAnyArgs()
            .StartStream<PlanProjectionDto>(Arg.Any<Guid>(), Arg.Any<object[]>());
        session.Events.DidNotReceiveWithAnyArgs()
            .Append(Arg.Any<Guid>(), Arg.Any<PlanLinkedToUser>());

        // And the handler did NOT re-record the response — replaying the
        //   cached payload is a pure read.
        idempotency.DidNotReceiveWithAnyArgs()
            .Record<RegeneratePlanResponse>(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<RegeneratePlanResponse>());
    }

    private static OnboardingView BuildViewWithPriorPlan(Guid userId, Guid priorPlanId) => new()
    {
        Id = userId,
        UserId = userId,
        Status = OnboardingStatus.Completed,
        OnboardingStartedAt = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
        OnboardingCompletedAt = new DateTimeOffset(2026, 4, 1, 12, 5, 0, TimeSpan.Zero),
        CurrentPlanId = priorPlanId,
        PrimaryGoal = new PrimaryGoalAnswer { Goal = PrimaryGoal.GeneralFitness, Description = "fitness" },
        TargetEvent = null,
        CurrentFitness = new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = 30,
            LongestRecentRunKm = 12,
            RecentRaceDistanceKm = null,
            RecentRaceTimeIso = null,
            Description = "moderate",
        },
        WeeklySchedule = new WeeklyScheduleAnswer
        {
            MaxRunDaysPerWeek = 4,
            TypicalSessionMinutes = 45,
            Monday = true,
            Tuesday = false,
            Wednesday = true,
            Thursday = false,
            Friday = true,
            Saturday = false,
            Sunday = true,
            Description = "evenings",
        },
        InjuryHistory = new InjuryHistoryAnswer
        {
            HasActiveInjury = false,
            ActiveInjuryDescription = string.Empty,
            PastInjurySummary = "none",
        },
        Preferences = new PreferencesAnswer
        {
            PreferredUnits = PreferredUnits.Kilometers,
            PreferTrail = false,
            ComfortableWithIntensity = true,
            Description = "ok",
        },
        OutstandingClarifications = [],
    };
}
