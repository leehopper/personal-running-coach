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
/// Observability tests over <see cref="PlanGenerationService"/> per Slice 1
/// § Unit 2 R02.8 / R-051. Asserts the chain emits the documented OTel
/// surface: a parent <c>runcoach.plan.generation</c> activity wrapping six
/// <c>runcoach.plan.generation.tier</c> child spans (1 macro + 4 meso + 1
/// micro), and a <c>runcoach.plan.generation.completed</c> histogram
/// measurement on the <c>RunCoach.Llm</c> Meter carrying
/// <c>{ planId, userId, totalCalls, macroOutputChars, mesoOutputCharsTotal,
/// microOutputChars }</c> tags with the wall-clock duration as the recorded
/// value. Phoenix per R-051 picks these up via OTLP for the seven-span trace
/// timeline (1 onboarding + 6 plan-gen) called out in the spec.
/// </summary>
public sealed class PlanGenerationServiceObservabilityTests
{
    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");

    private static readonly DateTimeOffset Now = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GeneratePlanAsync_Emits_PlanGenerationCompleted_Metric_With_Documented_Tag_Bag()
    {
        // Arrange — capture every measurement on the RunCoach.Llm Meter so we
        // can assert the histogram name + tag bag without a full OTel pipeline.
        var planId = NewPlanId();
        var measurements = new List<RecordedMeasurement>();
        using var listener = CreateMeterListener(measurements);
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);

        // Act
        await sut.GeneratePlanAsync(
            CreateCompletedView(),
            UserId,
            planId,
            intent: null,
            previousPlanId: null,
            TestContext.Current.CancellationToken);

        // Assert — exactly one runcoach.plan.generation.completed measurement
        // landed on the documented histogram for this test's PlanId. Filter
        // by the tag bag so concurrent tests in the same process emitting
        // their own chains don't bleed into the assertion.
        var planMeasurements = measurements
            .Where(m => string.Equals(m.InstrumentName, PlanGenerationService.PlanGenerationCompletedMetricName, StringComparison.Ordinal)
                && m.Tags.TryGetValue("runcoach.plan.id", out var pid)
                && (pid as string) == planId.ToString())
            .ToList();
        planMeasurements.Should().ContainSingle(because: "one chain for this PlanId emits exactly one completion event");

        var completion = planMeasurements[0];
        completion.MeterName.Should().Be(PlanGenerationService.ObservabilitySourceName);
        completion.Value.Should().BeGreaterThanOrEqualTo(0d);

        completion.Tags.Should().ContainKey("runcoach.plan.id")
            .WhoseValue.Should().Be(planId.ToString());
        completion.Tags.Should().ContainKey("runcoach.user.id")
            .WhoseValue.Should().Be(UserId.ToString());
        completion.Tags.Should().ContainKey("runcoach.plan.total_calls")
            .WhoseValue.Should().Be(6);
        completion.Tags.Should().ContainKey("runcoach.plan.macro_output_chars")
            .WhoseValue.Should().BeOfType<int>().Which.Should().BeGreaterThan(0);
        completion.Tags.Should().ContainKey("runcoach.plan.meso_output_chars_total")
            .WhoseValue.Should().BeOfType<int>().Which.Should().BeGreaterThan(0);
        completion.Tags.Should().ContainKey("runcoach.plan.micro_output_chars")
            .WhoseValue.Should().BeOfType<int>().Which.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GeneratePlanAsync_Emits_Parent_And_Six_Tier_Activities_On_RunCoachLlm_Source()
    {
        // Arrange — listen for every Activity on the RunCoach.Llm
        // ActivitySource so we can verify the seven-span shape called out in
        // R02.8 (1 parent + 6 tier children).
        var planId = NewPlanId();
        var startedActivities = new List<Activity>();
        using var listener = CreateActivityListener(startedActivities);
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);

        // Act
        await sut.GeneratePlanAsync(
            CreateCompletedView(),
            UserId,
            planId,
            intent: null,
            previousPlanId: null,
            TestContext.Current.CancellationToken);

