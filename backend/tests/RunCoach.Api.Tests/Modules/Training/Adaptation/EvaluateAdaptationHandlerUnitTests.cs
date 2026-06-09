using System.Reflection;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Sanitization;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Modules.Training.Safety;
using RunCoach.Api.Modules.Training.Workouts;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace RunCoach.Api.Tests.Modules.Training.Adaptation;

/// <summary>
/// Unit tests for <see cref="EvaluateAdaptationHandler"/> (Slice 3 § Unit 5):
/// every deterministic path — idempotent replay, off-plan no-op, Red safety
/// short-circuit, L0 absorb, L1 nudge, the L1→L2 escalation into the
/// restructure seam — plus the SeenAsync-first/Record-last marker choreography
/// and the chain-scoped concurrency policy registration. The LLM restructure
/// path itself is the T05 seam and is covered only as "stages nothing yet".
/// </summary>
public sealed class EvaluateAdaptationHandlerUnitTests
{
    private static readonly Guid PlanId = Guid.NewGuid();

    [Fact]
    public async Task Handle_OnIdempotencyHit_ReturnsPriorResponseAndTouchesNothing()
    {
        // Arrange
        var harness = new Harness();
        var cmd = new EvaluateAdaptationCommand(Guid.NewGuid(), Guid.NewGuid());
        var expectedPrior = AdaptationResponseDto.Adapted(AdaptationKind.Nudge);
        harness.Idempotency
            .SeenAsync<AdaptationResponseDto>(cmd.WorkoutLogId, Arg.Any<CancellationToken>())
            .Returns(expectedPrior);

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — byte-identical replay, nothing re-evaluated, nothing staged.
        actual.Should().BeSameAs(expectedPrior);
        await harness.WorkoutLogs.DidNotReceiveWithAnyArgs()
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        harness.Appended.Should().BeEmpty();
        harness.Idempotency.DidNotReceiveWithAnyArgs()
            .Record(Arg.Any<Guid>(), Arg.Any<AdaptationResponseDto>());
    }

