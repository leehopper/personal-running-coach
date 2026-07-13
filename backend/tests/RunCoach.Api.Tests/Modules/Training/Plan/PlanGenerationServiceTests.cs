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
    public async Task GeneratePlanAsync_StampsTargetEventOnPlanGenerated()
    {
        // Arrange — local today 2026-06-12 → plan-start Sunday 2026-06-07; the race on
        // 2026-08-08 is week 9 from that anchor, so a 9-week phase-sum-consistent macro
        // passes horizon validation (mirrors AnchoredHorizon_ValidMacro_AppendsPlanWithLocalStartDate).
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacro(llm, BuildMacroWithTotalWeeks(9, NineWeekPhaseWeeks));
        ConfigureMesoMicroHappyPath(llm);
        var eventDateIso = "2026-08-08";
        var view = CreateRaceView(eventDateIso);

        // Act
        var sequence = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        var generated = sequence.Macro;
        generated.TargetEventName.Should().Be(view.TargetEvent!.EventName);
        generated.TargetEventDistanceKm.Should().Be(view.TargetEvent.DistanceKm);
        generated.TargetEventDate.Should().Be(new DateOnly(2026, 8, 8));
    }

    [Fact]
    public async Task GeneratePlanAsync_TargetEventFieldsNull_ForGeneralFitness()
    {
        // Arrange
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        var sequence = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        var generated = sequence.Macro;
        generated.TargetEventName.Should().BeNull();
        generated.TargetEventDistanceKm.Should().BeNull();
        generated.TargetEventDate.Should().BeNull();
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
    public async Task GeneratePlanAsync_MalformedEventDate_FallsThroughToGeneralFitness_DoesNotThrow()
    {
        // Arrange — an EventDateIso that is present but unparseable. ResolveTargetEventDate returns
        // null, so the horizon is non-anchored and the validator skips the event-horizon check. The
        // macro is a 16-week plan that WOULD violate a 9-week anchored horizon (cf.
        // AnchoredHorizon_HorizonViolatingMacro_Throws); generating it without rejection proves the
        // malformed date disabled anchoring, with only the always-on phase-sum check applied. Guards
        // against a future change to the parse format silently dropping anchoring on real dated plans.
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacro(llm, BuildMacroWithTotalWeeks(16, SixteenWeekPhaseWeeks));
        ConfigureMesoMicroHappyPath(llm);
        var view = CreateRaceView(eventDateIso: "not-a-date");

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

    // DEC-087: bounded corrective-hint retry on macro validation rejection (F-LIVE-1).
    [Fact]
    public async Task GeneratePlanAsync_MacroPhaseSumMismatchThenValid_RetriesAndSucceeds()
    {
        // Arrange — the first macro sample is phase-sum-inconsistent (6+4=10 != 12); the retry
        // returns a valid macro. With the default budget (1 retry) the service must recover and
        // return the canonical sequence without surfacing a rejection.
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacroSequence(
            llm,
            capturedPrompts: null,
            BuildMacroWithTotalWeeks(12, PhaseSumMismatchWeeks),
            BuildMacro());
        ConfigureMesoMicroHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        var sequence = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — recovered on the retry: canonical six-event sequence, macro invoked exactly twice.
        sequence.ToEvents().Should().HaveCount(6);
        await llm.Received(2)
            .GenerateStructuredAsync<MacroPlanOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeneratePlanAsync_MacroHorizonMismatchThenValid_RetriesAndSucceeds()
    {
        // Arrange — anchored 9-week race horizon (race 2026-08-08 from plan-start 2026-06-07).
        // The first macro is a phase-sum-consistent 16-week plan that violates the horizon by >1
        // week; the retry returns a horizon-consistent 9-week plan. The service must recover, and the
        // retry message must name the emitted (16) vs. required (9) weeks so a mutant that swaps them
        // in `BuildMacroCorrection`'s `HorizonMismatch` branch is caught.
        var capturedMacroPrompts = new List<string>();
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacroSequence(
            llm,
            capturedMacroPrompts,
            BuildMacroWithTotalWeeks(16, SixteenWeekPhaseWeeks),
            BuildMacroWithTotalWeeks(9, NineWeekPhaseWeeks));
        ConfigureMesoMicroHappyPath(llm);
        var view = CreateRaceView(eventDateIso: "2026-08-08");

        // Act
        var sequence = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — recovered on the retry.
        sequence.ToEvents().Should().HaveCount(6);
        await llm.Received(2)
            .GenerateStructuredAsync<MacroPlanOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>());

        // The retry correction names the horizon numbers (emitted 16, required 9), anchored to the
        // surrounding phrase so an incidental digit in the boilerplate can't satisfy it.
        capturedMacroPrompts.Should().HaveCount(2);
        capturedMacroPrompts[0].Should().NotContain(PlanGenerationService.MacroCorrectionLabel);
        capturedMacroPrompts[1].Should().Contain("total_weeks to 16", because: "the emitted total is named");
        capturedMacroPrompts[1].Should().Contain("EXACTLY 9 weeks", because: "the required target horizon is named");
    }

    [Theory]
    [InlineData(0, 1)] // retry disabled → single attempt, immediate reject
    [InlineData(1, 2)] // default budget → two attempts
    [InlineData(2, 3)] // raised budget → three attempts
    public async Task GeneratePlanAsync_MacroInvalidOnEveryAttempt_ThrowsAfterBudgetExhausted(
        int maxRetries,
        int expectedMacroCalls)
    {
        // Arrange — every macro sample is phase-sum-inconsistent. After the budget is spent the
        // service throws the terminal rejection, having invoked the macro tier exactly
        // (maxRetries + 1) times, and never reaches meso/micro.
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12), macroValidationMaxRetries: maxRetries);
        ConfigureMacro(llm, BuildMacroWithTotalWeeks(12, PhaseSumMismatchWeeks));
        ConfigureMesoMicroHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        var act = () => sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<PlanGenerationRejectedException>())
            .Which.Violation.Should().Be(MacroPlanOutputValidationViolation.PhaseSumMismatch);

        await llm.Received(expectedMacroCalls)
            .GenerateStructuredAsync<MacroPlanOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>());

        // A macro-tier exhaustion throws before any downstream tier runs — neither meso nor micro.
        await llm.DidNotReceive()
            .GenerateStructuredAsync<MesoWeekOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>());

        await llm.DidNotReceive()
            .GenerateStructuredAsync<MicroWorkoutListOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeneratePlanAsync_MacroRetry_AppendsCorrectionSuffixWithActualNumbersOnRetryOnly()
    {
        // Arrange — capture each macro user message. First sample is phase-sum-inconsistent
        // (6+4=10 != 12); the retry succeeds. The attempt-0 message must be suffix-free; the
        // attempt-1 message must name the observed sum (10) and the declared total (12).
        var capturedMacroPrompts = new List<string>();
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacroSequence(
            llm,
            capturedMacroPrompts,
            BuildMacroWithTotalWeeks(12, PhaseSumMismatchWeeks),
            BuildMacro());
        ConfigureMesoMicroHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert
        capturedMacroPrompts.Should().HaveCount(2);
        capturedMacroPrompts[0].Should().NotContain(
            PlanGenerationService.MacroCorrectionLabel,
            because: "the first attempt must be byte-identical to the no-retry path (input-prompt-stability contract)");
        capturedMacroPrompts[1].Should().Contain(PlanGenerationService.MacroCorrectionLabel);

        // Anchor to the surrounding phrase, not a bare digit: the mocked profile's UserId GUID
        // (…0010) contains "10", so a bare Contain("10") would pass even if the sum were miscomputed.
        capturedMacroPrompts[1].Should().Contain("summed to 10", because: "the observed phase-week sum is named in the correction");
        capturedMacroPrompts[1].Should().Contain("total_weeks was 12", because: "the declared total_weeks is named in the correction");
    }

    [Fact]
    public async Task GeneratePlanAsync_MacroRetry_StampsMacroAttemptCountOnCompletionMetric()
    {
        // Arrange — a MeterListener over the plan-generation completion instrument, same wiring as
        // the failure-OTel test. A phase-sum-mismatch-then-valid sequence recovers on attempt 2, so
        // the success measurement's tag bag must carry macro_attempts = 2.
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

        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacroSequence(
            llm,
            capturedPrompts: null,
            BuildMacroWithTotalWeeks(12, PhaseSumMismatchWeeks),
            BuildMacro());
        ConfigureMesoMicroHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — the single success measurement carries macro_attempts = 2.
        var successMeasurements = measurements
            .Where(m => m.Tags.Any(t =>
                t.Key == PlanGenerationTagNames.Outcome
                && string.Equals(t.Value as string, "success", StringComparison.Ordinal)))
            .ToArray();
        successMeasurements.Should().HaveCount(1);
        successMeasurements[0].Tags
            .Should().Contain(t =>
                t.Key == PlanGenerationTagNames.MacroAttempts
                && (int)t.Value! == 2);

        // total_calls reflects the ACTUAL LLM call volume (2 macro + 4 meso + 1 micro = 7), not the
        // nominal 6 constant — a retry adds a real macro call the cost dashboards must see.
        successMeasurements[0].Tags
            .Should().Contain(t =>
                t.Key == PlanGenerationTagNames.TotalCalls
                && (int)t.Value! == 7);
    }

    [Fact]
    public async Task GeneratePlanAsync_MacroRetry_AccumulatesRejectedAttemptTokensIntoUsageRollup()
    {
        // Arrange — a MeterListener over the completion instrument. The rejected first attempt reports
        // 100 fresh input tokens; the accepted retry reports 40 (meso/micro report zero). Those tokens
        // are really spent, so the success measurement's input_tokens_fresh must be their SUM (140),
        // not just the winner's 40 — pins the "accumulate every attempt" behavior (the unconditional
        // `totalUsage.Add` inside the loop) against a mutant that only counts the accepted attempt.
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

        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        var badMacro = BuildMacroWithTotalWeeks(12, PhaseSumMismatchWeeks);
        var goodMacro = BuildMacro();
        var macroCalls = 0;
        llm
            .GenerateStructuredAsync<MacroPlanOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                macroCalls++;
                return macroCalls == 1
                    ? (badMacro, new AnthropicUsage(InputTokens: 100, OutputTokens: 0, CacheCreationInputTokens: 0, CacheReadInputTokens: 0))
                    : (goodMacro, new AnthropicUsage(InputTokens: 40, OutputTokens: 0, CacheCreationInputTokens: 0, CacheReadInputTokens: 0));
            });
        ConfigureMesoMicroHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — the single success measurement's fresh-input-tokens tag sums both attempts (100 + 40).
        var successMeasurements = measurements
            .Where(m => m.Tags.Any(t =>
                t.Key == PlanGenerationTagNames.Outcome
                && string.Equals(t.Value as string, "success", StringComparison.Ordinal)))
            .ToArray();
        successMeasurements.Should().HaveCount(1);
        successMeasurements[0].Tags
            .Should().Contain(t =>
                t.Key == PlanGenerationTagNames.InputTokensFresh
                && (long)t.Value! == 140L);
    }

    [Fact]
    public async Task GeneratePlanAsync_MacroExhaustion_StampsMacroAttemptsOnFailureMetric()
    {
        // Arrange — a MeterListener; every macro attempt is invalid, so the default budget (1 retry →
        // 2 attempts) is exhausted and the terminal `PlanGenerationRejectedException` is thrown. The
        // failure measurement must carry macro_attempts = 2, pinning the catch-block tag against a
        // mutant that hardcodes 0 or omits it.
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

        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacro(llm, BuildMacroWithTotalWeeks(12, PhaseSumMismatchWeeks));
        ConfigureMesoMicroHappyPath(llm);
        var view = CreateCompletedView();

        // Act
        await FluentActions
            .Awaiting(() => sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken))
            .Should()
            .ThrowAsync<PlanGenerationRejectedException>();

        // Assert — the single failure measurement carries macro_attempts = 2.
        var failureMeasurements = measurements
            .Where(m => m.Tags.Any(t =>
                t.Key == PlanGenerationTagNames.Outcome
                && string.Equals(t.Value as string, "failure", StringComparison.Ordinal)))
            .ToArray();
        failureMeasurements.Should().HaveCount(1);
        failureMeasurements[0].Tags
            .Should().Contain(t =>
                t.Key == PlanGenerationTagNames.MacroAttempts
                && (int)t.Value! == 2);
    }

    // DEC-088: bounded corrective-hint retry on meso/micro consistency rejection (F-LIVE-2).
    [Fact]
    public async Task GeneratePlanAsync_MicroInconsistentThenConsistent_RetriesAndSucceeds()
    {
        // Arrange — the first micro sample schedules one run day (Sunday) against a meso week with
        // four run days (Sun/Tue/Thu/Sat); the retry returns a consistent micro. With the default
        // budget (1 retry) the service must recover and return the canonical sequence without
        // surfacing a rejection.
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacroSuccess(llm);
        ConfigureMesoHappyPathAndMicroSequence(
            llm,
            capturedMicroPrompts: null,
            BuildInconsistentMicro(),
            BuildMicro());
        var view = CreateCompletedView();

        // Act
        var sequence = await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — recovered on the retry: canonical six-event sequence, micro invoked exactly twice,
        // macro/meso unaffected (no extra macro or meso calls triggered by a micro re-roll).
        sequence.ToEvents().Should().HaveCount(6);
        await llm.Received(2)
            .GenerateStructuredAsync<MicroWorkoutListOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>());
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
    }

    [Theory]
    [InlineData(0, 1)] // retry disabled → single attempt, immediate reject
    [InlineData(1, 2)] // default budget → two attempts
    [InlineData(2, 3)] // raised budget → three attempts
    [InlineData(100, 6)] // misconfigured budget clamps to MaxAllowedMicroValidationRetries (5) → six attempts
    public async Task GeneratePlanAsync_MicroInconsistentOnEveryAttempt_ThrowsAfterBudgetExhausted(
        int maxRetries,
        int expectedMicroCalls)
    {
        // Arrange — every micro sample is inconsistent with the meso week (one run day vs. four), so
        // the budget is exhausted and the generation is terminally rejected. Macro + meso still run
        // fully (the reject is downstream of them); only micro re-rolls.
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12), microValidationMaxRetries: maxRetries);
        ConfigureMacroSuccess(llm);
        ConfigureMesoHappyPathAndMicroSequence(llm, capturedMicroPrompts: null, BuildInconsistentMicro());
        var view = CreateCompletedView();

        // Act
        var act = () => sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — the terminal rejection carries the count-mismatch violation, with exactly
        // expectedMicroCalls micro calls, macro invoked once, and meso invoked four times.
        (await act.Should().ThrowAsync<MesoMicroConsistencyRejectedException>())
            .Which.Violation.Should().Be(MesoMicroConsistencyViolation.RunDayCountMismatch);
        await llm.Received(expectedMicroCalls)
            .GenerateStructuredAsync<MicroWorkoutListOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>());
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
    }

    [Fact]
    public async Task GeneratePlanAsync_MicroRetry_AppendsCorrectionSuffixWithRunScheduleOnRetryOnly()
    {
        // Arrange — capture each micro user message. The inconsistent first attempt (Sunday only) is
        // rejected; the retry must carry a correction suffix naming the meso run schedule
        // (Sun/Tue/Thu/Sat), while attempt 0 stays byte-identical to the no-retry path.
        var capturedMicroPrompts = new List<string>();
        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacroSuccess(llm);
        ConfigureMesoHappyPathAndMicroSequence(
            llm,
            capturedMicroPrompts,
            BuildInconsistentMicro(),
            BuildMicro());
        var view = CreateCompletedView();

        // Act
        await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — attempt 0 has no correction suffix; attempt 1 names the correction and the run days
        // the meso week schedules that the rejected micro omitted.
        capturedMicroPrompts.Should().HaveCount(2);
        capturedMicroPrompts[0].Should().NotContain(
            PlanGenerationService.MicroCorrectionLabel,
            because: "the first attempt must be byte-identical to the no-retry path (input-prompt-stability contract)");
        capturedMicroPrompts[1].Should().Contain(PlanGenerationService.MicroCorrectionLabel);
        capturedMicroPrompts[1].Should().Contain("Tuesday");
        capturedMicroPrompts[1].Should().Contain("Thursday");
    }

    [Fact]
    public async Task GeneratePlanAsync_MicroRetry_StampsMicroAttemptCountOnCompletionMetric()
    {
        // Arrange — a MeterListener over the completion instrument. An inconsistent-then-consistent
        // micro sequence recovers on attempt 2, so the success measurement must carry micro_attempts = 2
        // and total_calls = 7 (1 macro + 4 meso + 2 micro).
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

        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacroSuccess(llm);
        ConfigureMesoHappyPathAndMicroSequence(
            llm,
            capturedMicroPrompts: null,
            BuildInconsistentMicro(),
            BuildMicro());
        var view = CreateCompletedView();

        // Act
        await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — the single success measurement carries micro_attempts = 2 and total_calls = 7.
        var successMeasurements = measurements
            .Where(m => m.Tags.Any(t =>
                t.Key == PlanGenerationTagNames.Outcome
                && string.Equals(t.Value as string, "success", StringComparison.Ordinal)))
            .ToArray();
        successMeasurements.Should().HaveCount(1);
        successMeasurements[0].Tags
            .Should().Contain(t =>
                t.Key == PlanGenerationTagNames.MicroAttempts
                && (int)t.Value! == 2);
        successMeasurements[0].Tags
            .Should().Contain(t =>
                t.Key == PlanGenerationTagNames.TotalCalls
                && (int)t.Value! == 7);
    }

    [Fact]
    public async Task GeneratePlanAsync_MicroExhaustion_StampsMicroAttemptsOnFailureMetric()
    {
        // Arrange — a MeterListener; every micro attempt is inconsistent, so the default budget (1 retry
        // → 2 attempts) is exhausted and the terminal MesoMicroConsistencyRejectedException is thrown.
        // The failure measurement must carry micro_attempts = 2, pinning the catch-block tag.
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

        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacroSuccess(llm);
        ConfigureMesoHappyPathAndMicroSequence(llm, capturedMicroPrompts: null, BuildInconsistentMicro());
        var view = CreateCompletedView();

        // Act
        await FluentActions
            .Awaiting(() => sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken))
            .Should()
            .ThrowAsync<MesoMicroConsistencyRejectedException>();

        // Assert — the single failure measurement carries micro_attempts = 2.
        var failureMeasurements = measurements
            .Where(m => m.Tags.Any(t =>
                t.Key == PlanGenerationTagNames.Outcome
                && string.Equals(t.Value as string, "failure", StringComparison.Ordinal)))
            .ToArray();
        failureMeasurements.Should().HaveCount(1);
        failureMeasurements[0].Tags
            .Should().Contain(t =>
                t.Key == PlanGenerationTagNames.MicroAttempts
                && (int)t.Value! == 2);
    }

    [Fact]
    public async Task GeneratePlanAsync_MicroRetry_AccumulatesRejectedAttemptTokensIntoUsageRollup()
    {
        // Arrange — a MeterListener over the completion instrument. The rejected first micro attempt
        // reports 100 fresh input tokens; the accepted retry reports 40 (meso/macro report zero). Those
        // tokens are really spent, so the success measurement's input_tokens_fresh must be their SUM
        // (140), not just the winner's 40 — pins the unconditional `totalUsage.Add(microUsage)` inside
        // the micro retry loop against a mutant that only counts the accepted attempt.
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

        var (sut, llm, _) = CreateSut(localToday: new DateOnly(2026, 6, 12));
        ConfigureMacroSuccess(llm);
        ConfigureMesoMicroHappyPath(llm);

        var badMicro = BuildInconsistentMicro();
        var goodMicro = BuildMicro();
        var microCalls = 0;
        llm
            .GenerateStructuredAsync<MicroWorkoutListOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                microCalls++;
                return microCalls == 1
                    ? (badMicro, new AnthropicUsage(InputTokens: 100, OutputTokens: 0, CacheCreationInputTokens: 0, CacheReadInputTokens: 0))
                    : (goodMicro, new AnthropicUsage(InputTokens: 40, OutputTokens: 0, CacheCreationInputTokens: 0, CacheReadInputTokens: 0));
            });
        var view = CreateCompletedView();

        // Act
        await sut.GeneratePlanAsync(view, UserId, PlanId, intent: null, previousPlanId: null, TestContext.Current.CancellationToken);

        // Assert — the single success measurement's fresh-input-tokens tag sums both attempts (100 + 40).
        var successMeasurements = measurements
            .Where(m => m.Tags.Any(t =>
                t.Key == PlanGenerationTagNames.Outcome
                && string.Equals(t.Value as string, "success", StringComparison.Ordinal)))
            .ToArray();
        successMeasurements.Should().HaveCount(1);
        successMeasurements[0].Tags
            .Should().Contain(t =>
                t.Key == PlanGenerationTagNames.InputTokensFresh
                && (long)t.Value! == 140L);
    }

    [Fact]
    public void BuildMicroCorrection_ExpectedScheduleNamesRunDaysAndFallsBackToAnyRunTypeWhenWorkoutTypeNull()
    {
        // Arrange — a meso week whose Sunday run slot has a null WorkoutType (an under-specified LLM
        // sample); the other run days (Tuesday, Thursday, Saturday) carry concrete types. The micro
        // week generated zero run workouts, so the "You generated:" summary must render "none".
        var meso = BuildMesoFixture(
            sunday: new MesoDaySlotOutput { SlotType = DaySlotType.Run, WorkoutType = null, Notes = string.Empty },
            tuesday: new MesoDaySlotOutput { SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Tempo, Notes = string.Empty },
            thursday: new MesoDaySlotOutput { SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Easy, Notes = string.Empty },
            saturday: new MesoDaySlotOutput { SlotType = DaySlotType.Run, WorkoutType = WorkoutType.LongRun, Notes = string.Empty });
        var micro = new MicroWorkoutListOutput { Workouts = Array.Empty<WorkoutOutput>() };

        // Act
        var correction = PlanGenerationService.BuildMicroCorrection(meso, micro);

        // Assert
        correction.Should().Contain(PlanGenerationService.MicroCorrectionLabel);
        correction.Should().Contain(
            "Sunday: any run type",
            because: "a null WorkoutType on a run slot falls back to the generic label");
        correction.Should().Contain("Tuesday: Tempo");
        correction.Should().Contain("Thursday: Easy");
        correction.Should().Contain("Saturday: LongRun");
        correction.Should().Contain(
            "You generated: none",
            because: "zero run workouts in the micro output renders the none fallback");
    }

    [Fact]
    public void BuildMicroCorrection_GeneratedSummaryJoinsMultipleRunDaysAndExcludesCrossTrain()
    {
        // Arrange — a meso week with two run days (Sunday, Tuesday); the rejected micro emitted a
        // matching pair of run workouts plus an extra cross-train workout on Thursday. The
        // "You generated:" summary must comma-join the run days in encounter order and omit the
        // cross-train entry (the consistency validator's run-day-only scope).
        var meso = BuildMesoFixture(
            sunday: new MesoDaySlotOutput { SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Easy, Notes = string.Empty },
            tuesday: new MesoDaySlotOutput { SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Tempo, Notes = string.Empty },
            thursday: new MesoDaySlotOutput { SlotType = DaySlotType.Rest, WorkoutType = null, Notes = string.Empty },
            saturday: new MesoDaySlotOutput { SlotType = DaySlotType.Rest, WorkoutType = null, Notes = string.Empty });
        var micro = new MicroWorkoutListOutput
        {
            Workouts = new[]
            {
                BuildWorkout(dayOfWeek: 0, WorkoutType.Easy),
                BuildWorkout(dayOfWeek: 2, WorkoutType.Tempo),
                BuildWorkout(dayOfWeek: 4, WorkoutType.CrossTrain),
            },
        };

        // Act
        var correction = PlanGenerationService.BuildMicroCorrection(meso, micro);

        // Assert
        correction.Should().Contain("You generated: Sunday: Easy, Tuesday: Tempo");
        correction.Should().NotContain(
            "Thursday: CrossTrain",
            because: "cross-train workouts are excluded from the generated-summary, matching the validator's run-day-only scope");
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
        DateOnly? localToday = null,
        int? macroValidationMaxRetries = null,
        int? microValidationMaxRetries = null)
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

        // Inherit the production default (MacroValidationMaxRetries = 1) unless a test overrides it,
        // so the default-budget behavior is exercised without pinning the number in every test.
        var settingsRecord = new CoachingLlmSettings
        {
            ApiKey = "[REDACTED]",
            ModelId = "test-model-id",
        };
        if (macroValidationMaxRetries is int retries)
        {
            settingsRecord = settingsRecord with { MacroValidationMaxRetries = retries };
        }

        if (microValidationMaxRetries is int microRetries)
        {
            settingsRecord = settingsRecord with { MicroValidationMaxRetries = microRetries };
        }

        var settings = Options.Create(settingsRecord);

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
    /// Stubs the macro tier to return each element of <paramref name="sequence"/> on successive
    /// calls, clamping to the last element once the sequence is exhausted (so an all-invalid run
    /// keeps returning the same rejected macro). Captures each call's user-message argument into
    /// <paramref name="capturedPrompts"/> when supplied so retry-suffix assertions can inspect the
    /// exact bytes handed to the LLM per attempt. Uses the file's established counter-closure idiom.
    /// </summary>
    private static void ConfigureMacroSequence(
        ICoachingLlm llm,
        List<string>? capturedPrompts,
        params MacroPlanOutput[] sequence)
    {
        var calls = 0;
        llm
            .GenerateStructuredAsync<MacroPlanOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedPrompts?.Add(call.ArgAt<string>(1));
                var index = Math.Min(calls, sequence.Length - 1);
                calls++;
                return WithZeroUsage(sequence[index]);
            });
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

    /// <summary>
    /// Stubs the meso tier on the happy path but scripts the micro tier to return each element of
    /// <paramref name="microSequence"/> on successive calls, clamping to the last element once the
    /// sequence is exhausted (so an all-inconsistent run keeps returning the same rejected micro).
    /// Captures each micro call's user-message argument into <paramref name="capturedMicroPrompts"/>
    /// when supplied so retry-suffix assertions can inspect the exact bytes per attempt. Mirrors
    /// <see cref="ConfigureMacroSequence"/> for the DEC-088 meso/micro consistency-retry tests.
    /// </summary>
    private static void ConfigureMesoHappyPathAndMicroSequence(
        ICoachingLlm llm,
        List<string>? capturedMicroPrompts,
        params MicroWorkoutListOutput[] microSequence)
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

        var microCalls = 0;
        llm
            .GenerateStructuredAsync<MicroWorkoutListOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedMicroPrompts?.Add(call.ArgAt<string>(1));
                var index = Math.Min(microCalls, microSequence.Length - 1);
                microCalls++;
                return WithZeroUsage(microSequence[index]);
            });
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

    /// <summary>
    /// The default happy-path micro week. Emits one workout per <see cref="BuildMeso"/> run slot
    /// (Sunday/Tuesday/Thursday/Saturday, all <see cref="WorkoutType.Easy"/>) so the meso/micro
    /// consistency validator (DEC-088 / F-LIVE-2) passes on attempt 0 — the meso and micro fixtures
    /// must agree or every happy-path generation would now retry and terminally reject.
    /// </summary>
    private static MicroWorkoutListOutput BuildMicro()
    {
        return new MicroWorkoutListOutput
        {
            Workouts = new[]
            {
                BuildWorkout(dayOfWeek: 0, WorkoutType.Easy),
                BuildWorkout(dayOfWeek: 2, WorkoutType.Easy),
                BuildWorkout(dayOfWeek: 4, WorkoutType.Easy),
                BuildWorkout(dayOfWeek: 6, WorkoutType.Easy),
            },
        };
    }

    /// <summary>
    /// A micro week that disagrees with <see cref="BuildMeso"/>'s four run slots — a single workout
    /// on Sunday. Used as the "bad micro" starting point for the DEC-088 consistency-retry tests
    /// (run-day count 1 vs. 4). This is the exact shape the pre-F-LIVE-2 default fixture carried.
    /// </summary>
    private static MicroWorkoutListOutput BuildInconsistentMicro()
    {
        return new MicroWorkoutListOutput
        {
            Workouts = new[] { BuildWorkout(dayOfWeek: 0, WorkoutType.Easy) },
        };
    }

    /// <summary>
    /// Builds a <see cref="MesoWeekOutput"/> with all seven day slots so <c>BuildMicroCorrection</c>
    /// fixtures can specify only the run-slot days under test; Monday/Wednesday/Friday default to rest.
    /// </summary>
    private static MesoWeekOutput BuildMesoFixture(
        MesoDaySlotOutput sunday,
        MesoDaySlotOutput tuesday,
        MesoDaySlotOutput thursday,
        MesoDaySlotOutput saturday)
    {
        var rest = new MesoDaySlotOutput { SlotType = DaySlotType.Rest, WorkoutType = null, Notes = string.Empty };
        return new MesoWeekOutput
        {
            WeekNumber = 1,
            PhaseType = PhaseType.Base,
            WeeklyTargetKm = 40,
            IsDeloadWeek = false,
            Sunday = sunday,
            Monday = rest,
            Tuesday = tuesday,
            Wednesday = rest,
            Thursday = thursday,
            Friday = rest,
            Saturday = saturday,
            WeekSummary = "Week 1",
        };
    }

    private static WorkoutOutput BuildWorkout(int dayOfWeek, WorkoutType workoutType)
    {
        return new WorkoutOutput
        {
            DayOfWeek = dayOfWeek,
            WorkoutType = workoutType,
            Title = $"{workoutType} run",
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
        };
    }
}
