using System.Diagnostics;
using System.Diagnostics.Metrics;
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

    // Phase-week shapes for the F3 validation tests (CA1861: hoisted to static fields
    // so the repeated BuildMacroWithTotalWeeks calls do not allocate a fresh array each time).
    private static readonly int[] NineWeekPhaseWeeks = [5, 4];

    private static readonly int[] SixteenWeekPhaseWeeks = [8, 4, 2, 2];

    private static readonly int[] PhaseSumMismatchWeeks = [6, 4];

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

        // Per CodeRabbit feedback: count assertions alone do not catch a
        // micro-before-meso regression. Inspect the actual invocation
        // sequence via ReceivedCalls() and assert macro → meso × N → micro.
        var llmCallTypes = llm.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ICoachingLlm.GenerateStructuredAsync))
            .Select(c => c.GetMethodInfo().GetGenericArguments()[0])
            .ToArray();

        var expectedSequence = new List<Type>(2 + PlanGenerationService.MesoWeekCount)
        {
            typeof(MacroPlanOutput),
        };
        for (var i = 0; i < PlanGenerationService.MesoWeekCount; i++)
        {
            expectedSequence.Add(typeof(MesoWeekOutput));
        }

        expectedSequence.Add(typeof(MicroWorkoutListOutput));
        llmCallTypes.Should().Equal(
            expectedSequence,
            because: "macro → meso×4 → micro must fire in canonical order");
    }

    [Fact]
    public async Task GeneratePlanAsync_ReturnsCanonicalEventSequence()
    {
        // Arrange
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        var sequence = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — wrapper enforces shape; ToEvents flattens to canonical order.
        sequence.Mesos.Should().HaveCount(PlanEventSequence.ExpectedMesoCount);
        var events = sequence.ToEvents();
        events.Should().HaveCount(6);
        events[0].Should().BeOfType<PlanGenerated>();
        events[1].Should().BeOfType<MesoCycleCreated>();
        events[2].Should().BeOfType<MesoCycleCreated>();
        events[3].Should().BeOfType<MesoCycleCreated>();
        events[4].Should().BeOfType<MesoCycleCreated>();
        events[5].Should().BeOfType<FirstMicroCycleCreated>();

        var weekIndices = sequence.Mesos.Select(e => e.WeekIndex).ToArray();
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
        var sequence = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        var generated = sequence.Macro;
        generated.PlanId.Should().Be(PlanId);
        generated.UserId.Should().Be(UserId);
        generated.PromptVersion.Should().Be("v1");
        generated.ModelId.Should().Be("test-model-id");
        generated.GeneratedAt.Should().Be(Now);

        // PlanStartDate anchors to the Sunday opening the generation week.
        // Now is 2026-04-25 (a Saturday); the preceding Sunday is 2026-04-19.
        generated.PlanStartDate.Should().Be(new DateOnly(2026, 4, 19));
    }

    [Fact]
    public async Task GeneratePlanAsync_AnchorsPlanStartDateToGenerationWeekSunday()
    {
        // Arrange — generate on Wednesday 2026-06-10; week 1, day 0 anchors to the
        // preceding Sunday 2026-06-07 (slice-2b Unit 1 / DEC-076).
        var generationDate = new DateTimeOffset(2026, 6, 10, 9, 30, 0, TimeSpan.Zero);
        var (sut, llm, _) = CreateSut(generationDate);
        ConfigureLlmHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        var sequence = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        sequence.Macro.PlanStartDate.Should().Be(new DateOnly(2026, 6, 7));
    }

    [Fact]
    public async Task GeneratePlanAsync_Regenerate_ReanchorsPlanStartDateToRegenerationWeek()
    {
        // Arrange — regenerating on Tuesday 2026-06-23 re-anchors week 1 to that
        // week's Sunday 2026-06-21; the regenerate flow shares this construction site.
        var regenerationDate = new DateTimeOffset(2026, 6, 23, 18, 0, 0, TimeSpan.Zero);
        var (sut, llm, _) = CreateSut(regenerationDate);
        ConfigureLlmHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        var sequence = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: PreviousPlanId, TestContext.Current.CancellationToken);

        // Assert
        sequence.Macro.PlanStartDate.Should().Be(new DateOnly(2026, 6, 21));
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
        var sequence = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: PreviousPlanId, TestContext.Current.CancellationToken);

        // Assert
        sequence.Macro.PreviousPlanId.Should().Be(PreviousPlanId);
    }

    [Fact]
    public async Task GeneratePlanAsync_PreviousPlanIdNull_LeavesPlanGeneratedPreviousNull()
    {
        // Arrange — Unit 1 onboarding-terminal flow passes null; the projection
        // surface treats null as "this is the first plan".
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);

        // Act
        var sequence = await sut.GeneratePlanAsync(CreateCompletedView(), UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        sequence.Macro.PreviousPlanId.Should().BeNull();
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
    public async Task GeneratePlanAsync_FailureOnFourthMesoCall_EmitsFailureOtel()
    {
        // Arrange — same mock-LLM throw-on-meso-4 setup as the sibling
        // `FailureOnFourthMesoCall_ThrowsAndReturnsNoEvents` test, with an
        // in-process `ActivityListener` and `MeterListener` wired to the
        // `PlanGenerationService.ObservabilitySourceName` source so the
        // catch block's OTel emissions can be asserted.
        var capturedActivities = new List<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PlanGenerationService.ObservabilitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                lock (capturedActivities)
                {
                    capturedActivities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);

        var measurements = new List<(double Value, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == PlanGenerationService.ObservabilitySourceName
                    && instrument.Name == PlanGenerationService.PlanGenerationCompletedMetricName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
        {
            lock (measurements)
            {
                measurements.Add((value, tags.ToArray()));
            }
        });
        meterListener.Start();

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
        await FluentActions
            .Awaiting(() => sut.GeneratePlanAsync(
                CreateCompletedView(),
                UserId,
                PlanId,
                intent: null,
                previousPlanId: null,
                TestContext.Current.CancellationToken))
            .Should()
            .ThrowAsync<InvalidOperationException>();

        // Assert — parent span carries error status + `exception` event.
        var parentSpan = capturedActivities
            .FirstOrDefault(a => a.OperationName == PlanGenerationService.PlanGenerationActivityName);
        parentSpan.Should().NotBeNull(
            because: $"`{PlanGenerationService.PlanGenerationActivityName}` must wrap the whole chain");
        parentSpan!.Status.Should().Be(ActivityStatusCode.Error);
        parentSpan.Events.Should().Contain(
            e => e.Name == "exception",
            because: "`Activity.AddException` records an `ActivityEvent` named `exception` per BCL");

        // Failure measurement: at least one record with `outcome = failure`
        // and the exception type stamped per the catch block's tag bag.
        var failureMeasurements = measurements
            .Where(m => m.Tags.Any(t =>
                t.Key == PlanGenerationTagNames.Outcome
                && string.Equals(t.Value as string, "failure", StringComparison.Ordinal)))
            .ToArray();
        failureMeasurements.Should().HaveCount(1, because: "the catch block records exactly one failure histogram event");

        var failureTags = failureMeasurements[0].Tags;
        failureTags
            .Should().Contain(t =>
                t.Key == PlanGenerationTagNames.ExceptionType
                && (t.Value as string) == typeof(InvalidOperationException).FullName);
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

    [Fact]
    public async Task GeneratePlanAsync_AnchoredHorizon_ValidMacro_AppendsPlanWithLocalStartDate()
    {
        // Arrange — local today 2026-06-12 → plan-start Sunday 2026-06-07; the race on
        // 2026-08-08 is week 9 from that anchor, so a 9-week phase-sum-consistent macro
        // passes the horizon validation as an exact-fit horizon (delta 0). The ±1-week
        // tolerance boundary is covered by the MacroPlanOutputValidator unit tests.
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacro(llm, BuildMacroWithTotalWeeks(9, NineWeekPhaseWeeks));
        ConfigureMesoMicroHappyPath(llm);
        var view = CreateRaceView(eventDateIso: "2026-08-08");

        // Act
        var sequence = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — PlanStartDate anchors to the app-local generation-week Sunday.
        sequence.Macro.PlanStartDate.Should().Be(new DateOnly(2026, 6, 7));
    }

    [Fact]
    public async Task GeneratePlanAsync_AnchoredHorizon_HorizonViolatingMacro_Throws()
    {
        // Arrange — a phase-sum-consistent 16-week macro against a 9-week race horizon
        // exceeds the ±1-week tolerance, so the macro is terminally rejected.
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacro(llm, BuildMacroWithTotalWeeks(16, SixteenWeekPhaseWeeks));
        ConfigureMesoMicroHappyPath(llm);
        var view = CreateRaceView(eventDateIso: "2026-08-08");

        // Act
        var act = () => sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<PlanGenerationRejectedException>())
            .Which.Violation.Should().Be(MacroPlanOutputValidationViolation.HorizonMismatch);
    }

    [Fact]
    public async Task GeneratePlanAsync_NoEventDate_GeneralFitness_DoesNotThrow()
    {
        // Arrange — no target event → non-anchored horizon; the validator only checks
        // phase-sum consistency, which the shared happy-path macro satisfies.
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureLlmHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        var act = () => sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GeneratePlanAsync_PhaseSumMismatch_Throws()
    {
        // Arrange — phases (6 + 4 = 10) do not sum to TotalWeeks (12). The phase-sum check
        // runs on every generation, anchored or not, and rejects the macro terminally.
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacro(llm, BuildMacroWithTotalWeeks(12, PhaseSumMismatchWeeks));
        ConfigureMesoMicroHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        var act = () => sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<PlanGenerationRejectedException>())
            .Which.Violation.Should().Be(MacroPlanOutputValidationViolation.PhaseSumMismatch);
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
        var actual = WeekContext.FromMacro(macro, weekIndex);

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
        var actual = WeekContext.FromMacro(macro, weekIndex: 1);

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
        var act = () => WeekContext.FromMacro(macro, weekIndex: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WeekContext_FromMacro_WeekIndexPastDeclaredPhases_ReturnsLastPhaseWithoutDeload()
    {
        // Arrange — 12-week macro (8 Base + 4 Build); the defensive path at
        // PlanGenerationService.cs:340-341 fires when weekIndex exceeds the
        // cumulative phase coverage (e.g. an LLM under-declared the macro).
        var macro = BuildMacro();
        const int weekIndexPastEnd = 13;

        // Act
        var actual = WeekContext.FromMacro(macro, weekIndexPastEnd);

        // Assert — fall through returns the last declared phase (Build) with
        // IsDeloadCandidate forced false so the deload heuristic stays off
        // for under-declared territory.
        actual.WeekIndex.Should().Be(weekIndexPastEnd);
        actual.PhaseType.Should().Be(PhaseType.Build);
        actual.IsDeloadCandidate.Should().BeFalse();
    }

    private static (PlanGenerationService Sut, ICoachingLlm Llm, IContextAssembler Assembler) CreateSut(
        DateTimeOffset? now = null,
        DateOnly? localToday = null)
    {
        var assembler = Substitute.For<IContextAssembler>();
        assembler
            .ComposeForPlanGenerationAsync(
                Arg.Any<OnboardingView>(),
                Arg.Any<RegenerationIntent?>(),
                Arg.Any<DateOnly>(),
                Arg.Any<PlanHorizon>(),
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

        var effectiveNow = now ?? Now;
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(effectiveNow);

        // The local-date provider drives both the date-aware horizon and the
        // PlanStartDate anchor (F3). It defaults to the calendar day of the pinned
        // clock so the existing PlanStartDate assertions stay valid. Tests that
        // exercise anchoring pass an explicit localToday instead.
        var localDate = Substitute.For<ILocalDateProvider>();
        localDate.Today().Returns(localToday ?? DateOnly.FromDateTime(effectiveNow.UtcDateTime));

        var sut = new PlanGenerationService(
            assembler,
            llm,
            promptStore,
            settings,
            timeProvider,
            localDate,
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

    /// <summary>
    /// Stubs the macro tier call to return the supplied macro verbatim. Used by the F3
    /// validation tests to feed a horizon-violating or phase-sum-inconsistent macro.
    /// </summary>
    private static void ConfigureMacro(ICoachingLlm llm, MacroPlanOutput macro)
    {
        llm
            .GenerateStructuredAsync<MacroPlanOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => WithZeroUsage(macro));
    }

    /// <summary>
    /// Stubs the meso + micro tier calls on the happy path. Paired with
    /// <see cref="ConfigureMacro"/> when the macro tier is configured separately so a
    /// rejection-before-meso assertion still has well-formed downstream stubs available.
    /// </summary>
    private static void ConfigureMesoMicroHappyPath(ICoachingLlm llm)
    {
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
    }

    private static void ConfigureLlmHappyPath(
        ICoachingLlm llm,
        List<CacheControl?>? cacheCapture = null)
    {
        ConfigureMacroSuccess(llm);
        ConfigureMesoMicroHappyPath(llm);

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

    /// <summary>
    /// A completed view carrying a future target event (F3). The supplied ISO date drives
    /// the deterministic horizon, which gates macro validation in the service.
    /// </summary>
    private static OnboardingView CreateRaceView(string eventDateIso)
    {
        var view = CreateCompletedView();
        view.TargetEvent = new TargetEventAnswer
        {
            EventName = "Local Half Marathon",
            DistanceKm = 21.1,
            EventDateIso = eventDateIso,
            TargetFinishTimeIso = "PT1H45M",
        };
        return view;
    }

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

    /// <summary>
    /// Builds a macro with an explicit <paramref name="totalWeeks"/> and one phase per entry in
    /// <paramref name="phaseWeeks"/>. The validator checks both the phase-sum (against
    /// <paramref name="totalWeeks"/>) and the horizon (against <paramref name="totalWeeks"/>), so
    /// callers control each independently: a phase-sum that matches isolates the horizon check, a
    /// mismatching sum isolates the phase-sum check.
    /// </summary>
    private static MacroPlanOutput BuildMacroWithTotalWeeks(int totalWeeks, int[] phaseWeeks)
    {
        var phaseTypes = new[] { PhaseType.Base, PhaseType.Build, PhaseType.Peak, PhaseType.Taper };
        var phases = new PlanPhaseOutput[phaseWeeks.Length];
        for (var i = 0; i < phaseWeeks.Length; i++)
        {
            phases[i] = new PlanPhaseOutput
            {
                PhaseType = phaseTypes[i % phaseTypes.Length],
                Weeks = phaseWeeks[i],
                WeeklyDistanceStartKm = 30,
                WeeklyDistanceEndKm = 50,
                IntensityDistribution = "80/20",
                AllowedWorkoutTypes = new[] { WorkoutType.Easy, WorkoutType.LongRun },
                TargetPaceEasySecPerKm = 360,
                TargetPaceFastSecPerKm = 300,
                Notes = string.Empty,
                IncludesDeload = false,
            };
        }

        return new MacroPlanOutput
        {
            TotalWeeks = totalWeeks,
            GoalDescription = "Half Marathon",
            Phases = phases,
            Rationale = "Configured for validation tests.",
            Warnings = string.Empty,
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
