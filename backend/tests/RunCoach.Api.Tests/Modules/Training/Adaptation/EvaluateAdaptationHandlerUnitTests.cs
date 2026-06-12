using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using JasperFx.Events;
using Marten;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Models;
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
/// short-circuit, L0 absorb, L1 nudge, the L1→L2 escalation — plus the L2 LLM
/// restructure path (single frozen-schema call, deterministic diff, Amber
/// dual-append, the DEC-073 error envelope with stage-nothing-on-failure), the
/// SeenAsync-first/Record-last marker choreography, and the chain-scoped
/// concurrency policy registration.
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
    public async Task Handle_MicroAdjust_WithNoForwardSwap_EscalatesIntoTheLlmRestructure()
    {
        // Arrange — the missed key workout is Saturday's long run with no later
        //   easy day, so TryPlanSwap must fail and the L1 escalates to L2.
        var harness = new Harness();
        var snapshot = BuildSnapshot(weekNumber: 1, dayOfWeek: 6, type: WorkoutType.LongRun);
        var log = BuildLog(snapshot);
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        harness.StubLlm(BuildRestructureOutput());
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(MicroAdjustDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — the escalated L1 lands in the LLM restructure path and the
        //   event records the executed level (Restructure), not the classifier's.
        actual.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        var adapted = harness.Appended.Should().ContainSingle()
            .Which.Event.Should().BeOfType<PlanAdaptedFromLog>().Subject;
        adapted.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        adapted.EscalationLevel.Should().Be(EscalationLevel.Restructure);
    }

    [Fact]
    public async Task Handle_MicroAdjust_WithNoLiveMicroWeek_EscalatesIntoTheLlmRestructure()
    {
        // Arrange — prescription targets week 2 but only week 1 micro detail is
        //   materialized at MVP-0: never swap blind, escalate instead. The diff
        //   then carries no workout change (week 2 has no micro detail to edit)
        //   but the meso weekly-target edits still apply.
        var harness = new Harness();
        var snapshot = BuildSnapshot(weekNumber: 2, dayOfWeek: 2);
        var log = BuildLog(snapshot);
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        harness.StubLlm(BuildRestructureOutput());
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(MicroAdjustDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert
        actual.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        var adapted = harness.Appended.Should().ContainSingle()
            .Which.Event.Should().BeOfType<PlanAdaptedFromLog>().Subject;
        adapted.Diff.WorkoutChanges.Should().BeEmpty();
        adapted.Diff.WeeklyTargetChanges.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_RestructureDecision_CallsTheLlmOnceWithTheFrozenSchemaAndAppendsTheDeterministicDiff()
    {
        // Arrange — classifier-direct L2 under Green (R05.1 / R05.2).
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot());
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        var output = BuildRestructureOutput();
        harness.StubLlm(output);
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(RestructureDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — exactly ONE LLM call, byte-stable frozen schema + 1h cache
        //   marker (DEC-073: no handler-side retry loop).
        await harness.Llm.Received(1).GenerateStructuredAsync<PlanAdaptationOutput>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<IReadOnlyDictionary<string, JsonElement>?>(s => ReferenceEquals(s, AdaptationSchema.Frozen)),
            CacheControl.Ephemeral1h,
            Arg.Any<CancellationToken>());

        // The single restructure event carries the LLM rationale verbatim and
        // the deterministic projection-space diff — never parsed prose.
        actual.Kind.Should().Be(AdaptationResponseKind.Adapted);
        actual.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        var (streamId, evt) = harness.Appended.Should().ContainSingle().Subject;
        streamId.Should().Be(PlanId);
        var adapted = evt.Should().BeOfType<PlanAdaptedFromLog>().Subject;
        adapted.TriggeringWorkoutLogId.Should().Be(cmd.WorkoutLogId);
        adapted.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        adapted.EscalationLevel.Should().Be(EscalationLevel.Restructure);
        adapted.SafetyTier.Should().Be(SafetyTier.Green);
        adapted.Rationale.Should().Be(output.Rationale);

        // Diff: the revised Tuesday workout against week 1's live micro week,
        // plus the week-2 target cut 30 -> 24 (week 3's no-op edit is dropped).
        var workoutChange = adapted.Diff.WorkoutChanges.Should().ContainSingle().Subject;
        workoutChange.WeekNumber.Should().Be(1);
        workoutChange.DayOfWeek.Should().Be(2);
        workoutChange.Before!.WorkoutType.Should().Be(WorkoutType.Tempo);
        workoutChange.After!.WorkoutType.Should().Be(WorkoutType.Easy);
        var targetChange = adapted.Diff.WeeklyTargetChanges.Should().ContainSingle().Subject;
        targetChange.Should().Be(new WeeklyTargetChange(2, 30, 24));

        // Choreography: append -> state -> marker LAST, all staged post-success.
        Received.InOrder(() =>
        {
            harness.Session.Events.Append(PlanId, Arg.Any<object[]>());
            harness.Session.Store(Arg.Any<AdaptationSignalStateDocument>());
            harness.Idempotency.Record(cmd.WorkoutLogId, Arg.Any<AdaptationResponseDto>());
        });
    }

    [Theory]
    [InlineData(ReferralCategory.Injury)]
    [InlineData(ReferralCategory.RedS)]
    public async Task Handle_AmberRestructure_AlsoAppendsTheScriptedReferralSignal(ReferralCategory category)
    {
        // Arrange — Amber L2 (R05.3): the adaptation event AND the scripted
        //   referral signal ride the same transaction; the referral content is
        //   versioned scripted copy routed by category, never LLM prose.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot());
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        harness.StubLlm(BuildRestructureOutput(tier: SafetyTier.Amber, referralCategory: category));
        harness.SafetyGate
            .Classify(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(SafetyClassification.Amber(category));
        harness.Classifier
            .Classify(deviation, SafetyTier.Amber, Arg.Any<AdaptationSignalState>())
            .Returns(RestructureDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — dual append on the plan stream: the referral stages FIRST
        //   (hoisted before the escalation branch — slice 3B F1), then the
        //   restructure event once the LLM + validators succeed.
        actual.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        harness.Appended.Should().HaveCount(2);
        var expectedContent = category == ReferralCategory.Injury
            ? AmberReferralContent.InjuryReferral
            : AmberReferralContent.RedSReferral;
        var expectedSignal = new SafetySignalRaised(cmd.WorkoutLogId, SafetyTier.Amber, category, expectedContent);
        harness.Appended[0].Event.Should().Be(expectedSignal);
        var adapted = harness.Appended[1].Event.Should().BeOfType<PlanAdaptedFromLog>().Subject;
        adapted.SafetyTier.Should().Be(SafetyTier.Amber);
        harness.Idempotency.Received(1).Record(cmd.WorkoutLogId, Arg.Any<AdaptationResponseDto>());
    }

    [Theory]
    [InlineData(ReferralCategory.Injury)]
    [InlineData(ReferralCategory.RedS)]
    public async Task Handle_AmberAbsorb_AppendsTheScriptedReferralSignal(ReferralCategory category)
    {
        // Arrange — slice 3B F1: an Amber classification must surface the scripted
        //   referral even when the escalation outcome is a no-event L0 absorb
        //   (the live-pass repro shape: Amber inside the cooldown dead-zone — an
        //   Absorb decision from the handler's perspective — produced silence).
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot(), notes: "amber-matching note");
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.SafetyGate
            .Classify(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(SafetyClassification.Amber(category));
        var nextState = AdaptationSignalState.Create(PlanState.MinorDeviation, 1.0, 0, null);
        harness.Classifier
            .Classify(deviation, SafetyTier.Amber, Arg.Any<AdaptationSignalState>())
            .Returns(new EscalationDecision(EscalationLevel.Absorb, AdaptationKind.Absorb, nextState));

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — the absorb itself stays event-free, but the referral appends.
        actual.AdaptationKind.Should().Be(AdaptationKind.Absorb);
        var (streamId, evt) = harness.Appended.Should().ContainSingle().Subject;
        streamId.Should().Be(PlanId);
        var expectedContent = category == ReferralCategory.Injury
            ? AmberReferralContent.InjuryReferral
            : AmberReferralContent.RedSReferral;
        evt.Should().Be(new SafetySignalRaised(cmd.WorkoutLogId, SafetyTier.Amber, category, expectedContent));

        // The absorb's signal-state advance and marker choreography are unchanged.
        harness.Session.Received(1).Store(Arg.Is<AdaptationSignalStateDocument>(d =>
            d.PlanId == PlanId && d.ToState() == nextState));
        harness.Idempotency.Received(1).Record(cmd.WorkoutLogId, Arg.Any<AdaptationResponseDto>());
    }

    [Fact]
    public async Task Handle_AmberScoreDrivenMicroAdjust_AbsorbsButStillAppendsTheReferral()
    {
        // Arrange — a COMPLETED slow key workout rides a score-driven L1 (no
        //   missed session to swap), which the handler absorbs deterministically.
        //   Slice 3B F1: the Amber referral must ride that absorb.
        var harness = new Harness();
        var snapshot = BuildSnapshot(weekNumber: 1, dayOfWeek: 2);
        var log = BuildLog(snapshot, notes: "completed, but my shin aches");
        var cmd = CommandFor(log);
        harness.StubLog(cmd, log);
        var deviation = BuildDeviation() with
        {
            CompletionStatus = CompletionStatus.Complete,
            DistanceDeviationPercent = 0.0,
            DurationDeviationPercent = 10.0,
            PaceBand = PaceBandMembership.SlowerThanSlow,
        };
        harness.DeviationEngine.Evaluate(log).Returns(deviation);
        harness.Session
            .LoadAsync<AdaptationSignalStateDocument>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AdaptationSignalStateDocument?)null);
        harness.StubMicroWeek(weekNumber: 1, BuildSwappableWeek());
        harness.SafetyGate
            .Classify(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(SafetyClassification.Amber(ReferralCategory.Injury));
        harness.Classifier
            .Classify(deviation, SafetyTier.Amber, Arg.Any<AdaptationSignalState>())
            .Returns(MicroAdjustDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — absorbed (no plan event, no LLM), yet the referral surfaced.
        actual.AdaptationKind.Should().Be(AdaptationKind.Absorb);
        var signal = harness.Appended.Should().ContainSingle()
            .Which.Event.Should().BeOfType<SafetySignalRaised>().Subject;
        signal.Should().Be(new SafetySignalRaised(
            cmd.WorkoutLogId, SafetyTier.Amber, ReferralCategory.Injury, AmberReferralContent.InjuryReferral));
        await harness.Llm.DidNotReceiveWithAnyArgs().GenerateStructuredAsync<PlanAdaptationOutput>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
            Arg.Any<CacheControl?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AmberMicroAdjustNudge_AppendsTheReferralAndTheNudge()
    {
        // Arrange — a missed key workout with a valid forward swap nudges
        //   deterministically; slice 3B F1 adds the referral alongside it.
        var harness = new Harness();
        var snapshot = BuildSnapshot(weekNumber: 1, dayOfWeek: 2);
        var log = BuildLog(snapshot, notes: "skipped it, knee pain");
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubMicroWeek(weekNumber: 1, BuildSwappableWeek());
        harness.SafetyGate
            .Classify(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(SafetyClassification.Amber(ReferralCategory.Injury));
        harness.Classifier
            .Classify(deviation, SafetyTier.Amber, Arg.Any<AdaptationSignalState>())
            .Returns(MicroAdjustDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — referral first (staged before the escalation branch), then the
        //   nudge event; the nudge carries the gate's Amber tier as before.
        actual.AdaptationKind.Should().Be(AdaptationKind.Nudge);
        harness.Appended.Should().HaveCount(2);
        var signal = harness.Appended[0].Event.Should().BeOfType<SafetySignalRaised>().Subject;
        signal.TriggeringWorkoutLogId.Should().Be(cmd.WorkoutLogId);
        signal.Content.Should().Be(AmberReferralContent.InjuryReferral);
        var adapted = harness.Appended[1].Event.Should().BeOfType<PlanAdaptedFromLog>().Subject;
        adapted.AdaptationKind.Should().Be(AdaptationKind.Nudge);
        adapted.SafetyTier.Should().Be(SafetyTier.Amber);
    }

    [Fact]
    public async Task Handle_AmberRestructureLlmFailure_StillStagesTheReferralWithTheErrorEnvelope()
    {
        // Arrange — slice 3B F1 (user decision 2026-06-11): the referral must not
        //   depend on restructure success. A terminal LLM failure returns the
        //   Kind=Error envelope normally, so Wolverine commits the session — and
        //   the referral, staged before the LLM call, commits with it. The plan
        //   event, state document, and marker stay un-staged (retry still works).
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot(), notes: "sharp pain in my left shin");
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        harness.SafetyGate
            .Classify(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(SafetyClassification.Amber(ReferralCategory.Injury));
        harness.StubLlmThrows(new PermanentCoachingLlmException("The coach could not process this request.", null));
        harness.Classifier
            .Classify(deviation, SafetyTier.Amber, Arg.Any<AdaptationSignalState>())
            .Returns(RestructureDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — error envelope, yet the referral is the one staged append.
        actual.Kind.Should().Be(AdaptationResponseKind.Error);
        actual.Retryable.Should().BeFalse();
        var signal = harness.Appended.Should().ContainSingle()
            .Which.Event.Should().BeOfType<SafetySignalRaised>().Subject;
        signal.TriggeringWorkoutLogId.Should().Be(cmd.WorkoutLogId);

        // Nothing else staged: stream gains only the referral, marker released.
        harness.Session.DidNotReceiveWithAnyArgs().Store(Arg.Any<AdaptationSignalStateDocument>());
        harness.Idempotency.DidNotReceiveWithAnyArgs()
            .Record(Arg.Any<Guid>(), Arg.Any<AdaptationResponseDto>());
    }

    [Fact]
    public async Task Handle_AmberReferralAlreadyOnStream_DoesNotDoubleAppend()
    {
        // Arrange — the marker-released retry after a terminal L2 failure re-runs
        //   the evaluation in full; the referral committed by the failed run must
        //   not append again (slice 3B F1 per-log dedupe).
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot(), notes: "sharp pain in my left shin");
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        harness.StubStream(StreamedSignal(new SafetySignalRaised(
            cmd.WorkoutLogId, SafetyTier.Amber, ReferralCategory.Injury, AmberReferralContent.InjuryReferral)));
        harness.SafetyGate
            .Classify(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(SafetyClassification.Amber(ReferralCategory.Injury));
        harness.StubLlm(BuildRestructureOutput(tier: SafetyTier.Amber, referralCategory: ReferralCategory.Injury));
        harness.Classifier
            .Classify(deviation, SafetyTier.Amber, Arg.Any<AdaptationSignalState>())
            .Returns(RestructureDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — the retried restructure commits, with no second referral.
        actual.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        var adapted = harness.Appended.Should().ContainSingle()
            .Which.Event.Should().BeOfType<PlanAdaptedFromLog>().Subject;
        adapted.AdaptationKind.Should().Be(AdaptationKind.Restructure);
    }

    [Fact]
    public async Task Handle_AmberWithAnotherLogsReferralOnStream_StillAppendsItsOwnReferral()
    {
        // Arrange — the dedupe is keyed per WorkoutLogId: an earlier log's
        //   referral on the same plan stream never suppresses this log's.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot(), notes: "shin still hurts today");
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubStream(StreamedSignal(new SafetySignalRaised(
            Guid.NewGuid(), SafetyTier.Amber, ReferralCategory.Injury, AmberReferralContent.InjuryReferral)));
        harness.SafetyGate
            .Classify(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(SafetyClassification.Amber(ReferralCategory.Injury));
        harness.Classifier
            .Classify(deviation, SafetyTier.Amber, Arg.Any<AdaptationSignalState>())
            .Returns(new EscalationDecision(
                EscalationLevel.Absorb,
                AdaptationKind.Absorb,
                AdaptationSignalState.Create(PlanState.MinorDeviation, 1.0, 0, null)));

        // Act
        await harness.HandleAsync(cmd);

        // Assert
        var signal = harness.Appended.Should().ContainSingle()
            .Which.Event.Should().BeOfType<SafetySignalRaised>().Subject;
        signal.TriggeringWorkoutLogId.Should().Be(cmd.WorkoutLogId);
    }

    [Fact]
    public async Task Handle_TransientLlmFailure_ReturnsRetryableErrorEnvelopeWithNothingStaged()
    {
        // Arrange — DEC-073 (R05.4): the adapter's terminal transient failure
        //   maps to a retryable Kind=Error envelope carrying the retry hint.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot());
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        harness.StubLlmThrows(new TransientCoachingLlmException("The coach is briefly unavailable.", 42, null));
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(RestructureDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — error envelope over a NORMAL return: Wolverine commits the
        //   session, so the path must have staged zero writes for "stream
        //   unchanged / marker released" to hold.
        actual.Kind.Should().Be(AdaptationResponseKind.Error);
        actual.AdaptationKind.Should().BeNull();
        actual.ErrorMessage.Should().Be("The coach is briefly unavailable.");
        actual.Retryable.Should().BeTrue();
        actual.RetryAfterSeconds.Should().Be(42);
        AssertNothingStaged(harness);
    }

    [Fact]
    public async Task Handle_PermanentLlmFailure_ReturnsNonRetryableErrorEnvelopeWithNothingStaged()
    {
        // Arrange — R05.4: a permanent failure yields a non-retryable envelope.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot());
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        harness.StubLlmThrows(new PermanentCoachingLlmException("The coach could not process this request.", null));
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(RestructureDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert
        actual.Kind.Should().Be(AdaptationResponseKind.Error);
        actual.Retryable.Should().BeFalse();
        actual.RetryAfterSeconds.Should().BeNull();
        AssertNothingStaged(harness);
    }

    [Fact]
    public async Task Handle_ValidatorRejectedOutput_ReturnsNonRetryableErrorEnvelopeWithNothingStaged()
    {
        // Arrange — R05.5: a successfully decoded but invariant-violating output
        //   (both typed slots filled) is the handler's policy call: same
        //   Kind=Error envelope, append nothing. No second LLM attempt.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot());
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        var invalid = BuildRestructureOutput() with
        {
            NudgePatch = new NudgePatch { WeekNumber = 1, RevisedWorkouts = [] },
        };
        harness.StubLlm(invalid);
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(RestructureDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert
        actual.Kind.Should().Be(AdaptationResponseKind.Error);
        actual.Retryable.Should().BeFalse();
        AssertNothingStaged(harness);
        await harness.Llm.Received(1).GenerateStructuredAsync<PlanAdaptationOutput>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
            Arg.Any<CacheControl?>(),
            Arg.Any<CancellationToken>());
        harness.Logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("proposal rejected"));
    }

    [Fact]
    public async Task Handle_NonRestructureProposal_ReturnsErrorEnvelopeWithNothingStaged()
    {
        // Arrange — a structurally valid Absorb proposal still contradicts the
        //   deterministic ladder (DEC-079: the LLM never absorbs a sustained
        //   deviation away), so the handler rejects it the same way.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot());
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        harness.StubLlm(BuildRestructureOutput() with
        {
            AdaptationKind = AdaptationKind.Absorb,
            RestructurePlan = null,
        });
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(RestructureDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert
        actual.Kind.Should().Be(AdaptationResponseKind.Error);
        actual.Retryable.Should().BeFalse();
        AssertNothingStaged(harness);
    }

    [Fact]
    public async Task Handle_RestructureWithoutAPlanProjection_ThrowsWithNothingStagedAndNoLlmCall()
    {
        // Arrange — an on-plan log whose plan stream has no materialized
        //   projection is a protocol violation: abort the transaction before
        //   composing any prompt.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot());
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(RestructureDecision());

        // Act
        var act = async () => await harness.HandleAsync(cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no plan projection*");
        AssertNothingStaged(harness);
        await harness.Llm.DidNotReceiveWithAnyArgs().GenerateStructuredAsync<PlanAdaptationOutput>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
            Arg.Any<CacheControl?>(),
            Arg.Any<CancellationToken>());
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
    public async Task Handle_ScoreDrivenMicroAdjustOnACompletedLog_AbsorbsWithNoSwapNoEscalationNoLlm()
    {
        // Arrange — the classifier fires L1 on accumulated under-performance, but the
        //   triggering log is a COMPLETED (slow) key workout: there is no missed
        //   session to reschedule. A swap WOULD be available on this day, yet the
        //   handler must absorb deterministically — never swap a completed workout
        //   (a false "you missed" nudge) nor escalate a 2.0-score signal into the LLM
        //   restructure reserved for the 3.0 crossing.
        var harness = new Harness();
        var snapshot = BuildSnapshot(weekNumber: 1, dayOfWeek: 2);
        var log = BuildLog(snapshot, notes: "felt sluggish but finished");
        var cmd = CommandFor(log);
        harness.StubLog(cmd, log);
        var deviation = new DeviationResult(
            OccurredOn: new DateOnly(2026, 6, 9),
            CompletionStatus: CompletionStatus.Complete,
            IsKeyWorkout: true,
            DistanceDeviationPercent: 0.0,
            DurationDeviationPercent: 10.0,
            PaceBand: PaceBandMembership.SlowerThanSlow,
            PaceDeviationSecondsPerKm: 25.0);
        harness.DeviationEngine.Evaluate(log).Returns(deviation);
        harness.Session
            .LoadAsync<AdaptationSignalStateDocument>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AdaptationSignalStateDocument?)null);
        harness.StubMicroWeek(weekNumber: 1, BuildSwappableWeek());
        var nextState = AdaptationSignalState.Create(PlanState.MinorDeviation, 2.0, 0, null);
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(new EscalationDecision(EscalationLevel.MicroAdjust, AdaptationKind.Nudge, nextState));

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — absorbed: no event, no LLM, state advanced, marker recorded.
        actual.Kind.Should().Be(AdaptationResponseKind.Adapted);
        actual.AdaptationKind.Should().Be(AdaptationKind.Absorb);
        harness.Appended.Should().BeEmpty();
        await harness.Llm.DidNotReceiveWithAnyArgs().GenerateStructuredAsync<PlanAdaptationOutput>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
            Arg.Any<CacheControl?>(),
            Arg.Any<CancellationToken>());
        harness.Session.Received(1).Store(Arg.Is<AdaptationSignalStateDocument>(d =>
            d.PlanId == PlanId && d.ToState() == nextState));
        harness.Idempotency.Received(1).Record(cmd.WorkoutLogId, Arg.Any<AdaptationResponseDto>());
    }

    [Fact]
    public async Task Handle_EscalatedMicroAdjust_StampsRestructureStateAndComposesWithRestructureLevel()
    {
        // Arrange — a missed key workout whose week has no live micro detail escalates
        //   L1 -> L2. The persisted state must ENTER NeedsAdjustment with
        //   LastAdaptationOn stamped (so the cooldown/dead-zone hysteresis engages),
        //   NOT the classifier's MicroAdjust NextState (MinorDeviation, date null),
        //   and the prompt must echo the EXECUTED Restructure level.
        var harness = new Harness();
        var snapshot = BuildSnapshot(weekNumber: 2, dayOfWeek: 2);
        var log = BuildLog(snapshot);
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        harness.StubLlm(BuildRestructureOutput());
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(MicroAdjustDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — restructure executed...
        actual.AdaptationKind.Should().Be(AdaptationKind.Restructure);

        // ...the prompt echoed the executed Restructure level (not MicroAdjust)...
        await harness.Assembler.Received(1).ComposeForAdaptationAsync(
            Arg.Any<PlanProjectionDto>(),
            EscalationLevel.Restructure,
            Arg.Any<SafetyTier>(),
            Arg.Any<DeviationResult>(),
            Arg.Any<LoggedWorkoutDetail>(),
            Arg.Any<CancellationToken>());

        // ...and the persisted state entered NeedsAdjustment with the date stamped.
        var expectedState = AdaptationSignalState.Create(
            PlanState.NeedsAdjustment, 2.0, 1, deviation.OccurredOn);
        harness.Session.Received(1).Store(Arg.Is<AdaptationSignalStateDocument>(d =>
            d.PlanId == PlanId && d.ToState() == expectedState));
    }

    [Fact]
    public async Task Handle_LlmEchoesADifferentTierThanTheGate_RejectsTheProposalWithNothingStaged()
    {
        // Arrange — GATE-BEFORE-INCREASE must key off the deterministic gate, never
        //   the LLM's echoed tier. An Amber gate with an echoed-Green proposal
        //   carrying a positive load delta PASSES the validator (Green permits an
        //   increase) but must be rejected: trusting the echo would commit a load
        //   increase under a tier the gate never assigned.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot());
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        harness.SafetyGate
            .Classify(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(SafetyClassification.Amber(ReferralCategory.Injury));
        harness.StubLlm(BuildRestructureOutput() with { NetLoadDelta = 5 });
        harness.Classifier
            .Classify(deviation, SafetyTier.Amber, Arg.Any<AdaptationSignalState>())
            .Returns(RestructureDecision());

        // Act
        var actual = await harness.HandleAsync(cmd);

        // Assert — the proposal is rejected, but the Amber referral staged before
        //   the LLM call persists with the error envelope (slice 3B F1): a safety
        //   turn is never dropped because the restructure failed. The plan event,
        //   state document, and marker stay un-staged so the retry re-evaluates.
        actual.Kind.Should().Be(AdaptationResponseKind.Error);
        actual.Retryable.Should().BeFalse();
        var signal = harness.Appended.Should().ContainSingle()
            .Which.Event.Should().BeOfType<SafetySignalRaised>().Subject;
        signal.TriggeringWorkoutLogId.Should().Be(cmd.WorkoutLogId);
        harness.Session.DidNotReceiveWithAnyArgs().Store(Arg.Any<AdaptationSignalStateDocument>());
        harness.Idempotency.DidNotReceiveWithAnyArgs()
            .Record(Arg.Any<Guid>(), Arg.Any<AdaptationResponseDto>());
        harness.Logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("echo mismatch"));
    }

    [Fact]
    public async Task Handle_RestructurePath_SanitizesViaTheAssemblerOnly_PassingTheRawDetail()
    {
        // Arrange — the handler sanitizes a SEPARATE copy for the safety gate and
        //   passes the RAW triggering log to the assembler, which owns the single
        //   sanitization pass for the prompt. Pre-sanitizing here too would
        //   double-wrap and triple-escape the note the LLM sees.
        var harness = new Harness();
        var log = BuildLog(BuildSnapshot(), notes: "raw note");
        var cmd = CommandFor(log);
        var deviation = harness.StubOnPlan(cmd, log);
        harness.StubPlan(BuildPlan());
        harness.StubLlm(BuildRestructureOutput());
        harness.Sanitizer.Transform = detail => detail with { Notes = "SANITIZED" };
        LoggedWorkoutDetail? assemblerInput = null;
        harness.Assembler
            .ComposeForAdaptationAsync(
                Arg.Any<PlanProjectionDto>(),
                Arg.Any<EscalationLevel>(),
                Arg.Any<SafetyTier>(),
                Arg.Any<DeviationResult>(),
                Arg.Do<LoggedWorkoutDetail>(d => assemblerInput = d),
                Arg.Any<CancellationToken>())
            .Returns(new AdaptationPromptComposition("system prompt", "user message"));
        harness.Classifier
            .Classify(deviation, SafetyTier.Green, Arg.Any<AdaptationSignalState>())
            .Returns(RestructureDecision());

        // Act
        await harness.HandleAsync(cmd);

        // Assert — the gate scanned the sanitized copy; the assembler received the
        //   RAW note and runs its own single sanitization pass.
        harness.SafetyGate.Received(1).Classify(
            "SANITIZED", Arg.Any<IReadOnlyDictionary<string, string>?>());
        assemblerInput.Should().NotBeNull();
        assemblerInput!.Notes.Should().Be("raw note");
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""{"rpe": 7""")]
    public void ToDisplayMetrics_MalformedStoredMetrics_ThrowsJsonException(string metricsJson)
    {
        // The API owns this column (DEC-072); a stored value that is not a JSON
        // object is data corruption and MUST fail loudly rather than silently
        // skipping the safety-gate scan of its free-text values.
        Action act = () => WorkoutMetricsProjection.ToDisplayMetrics(metricsJson);

        act.Should().Throw<JsonException>();
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

    /// <summary>
    /// Wraps a domain event in a substituted <see cref="IEvent"/> the way
    /// <c>FetchStreamAsync</c> returns committed stream entries — only
    /// <see cref="IEvent.Data"/> matters to the handler's per-log referral dedupe.
    /// </summary>
    private static IEvent StreamedSignal(SafetySignalRaised signal)
    {
        var evt = Substitute.For<IEvent>();
        evt.Data.Returns(signal);
        return evt;
    }

    private static void AssertNothingStaged(Harness harness)
    {
        // The L2 failure contract (DEC-073), for a non-Amber gate: stage NOTHING —
        // no event, no signal-state advance, no idempotency marker — so the
        // committing (or aborting) transaction leaves the stream and marker
        // untouched. The slice 3B F1 / DEC-081 exception (the step-5b Amber referral
        // commits on a terminal L2 failure) does not apply here: every remaining
        // caller gates on a Green tier, so the stream is genuinely empty.
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

    private static EscalationDecision RestructureDecision() =>
        new(
            EscalationLevel.Restructure,
            AdaptationKind.Restructure,
            AdaptationSignalState.Create(PlanState.NeedsAdjustment, 5.0, 0, new DateOnly(2026, 6, 8)));

    /// <summary>
    /// A valid Green restructure proposal: cuts week 2's target 30 -> 24, echoes
    /// week 3 unchanged (a no-op edit the diff must drop), and revises Tuesday
    /// (day 2) of the current week into an easy run.
    /// </summary>
    private static PlanAdaptationOutput BuildRestructureOutput(
        SafetyTier tier = SafetyTier.Green,
        ReferralCategory? referralCategory = null) =>
        new()
        {
            AdaptationKind = AdaptationKind.Restructure,
            SafetyTier = tier,
            NudgePatch = null,
            RestructurePlan = new RestructurePlan
            {
                RevisedWeeklyTargets =
                [
                    new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = 24 },
                    new WeeklyTargetEdit { WeekNumber = 3, WeeklyTargetKm = 35 },
                ],
                RevisedCurrentWeekWorkouts = [BuildWorkout(2, WorkoutType.Easy)],
                ForwardPath = "Hold the reduced volume this week, then ramp back ~10% per week.",
            },
            NetLoadDelta = -6,
            Rationale = "You have missed two key sessions, so I trimmed next week and eased Tuesday.",
            ReferralCategory = referralCategory,
        };

    /// <summary>
    /// The plan projection both L2 paths diff against: week 1 carries the live
    /// micro detail (the swappable week) and the meso tier has weeks 1-3 at
    /// 20/30/35 km.
    /// </summary>
    private static PlanProjectionDto BuildPlan() =>
        new()
        {
            PlanId = PlanId,
            MesoWeeks =
            [
                BuildMesoWeek(1, 20),
                BuildMesoWeek(2, 30),
                BuildMesoWeek(3, 35),
            ],
            MicroWorkoutsByWeek = new Dictionary<int, MicroWorkoutListOutput>
            {
                [1] = new() { Workouts = BuildSwappableWeek() },
            },
        };

    private static MesoWeekOutput BuildMesoWeek(int weekNumber, int weeklyTargetKm)
    {
        var rest = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Rest,
            WorkoutType = null,
            Notes = string.Empty,
        };
        return new MesoWeekOutput
        {
            WeekNumber = weekNumber,
            PhaseType = PhaseType.Base,
            WeeklyTargetKm = weeklyTargetKm,
            IsDeloadWeek = false,
            Sunday = rest,
            Monday = rest,
            Tuesday = rest,
            Wednesday = rest,
            Thursday = rest,
            Friday = rest,
            Saturday = rest,
            WeekSummary = string.Empty,
        };
    }

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
    /// shares: idempotency miss, pass-through sanitizer, Green gate, a stubbed
    /// adaptation prompt composition, and an append-capturing event store.
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
            Assembler
                .ComposeForAdaptationAsync(
                    Arg.Any<PlanProjectionDto>(),
                    Arg.Any<EscalationLevel>(),
                    Arg.Any<SafetyTier>(),
                    Arg.Any<DeviationResult>(),
                    Arg.Any<LoggedWorkoutDetail>(),
                    Arg.Any<CancellationToken>())
                .Returns(new AdaptationPromptComposition("system prompt", "user message"));
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

        public IContextAssembler Assembler { get; } = Substitute.For<IContextAssembler>();

        public ICoachingLlm Llm { get; } = Substitute.For<ICoachingLlm>();

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
            StubPlan(new PlanProjectionDto
            {
                PlanId = PlanId,
                MicroWorkoutsByWeek = new Dictionary<int, MicroWorkoutListOutput>
                {
                    [weekNumber] = new() { Workouts = week },
                },
            });

        public void StubPlan(PlanProjectionDto plan) =>
            Session
                .LoadAsync<PlanProjectionDto>(PlanId, Arg.Any<CancellationToken>())
                .Returns(plan);

        /// <summary>
        /// Stubs the committed plan stream the Amber referral dedupe fetches.
        /// Unstubbed, the substituted event store returns null, which the
        /// handler treats as an empty stream.
        /// </summary>
        public void StubStream(params IEvent[] events) =>
            Session.Events
                .FetchStreamAsync(
                    PlanId,
                    Arg.Any<long>(),
                    Arg.Any<DateTimeOffset?>(),
                    Arg.Any<long>(),
                    Arg.Any<CancellationToken>())
                .Returns(events);

        public void StubLlm(PlanAdaptationOutput output) =>
            Llm
                .GenerateStructuredAsync<PlanAdaptationOutput>(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                    Arg.Any<CacheControl?>(),
                    Arg.Any<CancellationToken>())
                .Returns((output, AnthropicUsage.Zero));

        public void StubLlmThrows(CoachingLlmException exception) =>
            Llm
                .GenerateStructuredAsync<PlanAdaptationOutput>(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                    Arg.Any<CacheControl?>(),
                    Arg.Any<CancellationToken>())
                .ThrowsAsync(exception);

        public Task<AdaptationResponseDto> HandleAsync(EvaluateAdaptationCommand cmd) =>
            EvaluateAdaptationHandler.Handle(
                cmd,
                Session,
                WorkoutLogs,
                DeviationEngine,
                Sanitizer,
                SafetyGate,
                Classifier,
                Assembler,
                Llm,
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
