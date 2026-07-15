using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Workouts;
using Wolverine;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Unit tests for <see cref="ConfirmConversationalLogService"/> — the Slice 4B PR5
/// confirm-then-commit orchestration: it maps the confirmed draft onto the unchanged Slice 2b
/// create path (with a derived EF idempotency key), runs the identical post-create adaptation
/// seam, and then persists a single coach acknowledgment turn — an LLM ack on an adapted
/// outcome, a scripted ack on a terminal review failure — strictly AFTER the adaptation has
/// committed its proactive turns. The full end-to-end paths live in the integration suite.
/// </summary>
public sealed class ConfirmConversationalLogServiceTests
{
    private static readonly Guid UserId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ClientMessageId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid WorkoutLogId = new("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task ConfirmAsync_CommitsTheDraft_ThenEvaluatesAdaptation_ThenPersistsTheAck()
    {
        // Arrange
        var (sut, deps) = CreateSut();

        // Act
        await sut.ConfirmAsync(UserId, Request(), TestContext.Current.CancellationToken);

        // Assert — the ordering contract: the log commits, the adaptation runs against the
        // committed log, and only then does the ack persist (so it never preempts a referral).
        Received.InOrder(() =>
        {
            deps.WorkoutLogService.CreateAsync(UserId, Arg.Any<CreateWorkoutLogRequestDto>(), Arg.Any<CancellationToken>());
            deps.Dispatcher.EvaluateAsync(WorkoutLogId, UserId, Arg.Any<CancellationToken>());
            deps.Bus.InvokeForTenantAsync<ConversationTurnPostedResponse>(
                UserId.ToString(), Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>());
        });
    }

    [Fact]
    public async Task ConfirmAsync_DerivesEfIdempotencyKeyFromClientMessageId_AndConvertsUnits()
    {
        // Arrange
        var (sut, deps) = CreateSut();
        var expectedKey = ConversationTurnId.DeriveWorkoutLogIdempotencyKey(ClientMessageId);

        // Act — 5 km / 25 min draft.
        await sut.ConfirmAsync(UserId, Request(), TestContext.Current.CancellationToken);

        // Assert — the create request carries the DERIVED EF key (not the clientMessageId) and the
        // server-side SI conversion (5 km -> 5000 m, 25 min -> 1500 s).
        await deps.WorkoutLogService.Received(1).CreateAsync(
            UserId,
            Arg.Is<CreateWorkoutLogRequestDto>(r =>
                r.IdempotencyKey == expectedKey
                && Math.Abs(r.DistanceMeters - 5000) < 0.0001
                && Math.Abs(r.DurationSeconds - 1500) < 0.0001),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmAsync_AdaptedOutcome_GeneratesAndPersistsTheLlmAck()
    {
        // Arrange
        var (sut, deps) = CreateSut(AdaptationResponseDto.Adapted(AdaptationKind.Nudge));
        deps.ContextAssembler
            .ComposeForAckAsync(Arg.Any<StructuredLogDraft>(), AdaptationKind.Nudge, Arg.Any<CancellationToken>())
            .Returns(new AckPromptComposition("sys", "usr", []));
        deps.Llm.GenerateAsync("sys", "usr", Arg.Any<CancellationToken>())
            .Returns("Logged your 5K. Eased the next day or two — check your plan.");

        // Act
        await sut.ConfirmAsync(UserId, Request(), TestContext.Current.CancellationToken);

        // Assert — the LLM-authored ack persists as a non-errored coach turn keyed to the client id
        // (record equality matches the full command).
        await deps.Bus.Received(1).InvokeForTenantAsync<ConversationTurnPostedResponse>(
            UserId.ToString(),
            new PostCoachConversationTurn(
                UserId,
                ClientMessageId,
                "Logged your 5K. Eased the next day or two — check your plan.",
                false,
                LoggedRun: new LoggedRunSummary(WorkoutLogId, 5.0, 1500d, new DateOnly(2026, 6, 24), CompletionStatus.Complete)),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task ConfirmAsync_ErrorOutcome_PersistsScriptedAck_WithoutCallingTheLlm()
    {
        // Arrange — a terminal review failure (or lost race) rode back as Kind=Error. The review
        // already failed, so the ack must NOT call the LLM again; it surfaces a scripted message.
        var (sut, deps) = CreateSut(new AdaptationResponseDto
        {
            Kind = AdaptationResponseKind.Error,
            ErrorMessage = "The coach is briefly unavailable.",
            Retryable = true,
        });

        // Act
        await sut.ConfirmAsync(UserId, Request(), TestContext.Current.CancellationToken);

        // Assert
        await deps.ContextAssembler.DidNotReceive().ComposeForAckAsync(
            Arg.Any<StructuredLogDraft>(), Arg.Any<AdaptationKind>(), Arg.Any<CancellationToken>());
        await deps.Llm.DidNotReceive().GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await deps.Bus.Received(1).InvokeForTenantAsync<ConversationTurnPostedResponse>(
            UserId.ToString(),
            new PostCoachConversationTurn(
                UserId,
                ClientMessageId,
                ConversationAckScripts.SavedReviewRetrying,
                false,
                LoggedRun: new LoggedRunSummary(WorkoutLogId, 5.0, 1500d, new DateOnly(2026, 6, 24), CompletionStatus.Complete)),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task ConfirmAsync_AckLlmFailure_FallsBackToScriptedAck_AndStillReturns()
    {
        // Arrange — the log is committed and the plan already adapted; only the ack generation
        // fails. The confirm must not throw — it persists a scripted ack and returns the envelope.
        var (sut, deps) = CreateSut(AdaptationResponseDto.Adapted(AdaptationKind.Restructure));
        deps.ContextAssembler
            .ComposeForAckAsync(Arg.Any<StructuredLogDraft>(), Arg.Any<AdaptationKind>(), Arg.Any<CancellationToken>())
            .Returns(new AckPromptComposition("sys", "usr", []));
        deps.Llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new PermanentCoachingLlmException("ack generation failed", null));

        // Act
        var response = await sut.ConfirmAsync(UserId, Request(), TestContext.Current.CancellationToken);

        // Assert
        response.Adaptation.Kind.Should().Be(AdaptationResponseKind.Adapted);
        await deps.Bus.Received(1).InvokeForTenantAsync<ConversationTurnPostedResponse>(
            UserId.ToString(),
            new PostCoachConversationTurn(
                UserId,
                ClientMessageId,
                ConversationAckScripts.AckUnavailable,
                false,
                LoggedRun: new LoggedRunSummary(WorkoutLogId, 5.0, 1500d, new DateOnly(2026, 6, 24), CompletionStatus.Complete)),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task ConfirmAsync_AckPersistenceFails_NeverThrows_AndReturnsTheCommittedEnvelope()
    {
        // Arrange — the log committed and the adaptation already ran, but persisting the ack turn
        // fails (a transient Marten/Npgsql append error). The contract is that the committed log
        // always wins, so the confirm must NOT surface a 500 — it returns the envelope.
        var expected = AdaptationResponseDto.Adapted(AdaptationKind.Absorb);
        var (sut, deps) = CreateSut(expected);
        deps.Bus.InvokeForTenantAsync<ConversationTurnPostedResponse>(
                Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns<Task<ConversationTurnPostedResponse>>(_ => throw new InvalidOperationException("transient append failure"));

        // Act
        var response = await sut.ConfirmAsync(UserId, Request(), TestContext.Current.CancellationToken);

        // Assert — no throw; the committed log id + adaptation envelope still come back.
        response.WorkoutLogId.Should().Be(WorkoutLogId);
        response.Adaptation.Should().Be(expected);
    }

    [Fact]
    public async Task ConfirmAsync_ClientAbortDuringAck_Propagates_NotSwallowed()
    {
        // Arrange — a genuine client abort during ack persistence must surface (the request is
        // gone), distinct from a transient infrastructure failure which is swallowed.
        var (sut, deps) = CreateSut(AdaptationResponseDto.Adapted(AdaptationKind.Absorb));
        deps.Bus.InvokeForTenantAsync<ConversationTurnPostedResponse>(
                Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns<Task<ConversationTurnPostedResponse>>(_ => throw new OperationCanceledException());

        // Act
        var act = async () => await sut.ConfirmAsync(UserId, Request(), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ConfirmAsync_ReturnsTheWorkoutLogIdAndAdaptationEnvelope()
    {
        // Arrange
        var expectedEnvelope = AdaptationResponseDto.Adapted(AdaptationKind.Absorb);
        var (sut, _) = CreateSut(expectedEnvelope);

        // Act
        var response = await sut.ConfirmAsync(UserId, Request(), TestContext.Current.CancellationToken);

        // Assert
        response.WorkoutLogId.Should().Be(WorkoutLogId);
        response.Adaptation.Should().Be(expectedEnvelope);
    }

    [Fact]
    public async Task ConfirmAsync_StampsLoggedRunSummaryOnAckTurn()
    {
        // Arrange
        var (sut, deps) = CreateSut();

        // Act
        await sut.ConfirmAsync(UserId, Request(), TestContext.Current.CancellationToken);

        // Assert — the dispatched ack turn carries the confirmed draft's actuals, km-converted
        // and second-converted, keyed to the committed WorkoutLogId. Record value equality (the
        // default CreateSut() ack content is the fixed "ack" LLM stub response) matches the same
        // pattern the sibling ack-content tests already use.
        await deps.Bus.Received(1).InvokeForTenantAsync<ConversationTurnPostedResponse>(
            UserId.ToString(),
            new PostCoachConversationTurn(
                UserId,
                ClientMessageId,
                "ack",
                false,
                LoggedRun: new LoggedRunSummary(WorkoutLogId, 5.0, 1500d, new DateOnly(2026, 6, 24), CompletionStatus.Complete)),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task ConfirmAsync_LoggedRunDistance_ConvertsMilesDraftToKm()
    {
        // Arrange — a miles draft guards the /1000d + unit-conversion path, not a pass-through.
        var (sut, deps) = CreateSut();
        var milesRequest = Request(distanceValue: 3.0, distanceUnit: RunnerDistanceUnit.Miles);
        const double expectedKm = 3.0 * WorkoutDraftUnitConverter.MetersPerMile / 1000d;

        // Act
        await sut.ConfirmAsync(UserId, milesRequest, TestContext.Current.CancellationToken);

        // Assert — same DurationSeconds/OccurredOn/CompletionStatus as the km draft; only the
        // unit-converted DistanceKm differs, guarding the /1000d + Miles conversion specifically.
        await deps.Bus.Received(1).InvokeForTenantAsync<ConversationTurnPostedResponse>(
            UserId.ToString(),
            new PostCoachConversationTurn(
                UserId,
                ClientMessageId,
                "ack",
                false,
                LoggedRun: new LoggedRunSummary(WorkoutLogId, expectedKm, 1500d, new DateOnly(2026, 6, 24), CompletionStatus.Complete)),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    private static ConfirmConversationalLogRequestDto Request(
        double distanceValue = 5,
        RunnerDistanceUnit distanceUnit = RunnerDistanceUnit.Kilometers) => new(
        new StructuredLogDraft
        {
            OccurredOn = new DateOnly(2026, 6, 24),
            DistanceValue = distanceValue,
            DistanceUnit = distanceUnit,
            DurationHours = 0,
            DurationMinutes = 25,
            DurationSeconds = 0,
            CompletionStatus = CompletionStatus.Complete,
            Notes = "legs were heavy",
        },
        ClientMessageId);

    private static (ConfirmConversationalLogService Sut, Deps Deps) CreateSut(AdaptationResponseDto? envelope = null)
    {
        var workoutLogService = Substitute.For<IWorkoutLogService>();
        workoutLogService.CreateAsync(Arg.Any<Guid>(), Arg.Any<CreateWorkoutLogRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(WorkoutLogId));

        var dispatcher = Substitute.For<IAdaptationEvaluationDispatcher>();
        dispatcher.EvaluateAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(envelope ?? AdaptationResponseDto.Adapted(AdaptationKind.Absorb)));

        var contextAssembler = Substitute.For<IContextAssembler>();
        contextAssembler.ComposeForAckAsync(Arg.Any<StructuredLogDraft>(), Arg.Any<AdaptationKind>(), Arg.Any<CancellationToken>())
            .Returns(new AckPromptComposition("sys", "usr", []));

        var llm = Substitute.For<ICoachingLlm>();
        llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("ack"));

        var bus = Substitute.For<IMessageBus>();
        bus.InvokeForTenantAsync<ConversationTurnPostedResponse>(
                Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(Task.FromResult(new ConversationTurnPostedResponse(Guid.NewGuid())));

        var sut = new ConfirmConversationalLogService(
            workoutLogService,
            dispatcher,
            contextAssembler,
            llm,
            bus,
            NullLogger<ConfirmConversationalLogService>.Instance);

        return (sut, new Deps(workoutLogService, dispatcher, contextAssembler, llm, bus));
    }

    private sealed record Deps(
        IWorkoutLogService WorkoutLogService,
        IAdaptationEvaluationDispatcher Dispatcher,
        IContextAssembler ContextAssembler,
        ICoachingLlm Llm,
        IMessageBus Bus);
}