    [Fact]
    public async Task Handle_WhenLogIsMissing_ThrowsWithNothingStaged()
    {
        // Arrange — the create flow dispatches only after its EF commit, so a
        //   missing row is a protocol violation that must abort the transaction.
        var harness = new Harness();
        var cmd = new EvaluateAdaptationCommand(Guid.NewGuid(), Guid.NewGuid());
        harness.WorkoutLogs
            .GetByIdAsync(cmd.UserId, cmd.WorkoutLogId, Arg.Any<CancellationToken>())
            .Returns((WorkoutLog?)null);

        // Act
        var act = async () => await harness.HandleAsync(cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{cmd.WorkoutLogId}*");
        harness.Appended.Should().BeEmpty();
        harness.Session.DidNotReceiveWithAnyArgs().Store(Arg.Any<AdaptationSignalStateDocument>());
        harness.Idempotency.DidNotReceiveWithAnyArgs()
            .Record(Arg.Any<Guid>(), Arg.Any<AdaptationResponseDto>());
    }

    [Fact]
    public async Task Handle_OffPlanLog_RecordsNoOpMarkerAndAppendsNothing()
    {
        // Arrange — null prescription => null deviation => cheap no-op (R04.2).
        var harness = new Harness();
        var log = BuildLog(prescription: null);
        var cmd = CommandFor(log);
        harness.StubLog(cmd, log);
        harness.DeviationEngine.Evaluate(log).Returns((DeviationResult?)null);

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — no event, no safety gate, no classifier, no state doc; the
        //   no-op response IS recorded so replays short-circuit cheaply.
        actual.Kind.Should().Be(AdaptationResponseKind.Adapted);
        actual.AdaptationKind.Should().Be(AdaptationKind.Absorb);
        harness.Appended.Should().BeEmpty();
        harness.SafetyGate.DidNotReceiveWithAnyArgs()
            .Classify(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>());
        harness.Classifier.DidNotReceiveWithAnyArgs().Classify(
            Arg.Any<DeviationResult>(), Arg.Any<SafetyTier>(), Arg.Any<AdaptationSignalState>());
        harness.Session.DidNotReceiveWithAnyArgs().Store(Arg.Any<AdaptationSignalStateDocument>());
        harness.Idempotency.Received(1).Record(
            cmd.WorkoutLogId,
            Arg.Is<AdaptationResponseDto>(r => r.Kind == AdaptationResponseKind.Adapted));
    }

    [Fact]
    public async Task Handle_RedCrisis_AppendsOnlyScriptedCrisisTurnToThePlanStream()
    {
        // Arrange — gate resolves Red/Crisis (R04.5).
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot(), notes: "crisis note");
        var cmd = CommandFor(log);
        harness.StubOnPlan(cmd, log);
        harness.SafetyGate
            .Classify(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(SafetyClassification.Red(ReferralCategory.Crisis));

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — exactly one SafetySignalRaised with the verbatim scripted
        //   crisis content, appended to the prescription's source plan stream.
        harness.Appended.Should().ContainSingle();
        var (streamId, evt) = harness.Appended[0];
        streamId.Should().Be(PlanId);
        var expectedSignal = new SafetySignalRaised(
            cmd.WorkoutLogId, SafetyTier.Red, ReferralCategory.Crisis, CrisisResponseContent.CrisisResponse);
        evt.Should().Be(expectedSignal);

        // No LLM-bearing plan change, no classifier, no signal-state advance.
        harness.Classifier.DidNotReceiveWithAnyArgs().Classify(
            Arg.Any<DeviationResult>(), Arg.Any<SafetyTier>(), Arg.Any<AdaptationSignalState>());
        harness.Session.DidNotReceiveWithAnyArgs().Store(Arg.Any<AdaptationSignalStateDocument>());

        // Marker records the short-circuit so a replay never re-appends.
        harness.Idempotency.Received(1).Record(cmd.WorkoutLogId, Arg.Any<AdaptationResponseDto>());
        actual.AdaptationKind.Should().Be(AdaptationKind.Absorb);
    }

    [Fact]
    public async Task Handle_RedEmergencyReferral_AppendsStopAndReferContentNotTheCrisisScript()
    {
        // Arrange — chest-pain-style Red must direct to urgent medical care,
        //   never to the 988 mental-health crisis lines.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot(), notes: "chest pain note");
        var cmd = CommandFor(log);
        harness.StubOnPlan(cmd, log);
        harness.SafetyGate
            .Classify(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(SafetyClassification.Red(ReferralCategory.EmergencyReferral));

        // Act
        await harness.HandleAsync(cmd);

        // Assert
        harness.Appended.Should().ContainSingle();
        var signal = harness.Appended[0].Event.Should().BeOfType<SafetySignalRaised>().Subject;
        signal.ReferralCategory.Should().Be(ReferralCategory.EmergencyReferral);
        signal.Content.Should().Be(EmergencyResponseContent.EmergencyResponse);
        signal.Content.Should().NotContain("988");
    }

    [Fact]
    public async Task Handle_AbsorbDecision_AppendsNothingButAdvancesSignalState()
    {
        // Arrange — L0 absorb (R04.3): no stored state doc yet, so the
        //   classifier must receive Initial; its NextState must be persisted.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot());
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        var expectedNextState = AdaptationSignalState.Create(PlanState.MinorDeviation, 1.0, 0, null);
        AdaptationSignalState? actualPriorState = null;
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Do<AdaptationSignalState>(s => actualPriorState = s))
            .Returns(new EscalationDecision(EscalationLevel.Absorb, AdaptationKind.Absorb, expectedNextState));

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — an absorb never produces an event...
        actual.AdaptationKind.Should().Be(AdaptationKind.Absorb);
        harness.Appended.Should().BeEmpty();
        actualPriorState.Should().Be(AdaptationSignalState.Initial);

        // ...but the signal-state document still advances, keyed by the plan.
        harness.Session.Received(1).Store(Arg.Is<AdaptationSignalStateDocument>(d =>
            d.PlanId == PlanId && d.ToState() == expectedNextState));
        harness.Idempotency.Received(1).Record(cmd.WorkoutLogId, Arg.Any<AdaptationResponseDto>());
    }

    [Fact]
    public async Task Handle_WithStoredSignalState_RehydratesThroughTheValidatingFactory()
    {
        // Arrange — a stored document must reach the classifier as its
        //   validated ToState() shape, not Initial.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot());
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        var storedState = AdaptationSignalState.Create(
            PlanState.NeedsAdjustment, 4.0, 2, new DateOnly(2026, 5, 30));
        harness.Session
            .LoadAsync<AdaptationSignalStateDocument>(PlanId, Arg.Any<CancellationToken>())
            .Returns(AdaptationSignalStateDocument.From(PlanId, storedState));
        AdaptationSignalState? actualPriorState = null;
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Do<AdaptationSignalState>(s => actualPriorState = s))
            .Returns(new EscalationDecision(EscalationLevel.Absorb, AdaptationKind.Absorb, storedState));

        // Act
        await harness.HandleAsync(cmd);

        // Assert
        actualPriorState.Should().Be(storedState);
    }