        // Assert — exactly one parent + six tier child activities for THIS
        // test's PlanId. Filter by source + plan-id tag so concurrent tests
        // in the same process exercising plan generation against a
        // different planId don't inflate the counts.
        var planActivities = startedActivities
            .Where(a => string.Equals(a.Source.Name, PlanGenerationService.ObservabilitySourceName, StringComparison.Ordinal)
                && (Guid?)a.GetTagItem("runcoach.plan.id") == planId)
            .ToList();

        var parents = planActivities
            .Where(a => string.Equals(a.OperationName, PlanGenerationService.PlanGenerationActivityName, StringComparison.Ordinal))
            .ToList();
        parents.Should().ContainSingle();

        var tiers = planActivities
            .Where(a => string.Equals(a.OperationName, PlanGenerationService.TierActivityName, StringComparison.Ordinal))
            .ToList();
        tiers.Should().HaveCount(6, because: "1 macro + 4 meso + 1 micro");

        var tierValues = tiers
            .Select(a => (string?)a.GetTagItem("runcoach.plan.tier"))
            .ToList();
        tierValues.Count(t => t == PlanGenerationService.TierMacro).Should().Be(1);
        tierValues.Count(t => t == PlanGenerationService.TierMeso).Should().Be(4);
        tierValues.Count(t => t == PlanGenerationService.TierMicro).Should().Be(1);
    }

    [Fact]
    public async Task GeneratePlanAsync_Parent_Activity_Stamps_Rollup_Tags_Matching_Metric_Tags()
    {
        // Arrange — the parent span carries the same per-tier roll-up tags
        // as the metric event so a single trace view shows the totals
        // without scraping the metric exporter (R-051 Phoenix UX).
        var planId = NewPlanId();
        var startedActivities = new List<Activity>();
        using var listener = CreateActivityListener(startedActivities);
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);

        // Act
        await sut.GeneratePlanAsync(
            CreateCompletedView(),
            UserId,
            planId,
            intent: null,
            previousPlanId: null,
            TestContext.Current.CancellationToken);

        // Assert — pick the parent whose plan-id tag matches THIS test's
        // PlanId so concurrent tests don't collide.
        var parent = startedActivities.Single(a =>
            string.Equals(a.Source.Name, PlanGenerationService.ObservabilitySourceName, StringComparison.Ordinal)
            && string.Equals(a.OperationName, PlanGenerationService.PlanGenerationActivityName, StringComparison.Ordinal)
            && (Guid?)a.GetTagItem("runcoach.plan.id") == planId);

        parent.GetTagItem("runcoach.plan.id").Should().Be(planId);
        parent.GetTagItem("runcoach.user.id").Should().Be(UserId);
        parent.GetTagItem("runcoach.plan.total_calls").Should().Be(6);
        parent.GetTagItem("runcoach.plan.duration_ms").Should().BeOfType<double>()
            .Which.Should().BeGreaterThanOrEqualTo(0d);
        parent.GetTagItem("runcoach.plan.macro_output_chars").Should().BeOfType<int>()
            .Which.Should().BeGreaterThan(0);
        parent.GetTagItem("runcoach.plan.meso_output_chars_total").Should().BeOfType<int>()
            .Which.Should().BeGreaterThan(0);
        parent.GetTagItem("runcoach.plan.micro_output_chars").Should().BeOfType<int>()
            .Which.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GeneratePlanAsync_Meso_Tier_Activity_Carries_WeekIndex_And_DeloadCandidate_Tags()
    {
        // Arrange — meso child spans must stamp their week index and
        // deload-candidate hint so Phoenix can color-code week-by-week.
        var planId = NewPlanId();
        var startedActivities = new List<Activity>();
        using var listener = CreateActivityListener(startedActivities);
        var (sut, llm, _) = CreateSut();
        ConfigureLlmHappyPath(llm);

        // Act
        await sut.GeneratePlanAsync(
            CreateCompletedView(),
            UserId,
            planId,
            intent: null,
            previousPlanId: null,
            TestContext.Current.CancellationToken);

        // Assert — filter by PlanId so concurrent tests' meso spans don't
        // leak into the count.
        var mesos = startedActivities
            .Where(a => string.Equals(a.Source.Name, PlanGenerationService.ObservabilitySourceName, StringComparison.Ordinal)
                && string.Equals(a.OperationName, PlanGenerationService.TierActivityName, StringComparison.Ordinal)
                && string.Equals((string?)a.GetTagItem("runcoach.plan.tier"), PlanGenerationService.TierMeso, StringComparison.Ordinal)
                && (Guid?)a.GetTagItem("runcoach.plan.id") == planId)
            .ToList();
        mesos.Should().HaveCount(4);

        var weekIndices = mesos
            .Select(a => (int)(a.GetTagItem("runcoach.plan.week_index") ?? 0))
            .OrderBy(x => x)
            .ToArray();
        weekIndices.Should().Equal(1, 2, 3, 4);

        // All four meso spans must carry an explicit deload-candidate tag —
        // the value can be true or false but the absence of the tag breaks
        // Phoenix grouping on the deload axis.
        mesos.Should().AllSatisfy(a =>
            a.GetTagItem("runcoach.plan.is_deload_candidate").Should().NotBeNull());
    }

    [Fact]
    public async Task GeneratePlanAsync_FailureOnFourthMesoCall_DoesNotEmit_PlanGenerationCompleted_Metric()
    {
        // Arrange — when the chain throws mid-stream the rollup metric must
        // NOT fire, otherwise the dashboard would show false "successful
        // completion" counts for chains that actually rolled back. The
        // tier-level child spans for calls that did happen still emit (their
        // using/dispose lifecycle is independent of the throw).
        var planId = NewPlanId();
        var measurements = new List<RecordedMeasurement>();
        using var listener = CreateMeterListener(measurements);
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

                return BuildMeso(mesoCalls, PhaseType.Base, isDeload: false);
            });

        // Act
        var act = () => sut.GeneratePlanAsync(
            CreateCompletedView(),
            UserId,
            planId,
            intent: null,
            previousPlanId: null,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        measurements
            .Where(m => string.Equals(m.InstrumentName, PlanGenerationService.PlanGenerationCompletedMetricName, StringComparison.Ordinal)
                && m.Tags.TryGetValue("runcoach.plan.id", out var pid)
                && (pid as string) == planId.ToString())
            .Should().BeEmpty(because: "the rollup metric only fires on a successful chain");
    }

    // Per-test unique PlanIds so the metric/activity assertions can filter
    // by their own emission even when other tests in the class run
    // concurrently in the same process.
    private static Guid NewPlanId() => Guid.NewGuid();

    private static MeterListener CreateMeterListener(List<RecordedMeasurement> sink)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == PlanGenerationService.ObservabilitySourceName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
        {
            var bag = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < tags.Length; i++)
            {
                bag[tags[i].Key] = tags[i].Value;
            }

            sink.Add(new RecordedMeasurement(
                MeterName: instrument.Meter.Name,
                InstrumentName: instrument.Name,
                Value: value,
                Tags: bag));
        });

        listener.Start();
        return listener;
    }

    private static ActivityListener CreateActivityListener(List<Activity> startedSink)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                string.Equals(source.Name, PlanGenerationService.ObservabilitySourceName, StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> options) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => startedSink.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static (PlanGenerationService Sut, ICoachingLlm Llm, IContextAssembler Assembler) CreateSut()
    {
        var assembler = Substitute.For<IContextAssembler>();
        assembler
            .ComposeForPlanGenerationAsync(
                Arg.Any<OnboardingView>(),
                Arg.Any<RegenerationIntent?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new PlanGenerationPromptComposition(
                SystemPrompt: "stable test system prompt",
                UserMessage: "stable test base user message"));

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

    private static void ConfigureMacroSuccess(ICoachingLlm llm)
    {
        llm
            .GenerateStructuredAsync<MacroPlanOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => BuildMacro());
    }

    private static void ConfigureLlmHappyPath(ICoachingLlm llm)
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
                return BuildMeso(mesoCounter, PhaseType.Base, isDeload: false);
            });

        llm
            .GenerateStructuredAsync<MicroWorkoutListOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => BuildMicro());
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

    /// <summary>
    /// Captured measurement record from <see cref="MeterListener"/>. The
    /// listener delivers tags as a <c>ReadOnlySpan</c>; the test pipes them
    /// into a stable dictionary for assertion.
    /// </summary>
    private sealed record RecordedMeasurement(
        string MeterName,
        string InstrumentName,
        double Value,
        IReadOnlyDictionary<string, object?> Tags);
}
