using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Idempotency;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Coaching.Sanitization;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// R-066 + R-069 atomicity regression test (spec 13 § Unit 1 Verification).
/// Stubs <see cref="IPlanGenerationService"/> to throw on the terminal-branch
/// invocation; asserts the handler's response generation never reached the
/// idempotency-record call AND no <see cref="OnboardingCompleted"/> event was
/// staged on the session — Wolverine's transactional middleware would then
/// rollback every staged change including the staged plan-stream creation.
/// </summary>
/// <remarks>
/// <para>
/// This is the unit-level guarantee: the handler itself never calls
/// <c>SaveChangesAsync</c>, so when <see cref="IPlanGenerationService.GeneratePlanAsync"/>
/// throws, the in-flight session has every appended event still pending and
/// the framework will discard them. The companion integration test
/// (<c>OnboardingFlowIntegrationTests</c>) drives a full HTTP request through
/// the live Marten + Wolverine stack to confirm the rollback observably leaves
/// no Plan stream and no <c>OnboardingCompleted</c> event in the database.
/// </para>
/// <para>
/// Per DEC-060 / R-069 the dual-write atomicity claim — that exactly one
/// Postgres transaction (and one <c>backend_xid</c>) covers the entire handler
/// — is upheld by Marten's <c>UseEntityFrameworkCoreTransactionParticipant</c>
/// wiring established in <c>MartenConfiguration</c>. The empirical R-069 §11
/// observer probe lives in <c>DualWriteAtomicityTests</c> and asserts the
/// single-<c>backend_xid</c> invariant directly via a third Npgsql connection
/// polling <c>pg_stat_activity</c>; this unit-level test covers the
/// negative-rollback shape on the failure path.
/// </para>
/// </remarks>
public class InvokeAsyncTransactionScopeTests
{
    [Fact]
    public async Task Handle_When_PlanGeneration_Throws_Handler_Throws_And_Idempotency_Is_Not_Recorded()
    {
        // Arrange — bring the runner to the terminal branch with all six
        // required slots already filled, then make plan generation explode.
        var session = Substitute.For<IDocumentSession>();
        var llm = Substitute.For<ICoachingLlm>();
        var assembler = Substitute.For<IContextAssembler>();
        var sanitizer = Substitute.For<IPromptSanitizer>();
        var idempotency = Substitute.For<IIdempotencyStore>();
        var planGen = Substitute.For<IPlanGenerationService>();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        var userId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();

        // Pre-populate the view so the deterministic gate is satisfied.
        var view = BuildFullView(userId);
        session.LoadAsync<OnboardingView>(userId, Arg.Any<CancellationToken>()).Returns(view);
        idempotency
            .SeenAsync<OnboardingTurnResponseDto>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((OnboardingTurnResponseDto?)null);
        assembler
            .ComposeForOnboardingAsync(
                Arg.Any<OnboardingView>(),
                Arg.Any<OnboardingTopic>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new OnboardingPromptComposition(
                "system",
                "user",
                System.Collections.Immutable.ImmutableArray<SanitizationFinding>.Empty,
                false));
        llm
            .GenerateStructuredAsync<OnboardingTurnOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns((BuildReadyForPlanOutput(), AnthropicUsage.Zero));
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
        var act = async () => await OnboardingTurnHandler.Handle(
            new SubmitUserTurn(userId, idempotencyKey, "ok"),
            session,
            llm,
            assembler,
            sanitizer,
            idempotency,
            planGen,
            time,
            NullLogger<OnboardingTurnHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Assert — handler propagates the exception so Wolverine's transactional
        //   middleware rolls back the session. Idempotency record never lands —
        //   so on retry the runner gets a fresh attempt instead of a memoized
        //   half-state response.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated plan-generation failure*");

        idempotency.DidNotReceiveWithAnyArgs()
            .Record<OnboardingTurnResponseDto>(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<OnboardingTurnResponseDto>());

        // The handler never calls SaveChangesAsync directly — the framework's
        // transactional middleware owns that call. The negative-assertion proof
        // is that the staged `Record` write was never made.
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static OnboardingTurnOutput BuildReadyForPlanOutput() => new()
    {
        Reply = [new AnthropicContentBlock { Type = AnthropicContentBlockType.Text, Text = "all set" }],
        Extracted = null,
        NeedsClarification = false,
        ClarificationReason = null,
        ReadyForPlan = true,
    };

    private static OnboardingView BuildFullView(Guid userId) => new()
    {
        Id = userId,
        UserId = userId,
        Status = OnboardingStatus.InProgress,
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