    [Fact]
    public async Task Handle_MicroAdjust_AppendsExactlyOneNudgeEventWithThePlannedSwap()
    {
        // Arrange — missed Tuesday Tempo, Wednesday easy day available (R04.4).
        var harness = new Harness();
        var snapshot = BuildSnapshot(weekNumber: 1, dayOfWeek: 2);
        var log = BuildLog(snapshot);
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubMicroWeek(weekNumber: 1, BuildSwappableWeek());
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(MicroAdjustDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — exactly one PlanAdaptedFromLog nudge, no LLM anywhere in
        //   this path (no LLM seam exists on the L1 branch by construction).
        actual.AdaptationKind.Should().Be(AdaptationKind.Nudge);
        harness.Appended.Should().ContainSingle();
        var (streamId, evt) = harness.Appended[0];
        streamId.Should().Be(PlanId);
        var adapted = evt.Should().BeOfType<PlanAdaptedFromLog>().Subject;
        adapted.TriggeringWorkoutLogId.Should().Be(cmd.WorkoutLogId);
        adapted.AdaptationKind.Should().Be(AdaptationKind.Nudge);
        adapted.EscalationLevel.Should().Be(EscalationLevel.MicroAdjust);
        adapted.SafetyTier.Should().Be(SafetyTier.Green);

        // The diff is the planner's forward swap: Tempo Tue(2) <-> Easy Wed(3).
        adapted.Diff.WorkoutChanges.Should().HaveCount(2);
        adapted.Diff.WorkoutChanges[0].DayOfWeek.Should().Be(2);
        adapted.Diff.WorkoutChanges[1].DayOfWeek.Should().Be(3);
        adapted.Diff.WeeklyTargetChanges.Should().BeEmpty();

        // Deterministic, user-facing rationale rendered from the diff.
        adapted.Rationale.Should().Contain("Tempo").And.Contain("Tuesday").And.Contain("Wednesday");

        harness.Session.Received(1).Store(Arg.Any<AdaptationSignalStateDocument>());
        harness.Idempotency.Received(1).Record(cmd.WorkoutLogId, Arg.Any<AdaptationResponseDto>());
    }

    [Fact]
    public async Task Handle_MicroAdjust_StagesAppendThenStateThenMarkerInOrder()
    {
        // Arrange — R04.6 choreography: marker LAST so it commits atomically
        //   with (never without) the appends it memoizes.
        var harness = new Harness();
        var snapshot = BuildSnapshot(weekNumber: 1, dayOfWeek: 2);
        var log = BuildLog(snapshot);
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubMicroWeek(weekNumber: 1, BuildSwappableWeek());
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(MicroAdjustDecision());

        // Act
        await harness.HandleAsync(cmd);

        // Assert
        Received.InOrder(() =>
        {
            harness.Session.Events.Append(PlanId, Arg.Any<object[]>());
            harness.Session.Store(Arg.Any<AdaptationSignalStateDocument>());
            harness.Idempotency.Record(cmd.WorkoutLogId, Arg.Any<AdaptationResponseDto>());
        });
    }

    [Fact]
    public async Task Handle_MicroAdjust_WithNoForwardSwap_EscalatesToTheRestructureSeam()
    {
        // Arrange — the missed key workout is Saturday's long run with no later
        //   easy day, so TryPlanSwap must fail and the L1 escalates to L2.
        var harness = new Harness();
        var snapshot = BuildSnapshot(weekNumber: 1, dayOfWeek: 6, type: WorkoutType.LongRun);
        var log = BuildLog(snapshot);
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubMicroWeek(weekNumber: 1, BuildSwappableWeek());
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(MicroAdjustDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — seam behavior until T05: nothing staged at all, so the very
        //   same log re-evaluates (and restructures) once the LLM path lands.
        AssertDeferredToSeam(harness, actual);
        harness.Logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("Restructure required"));
    }

    [Fact]
    public async Task Handle_MicroAdjust_WithNoLiveMicroWeek_EscalatesToTheRestructureSeam()
    {
        // Arrange — prescription targets week 2 but only week 1 micro detail is
        //   materialized at MVP-0: never swap blind, escalate instead.
        var harness = new Harness();
        var snapshot = BuildSnapshot(weekNumber: 2, dayOfWeek: 2);
        var log = BuildLog(snapshot);
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubMicroWeek(weekNumber: 1, BuildSwappableWeek());
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(MicroAdjustDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert
        AssertDeferredToSeam(harness, actual);
    }

    [Fact]
    public async Task Handle_RestructureDecision_DefersToTheSeamWithNothingStaged()
    {
        // Arrange — classifier-direct L2: the LLM path is T05's; until it lands
        //   the handler must not memoize the log as a silent absorb.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot());
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(new EscalationDecision(
                EscalationLevel.Restructure,
                AdaptationKind.Restructure,
                AdaptationSignalState.Create(PlanState.NeedsAdjustment, 5.0, 0, new DateOnly(2026, 6, 8))));

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert
        AssertDeferredToSeam(harness, actual);
        harness.Logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("Restructure required"));
    }

    [Fact]
    public async Task Handle_FeedsTheSanitizedFreeTextToTheSafetyGate()
    {
        // Arrange — the gate expects DEC-059-sanitized input at the caller
        //   boundary; prove the sanitizer's output (not the raw note/metrics)
        //   is what the gate scans.
        var harness = new Harness();
        var snapshot = BuildSnapshot();
        var log = BuildLog(
            snapshot,
            notes: "raw note",
            metricsJson: """{"weather":"raw weather","rpe":7}""");
        var cmd = CommandFor(log);
        harness.StubLog(cmd, log);
        harness.DeviationEngine.Evaluate(log).Returns(BuildDeviation());
        harness.Sanitizer.Transform = detail =>
        {
            var metrics = new Dictionary<string, string>(detail.Metrics, StringComparer.Ordinal)
            {
                ["weather"] = "sanitized weather",
            };
            return detail with { Notes = "sanitized note", Metrics = metrics };
        };
        harness.Classifier
            .Classify(Arg.Any<DeviationResult>(), Arg.Any<SafetyTier>(), Arg.Any<AdaptationSignalState>())
            .Returns(new EscalationDecision(
                EscalationLevel.Absorb, AdaptationKind.Absorb, AdaptationSignalState.Initial));

        // Act
        await harness.HandleAsync(cmd);

        // Assert — the raw entity text reached the sanitizer...
        var actualSanitizerInput = harness.Sanitizer.LastInput;
        actualSanitizerInput.Should().NotBeNull();
        actualSanitizerInput!.Notes.Should().Be("raw note");
        actualSanitizerInput.Metrics.Should().Contain("weather", "raw weather");
        actualSanitizerInput.Metrics.Should().Contain("rpe", "7");

        // ...and only the sanitized text reached the gate.
        harness.SafetyGate.Received(1).Classify(
            "sanitized note",
            Arg.Is<IReadOnlyDictionary<string, string>?>(m =>
                m != null && m["weather"] == "sanitized weather"));
    }

    [Fact]
    public void ConfigureFailureRules_RegistersExactlyOneBoundedRetryRule()
    {
        // Arrange — R04.7: the adaptation chain retries stream-version
        //   conflicts; the global DEC-057 dead-letter rules stay untouched
        //   because chain rules are evaluated before global rules.
        var policies = Substitute.For<IWithFailurePolicies>();
        var actualRules = new FailureRuleCollection();
        policies.Failures.Returns(actualRules);

        // Act
        EvaluateAdaptationHandler.ConfigureFailureRules(policies);

        // Assert
        actualRules.Should().ContainSingle();
    }

    [Fact]
    public void Configure_ExposesTheWolverineChainConventionSignature()
    {
        // Arrange / Act — Wolverine discovers `public static void
        //   Configure(HandlerChain)` on the handler type at startup; if the
        //   signature drifts the policy silently stops applying.
        var actualMethod = typeof(EvaluateAdaptationHandler)
            .GetMethod("Configure", BindingFlags.Public | BindingFlags.Static);

        // Assert
        actualMethod.Should().NotBeNull();
        actualMethod!.ReturnType.Should().Be(typeof(void));
        actualMethod.GetParameters().Should().ContainSingle()
            .Which.ParameterType.Should().Be<HandlerChain>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ToDisplayMetrics_WithNoMetrics_ReturnsEmpty(string? metricsJson)
    {
        // Act
        var actual = WorkoutMetricsProjection.ToDisplayMetrics(metricsJson);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void ToDisplayMetrics_MapsScalarsAndDropsNonScalars()
    {
        // Arrange — strings verbatim, numbers/booleans as literal JSON text,
        //   arrays/objects/nulls dropped.
        const string metricsJson =
            """{"rpe":7,"hrAvg":148.5,"weather":"hot & humid","terrain":"trail","flag":true,"splits":[1,2],"nested":{"a":1},"empty":null}""";

        // Act
        var actual = WorkoutMetricsProjection.ToDisplayMetrics(metricsJson);

        // Assert
        var expected = new Dictionary<string, string>
        {
            ["rpe"] = "7",
            ["hrAvg"] = "148.5",
            ["weather"] = "hot & humid",
            ["terrain"] = "trail",
            ["flag"] = "true",
        };
        actual.Should().BeEquivalentTo(expected);
    }

    private static void AssertDeferredToSeam(Harness harness, AdaptationResponseDto actual)
    {
        // Seam contract until T05: respond as a no-change absorb but stage
        // NOTHING — no event, no signal-state advance, no idempotency marker.
        actual.Kind.Should().Be(AdaptationResponseKind.Adapted);
        actual.AdaptationKind.Should().Be(AdaptationKind.Absorb);
        harness.Appended.Should().BeEmpty();
        harness.Session.DidNotReceiveWithAnyArgs().Store(Arg.Any<AdaptationSignalStateDocument>());
        harness.Idempotency.DidNotReceiveWithAnyArgs()
            .Record(Arg.Any<Guid>(), Arg.Any<AdaptationResponseDto>());
    }

    private static EvaluateAdaptationCommand CommandFor(WorkoutLog log) =>
        new(log.WorkoutLogId, log.UserId);

    private static EscalationDecision MicroAdjustDecision() =>
        new(
            EscalationLevel.MicroAdjust,
            AdaptationKind.Nudge,
            AdaptationSignalState.Create(PlanState.MinorDeviation, 2.0, 1, null));

    private static DeviationResult BuildDeviation() =>
        new(
            OccurredOn: new DateOnly(2026, 6, 9),
            CompletionStatus: CompletionStatus.Skipped,
            IsKeyWorkout: true,
            DistanceDeviationPercent: -100.0,
            DurationDeviationPercent: -100.0,
            PaceBand: PaceBandMembership.Unknown,
            PaceDeviationSecondsPerKm: 0.0);

    private static WorkoutLog BuildLog(
        WorkoutPrescriptionSnapshot? prescription,
        string? notes = null,
        string? metricsJson = null) =>
        new()
        {
            WorkoutLogId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid().ToString(),
            IdempotencyKey = Guid.NewGuid(),
            OccurredOn = new DateOnly(2026, 6, 9),
            Distance = Distance.FromKilometers(0),
            Duration = Duration.FromMinutes(0),
            CompletionStatus = CompletionStatus.Skipped,
            Notes = notes,
            Metrics = metricsJson,
            Prescription = prescription,
        };

    private static WorkoutPrescriptionSnapshot BuildSnapshot(
        int weekNumber = 1,
        int dayOfWeek = 2,
        WorkoutType type = WorkoutType.Tempo) =>
        WorkoutPrescriptionSnapshot.Create(
            sourcePlanId: PlanId,
            weekNumber: weekNumber,
            dayOfWeek: dayOfWeek,
            workoutType: type,
            prescribedDistance: Distance.FromKilometers(10),
            prescribedDuration: Duration.FromMinutes(50),
            prescribedPaceFast: Pace.FromSecondsPerKm(280),
            prescribedPaceSlow: Pace.FromSecondsPerKm(320));

    /// <summary>
    /// A week where Tuesday's Tempo (day 2) can swap forward with Wednesday's
    /// easy day (day 3) without stacking key days, but Saturday's long run
    /// (day 6) has no later swap target.
    /// </summary>
    private static WorkoutOutput[] BuildSwappableWeek() =>
    [
        BuildWorkout(0, WorkoutType.Easy),
        BuildWorkout(2, WorkoutType.Tempo),
        BuildWorkout(3, WorkoutType.Easy),
        BuildWorkout(6, WorkoutType.LongRun),
    ];

    private static WorkoutOutput BuildWorkout(int dayOfWeek, WorkoutType type) =>
        new()
        {
            DayOfWeek = dayOfWeek,
            WorkoutType = type,
            Title = type.ToString(),
            TargetDistanceKm = 10,
            TargetDurationMinutes = 50,
            TargetPaceEasySecPerKm = 330,
            TargetPaceFastSecPerKm = 280,
            Segments = [],
            WarmupNotes = string.Empty,
            CooldownNotes = string.Empty,
            CoachingNotes = string.Empty,
            PerceivedEffort = 5,
        };

    /// <summary>
    /// One substitute set per test with the pass-through defaults every path
    /// shares: idempotency miss, pass-through sanitizer, Green gate, and an
    /// append-capturing event store.
    /// </summary>
    private sealed class Harness
    {
        public Harness()
        {
            Idempotency
                .SeenAsync<AdaptationResponseDto>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((AdaptationResponseDto?)null);
            SafetyGate
                .Classify(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
                .Returns(SafetyClassification.Green());
            Session.Events
                .When(events => events.Append(Arg.Any<Guid>(), Arg.Any<object[]>()))
                .Do(callInfo =>
                {
                    foreach (var evt in callInfo.Arg<object[]>())
                    {
                        Appended.Add((callInfo.Arg<Guid>(), evt));
                    }
                });
        }

        public IDocumentSession Session { get; } = Substitute.For<IDocumentSession>();

        public IWorkoutLogRepository WorkoutLogs { get; } = Substitute.For<IWorkoutLogRepository>();

        public IDeviationEngine DeviationEngine { get; } = Substitute.For<IDeviationEngine>();

        public FakeRecentLogSanitizer Sanitizer { get; } = new();

        public ISafetyGate SafetyGate { get; } = Substitute.For<ISafetyGate>();

        public IEscalationClassifier Classifier { get; } = Substitute.For<IEscalationClassifier>();

        public IIdempotencyStore Idempotency { get; } = Substitute.For<IIdempotencyStore>();

        public CollectingLogger Logger { get; } = new();

        public List<(Guid StreamId, object Event)> Appended { get; } = [];

        public void StubLog(EvaluateAdaptationCommand cmd, WorkoutLog log) =>
            WorkoutLogs
                .GetByIdAsync(cmd.UserId, cmd.WorkoutLogId, Arg.Any<CancellationToken>())
                .Returns(log);

        /// <summary>
        /// Stubs the shared on-plan preamble — log read, deviation, empty prior
        /// signal state — and returns the deviation handed to the classifier.
        /// </summary>
        public DeviationResult StubOnPlan(EvaluateAdaptationCommand cmd, WorkoutLog log)
        {
            StubLog(cmd, log);
            var deviation = BuildDeviation();
            DeviationEngine.Evaluate(log).Returns(deviation);
            Session
                .LoadAsync<AdaptationSignalStateDocument>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((AdaptationSignalStateDocument?)null);
            return deviation;
        }

        public void StubMicroWeek(int weekNumber, WorkoutOutput[] week) =>
            Session
                .LoadAsync<PlanProjectionDto>(PlanId, Arg.Any<CancellationToken>())
                .Returns(new PlanProjectionDto
                {
                    PlanId = PlanId,
                    MicroWorkoutsByWeek = new Dictionary<int, MicroWorkoutListOutput>
                    {
                        [weekNumber] = new() { Workouts = week },
                    },
                });

        public Task<AdaptationResponseDto> HandleAsync(EvaluateAdaptationCommand cmd) =>
            EvaluateAdaptationHandler.Handle(
                cmd,
                Session,
                WorkoutLogs,
                DeviationEngine,
                Sanitizer,
                SafetyGate,
                Classifier,
                Idempotency,
                Logger,
                TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Hand-written fake (not an NSubstitute substitute): stubbing a
    /// <see cref="ValueTask{TResult}"/>-returning member through NSubstitute's
    /// <c>Returns</c> trips CA2012, and a plain fake also gives input capture
    /// for free. Pass-through by default; tests override <see cref="Transform"/>.
    /// </summary>
    private sealed class FakeRecentLogSanitizer : IRecentLogSanitizer
    {
        public Func<LoggedWorkoutDetail, LoggedWorkoutDetail> Transform { get; set; } = detail => detail;

        public LoggedWorkoutDetail? LastInput { get; private set; }

        public ValueTask<LoggedWorkoutDetail> SanitizeAsync(
            LoggedWorkoutDetail detail,
            CancellationToken ct = default)
        {
            LastInput = detail;
            return new ValueTask<LoggedWorkoutDetail>(Transform(detail));
        }
    }

    /// <summary>
    /// Minimal capturing logger: the source-generated <c>[LoggerMessage]</c>
    /// call sites use a generic internal state struct that NSubstitute cannot
    /// match, so assertions read the formatted entries instead.
    /// </summary>
    private sealed class CollectingLogger : ILogger<EvaluateAdaptationHandler>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
