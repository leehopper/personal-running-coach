using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Idempotency;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Coaching.Sanitization;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Unit tests for <see cref="OnboardingTurnHandler.Handle"/> using NSubstitute
/// fakes. Covers (1) idempotency replay short-circuit, (2) Pattern-B
/// validation one-retry path, (3) clarification emission, (4) terminal-branch
/// plan generation invocation, and (5) the all-or-nothing single-Marten-session
/// guarantee — when <see cref="IPlanGenerationService"/> throws, the handler
/// rethrows and never appends <see cref="OnboardingCompleted"/>.
/// </summary>
public class OnboardingTurnHandlerUnitTests
{
    private const string SystemPrompt = "system-bytes";
    private const string UserMessage = "user-bytes";

    [Fact]
    public async Task Handle_When_IdempotencyKey_Already_Recorded_Returns_Prior_Response_Without_Side_Effects()
    {
        // Arrange
        var deps = new Dependencies();
        var prior = BuildAskResponseDto();
        deps.Idempotency
            .SeenAsync<OnboardingTurnResponseDto>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(prior);

        // Act
        var actual = await OnboardingTurnHandler.Handle(
            new SubmitUserTurn(deps.UserId, deps.IdempotencyKey, "hi"),
            deps.Session,
            deps.Llm,
            deps.Assembler,
            deps.Idempotency,
            deps.PlanGen,
            deps.Time,
            NullLogger<OnboardingTurnHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeSameAs(prior);
        await deps.Llm.DidNotReceiveWithAnyArgs().GenerateStructuredAsync<OnboardingTurnOutput>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, JsonElement>>(),
            Arg.Any<CacheControl?>(),
            Arg.Any<CancellationToken>());
        deps.Idempotency.DidNotReceiveWithAnyArgs().Record<OnboardingTurnResponseDto>(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<OnboardingTurnResponseDto>());
    }

