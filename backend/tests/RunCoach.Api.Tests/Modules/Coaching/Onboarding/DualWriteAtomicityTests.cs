using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using NSubstitute;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Idempotency;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Coaching.Sanitization;
using RunCoach.Api.Modules.Identity.Entities;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Empirical R-069 §11 regression test for DEC-060's atomic-dual-write claim.
/// Drives the onboarding terminal-branch handler and the regenerate handler
/// against a live Marten + EF stack, while a third observer
/// <see cref="NpgsqlConnection"/> polls
/// <c>pg_stat_activity.backend_xid</c> snapshots throughout the handler run.
/// Asserts that exactly ONE distinct <c>backend_xid</c> is observed for app
/// connections — proving Marten's <c>IDocumentSession</c> is the only Postgres
/// transaction Wolverine's transactional middleware brackets and the EF
/// projection's writes ride that same transaction via
/// <c>UseEntityFrameworkCoreTransactionParticipant</c> (DEC-060).
/// </summary>
/// <remarks>
/// <para>
/// Why a third observer connection: Marten's session and the EF projection's
/// <c>DbContext</c> would each be bound to their own <c>NpgsqlConnection</c>
/// in a naive dual-write design — distinct backends, distinct
/// <c>backend_xid</c> values. With the participant wired up, the EF context
/// enrolls in Marten's transaction so all writes land on a single backend with
/// a single <c>backend_xid</c>. A SELECT-only backend never gets an xid
/// assigned, so the polling reliably ignores read-only chatter and only
/// captures backends that actually wrote.
/// </para>
/// <para>
/// Polling cadence vs. handler duration: the polling thread snapshots every
/// 25ms; the stub <see cref="DelayingPlanGenerationService"/> blocks for
/// 200ms inside <c>GeneratePlanAsync</c>. If a second backend enrolled with
/// its own xid mid-handler, the polling window almost certainly catches it.
/// The handler's writes themselves are sub-millisecond Postgres ops, but the
/// transaction stays open from the first write through SaveChangesAsync, so
/// the active backend remains visible in pg_stat_activity for the full
/// handler duration.
/// </para>
/// <para>
/// Filter strategy: snapshots are taken from the observer's pid only (via
/// <c>pid &lt;&gt; pg_backend_pid()</c>), and only rows where
/// <c>backend_xid IS NOT NULL</c> are kept. This excludes the observer itself
/// (it never writes) AND any pooled idle backends that happen to belong to
/// the same data source. Marten's pool may surface multiple pids if the
/// session's pooled connection is recycled between writes, but they all
/// share the same xid because they all run inside one outer transaction
/// committed via the participant.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public class DualWriteAtomicityTests(RunCoachAppFactory factory) : DbBackedIntegrationTestBase(factory)
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";

    // 1ms poll interval keeps the observer well under the SaveChangesAsync
    // write phase (BEGIN + INSERT + COMMIT) which Marten + EF runs at
    // roughly 10-30ms wall clock for the canonical Slice 1 event payload.
    // R-069 §11's reference uses 5ms; tightening to 1ms further reduces
    // whiff probability on fast machines.
    private const int PollIntervalMs = 1;

    // Plan generation runs INSIDE the handler but BEFORE the
    // session.SaveChangesAsync the test harness fires. The delay therefore
    // does not extend the commit window itself; it serves to ensure the
    // poller has time to spin up against the same backend pool, and to make
    // the in-handler timing deterministic across machines.
    private const int PlanGenStubDelayMs = 50;

    /// <summary>
    /// Asserts the onboarding terminal-branch handler runs all writes —
    /// onboarding-stream events (<see cref="UserTurnRecorded"/>,
    /// <see cref="AssistantTurnRecorded"/>, <see cref="AnswerCaptured"/>,
    /// <see cref="PlanLinkedToUser"/>, <see cref="OnboardingCompleted"/>),
    /// the new Plan stream's events, the EF
    /// <see cref="UserProfileFromOnboardingProjection"/> projection write,
    /// and the idempotency marker — under exactly ONE Postgres
    /// <c>backend_xid</c>. This is the load-bearing R-069 §11 proof for
    /// DEC-060: Marten's session is the sole Postgres transaction.
    /// </summary>
    [Fact]
    public async Task OnboardingTerminalHandler_RunsInOnePostgresTransaction()
    {
        // Arrange — provision a user, drive the onboarding stream to the
        //   pre-terminal state (six answers captured; gate satisfied; LLM
        //   awaiting agreement on ReadyForPlan), then run one more SubmitUserTurn
        //   that flips the terminal branch.
        var userId = await SeedUserAsync();
        await SeedOnboardingPreTerminalAsync(userId);

        // Open a third NpgsqlConnection — bypassing DI/Marten/EF entirely —
        //   for the pg_stat_activity observer.
        await using var observer = new NpgsqlConnection(Factory.ConnectionString);
        await observer.OpenAsync(TestContext.Current.CancellationToken);

        var observerPid = await GetBackendPidAsync(observer);
        var snapshots = new List<(int Pid, uint Xid)>();
        using var pollerCts = new CancellationTokenSource();
        var poller = StartObserverPollerAsync(observer, observerPid, snapshots, pollerCts.Token);

        // Act — drive the handler with a stubbed LLM/assembler that returns
        //   ReadyForPlan=true and a stubbed plan generator that delays so the
        //   handler run is slow relative to the polling cadence.
        using var scope = Factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var idempotency = new MartenIdempotencyStore(session);
        var llm = StubLlmReturnsReadyForPlan();
        var assembler = StubAssembler();
        var sanitizer = StubSanitizer();
        var planGen = new DelayingPlanGenerationService(PlanGenStubDelayMs);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));

        var response = await OnboardingTurnHandler.Handle(
            new SubmitUserTurn(userId, Guid.NewGuid(), "ok"),
            session,
            llm,
            assembler,
            sanitizer,
            idempotency,
            planGen,
            time,
            NullLogger<OnboardingTurnHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Wolverine owns SaveChangesAsync in production — stand in for the
        //   transactional middleware here so the framework-managed commit
        //   actually fires and the participant pulls the EF projection write
        //   onto the same Postgres transaction.
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Stop the poller AFTER the commit so the closing window of the
        //   transaction is captured. A small post-commit grace lets in-flight
        //   snapshots finish landing in the list.
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await pollerCts.CancelAsync();
        try
        {
            await poller;
        }
        catch (OperationCanceledException)
        {
            // Expected — the poller observes the cancellation token.
        }

        // Assert — terminal branch fired (sanity check on the act), AND
        //   exactly one distinct backend_xid was seen across the handler run.
        response.Kind.Should().Be(OnboardingTurnKind.Complete);
        response.PlanId.Should().NotBeNull();

        var distinctXids = snapshots.Select(s => s.Xid).Distinct().ToArray();
        var because =
            "DEC-060 / R-069 §11: Marten's session is the only Postgres transaction. "
            + "The EF UserProfileFromOnboardingProjection write enrolls in that same "
            + "transaction via UseEntityFrameworkCoreTransactionParticipant, so every "
            + "write — onboarding-stream events, the new Plan stream, the EF projection "
            + "row, and the idempotency marker — shares one backend_xid. "
            + $"Observed pids/xids: [{string.Join(", ", snapshots.Select(s => $"({s.Pid},{s.Xid})"))}]";
        distinctXids.Should().HaveCount(1, because);
    }

    /// <summary>
    /// Asserts the regenerate handler runs identically — one Postgres
    /// transaction across the new Plan stream, the appended
    /// <see cref="PlanLinkedToUser"/> event on the onboarding stream, the EF
    /// <see cref="UserProfile"/> row update, and the idempotency marker.
    /// Mirror of the onboarding terminal-branch test for the regenerate code
    /// path (the same DEC-060 invariant covers it).
    /// </summary>
    [Fact]
    public async Task RegenerateHandler_RunsInOnePostgresTransaction()
    {
        // Arrange — seed prior plan + completed onboarding so the regenerate
        //   handler reaches the plan-generation call.
        var userId = await SeedUserAsync();
        var initialPlanId = Guid.NewGuid();
        await SeedInitialPlanStreamAsync(userId, initialPlanId);
        await SeedOnboardingCompletionAsync(userId, initialPlanId);

        await using var observer = new NpgsqlConnection(Factory.ConnectionString);
        await observer.OpenAsync(TestContext.Current.CancellationToken);
        var observerPid = await GetBackendPidAsync(observer);
        var snapshots = new List<(int Pid, uint Xid)>();
        using var pollerCts = new CancellationTokenSource();
        var poller = StartObserverPollerAsync(observer, observerPid, snapshots, pollerCts.Token);

        // Act
        using var scope = Factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var idempotency = new MartenIdempotencyStore(session);
        var planGen = new DelayingPlanGenerationService(PlanGenStubDelayMs);

        var response = await RegeneratePlanHandler.Handle(
            new RegeneratePlanCommand(userId, Intent: null, Guid.NewGuid()),
            session,
            planGen,
            idempotency,
            NullLogger<RegeneratePlanHandler>.Instance,
            TestContext.Current.CancellationToken);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Task.Delay(50, TestContext.Current.CancellationToken);
        await pollerCts.CancelAsync();
        try
        {
            await poller;
        }
        catch (OperationCanceledException)
        {
            // Expected — the poller observes the cancellation token.
        }

        // Assert
        response.PlanId.Should().NotBe(initialPlanId);
        response.Status.Should().Be("generated");

        var distinctXids = snapshots.Select(s => s.Xid).Distinct().ToArray();
        var because =
            "DEC-060 / R-069 §11: regenerate also runs under one Postgres transaction. "
            + $"Observed pids/xids: [{string.Join(", ", snapshots.Select(s => $"({s.Pid},{s.Xid})"))}]";
        distinctXids.Should().HaveCount(1, because);
    }

    /// <summary>
    /// Marten state lives in <c>runcoach_events</c>, which Respawn skips.
    /// Reset it explicitly so seeded streams + idempotency markers do not
    /// leak between tests.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static async Task<int> GetBackendPidAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand("SELECT pg_backend_pid()", conn);
        var result = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Task StartObserverPollerAsync(
        NpgsqlConnection observer,
        int observerPid,
        List<(int Pid, uint Xid)> snapshots,
        CancellationToken ct)
    {
        return Task.Run(() => PollAsync(observer, observerPid, snapshots, ct), ct);
    }

    private static async Task PollAsync(
        NpgsqlConnection observer,
        int observerPid,
        List<(int Pid, uint Xid)> snapshots,
        CancellationToken ct)
    {
        // Poll pg_stat_activity for backends with an active txid that are NOT
        // the observer connection. backend_xid is `xid` in Postgres, which
        // Npgsql surfaces as uint. Casting through ::text::bigint keeps it
        // portable across Npgsql versions where xid mapping shifts.
        const string sql =
            "SELECT pid, backend_xid::text::bigint FROM pg_stat_activity "
            + "WHERE pid <> @observerPid AND backend_xid IS NOT NULL";

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var cmd = new NpgsqlCommand(sql, observer);
                cmd.Parameters.AddWithValue("observerPid", observerPid);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var pid = reader.GetInt32(0);
                    var xid = (uint)reader.GetInt64(1);

                    // Mutex-free append: the test thread reads `snapshots`
                    // only after cancelling and awaiting this task, so the
                    // `List<>.Add` races aren't observable. ConcurrentBag
                    // would be slightly cleaner but isn't needed given the
                    // strict happens-before edge.
                    snapshots.Add((pid, xid));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await Task.Delay(PollIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static ICoachingLlm StubLlmReturnsReadyForPlan()
    {
        var stub = NSubstitute.Substitute.For<ICoachingLlm>();
        stub.GenerateStructuredAsync<OnboardingTurnOutput>(
                NSubstitute.Arg.Any<string>(),
                NSubstitute.Arg.Any<string>(),
                NSubstitute.Arg.Any<IReadOnlyDictionary<string, JsonElement>>(),
                NSubstitute.Arg.Any<CacheControl?>(),
                NSubstitute.Arg.Any<CancellationToken>())
            .Returns(new OnboardingTurnOutput
            {
                Reply =
                [
                    new AnthropicContentBlock
                    {
                        Type = AnthropicContentBlockType.Text,
                        Text = "all set, generating your plan now",
                    },
                ],
                Extracted = null,
                NeedsClarification = false,
                ClarificationReason = null,
                ReadyForPlan = true,
            });
        return stub;
    }

    private static IContextAssembler StubAssembler()
    {
        var stub = NSubstitute.Substitute.For<IContextAssembler>();
        stub.ComposeForOnboardingAsync(
                NSubstitute.Arg.Any<OnboardingView>(),
                NSubstitute.Arg.Any<OnboardingTopic>(),
                NSubstitute.Arg.Any<string>(),
                NSubstitute.Arg.Any<CancellationToken>())
            .Returns(new OnboardingPromptComposition(
                "system",
                "user",
                System.Collections.Immutable.ImmutableArray<SanitizationFinding>.Empty,
                Neutralized: false));
        return stub;
    }

    private static IPromptSanitizer StubSanitizer() =>
        NSubstitute.Substitute.For<IPromptSanitizer>();

    private static MacroPlanOutput BuildMacro(string goal) => new()
    {
        TotalWeeks = 16,
        GoalDescription = goal,
        Phases = new[]
        {
            new PlanPhaseOutput
            {
                PhaseType = PhaseType.Base,
                Weeks = 8,
                WeeklyDistanceStartKm = 30,
                WeeklyDistanceEndKm = 50,
                IntensityDistribution = "80/20 easy/hard",
                AllowedWorkoutTypes = new[] { WorkoutType.Easy, WorkoutType.LongRun, WorkoutType.Recovery },
                TargetPaceEasySecPerKm = 360,
                TargetPaceFastSecPerKm = 300,
                Notes = "Aerobic base build.",
                IncludesDeload = true,
            },
        },
        Rationale = "Progressive base then build to race specificity.",
        Warnings = "Stop and reassess if any sharp pain emerges.",
    };

    private static MesoWeekOutput BuildMeso(int weekNumber, PhaseType phase, bool isDeload)
    {
        var rest = new MesoDaySlotOutput { SlotType = DaySlotType.Rest, WorkoutType = null, Notes = "Recovery." };
        var easy = new MesoDaySlotOutput { SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Easy, Notes = "Easy aerobic." };
        return new MesoWeekOutput
        {
            WeekNumber = weekNumber,
            PhaseType = phase,
            WeeklyTargetKm = isDeload ? 30 : 45,
            IsDeloadWeek = isDeload,
            Sunday = easy,
            Monday = rest,
            Tuesday = easy,
            Wednesday = rest,
            Thursday = easy,
            Friday = rest,
            Saturday = easy,
            WeekSummary = $"Week {weekNumber} - {phase}.",
        };
    }

    private static MicroWorkoutListOutput BuildMicro() => new()
    {
        Workouts = new[]
        {
            new WorkoutOutput
            {
                DayOfWeek = 0,
                WorkoutType = WorkoutType.Easy,
                Title = "Easy Aerobic Run",
                TargetDistanceKm = 8,
                TargetDurationMinutes = 50,
                TargetPaceEasySecPerKm = 360,
                TargetPaceFastSecPerKm = 360,
                Segments = new[]
                {
                    new WorkoutSegmentOutput
                    {
                        SegmentType = SegmentType.Warmup,
                        DurationMinutes = 10,
                        TargetPaceSecPerKm = 400,
                        Intensity = IntensityProfile.Easy,
                        Repetitions = 1,
                        Notes = "Warm up gradually.",
                    },
                    new WorkoutSegmentOutput
                    {
                        SegmentType = SegmentType.Work,
                        DurationMinutes = 30,
                        TargetPaceSecPerKm = 360,
                        Intensity = IntensityProfile.Easy,
                        Repetitions = 1,
                        Notes = "Steady aerobic effort.",
                    },
                    new WorkoutSegmentOutput
                    {
                        SegmentType = SegmentType.Cooldown,
                        DurationMinutes = 10,
                        TargetPaceSecPerKm = 420,
                        Intensity = IntensityProfile.Easy,
                        Repetitions = 1,
                        Notes = "Cool down easy.",
                    },
                },
                WarmupNotes = "10 min walk-jog.",
                CooldownNotes = "10 min walk-jog.",
                CoachingNotes = "Conversational pace.",
                PerceivedEffort = 3,
            },
        },
    };

    private async Task<Guid> SeedUserAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"atomic-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { Email = email, UserName = email };
        var result = await users.CreateAsync(user, StrongPassword);
        result.Succeeded.Should()
            .BeTrue(because: $"seed must succeed — got [{string.Join(", ", result.Errors.Select(e => e.Code))}]");
        return user.Id;
    }

    private async Task SeedOnboardingPreTerminalAsync(Guid userId)
    {
        // Seed every captured-answer event so the deterministic completion
        // gate is satisfied on the next turn. The handler will:
        //   - load the OnboardingView (gate already true),
        //   - call the stubbed LLM (returns ReadyForPlan=true),
        //   - flip into the terminal branch and run plan generation,
        //   - stage events + EF projection write under one transaction.
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var startedAt = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

        session.Events.StartStream<OnboardingView>(
            userId,
            new OnboardingStarted(userId, startedAt),
            new AnswerCaptured(
                OnboardingTopic.PrimaryGoal,
                JsonSerializer.SerializeToDocument(new PrimaryGoalAnswer
                {
                    Goal = PrimaryGoal.GeneralFitness,
                    Description = "fitness",
                }),
                Confidence: 0.9,
                CapturedAt: startedAt),
            new AnswerCaptured(
                OnboardingTopic.CurrentFitness,
                JsonSerializer.SerializeToDocument(new CurrentFitnessAnswer
                {
                    TypicalWeeklyKm = 30,
                    LongestRecentRunKm = 12,
                    RecentRaceDistanceKm = null,
                    RecentRaceTimeIso = null,
                    Description = "moderate",
                }),
                Confidence: 0.9,
                CapturedAt: startedAt),
            new AnswerCaptured(
                OnboardingTopic.WeeklySchedule,
                JsonSerializer.SerializeToDocument(new WeeklyScheduleAnswer
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
                }),
                Confidence: 0.9,
                CapturedAt: startedAt),
            new AnswerCaptured(
                OnboardingTopic.InjuryHistory,
                JsonSerializer.SerializeToDocument(new InjuryHistoryAnswer
                {
                    HasActiveInjury = false,
                    ActiveInjuryDescription = string.Empty,
                    PastInjurySummary = "none",
                }),
                Confidence: 0.9,
                CapturedAt: startedAt),
            new AnswerCaptured(
                OnboardingTopic.Preferences,
                JsonSerializer.SerializeToDocument(new PreferencesAnswer
                {
                    PreferredUnits = PreferredUnits.Kilometers,
                    PreferTrail = false,
                    ComfortableWithIntensity = true,
                    Description = "ok",
                }),
                Confidence: 0.9,
                CapturedAt: startedAt),
            new AnswerCaptured(
                OnboardingTopic.TargetEvent,
                JsonSerializer.SerializeToDocument(new TargetEventAnswer
                {
                    EventName = "5k",
                    DistanceKm = 5,
                    EventDateIso = "2026-12-01",
                    TargetFinishTimeIso = null,
                }),
                Confidence: 0.9,
                CapturedAt: startedAt));

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedInitialPlanStreamAsync(Guid userId, Guid planId)
    {
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var generated = new PlanGenerated(
            planId,
            userId,
            BuildMacro("initial"),
            new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
            PromptVersion: "coaching-v1",
            ModelId: "claude-sonnet-4-5",
            PreviousPlanId: null);
        var events = new object[]
        {
            generated,
            new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, false)),
            new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, false)),
            new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, false)),
            new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, true)),
            new FirstMicroCycleCreated(BuildMicro()),
        };
        session.Events.StartStream<PlanProjectionDto>(planId, events);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedOnboardingCompletionAsync(Guid userId, Guid initialPlanId)
    {
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var startedAt = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        var completedAt = new DateTimeOffset(2026, 4, 1, 12, 5, 0, TimeSpan.Zero);
        session.Events.StartStream<OnboardingView>(
            userId,
            new OnboardingStarted(userId, startedAt),
            new PlanLinkedToUser(userId, initialPlanId),
            new OnboardingCompleted(initialPlanId, completedAt));
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Stub <see cref="IPlanGenerationService"/> that delays for a configured
    /// interval so the handler run is slow relative to the observer poller's
    /// 25ms cadence — guarantees the polling window catches any second
    /// <c>backend_xid</c> if the dual-write atomicity claim regresses.
    /// </summary>
    private sealed class DelayingPlanGenerationService(int delayMs) : IPlanGenerationService
    {
        public async Task<IReadOnlyList<object>> GeneratePlanAsync(
            OnboardingView profileSnapshot,
            Guid userId,
            Guid planId,
            RegenerationIntent? intent,
            Guid? previousPlanId,
            CancellationToken ct)
        {
            _ = profileSnapshot;
            _ = intent;
            await Task.Delay(delayMs, ct).ConfigureAwait(false);

            var generated = new PlanGenerated(
                planId,
                userId,
                BuildMacro("regenerated"),
                new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero),
                PromptVersion: "coaching-v1",
                ModelId: "claude-sonnet-4-5",
                PreviousPlanId: previousPlanId);

            return new object[]
            {
                generated,
                new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, false)),
                new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, false)),
                new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, false)),
                new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, true)),
                new FirstMicroCycleCreated(BuildMicro()),
            };
        }
    }
}
