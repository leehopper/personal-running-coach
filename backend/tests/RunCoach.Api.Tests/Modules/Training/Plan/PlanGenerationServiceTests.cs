using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Coaching.Prompts;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Tests.Modules.Training.Plan;

/// <summary>
/// Unit tests over <see cref="PlanGenerationService"/> per Slice 1 § Unit 2
/// R02.4-R02.6 (DEC-057 / R-066). Covers the six-call sequential ordering
/// (1 macro + 4 meso + 1 micro), the <see cref="CacheControl.Ephemeral1h"/>
/// breakpoint on every call, the partial-failure path (failure on the 4th
/// meso call throws and returns no events), the input-prompt-stability rule
/// (identical snapshot produces identical macro prompt bytes), and the
/// <c>previousPlanId</c> threading onto <see cref="PlanGenerated"/>.
/// </summary>
public sealed class PlanGenerationServiceTests
{
    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");

    private static readonly Guid PlanId = new("22222222-2222-2222-2222-222222222222");

    private static readonly Guid PreviousPlanId = new("33333333-3333-3333-3333-333333333333");

    private static readonly DateTimeOffset Now = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GeneratePlanAsync_InvokesSixCallsInMacroMesoMicroOrder()
    {
        // Arrange
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — exactly one macro call, four meso calls, one micro call.
        await llm.Received(1)
            .GenerateStructuredAsync<MacroPlanOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>());