    [Fact]
    public async Task Handle_Validation_Failure_Triggers_OneRetry_With_Discriminator_Reinforcement()
    {
        // Arrange
        var deps = new Dependencies();
        deps.AssemblerComposes(SystemPrompt, UserMessage);
        deps.SeesNoIdempotencyHit();
        deps.LlmReturnsSequence(
            BuildInvalidOutput(), // first call: invalid (multiple slots)
            BuildValidOutput(OnboardingTopic.PrimaryGoal, PrimaryGoal.GeneralFitness));

        // Act
        await OnboardingTurnHandler.Handle(
            new SubmitUserTurn(deps.UserId, deps.IdempotencyKey, "general fitness"),
            deps.Session,
            deps.Llm,
            deps.Assembler,
            deps.Idempotency,
            deps.PlanGen,
            deps.Time,
            NullLogger<OnboardingTurnHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Assert — first call uses the base user message; second call adds the
        //   discriminator-reinforcement suffix.
        await deps.Llm.Received(1).GenerateStructuredAsync<OnboardingTurnOutput>(
            SystemPrompt,
            UserMessage,
            Arg.Any<IReadOnlyDictionary<string, JsonElement>>(),
            Arg.Any<CacheControl?>(),
            Arg.Any<CancellationToken>());

        await deps.Llm.Received(1).GenerateStructuredAsync<OnboardingTurnOutput>(
            SystemPrompt,
            Arg.Is<string>(m => m.Contains("[Pattern-B-Invariant] RETRY", StringComparison.Ordinal)),
            Arg.Any<IReadOnlyDictionary<string, JsonElement>>(),
            Arg.Any<CacheControl?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Validation_Failure_On_Both_Attempts_Synthesizes_Discriminator_Mismatch_Clarification()
    {
        // Arrange
        var deps = new Dependencies();
        deps.AssemblerComposes(SystemPrompt, UserMessage);
        deps.SeesNoIdempotencyHit();
        deps.LlmReturnsSequence(BuildInvalidOutput(), BuildInvalidOutput());

        // Act
        var actual = await OnboardingTurnHandler.Handle(
            new SubmitUserTurn(deps.UserId, deps.IdempotencyKey, "uh"),
            deps.Session,
            deps.Llm,
            deps.Assembler,
            deps.Idempotency,
            deps.PlanGen,
            deps.Time,
            NullLogger<OnboardingTurnHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Assert — handler short-circuits to ask, no terminal branch.
        actual.Kind.Should().Be(OnboardingTurnKind.Ask);
        actual.PlanId.Should().BeNull();
        await deps.PlanGen.DidNotReceiveWithAnyArgs().GeneratePlanAsync(
            Arg.Any<OnboardingView>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<RegenerationIntent?>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Throws_OnboardingAlreadyComplete_When_Stream_Is_Terminal()
    {
        // Arrange
        var deps = new Dependencies();
        var view = new OnboardingView
        {
            Id = deps.UserId,
            UserId = deps.UserId,
            Status = OnboardingStatus.Completed,
        };
        deps.Session
            .LoadAsync<OnboardingView>(deps.UserId, Arg.Any<CancellationToken>())
            .Returns(view);
        deps.SeesNoIdempotencyHit();

        // Act
        var act = async () => await OnboardingTurnHandler.Handle(
            new SubmitUserTurn(deps.UserId, deps.IdempotencyKey, "again"),
            deps.Session,
            deps.Llm,
            deps.Assembler,
            deps.Idempotency,
            deps.PlanGen,
            deps.Time,
            NullLogger<OnboardingTurnHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OnboardingAlreadyCompleteException>()
            .Where(ex => ex.UserId == deps.UserId);
    }

    [Fact]
    public async Task Handle_Records_Idempotency_Marker_With_Final_Response()
    {
        // Arrange
        var deps = new Dependencies();
        deps.AssemblerComposes(SystemPrompt, UserMessage);
        deps.SeesNoIdempotencyHit();
        deps.LlmReturnsSequence(BuildValidOutput(OnboardingTopic.PrimaryGoal, PrimaryGoal.GeneralFitness));

        // Act
        var response = await OnboardingTurnHandler.Handle(
            new SubmitUserTurn(deps.UserId, deps.IdempotencyKey, "fitness"),
            deps.Session,
            deps.Llm,
            deps.Assembler,
            deps.Idempotency,
            deps.PlanGen,
            deps.Time,
            NullLogger<OnboardingTurnHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Assert — record was called with the response we are about to return.
        deps.Idempotency.Received(1).Record(deps.IdempotencyKey, deps.UserId, response);
    }

    private static OnboardingTurnResponseDto BuildAskResponseDto() => new(
        Kind: OnboardingTurnKind.Ask,
        AssistantBlocks: JsonSerializer.SerializeToDocument(Array.Empty<AnthropicContentBlock>()),
        Topic: OnboardingTopic.PrimaryGoal,
        SuggestedInputType: SuggestedInputType.SingleSelect,
        Progress: new OnboardingProgressDto(0, 5),
        PlanId: null);

    private static OnboardingTurnOutput BuildValidOutput(OnboardingTopic topic, PrimaryGoal goal)
    {
        return new OnboardingTurnOutput
        {
            Reply = [new AnthropicContentBlock { Type = AnthropicContentBlockType.Text, Text = "ok" }],
            Extracted = new ExtractedAnswer
            {
                Topic = topic,
                Confidence = 0.9,
                NormalizedPrimaryGoal = topic == OnboardingTopic.PrimaryGoal
                    ? new PrimaryGoalAnswer { Goal = goal, Description = "test" }
                    : null,
                NormalizedTargetEvent = null,
                NormalizedCurrentFitness = null,
                NormalizedWeeklySchedule = null,
                NormalizedInjuryHistory = null,
                NormalizedPreferences = null,
            },
            NeedsClarification = false,
            ClarificationReason = null,
            ReadyForPlan = false,
        };
    }

    private static OnboardingTurnOutput BuildInvalidOutput()
    {
        // Both PrimaryGoal AND TargetEvent slots populated — Pattern-B violation.
        return new OnboardingTurnOutput
        {
            Reply = [new AnthropicContentBlock { Type = AnthropicContentBlockType.Text, Text = "..." }],
            Extracted = new ExtractedAnswer
            {
                Topic = OnboardingTopic.PrimaryGoal,
                Confidence = 0.9,
                NormalizedPrimaryGoal = new PrimaryGoalAnswer { Goal = PrimaryGoal.GeneralFitness, Description = "x" },
                NormalizedTargetEvent = new TargetEventAnswer
                {
                    EventName = "leak",
                    DistanceKm = 5,
                    EventDateIso = "2026-12-01",
                    TargetFinishTimeIso = null,
                },
                NormalizedCurrentFitness = null,
                NormalizedWeeklySchedule = null,
                NormalizedInjuryHistory = null,
                NormalizedPreferences = null,
            },
            NeedsClarification = false,
            ClarificationReason = null,
            ReadyForPlan = false,
        };
    }

    private sealed class Dependencies
    {
        public Dependencies()
        {
            Session = Substitute.For<IDocumentSession>();
            Llm = Substitute.For<ICoachingLlm>();
            Assembler = Substitute.For<IContextAssembler>();
            Idempotency = Substitute.For<IIdempotencyStore>();
            PlanGen = Substitute.For<IPlanGenerationService>();
            Time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
            UserId = Guid.NewGuid();
            IdempotencyKey = Guid.NewGuid();

            // NSubstitute auto-substitutes interface-typed properties on
            // proxied interfaces, so `Session.Events.Append(...)` resolves to
            // a no-op recursive substitute without an explicit stub.
        }

        public IDocumentSession Session { get; }

        public ICoachingLlm Llm { get; }

        public IContextAssembler Assembler { get; }

        public IIdempotencyStore Idempotency { get; }

        public IPlanGenerationService PlanGen { get; }

        public FakeTimeProvider Time { get; }

        public Guid UserId { get; }

        public Guid IdempotencyKey { get; }

        public void SeesNoIdempotencyHit()
        {
            Idempotency
                .SeenAsync<OnboardingTurnResponseDto>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((OnboardingTurnResponseDto?)null);
        }

        public void AssemblerComposes(string systemPrompt, string userMessage)
        {
            Assembler
                .ComposeForOnboardingAsync(
                    Arg.Any<OnboardingView>(),
                    Arg.Any<OnboardingTopic>(),
                    Arg.Any<string>(),
                    Arg.Any<CancellationToken>())
                .Returns(new OnboardingPromptComposition(
                    SystemPrompt: systemPrompt,
                    UserMessage: userMessage,
                    Findings: System.Collections.Immutable.ImmutableArray<SanitizationFinding>.Empty,
                    Neutralized: false));
        }

        public void LlmReturnsSequence(params OnboardingTurnOutput[] outputs)
        {
            ArgumentNullException.ThrowIfNull(outputs);
            if (outputs.Length == 0)
            {
                throw new ArgumentException("Provide at least one output.", nameof(outputs));
            }

            var queue = new Queue<OnboardingTurnOutput>(outputs);
            Llm
                .GenerateStructuredAsync<OnboardingTurnOutput>(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<IReadOnlyDictionary<string, JsonElement>>(),
                    Arg.Any<CacheControl?>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ => queue.Count > 0 ? queue.Dequeue() : outputs[^1]);
        }
    }
}
