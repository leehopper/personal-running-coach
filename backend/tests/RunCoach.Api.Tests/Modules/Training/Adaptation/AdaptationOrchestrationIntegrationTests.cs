using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using JasperFx;
using Marten;
using Marten.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding.Entities;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Modules.Training.Safety;
using RunCoach.Api.Modules.Training.Workouts;
using RunCoach.Api.Tests.Infrastructure;
using Wolverine;
using RestructurePlanOutput = RunCoach.Api.Modules.Coaching.Adaptation.RestructurePlan;

namespace RunCoach.Api.Tests.Modules.Training.Adaptation;

/// <summary>
/// Integration suite for the Slice 3 adaptation orchestration (PR5 / Unit 5), one
/// test per scenario in <c>adaptation-orchestration.feature</c>. Every scenario
/// drives the LIVE pipeline — HTTP create + auth + antiforgery, or
/// <c>IMessageBus.InvokeForTenantAsync</c> for the concurrency race — never
/// <c>EvaluateAdaptationHandler.Handle</c> directly, so Wolverine's transactional
/// middleware, the chain-scoped <c>EventStreamUnexpectedMaxEventIdException</c>
/// retry policy, and both inline Marten projections are all under test. The coaching LLM is the
/// deterministic <see cref="StubCoachingLlm"/> swapped in by
/// <see cref="RunCoachAppFactory"/>; restructure scenarios script its outcome and
/// assert the exact call count (zero real Anthropic calls in this tier).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class AdaptationOrchestrationIntegrationTests : DbBackedIntegrationTestBase
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const string AntiforgeryCookieName = AuthCookieNames.Antiforgery;
    private const string AntiforgeryRequestCookieName = AuthCookieNames.AntiforgeryRequest;
    private const string AntiforgeryHeaderName = AuthCookieNames.AntiforgeryHeader;
    private const string CreateLogPath = "/api/v1/workouts/logs";
    private const string TurnsPath = "/api/v1/conversation/turns";

    /// <summary>
    /// Events the seeded plan stream starts with:
    /// <c>[PlanGenerated, MesoCycleCreated x4, FirstMicroCycleCreated]</c>.
    /// "Plan stream is unchanged" assertions compare against this count.
    /// </summary>
    private const int SeededStreamLength = 6;

    /// <summary>
    /// The canned restructure trims week 2 from the canonical 45 km to this, so a
    /// committed restructure is observable on the plan read model.
    /// </summary>
    private const int RevisedWeek2TargetKm = 40;

    private const string CannedRationale =
        "Recent sessions ran well short of plan, so I trimmed next week's volume to consolidate.";

    // 2026-06-07 is a Sunday — the PlanCalendar week-1/day-0 anchor for the seeded
    // plan. All scenario logs land inside plan week 1 (the only week with micro
    // detail), on the days the custom micro below prescribes.
    private static readonly DateOnly Week1Sunday = new(2026, 6, 7);
    private static readonly DateOnly Week1Monday = new(2026, 6, 8);
    private static readonly DateOnly Week1Tuesday = new(2026, 6, 9);
    private static readonly DateOnly Week1Thursday = new(2026, 6, 11);
    private static readonly DateOnly Week1Friday = new(2026, 6, 12);

    private static readonly DateTimeOffset GeneratedAt = new(2026, 6, 7, 8, 0, 0, TimeSpan.Zero);
    private static readonly Uri BaseUri = new("https://localhost");

    public AdaptationOrchestrationIntegrationTests(RunCoachAppFactory factory)
        : base(factory)
    {
        // xUnit constructs one instance per test, so this clears any scripted
        // behavior or call count a previous test (or fixture sibling) left behind.
        StubCoachingLlm.Reset();
    }

    // ── Scenario 1: a committed log synchronously invokes the adaptation command ──
    [Fact]
    public async Task Create_CommittedLog_SynchronouslyInvokesEvaluation_AfterEfCommit()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);

        // Act
        var response = await PostLogAsync(client, token, OnTargetEasyRequest(), ct);

        // Assert — the 201 body already carries the handler's envelope, which is
        // only possible if the command ran synchronously inside the request.
        var raw = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        raw.Should().NotContainEquivalentOf(
            "vdot", because: "user-facing surfaces must use pace-zone terminology only");
        var body = await response.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);
        body.Should().NotBeNull();
        body!.Adaptation.Kind.Should().Be(
            AdaptationResponseKind.Adapted,
            because: "the create response surfaces the synchronous evaluation's envelope");

        // The EF write happened outside the Wolverine handler: the relational row
        // exists via the repository path, while the handler's own side-effect (the
        // WorkoutLogId-keyed marker) committed separately in the Marten transaction.
        var persisted = await GetLogByIdAsync(userId, body.WorkoutLogId, ct);
        persisted.Should().NotBeNull(because: "the EF create committed before the dispatch");
        persisted!.Prescription.Should().NotBeNull(because: "the run's date maps to the week-1/day-0 slot");
        var marker = await LoadDocumentAsync<IdempotencyMarker>(userId, body.WorkoutLogId, ct);
        marker.Should().NotBeNull(
            because: "a committed evaluation records its WorkoutLogId-keyed marker on the Marten side");
    }

    // ── Scenario 2: an off-plan log short-circuits cheaply ──
    [Fact]
    public async Task Create_OffPlanLog_NoLlm_NoEvent_NoTurn()
    {
        // Arrange — an active plan, but Monday has no prescribed workout, so the
        // resolved prescription snapshot is null and the log is off-plan.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);

        // Act
        var response = await PostLogAsync(
            client, token, LogRequest(Week1Monday, 5000.0, 1500.0), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);
        body!.Adaptation.Kind.Should().Be(AdaptationResponseKind.Adapted);
        body.Adaptation.AdaptationKind.Should().Be(
            AdaptationKind.Absorb,
            because: "an off-plan log is a no-op absorb");

        StubCoachingLlm.StructuredCallCount.Should().Be(0, because: "an off-plan log never reaches the LLM");
        var events = await FetchPlanEventsAsync(userId, planId, ct);
        events.Should().HaveCount(SeededStreamLength, because: "the off-plan no-op appends nothing");
        var turns = await LoadTurnsAsync(userId, planId, ct);
        turns.Should().BeEmpty(because: "no event means no projected conversation turn");
        var marker = await LoadDocumentAsync<IdempotencyMarker>(userId, body.WorkoutLogId, ct);
        marker.Should().NotBeNull(
            because: "the no-op response is still recorded so a replayed submission short-circuits");
    }

    // ── Scenario 3: an on-target log absorbs with no plan change ──
    [Fact]
    public async Task Create_OnTargetLog_Absorbs_StreamUnchanged()
    {
        // Arrange — actuals exactly match the week-1/day-0 easy prescription.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);

        // Act
        var response = await PostLogAsync(client, token, OnTargetEasyRequest(), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);
        body!.Adaptation.AdaptationKind.Should().Be(
            AdaptationKind.Absorb);

        StubCoachingLlm.StructuredCallCount.Should().Be(0, because: "a Green Level 0 absorb is deterministic");
        var events = await FetchPlanEventsAsync(userId, planId, ct);
        events.Should().HaveCount(SeededStreamLength, because: "an absorb appends no PlanAdaptedFromLog");
        var turns = await LoadTurnsAsync(userId, planId, ct);
        turns.Should().BeEmpty(because: "an absorb emits no ConversationTurn");

        // The signal-state document still advances on an absorb so accumulated
        // deviations can cross a threshold on a later log.
        var state = await LoadDocumentAsync<
            AdaptationSignalStateDocument>(userId, planId, ct);
        state.Should().NotBeNull(because: "the absorb path stores the advanced signal state");
        state!.PlanState.Should().Be(PlanState.OnTrack);
    }

    // ── Scenario 4: a reschedulable miss nudges deterministically without the LLM ──
    [Fact]
    public async Task Create_MissedKeyWorkout_NudgesDeterministically_WithoutLlm()
    {
        // Arrange — skip the Tuesday tempo; Thursday's easy day is a valid
        // non-stacking forward swap target.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);

        // Act
        var response = await PostLogAsync(client, token, SkippedTempoRequest(), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);
        body!.Adaptation.AdaptationKind.Should().Be(
            AdaptationKind.Nudge);
        StubCoachingLlm.StructuredCallCount.Should().Be(0, because: "an L1 micro-adjust is deterministic — no LLM call");

        var events = await FetchPlanEventsAsync(userId, planId, ct);
        var adaptation = events.OfType<PlanAdaptedFromLog>().Should().ContainSingle(
            because: "exactly one nudge event is appended").Subject;
        adaptation.AdaptationKind.Should().Be(AdaptationKind.Nudge);
        adaptation.EscalationLevel.Should().Be(EscalationLevel.MicroAdjust);
        adaptation.TriggeringWorkoutLogId.Should().Be(body.WorkoutLogId);

        // The plan projection mutates: the deterministic swap moved the tempo
        // forward to Thursday and slotted Thursday's easy run into Tuesday.
        var plan = await LoadDocumentAsync<PlanProjectionDto>(userId, planId, ct);
        var week1 = plan!.MicroWorkoutsByWeek[1].Workouts;
        week1.Single(w => w.DayOfWeek == 2).Title.Should().Be(
            "Thursday Easy Run", because: "the easy day swapped back into the missed tempo slot");
        week1.Single(w => w.DayOfWeek == 4).Title.Should().Be(
            "Tuesday Threshold Tempo", because: "the missed key workout moved forward to Thursday");

        var turns = await LoadTurnsAsync(userId, planId, ct);
        var turn = turns.Should().ContainSingle(because: "exactly one inline turn is emitted").Subject;
        turn.Role.Should().Be(ConversationRole.AssistantAdaptation);
        turn.AdaptationKind.Should().Be(AdaptationKind.Nudge);
    }

    // ── Scenario 5: a sustained deviation restructures via the LLM ──
    [Fact]
    public async Task Create_SustainedDeviation_RestructuresViaLlm_ExactlyOneCall()
    {
        // Arrange — prior signal state sits one under-performing log below the L2
        // enter threshold; this log crosses it.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);
        await SeedSignalStateAsync(userId, planId, rollingScore: 2.0, ct);
        StubCoachingLlm.UseStructuredBehavior(CannedRestructureOutput);

        // Act
        var response = await PostLogAsync(client, token, UnderPerformingTempoRequest(), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);
        body!.Adaptation.AdaptationKind.Should().Be(
            AdaptationKind.Restructure);
        StubCoachingLlm.StructuredCallCount.Should().Be(1, because: "the L2 path makes exactly one structured-output call");

        var events = await FetchPlanEventsAsync(userId, planId, ct);
        var adaptation = events.OfType<PlanAdaptedFromLog>().Should().ContainSingle().Subject;
        adaptation.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        adaptation.EscalationLevel.Should().Be(EscalationLevel.Restructure);

        // The projection re-renders with the validated proposal's revised target.
        var plan = await LoadDocumentAsync<PlanProjectionDto>(userId, planId, ct);
        plan!.MesoWeeks.Single(w => w.WeekNumber == 2).WeeklyTargetKm.Should().Be(
            RevisedWeek2TargetKm, because: "the committed restructure revises week 2's volume target");

        // Exactly one turn, read through the production endpoint end-to-end.
        using var turnsResponse = await client.GetAsync(TurnsPath, ct);
        var turnsRaw = await turnsResponse.Content.ReadAsStringAsync(ct);
        turnsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        turnsRaw.Should().NotContainEquivalentOf(
            "vdot", because: "conversation copy is user-facing surface");
        var turnsBody = await turnsResponse.Content.ReadFromJsonAsync<ConversationTurnsResponseDto>(
            cancellationToken: ct);
        var turn = turnsBody!.Turns.Should().ContainSingle(
            because: "exactly one ConversationTurn is emitted for the restructure").Subject;
        turn.Role.Should().Be(ConversationRole.AssistantAdaptation);
        turn.Content.Should().Be(CannedRationale, because: "the turn carries the validated proposal's rationale");
    }

    // ── Scenario 6: a Red crisis note short-circuits before any LLM or plan change ──
    [Fact]
    public async Task Create_RedCrisisNote_AppendsOnlySafetySignal_NoLlm_NoPlanChange()
    {
        // Arrange — an on-plan, on-target run whose note trips the crisis rules.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);

        // Act
        var response = await PostLogAsync(
            client,
            token,
            OnTargetEasyRequest(notes: "Felt empty out there. Honestly I have been feeling suicidal."),
            ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        StubCoachingLlm.StructuredCallCount.Should().Be(0, because: "Red short-circuits before any LLM call");

        var events = await FetchPlanEventsAsync(userId, planId, ct);
        events.Should().HaveCount(
            SeededStreamLength + 1, because: "the stream is otherwise unchanged — exactly one event was appended");
        events.OfType<PlanAdaptedFromLog>().Should().BeEmpty(because: "Red never produces a plan change");
        var signal = events.OfType<SafetySignalRaised>().Should().ContainSingle(
            because: "the handler appends ONLY SafetySignalRaised").Subject;
        signal.SafetyTier.Should().Be(SafetyTier.Red);
        signal.ReferralCategory.Should().Be(ReferralCategory.Crisis);
        signal.Content.Should().Be(
            CrisisResponseContent.CrisisResponse,
            because: "Red content routes by category — the crisis script, never LLM prose");

        var turns = await LoadTurnsAsync(userId, planId, ct);
        turns.Should().ContainSingle().Which.Role.Should().Be(ConversationRole.SystemSafety);
        var state = await LoadDocumentAsync<
            AdaptationSignalStateDocument>(userId, planId, ct);
        state.Should().BeNull(because: "the Red short-circuit never advances the signal state");
    }

    // ── Scenario 7: an Amber tier restructure also raises a safety signal ──
    [Fact]
    public async Task Create_AmberRestructure_AppendsAdaptationAndSafetySignal()
    {
        // Arrange — the same L2-crossing deviation as the Green restructure, with
        // an injury note the gate classifies Amber. The scripted proposal echoes the
        // Amber tier the gate assigned (echo integrity) and reduces load (NetLoadDelta
        // below zero) so GATE-BEFORE-INCREASE validation passes.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);
        await SeedSignalStateAsync(userId, planId, rollingScore: 2.0, ct);
        StubCoachingLlm.UseStructuredBehavior(() => CannedRestructureOutput() with { SafetyTier = SafetyTier.Amber });

        // Act
        var response = await PostLogAsync(
            client,
            token,
            UnderPerformingTempoRequest(notes: "Sharp pain in my right knee from about halfway."),
            ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        StubCoachingLlm.StructuredCallCount.Should().Be(1, because: "Amber L2 still makes exactly one LLM call");

        var events = await FetchPlanEventsAsync(userId, planId, ct);
        var adaptation = events.OfType<PlanAdaptedFromLog>().Should().ContainSingle().Subject;
        adaptation.SafetyTier.Should().Be(SafetyTier.Amber);

        // Validated non-increasing: the committed diff only ever reduces the
        // weekly target relative to its before value.
        adaptation.Diff.WeeklyTargetChanges.Should().OnlyContain(
            change => change.AfterWeeklyTargetKm <= change.BeforeWeeklyTargetKm,
            because: "GATE-BEFORE-INCREASE forbids a load increase under a non-Green tier");

        var signal = events.OfType<SafetySignalRaised>().Should().ContainSingle(
            because: "an Amber restructure also raises the scripted referral").Subject;
        signal.SafetyTier.Should().Be(SafetyTier.Amber);
        signal.ReferralCategory.Should().Be(ReferralCategory.Injury);
        signal.Content.Should().Be(AmberReferralContent.InjuryReferral);

        var turns = await LoadTurnsAsync(userId, planId, ct);
        turns.Should().HaveCount(2, because: "the adaptation turn and the safety turn are both projected");
    }

    // ── Scenario 8: the handler body emits events only ──
    [Fact]
    public async Task Evaluation_SideEffects_CommitInOneTenantedMartenTransaction()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);

        // Act — one nudge evaluation through the live pipeline.
        var response = await PostLogAsync(client, token, SkippedTempoRequest(), ct);
        var body = await response.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);

        // Assert — every handler side-effect (event append, signal-state document,
        // idempotency marker) is visible from a fresh tenant-scoped Marten session,
        // proving they all committed together in the handler's single transaction.
        var events = await FetchPlanEventsAsync(userId, planId, ct);
        events.OfType<PlanAdaptedFromLog>().Should().ContainSingle();
        var state = await LoadDocumentAsync<
            AdaptationSignalStateDocument>(userId, planId, ct);
        state.Should().NotBeNull(because: "the signal state commits with the events it justified");
        var marker = await LoadDocumentAsync<IdempotencyMarker>(userId, body!.WorkoutLogId, ct);
        marker.Should().NotBeNull(because: "the marker commits atomically with the appends it memoizes");

        // And no relational write happened inside the handler: the only WorkoutLog
        // row is the one the controller's service created before the dispatch.
        var logs = await GetLogsByUserAsync(userId, ct);
        logs.Should().ContainSingle(
            because: "the handler is events-only — no DbContext writes, no repository CreateAsync");
    }

    // ── Scenario 9: a double-submit of the same log produces exactly one adaptation ──
    [Fact]
    public async Task Create_DoubleSubmit_SameLog_ExactlyOneEventAndTurn_ByteIdenticalReplay()
    {
        // Arrange — replaying the create idempotency key replays the same
        // WorkoutLogId, and the controller re-dispatches unconditionally.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);
        var request = SkippedTempoRequest(key: Guid.NewGuid());

        // Act
        var first = await PostLogAsync(client, token, request, ct);
        var firstRaw = await first.Content.ReadAsStringAsync(ct);
        var second = await PostLogAsync(client, token, request, ct);
        var secondRaw = await second.Content.ReadAsStringAsync(ct);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        secondRaw.Should().Be(
            firstRaw,
            because: "the marker short-circuit replays the byte-identical prior response");

        var events = await FetchPlanEventsAsync(userId, planId, ct);
        events.OfType<PlanAdaptedFromLog>().Should().ContainSingle(
            because: "the second invocation was short-circuited by the WorkoutLogId-keyed marker");
        var turns = await LoadTurnsAsync(userId, planId, ct);
        turns.Should().ContainSingle(because: "exactly one turn exists for the single committed event");
    }

    // ── Scenario 10: a failed run releases its idempotency marker for re-evaluation ──
    [Fact]
    public async Task Create_FailedEvaluation_ReleasesMarker_RetryReEvaluatesFromFreshState()
    {
        // Arrange — the first evaluation fails terminally at the LLM seam, which
        // commits an EMPTY transaction (nothing staged), releasing the marker.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);
        await SeedSignalStateAsync(userId, planId, rollingScore: 2.0, ct);
        StubCoachingLlm.UseStructuredBehavior(() => throw new TransientCoachingLlmException(
            "The coaching service is busy right now.", retryAfterSeconds: 30, innerException: null));
        var request = UnderPerformingTempoRequest(key: Guid.NewGuid());

        // Act 1 — the failing evaluation.
        var failed = await PostLogAsync(client, token, request, ct);
        var failedBody = await failed.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);

        // Assert 1 — error envelope, nothing staged, marker released.
        failed.StatusCode.Should().Be(HttpStatusCode.Created);
        failedBody!.Adaptation.Kind.Should().Be(AdaptationResponseKind.Error);
        failedBody.Adaptation.Retryable.Should().BeTrue();
        failedBody.Adaptation.RetryAfterSeconds.Should().Be(30);
        (await LoadDocumentAsync<IdempotencyMarker>(userId, failedBody.WorkoutLogId, ct))
            .Should().BeNull(because: "a failed evaluation must not record a marker that poisons the retry");
        (await FetchPlanEventsAsync(userId, planId, ct))
            .Should().HaveCount(SeededStreamLength, because: "the failed run staged nothing");

        // Act 2 — retry the same create key: the replayed log id re-dispatches and
        // re-evaluates from fresh state, this time succeeding.
        StubCoachingLlm.UseStructuredBehavior(CannedRestructureOutput);
        var retried = await PostLogAsync(client, token, request, ct);
        var retriedBody = await retried.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);

        // Assert 2 — the retried invocation re-evaluated (a second LLM call) and committed.
        retriedBody!.WorkoutLogId.Should().Be(failedBody.WorkoutLogId, because: "DEC-077 replays the same log id");
        retriedBody.Adaptation.AdaptationKind.Should().Be(
            AdaptationKind.Restructure);
        StubCoachingLlm.StructuredCallCount.Should().Be(
            2, because: "the released marker let the retried invocation re-run the full evaluation");
        (await FetchPlanEventsAsync(userId, planId, ct))
            .OfType<PlanAdaptedFromLog>().Should().ContainSingle();
        (await LoadDocumentAsync<IdempotencyMarker>(userId, retriedBody.WorkoutLogId, ct))
            .Should().NotBeNull(because: "the successful retry recorded its marker");
    }

    // ── Scenario 11: concurrent handlers on the same plan resolve via optimistic concurrency ──
    [Fact]
    public async Task ConcurrentEvaluations_SameLogSamePlan_ExactlyOneAdaptationWins()
    {
        // Arrange — commit the threshold-crossing log first WITHOUT an adaptation
        // (scripted terminal failure stages nothing and releases the marker), so
        // two bus invocations can then race the same evaluation.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);
        await SeedSignalStateAsync(userId, planId, rollingScore: 2.0, ct);
        StubCoachingLlm.UseStructuredBehavior(() => throw new PermanentCoachingLlmException(
            "seed-only failure so the log commits with no adaptation", innerException: null));
        var seeded = await PostLogAsync(client, token, UnderPerformingTempoRequest(), ct);
        seeded.StatusCode.Should().Be(HttpStatusCode.Created);
        var workoutLogId = (await seeded.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct))!.WorkoutLogId;
        StubCoachingLlm.Reset();

        // Deterministic start barrier INSIDE the LLM seam: neither handler can
        // proceed to its append + commit until BOTH have passed the idempotency
        // check and reached the L2 call, so the appends are guaranteed to race the
        // same stream version — no sleep, no lucky scheduling. (The flaky
        // OnboardingTurnConcurrency shape — N unsynchronized direct Handle calls —
        // is deliberately not copied.)
        using var rendezvous = new Barrier(2);
        StubCoachingLlm.UseStructuredBehavior(() =>
            rendezvous.SignalAndWait(TimeSpan.FromSeconds(15))
                ? CannedRestructureOutput()
                : throw new InvalidOperationException(
                    "Both concurrent evaluations must reach the LLM seam; the rendezvous timed out."));

        // Act — two live-bus invocations of the SAME EvaluateAdaptationCommand.
        var outcomes = await Task.WhenAll(
            Task.Run(() => InvokeEvaluationAsync(userId, workoutLogId, ct), ct),
            Task.Run(() => InvokeEvaluationAsync(userId, workoutLogId, ct), ct));

        // Assert — the barrier proves both raced past the marker check to the LLM.
        StubCoachingLlm.StructuredCallCount.Should().Be(
            2, because: "both handlers must have reached the LLM seam before either committed");

        // The bounded retry — keyed on the exception the race actually throws
        // (EventStreamUnexpectedMaxEventIdException, the d0bfd4a fix) — is the
        // mechanism that turns the loser's lost append into a graceful replay: the
        // winner commits its marker BEFORE the loser retries, so the loser's retry
        // hits SeenAsync and returns the replayed envelope rather than escaping as a
        // conflict. Were the rule re-keyed to the wrong exception, the retry would
        // never fire and the loser would throw — so assert BOTH invocations resolved
        // to the winner's committed restructure envelope with NO conflict escaping,
        // not merely that "at least one" returned.
        outcomes.Should().OnlyContain(
            o => o.Conflict == null,
            because: "the bounded retry deterministically resolves the loser to the replayed envelope");
        var envelopes = outcomes.Select(o => o.Envelope!).ToList();
        envelopes.Should().HaveCount(2);
        envelopes.Should().AllSatisfy(envelope =>
        {
            envelope.Kind.Should().Be(AdaptationResponseKind.Adapted);
            envelope.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        });

        var events = await FetchPlanEventsAsync(userId, planId, ct);
        events.OfType<PlanAdaptedFromLog>().Should().ContainSingle(
            because: "exactly one adaptation may commit for one threshold-crossing log");
        var turns = await LoadTurnsAsync(userId, planId, ct);
        turns.Should().ContainSingle(because: "the single committed event projects a single turn");
        var marker = await LoadDocumentAsync<IdempotencyMarker>(userId, workoutLogId, ct);
        marker.Should().NotBeNull(because: "the winner's marker committed with its append");
    }

    // ── Scenario 12: rapid-fire dead-zone logs do not each fire an adaptation ──
    [Fact]
    public async Task Create_RapidFireDeadZoneLogs_AtMostOneAdaptationPerThresholdCrossing()
    {
        // Arrange — first log crosses the L2 threshold and restructures; the next
        // two under-performing logs land inside the cooldown dead-zone.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);
        await SeedSignalStateAsync(userId, planId, rollingScore: 2.0, ct);
        StubCoachingLlm.UseStructuredBehavior(CannedRestructureOutput);

        // Act — the crossing log, then two rapid-fire in-zone under-performers.
        var crossing = await PostLogAsync(client, token, UnderPerformingTempoRequest(), ct);
        var thursday = await PostLogAsync(
            client, token, LogRequest(Week1Thursday, 3000.0, 1170.0), ct);
        var friday = await PostLogAsync(
            client, token, LogRequest(Week1Friday, 2500.0, 975.0), ct);

        // Assert
        crossing.StatusCode.Should().Be(HttpStatusCode.Created);
        thursday.StatusCode.Should().Be(HttpStatusCode.Created);
        friday.StatusCode.Should().Be(HttpStatusCode.Created);

        var thursdayBody = await thursday.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);
        var fridayBody = await friday.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);
        thursdayBody!.Adaptation.AdaptationKind.Should().Be(
            AdaptationKind.Absorb,
            because: "an in-zone log inside the cooldown absorbs without re-firing");
        fridayBody!.Adaptation.AdaptationKind.Should().Be(
            AdaptationKind.Absorb);

        StubCoachingLlm.StructuredCallCount.Should().Be(
            1, because: "only the threshold-crossing log reaches the LLM");
        var events = await FetchPlanEventsAsync(userId, planId, ct);
        events.OfType<PlanAdaptedFromLog>().Should().ContainSingle(
            because: "at most one PlanAdaptedFromLog is appended per threshold crossing");
        var turns = await LoadTurnsAsync(userId, planId, ct);
        turns.Should().ContainSingle(because: "the in-zone logs do not each produce an event");
    }

    // ── Scenario 13: a terminal LLM failure returns the error envelope and leaves the plan unchanged ──
    [Fact]
    public async Task Create_TerminalLlmFailure_ErrorEnvelope_PlanUnchanged_MarkerReleased()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);
        await SeedSignalStateAsync(userId, planId, rollingScore: 2.0, ct);
        const string terminalMessage = "The coaching request could not be completed.";
        StubCoachingLlm.UseStructuredBehavior(
            () => throw new PermanentCoachingLlmException(terminalMessage, innerException: null));
        var request = UnderPerformingTempoRequest(key: Guid.NewGuid());

        // Act
        var response = await PostLogAsync(client, token, request, ct);
        var body = await response.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);

        // Assert — the synchronous response carries the error envelope (a 201: the
        // log row had already committed), and nothing was staged.
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body!.Adaptation.Kind.Should().Be(AdaptationResponseKind.Error);
        body.Adaptation.ErrorMessage.Should().Be(terminalMessage);
        body.Adaptation.Retryable.Should().BeFalse(
            because: "a permanent failure advertises no auto-retry to the frontend");

        var persisted = await GetLogByIdAsync(userId, body.WorkoutLogId, ct);
        persisted.Should().NotBeNull(because: "the workout log itself is saved regardless of the LLM failure");
        (await FetchPlanEventsAsync(userId, planId, ct))
            .Should().HaveCount(SeededStreamLength, because: "the plan stream is unchanged");
        (await LoadTurnsAsync(userId, planId, ct)).Should().BeEmpty();
        (await LoadDocumentAsync<IdempotencyMarker>(userId, body.WorkoutLogId, ct))
            .Should().BeNull(because: "the marker is released for re-evaluation");

        // A follow-up submission of the same key re-runs the evaluation in full —
        // the second LLM call proves the marker did not stick.
        var followUp = await PostLogAsync(client, token, request, ct);
        followUp.StatusCode.Should().Be(HttpStatusCode.Created);
        StubCoachingLlm.StructuredCallCount.Should().Be(
            2, because: "a released marker means the follow-up invocation re-evaluates");
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        // Never leak a scripted LLM behavior or call count into a sibling fixture.
        StubCoachingLlm.Reset();

        // Plan streams, projections, signal-state documents, and idempotency
        // markers live in Marten's runcoach_events schema, which Respawn skips —
        // reset them explicitly. Base type already calls GC.SuppressFinalize.
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
    }

    // ── request builders ──
    private static CreateWorkoutLogRequestDto LogRequest(
        DateOnly occurredOn,
        double distanceMeters,
        double durationSeconds,
        CompletionStatus status = CompletionStatus.Complete,
        string? notes = null,
        Guid? key = null) =>
        new(
            IdempotencyKey: key ?? Guid.NewGuid(),
            OccurredOn: occurredOn,
            DistanceMeters: distanceMeters,
            DurationSeconds: durationSeconds,
            CompletionStatus: status,
            Notes: notes,
            Metrics: null,
            Splits: null);

    /// <summary>8 km in 48 min on the Sunday easy day: pace 360 s/km inside the
    /// [330, 390] band, distance on target — classifies Green Level 0.</summary>
    private static CreateWorkoutLogRequestDto OnTargetEasyRequest(string? notes = null) =>
        LogRequest(Week1Sunday, 8000.0, 2880.0, notes: notes);

    /// <summary>A skipped Tuesday tempo — a missed key workout with Thursday's easy
    /// day as a valid non-stacking forward swap, classifying Green Level 1.</summary>
    private static CreateWorkoutLogRequestDto SkippedTempoRequest(Guid? key = null) =>
        LogRequest(Week1Tuesday, 0.0, 0.0, CompletionStatus.Skipped, key: key);

    /// <summary>5 km in 25 min against the 10 km Tuesday tempo: pace 300 s/km inside
    /// the band but distance 50 percent short — an under-performing log that steps the
    /// rolling score from the seeded 2.0 across the L2 enter threshold.</summary>
    private static CreateWorkoutLogRequestDto UnderPerformingTempoRequest(
        string? notes = null, Guid? key = null) =>
        LogRequest(Week1Tuesday, 5000.0, 1500.0, notes: notes, key: key);

    /// <summary>
    /// The scripted, validator-passing restructure proposal: a pure load reduction
    /// (week 2 from 45 km to 40 km, negative net delta, forward path present) so it
    /// passes GATE-BEFORE-INCREASE and produces a non-empty deterministic diff against
    /// the seeded plan. It echoes the Green tier; a non-Green-gate scenario must
    /// override <c>SafetyTier</c> to the gate's tier (the handler rejects an echoed
    /// tier that diverges from the deterministic gate), e.g.
    /// <c>CannedRestructureOutput() with { SafetyTier = SafetyTier.Amber }</c>.
    /// </summary>
    private static PlanAdaptationOutput CannedRestructureOutput() =>
        new()
        {
            AdaptationKind = AdaptationKind.Restructure,
            SafetyTier = SafetyTier.Green,
            NudgePatch = null,
            RestructurePlan = new RestructurePlanOutput
            {
                RevisedWeeklyTargets =
                [
                    new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = RevisedWeek2TargetKm },
                ],
                RevisedCurrentWeekWorkouts = [],
                ForwardPath = "Hold the reduced volume for a week, then ramp back about ten percent per week.",
            },
            NetLoadDelta = -5,
            Rationale = CannedRationale,
            ReferralCategory = null,
        };

    /// <summary>
    /// The custom week-1 micro the scenarios prescribe against: an on-target-able
    /// Sunday easy run, a key Tuesday tempo (the miss/under-perform target), and
    /// two later easy days — Thursday is the planner's forward-swap target, Friday
    /// feeds the rapid-fire dead-zone scenario.
    /// </summary>
    private static MicroWorkoutListOutput BuildAdaptationMicro() =>
        new()
        {
            Workouts =
            [
                BuildWorkout(0, WorkoutType.Easy, "Sunday Easy Run", km: 8, minutes: 48, fast: 330, easy: 390, effort: 3),
                BuildWorkout(2, WorkoutType.Tempo, "Tuesday Threshold Tempo", km: 10, minutes: 50, fast: 280, easy: 330, effort: 7),
                BuildWorkout(4, WorkoutType.Easy, "Thursday Easy Run", km: 6, minutes: 39, fast: 330, easy: 420, effort: 3),
                BuildWorkout(5, WorkoutType.Easy, "Friday Easy Run", km: 5, minutes: 33, fast: 330, easy: 420, effort: 3),
            ],
        };

    private static WorkoutOutput BuildWorkout(
        int dayOfWeek, WorkoutType type, string title, int km, int minutes, int fast, int easy, int effort) =>
        new()
        {
            DayOfWeek = dayOfWeek,
            WorkoutType = type,
            Title = title,
            TargetDistanceKm = km,
            TargetDurationMinutes = minutes,
            TargetPaceEasySecPerKm = easy,
            TargetPaceFastSecPerKm = fast,
            Segments = [],
            WarmupNotes = "10 min easy.",
            CooldownNotes = "10 min easy.",
            CoachingNotes = "Run to feel.",
            PerceivedEffort = effort,
        };

    private static async Task<HttpResponseMessage> PostLogAsync(
        HttpClient client, string antiforgeryToken, CreateWorkoutLogRequestDto dto, CancellationToken ct)
    {
        using var request = BuildRequest(HttpMethod.Post, CreateLogPath, antiforgeryToken);
        request.Content = JsonContent.Create(dto);
        return await client.SendAsync(request, ct);
    }

    private static (HttpClient Client, CookieContainer Container) CreateCookieClient(RunCoachAppFactory factory)
    {
        var container = new CookieContainer();
        var client = factory.CreateDefaultClient(new CookieContainerHandler(container));
        client.BaseAddress = BaseUri;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return (client, container);
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string path, string antiforgeryToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(AntiforgeryHeaderName, antiforgeryToken);
        return request;
    }

    private static async Task<string> PrimeAntiforgeryAsync(HttpClient client, CookieContainer container)
    {
        using var response = await client.GetAsync("/api/v1/auth/xsrf", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var requestCookie = GetCookie(container, AntiforgeryRequestCookieName);
        requestCookie.Should().NotBeNull("/xsrf must issue the SPA-readable request token cookie");
        GetCookie(container, AntiforgeryCookieName).Should().NotBeNull(
            "the framework antiforgery cookie must also be set");
        return requestCookie!.Value;
    }

    private static async Task<Guid> RegisterAsync(
        HttpClient client, CookieContainer container, string email, string password)
    {
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/register", token);
        request.Content = JsonContent.Create(new RegisterRequestDto(email, password));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(
            HttpStatusCode.Created, because: $"register helper must succeed — got {(int)response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        return body!.UserId;
    }

    private static async Task LoginAsync(
        HttpClient client, CookieContainer container, string email, string password)
    {
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/login", token);
        request.Content = JsonContent.Create(new LoginRequestDto(email, password));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(
            HttpStatusCode.OK, because: $"login helper must succeed — got {(int)response.StatusCode}");
    }

    private static Cookie? GetCookie(CookieContainer container, string name)
    {
        foreach (Cookie c in container.GetCookies(BaseUri))
        {
            if (string.Equals(c.Name, name, StringComparison.Ordinal))
            {
                return c;
            }
        }

        return null;
    }

    private static string GenerateEmail() => $"adaptation-{Guid.NewGuid():N}@example.test";

    // ── seeding + state helpers ──
    private async Task<(HttpClient Client, Guid UserId, string Token)> RegisterLoginAndPrimeAsync()
    {
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);
        var token = await PrimeAntiforgeryAsync(client, container);
        return (client, userId, token);
    }

    /// <summary>
    /// Seeds the active plan the production way: the EF profile row carrying
    /// <c>CurrentPlanId</c>, plus a tenant-scoped Marten <c>StartStream</c> of the
    /// canonical plan event sequence (with this suite's custom week-1 micro), so
    /// both inline projections materialize and the adaptation handler appends to a
    /// REAL existing stream.
    /// </summary>
    private async Task SeedActivePlanAsync(Guid userId, Guid planId, CancellationToken ct)
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.RunnerOnboardingProfiles.Add(new RunnerOnboardingProfile
            {
                UserId = userId,
                TenantId = userId.ToString(),
                OnboardingCompletedAt = now,
                CurrentPlanId = planId,
                CreatedOn = now,
                ModifiedOn = now,
            });
            await db.SaveChangesAsync(ct);
        }

        // GeneratedAt falls on the 2026-06-07 Sunday, so PlanGenerated's
        // PlanStartDate anchors week 1 / day 0 exactly there.
        var sequence = StubPlanGenerationService.BuildCanonicalSequence(
                planId, userId, goal: "Adaptation suite plan", GeneratedAt, previousPlanId: null)
            with
        { Micro = new FirstMicroCycleCreated(BuildAdaptationMicro()) };

        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Events.StartStream<PlanProjectionDto>(planId, [.. sequence.ToEvents()]);
        await session.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds the per-plan signal state one under-performing log shy of the L2 enter
    /// threshold, so a single under-performing log crosses it — the "sustained
    /// deviation" Given without submitting a whole history of logs.
    /// </summary>
    private async Task SeedSignalStateAsync(
        Guid userId, Guid planId, double rollingScore, CancellationToken ct)
    {
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Store(new AdaptationSignalStateDocument(
            planId,
            PlanState.MinorDeviation,
            rollingScore,
            ConsecutiveMissedDays: 0,
            LastAdaptationOn: null));
        await session.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Invokes <c>EvaluateAdaptationCommand</c> through the live Wolverine bus for
    /// the user's tenant — exactly the controller's dispatch — capturing either the
    /// envelope or a recognized optimistic-concurrency conflict. Any OTHER failure
    /// propagates and fails the test; conflicts are never retried by the test.
    /// </summary>
    private async Task<(AdaptationResponseDto? Envelope, Exception? Conflict)> InvokeEvaluationAsync(
        Guid userId, Guid workoutLogId, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        try
        {
            var envelope = await bus.InvokeForTenantAsync<AdaptationResponseDto>(
                userId.ToString(),
                new EvaluateAdaptationCommand(workoutLogId, userId),
                ct);
            return (envelope, null);
        }
        catch (Exception ex) when (
            ex is ConcurrencyException or ConcurrentUpdateException or DocumentAlreadyExistsException)
        {
            // The losing append's legal surfaces, all of which roll the loser back
            // in full without a second adaptation:
            // - `EventStreamUnexpectedMaxEventIdException` (a `JasperFx.ConcurrencyException`):
            //   Marten Rich append mode detected the stream version moved — this is
            //   what the race actually throws. The chain-scoped policy retries it
            //   (bounded), so the loser normally re-runs, hits the winner's
            //   idempotency marker, and returns the replayed envelope instead of
            //   throwing; this catch only sees it if the conflict escapes the
            //   bounded retries.
            // - `ConcurrentUpdateException`: kept defensively for Marten's
            //   non-stream document-update conflict surface (e.g. the signal-state
            //   document), which is outside the stream-append exception hierarchy.
            // - `DocumentAlreadyExistsException`: the duplicate WorkoutLogId-keyed
            //   marker insert, when the marker conflict surfaces before the
            //   stream-version conflict.
            return (null, ex);
        }
    }

    // ── read helpers (all tenant-scoped) ──
    private async Task<IReadOnlyList<object>> FetchPlanEventsAsync(
        Guid userId, Guid planId, CancellationToken ct)
    {
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        var events = await session.Events.FetchStreamAsync(planId, token: ct);
        return [.. events.Select(e => e.Data)];
    }

    private async Task<IReadOnlyList<ConversationTurnView>> LoadTurnsAsync(
        Guid userId, Guid planId, CancellationToken ct)
    {
        var view = await LoadDocumentAsync<ConversationLogView>(userId, planId, ct);
        return view?.Turns ?? [];
    }

    private async Task<T?> LoadDocumentAsync<T>(Guid userId, Guid id, CancellationToken ct)
        where T : class
    {
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        return await session.LoadAsync<T>(id, ct);
    }

    private async Task<WorkoutLog?> GetLogByIdAsync(Guid userId, Guid id, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWorkoutLogRepository>();
        return await repo.GetByIdAsync(userId, id, ct);
    }

    private async Task<IReadOnlyList<WorkoutLog>> GetLogsByUserAsync(Guid userId, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWorkoutLogRepository>();
        return await repo.GetByUserAsync(userId, cursor: null, limit: 100, ct);
    }
}