        await llm.Received(PlanGenerationService.MesoWeekCount)
            .GenerateStructuredAsync<MesoWeekOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>());

        await llm.Received(1)
            .GenerateStructuredAsync<MicroWorkoutListOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeneratePlanAsync_ReturnsCanonicalEventSequence()
    {
        // Arrange
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        var events = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — sequence is exactly [PlanGenerated, MesoCycleCreated x4, FirstMicroCycleCreated].
        events.Should().HaveCount(6);
        events[0].Should().BeOfType<PlanGenerated>();
        events[1].Should().BeOfType<MesoCycleCreated>();
        events[2].Should().BeOfType<MesoCycleCreated>();
        events[3].Should().BeOfType<MesoCycleCreated>();
        events[4].Should().BeOfType<MesoCycleCreated>();
        events[5].Should().BeOfType<FirstMicroCycleCreated>();

        var weekIndices = events.OfType<MesoCycleCreated>().Select(e => e.WeekIndex).ToArray();
        weekIndices.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task GeneratePlanAsync_StampsModelIdAndPromptVersionOnPlanGenerated()
    {
        // Arrange
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        var events = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        var generated = events[0].Should().BeOfType<PlanGenerated>().Which;
        generated.PlanId.Should().Be(PlanId);
        generated.UserId.Should().Be(UserId);
        generated.PromptVersion.Should().Be("v1");
        generated.ModelId.Should().Be("test-model-id");
        generated.GeneratedAt.Should().Be(Now);
    }

    [Fact]
    public async Task GeneratePlanAsync_PreviousPlanIdNonNull_ThreadsOntoPlanGenerated()
    {
        // Arrange — Unit 5 regenerate flow passes the prior plan id; the
        // service threads it through verbatim onto the stream-creation event.
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        var events = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: PreviousPlanId, TestContext.Current.CancellationToken);

        // Assert
        var generated = events[0].Should().BeOfType<PlanGenerated>().Which;
        generated.PreviousPlanId.Should().Be(PreviousPlanId);
    }

    [Fact]
    public async Task GeneratePlanAsync_PreviousPlanIdNull_LeavesPlanGeneratedPreviousNull()
    {
        // Arrange — Unit 1 onboarding-terminal flow passes null; the projection
        // surface treats null as "this is the first plan".
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);

        // Act
        var events = await sut.GeneratePlanAsync(CreateCompletedView(), UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        events[0].Should().BeOfType<PlanGenerated>().Which.PreviousPlanId.Should().BeNull();
    }

    [Fact]
    public async Task GeneratePlanAsync_SetsCacheControlEphemeral1hOnEveryCall()
    {
        // Arrange
        var capturedCacheControls = new List<CacheControl?>();
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm, cacheCapture: capturedCacheControls);

        // Act
        await sut.GeneratePlanAsync(CreateCompletedView(), UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — exactly six calls and every one carries the 1h ephemeral marker.
        capturedCacheControls.Should().HaveCount(6);
        capturedCacheControls.Should().AllSatisfy(c =>
        {
            c.Should().NotBeNull();
            c!.Type.Should().Be("ephemeral");
            c.Ttl.Should().Be("1h");
        });
        capturedCacheControls.Should().AllBeEquivalentTo(CacheControl.Ephemeral1h);
    }

    [Fact]
    public async Task GeneratePlanAsync_FailureOnFourthMesoCall_ThrowsAndReturnsNoEvents()
    {
        // Arrange — macro succeeds, mesos 1-3 succeed, meso 4 throws. The
        // service must propagate the exception without returning any partial
        // event list. The caller's transactional middleware then rolls back.
        var (sut, llm, _) = CreateSut();
        ConfigureMacroSuccess(llm);

        var mesoCalls = 0;
        llm
            .GenerateStructuredAsync<MesoWeekOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                mesoCalls++;
                if (mesoCalls == 4)
                {
                    throw new InvalidOperationException("simulated meso 4 failure");
                }

                return WithZeroUsage(BuildMeso(mesoCalls, PhaseType.Base, isDeload: false));
            });

        // Act
        var act = () => sut.GeneratePlanAsync(CreateCompletedView(), UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — exception propagates; micro is never called.
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("meso 4 failure");

        await llm.DidNotReceive()
            .GenerateStructuredAsync<MicroWorkoutListOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeneratePlanAsync_TwoInvocations_SameSnapshot_ProduceIdenticalMacroUserMessage()
    {
        // Arrange — two replays of the chain on the same captured snapshot
        // must hand the LLM byte-identical macro user-message bytes so
        // Anthropic's prompt-prefix cache hits on call 1 of replay 2.
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);

        var capturedMacroPrompts = new List<string>();
        llm
            .GenerateStructuredAsync<MacroPlanOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedMacroPrompts.Add(call.ArgAt<string>(1));
                return WithZeroUsage(BuildMacro());
            });

        var view = CreateCompletedView();

        // Act
        await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);
        await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — both macro prompts are byte-identical.
        capturedMacroPrompts.Should().HaveCount(2);
        capturedMacroPrompts[0].Should().Be(capturedMacroPrompts[1]);
    }

    [Fact]
    public async Task GeneratePlanAsync_NullProfileSnapshot_Throws()
    {
        // Arrange
        var (sut, _, _) = CreateSut();

        // Act
        var act = () => sut.GeneratePlanAsync(profileSnapshot: null!, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(1, PhaseType.Base, false)]
    [InlineData(8, PhaseType.Base, true)] // last week of an 8-week phase that includes deload.
    [InlineData(9, PhaseType.Build, false)] // first week of next phase.
    public void WeekContext_FromMacro_DerivesPhaseAndDeloadCandidate(
        int weekIndex,
        PhaseType expectedPhase,
        bool expectedDeloadCandidate)
    {
        // Arrange — 8-week Base (includes deload) followed by 4-week Build.
        var macro = new MacroPlanOutput
        {
            TotalWeeks = 12,
            GoalDescription = "Half Marathon",
            Phases = new[]
            {
                new PlanPhaseOutput
                {
                    PhaseType = PhaseType.Base,
                    Weeks = 8,
                    WeeklyDistanceStartKm = 30,
                    WeeklyDistanceEndKm = 50,
                    IntensityDistribution = "80/20",
                    AllowedWorkoutTypes = new[] { WorkoutType.Easy, WorkoutType.LongRun },
                    TargetPaceEasySecPerKm = 360,
                    TargetPaceFastSecPerKm = 300,
                    Notes = string.Empty,
                    IncludesDeload = true,
                },
                new PlanPhaseOutput
                {
                    PhaseType = PhaseType.Build,
                    Weeks = 4,
                    WeeklyDistanceStartKm = 50,
                    WeeklyDistanceEndKm = 60,
                    IntensityDistribution = "70/30",
                    AllowedWorkoutTypes = new[] { WorkoutType.Easy, WorkoutType.Tempo },
                    TargetPaceEasySecPerKm = 350,
                    TargetPaceFastSecPerKm = 280,
                    Notes = string.Empty,
                    IncludesDeload = false,
                },
            },
            Rationale = string.Empty,
            Warnings = string.Empty,
        };

        // Act
        var actual = PlanGenerationService.WeekContext.FromMacro(macro, weekIndex);

        // Assert
        actual.WeekIndex.Should().Be(weekIndex);
        actual.PhaseType.Should().Be(expectedPhase);
        actual.IsDeloadCandidate.Should().Be(expectedDeloadCandidate);
    }

    [Fact]
    public void WeekContext_FromMacro_NoPhases_ReturnsBaseDefault()
    {
        // Arrange — defensive path: macro with empty Phases shouldn't crash.
        var macro = new MacroPlanOutput
        {
            TotalWeeks = 0,
            GoalDescription = string.Empty,
            Phases = Array.Empty<PlanPhaseOutput>(),
            Rationale = string.Empty,
            Warnings = string.Empty,
        };

        // Act
        var actual = PlanGenerationService.WeekContext.FromMacro(macro, weekIndex: 1);

        // Assert
        actual.PhaseType.Should().Be(PhaseType.Base);
        actual.IsDeloadCandidate.Should().BeFalse();
    }

    [Fact]
    public void WeekContext_FromMacro_WeekIndexBelowOne_Throws()
    {
        // Arrange
        var macro = BuildMacro();

        // Act
        var act = () => PlanGenerationService.WeekContext.FromMacro(macro, weekIndex: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static (PlanGenerationService Sut, ICoachingLlm Llm, IContextAssembler Assembler) CreateSut()
    {
        var assembler = Substitute.For<IContextAssembler>();
        assembler
            .ComposeForPlanGenerationAsync(
                Arg.Any<OnboardingView>(),
                Arg.Any<RegenerationIntent?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                // Mirror the real assembler's deterministic shape: a stable
                // system prompt + a snapshot-derived user message that
                // appends the optional intent at the end.
                var view = call.ArgAt<OnboardingView>(0);
                var intent = call.ArgAt<RegenerationIntent?>(1);
                var userMessage = $"PROFILE SNAPSHOT for {view.UserId}\nPrimaryGoal: {view.PrimaryGoal?.Goal}";
                if (intent is not null)
                {
                    userMessage += $"\n\n[Regeneration intent provided by user]\n{intent.FreeText}";
                }

                return new PlanGenerationPromptComposition(
                    SystemPrompt: "stable test system prompt",
                    UserMessage: userMessage);
            });

        var llm = Substitute.For<ICoachingLlm>();

        var promptStore = Substitute.For<IPromptStore>();
        promptStore.GetActiveVersion(ContextAssembler.CoachingPromptId).Returns("v1");

        var settings = Options.Create(new CoachingLlmSettings
        {
            ApiKey = "[REDACTED]",
            ModelId = "test-model-id",
        });

        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);

        var sut = new PlanGenerationService(
            assembler,
            llm,
            promptStore,
            settings,
            timeProvider,
            NullLogger<PlanGenerationService>.Instance);

        return (sut, llm, assembler);
    }

    /// <summary>
    /// Wraps a structured-output payload in the <c>(Result, Usage)</c> tuple
    /// shape <see cref="ICoachingLlm.GenerateStructuredAsync{T}(string, string, CancellationToken)"/>
    /// returns. Tests that don't care about the per-call usage counters return
    /// <see cref="AnthropicUsage.Zero"/> so the chain-wide rollup arithmetic
    /// still has a well-defined value (cache_hit_rate = 0.0 when no tokens
    /// flowed).
    /// </summary>
    private static (T Result, AnthropicUsage Usage) WithZeroUsage<T>(T result) =>
        (result, AnthropicUsage.Zero);

    private static void ConfigureMacroSuccess(ICoachingLlm llm)
    {
        llm
            .GenerateStructuredAsync<MacroPlanOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => WithZeroUsage(BuildMacro()));
    }

    private static void ConfigureLlmHappyPath(
        ICoachingLlm llm,
        List<CacheControl?>? cacheCapture = null)
    {
        ConfigureMacroSuccess(llm);

        var mesoCounter = 0;
        llm
            .GenerateStructuredAsync<MesoWeekOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                mesoCounter++;
                return WithZeroUsage(BuildMeso(mesoCounter, PhaseType.Base, isDeload: false));
            });

        llm
            .GenerateStructuredAsync<MicroWorkoutListOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => WithZeroUsage(BuildMicro()));

        if (cacheCapture is not null)
        {
            llm
                .When(static x => x.GenerateStructuredAsync<MacroPlanOutput>(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                    Arg.Any<CacheControl?>(),
                    Arg.Any<CancellationToken>()))
                .Do(call => cacheCapture.Add(call.ArgAt<CacheControl?>(3)));

            llm
                .When(static x => x.GenerateStructuredAsync<MesoWeekOutput>(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                    Arg.Any<CacheControl?>(),
                    Arg.Any<CancellationToken>()))
                .Do(call => cacheCapture.Add(call.ArgAt<CacheControl?>(3)));

            llm
                .When(static x => x.GenerateStructuredAsync<MicroWorkoutListOutput>(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                    Arg.Any<CacheControl?>(),
                    Arg.Any<CancellationToken>()))
                .Do(call => cacheCapture.Add(call.ArgAt<CacheControl?>(3)));
        }
    }

    private static OnboardingView CreateCompletedView() => new()
    {
        Id = Guid.Parse("00000000-0000-0000-0000-000000000010"),
        UserId = Guid.Parse("00000000-0000-0000-0000-000000000010"),
        TenantId = "00000000-0000-0000-0000-000000000010",
        Status = OnboardingStatus.Completed,
        OnboardingStartedAt = new DateTimeOffset(2026, 04, 25, 12, 0, 0, TimeSpan.Zero),
        OnboardingCompletedAt = new DateTimeOffset(2026, 04, 25, 12, 30, 0, TimeSpan.Zero),
        Version = 12,
        PrimaryGoal = new PrimaryGoalAnswer
        {
            Goal = PrimaryGoal.RaceTraining,
            Description = "training for a half marathon",
        },
    };

    private static MacroPlanOutput BuildMacro()
    {
        return new MacroPlanOutput
        {
            TotalWeeks = 12,
            GoalDescription = "Half Marathon",
            Phases = new[]
            {
                new PlanPhaseOutput
                {
                    PhaseType = PhaseType.Base,
                    Weeks = 8,
                    WeeklyDistanceStartKm = 30,
                    WeeklyDistanceEndKm = 50,
                    IntensityDistribution = "80/20",
                    AllowedWorkoutTypes = new[] { WorkoutType.Easy, WorkoutType.LongRun, WorkoutType.Recovery },
                    TargetPaceEasySecPerKm = 360,
                    TargetPaceFastSecPerKm = 300,
                    Notes = "Aerobic base.",
                    IncludesDeload = true,
                },
                new PlanPhaseOutput
                {
                    PhaseType = PhaseType.Build,
                    Weeks = 4,
                    WeeklyDistanceStartKm = 50,
                    WeeklyDistanceEndKm = 60,
                    IntensityDistribution = "70/30",
                    AllowedWorkoutTypes = new[] { WorkoutType.Easy, WorkoutType.Tempo },
                    TargetPaceEasySecPerKm = 350,
                    TargetPaceFastSecPerKm = 280,
                    Notes = "Race-specific build.",
                    IncludesDeload = false,
                },
            },
            Rationale = "Base then build.",
            Warnings = "Stop on sharp pain.",
        };
    }

    private static MesoWeekOutput BuildMeso(int weekNumber, PhaseType phase, bool isDeload)
    {
        var slot = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Run,
            WorkoutType = WorkoutType.Easy,
            Notes = "Easy aerobic.",
        };

        var rest = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Rest,
            WorkoutType = null,
            Notes = "Rest.",
        };

        return new MesoWeekOutput
        {
            WeekNumber = weekNumber,
            PhaseType = phase,
            WeeklyTargetKm = isDeload ? 30 : 45,
            IsDeloadWeek = isDeload,
            Sunday = slot,
            Monday = rest,
            Tuesday = slot,
            Wednesday = rest,
            Thursday = slot,
            Friday = rest,
            Saturday = slot,
            WeekSummary = $"Week {weekNumber}",
        };
    }

    private static MicroWorkoutListOutput BuildMicro()
    {
        return new MicroWorkoutListOutput
        {
            Workouts = new[]
            {
                new WorkoutOutput
                {
                    DayOfWeek = 0,
                    WorkoutType = WorkoutType.Easy,
                    Title = "Easy run",
                    TargetDistanceKm = 8,
                    TargetDurationMinutes = 50,
                    TargetPaceEasySecPerKm = 360,
                    TargetPaceFastSecPerKm = 360,
                    Segments = new[]
                    {
                        new WorkoutSegmentOutput
                        {
                            SegmentType = SegmentType.Work,
                            DurationMinutes = 50,
                            TargetPaceSecPerKm = 360,
                            Intensity = IntensityProfile.Easy,
                            Repetitions = 1,
                            Notes = "Steady aerobic.",
                        },
                    },
                    WarmupNotes = string.Empty,
                    CooldownNotes = string.Empty,
                    CoachingNotes = "Conversational pace.",
                    PerceivedEffort = 3,
                },
            },
        };
    }
}
