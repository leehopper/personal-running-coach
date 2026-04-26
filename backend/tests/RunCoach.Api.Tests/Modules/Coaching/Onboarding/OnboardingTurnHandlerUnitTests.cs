using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Modules.Coaching;
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
            deps.Sanitizer,
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
        deps.Idempotency.DidNotReceiveWithAnyArgs().Record<OnboardingTurnResponseDto>(Arg.Any<Guid>(), Arg.Any<OnboardingTurnResponseDto>());
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
            deps.Sanitizer,
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
            deps.Sanitizer,
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

        // Audit trail: the handler must stage exactly one ClarificationRequested
        // for the current topic with the reason carrying the topic name, and must
        // NOT stage any AnswerCaptured (the synthesized fallback has no extraction).
        var appended = CollectAppendedEvents(deps.Session);
        var clarifications = appended.OfType<ClarificationRequested>().ToArray();
        clarifications.Should().ContainSingle();
        clarifications[0].Topic.Should().Be(OnboardingTopic.PrimaryGoal);
        clarifications[0].Reason.Should().StartWith("Discriminator mismatch");
        appended.OfType<AnswerCaptured>().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TerminalBranch_StagesPlanStreamAndCompletionEvents()
    {
        // Arrange — gate satisfied + LLM agrees ⇒ terminal branch fires.
        var deps = new Dependencies();
        var planId = Guid.Empty;
        deps.AssemblerComposes(SystemPrompt, UserMessage);
        deps.SeesNoIdempotencyHit();
        deps.LlmReturnsSequence(BuildReadyForPlanOutput());

        var fullView = BuildFullView(deps.UserId);
        deps.Session.LoadAsync<OnboardingView>(deps.UserId, Arg.Any<CancellationToken>())
            .Returns(fullView);

        var fakeSequence = BuildFakePlanSequence(deps.UserId);
        deps.PlanGen
            .GeneratePlanAsync(
                Arg.Any<OnboardingView>(),
                Arg.Any<Guid>(),
                Arg.Do<Guid>(id => planId = id),
                Arg.Any<RegenerationIntent?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(fakeSequence);

        // Act
        var response = await OnboardingTurnHandler.Handle(
            new SubmitUserTurn(deps.UserId, deps.IdempotencyKey, "ready"),
            deps.Session,
            deps.Llm,
            deps.Assembler,
            deps.Sanitizer,
            deps.Idempotency,
            deps.PlanGen,
            deps.Time,
            NullLogger<OnboardingTurnHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Assert
        response.Kind.Should().Be(OnboardingTurnKind.Complete);
        response.PlanId.Should().Be(planId);
        response.PlanId.Should().NotBe(Guid.Empty);

        // Plan stream creation goes through StartStream<PlanProjectionDto>.
        var startCalls = deps.Session.Events.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "StartStream"
                && c.GetMethodInfo().IsGenericMethod
                && c.GetMethodInfo().GetGenericArguments()[0] == typeof(RunCoach.Api.Modules.Training.Plan.Models.PlanProjectionDto))
            .ToArray();
        startCalls.Should().ContainSingle();
        var startArgs = startCalls[0].GetArguments();
        startArgs[0].Should().Be(planId);
        var staged = (object[])startArgs[1]!;
        staged.Should().HaveCount(6);
        staged[0].Should().BeOfType<PlanGenerated>();

        // PlanLinkedToUser + OnboardingCompleted are appended on the onboarding stream.
        var appended = CollectAppendedEvents(deps.Session);
        var links = appended.OfType<PlanLinkedToUser>().ToArray();
        links.Should().ContainSingle();
        links[0].UserId.Should().Be(deps.UserId);
        links[0].PlanId.Should().Be(planId);

        var completions = appended.OfType<OnboardingCompleted>().ToArray();
        completions.Should().ContainSingle();
        completions[0].PlanId.Should().Be(planId);
    }

    [Theory]
    [InlineData(0.59, false, false)] // below floor → no AnswerCaptured
    [InlineData(0.60, false, true)] // exact boundary → AnswerCaptured fires
    [InlineData(0.95, true, false)] // NeedsClarification gates capture even with high confidence
    public async Task Handle_AnswerCaptured_RespectsConfidenceFloorAndClarificationGate(
        double confidence,
        bool needsClarification,
        bool expectAnswerCaptured)
    {
        // Arrange
        var deps = new Dependencies();
        deps.AssemblerComposes(SystemPrompt, UserMessage);
        deps.SeesNoIdempotencyHit();
        deps.LlmReturnsSequence(BuildOutputWithExtraction(
            confidence: confidence,
            needsClarification: needsClarification));

        // Act
        await OnboardingTurnHandler.Handle(
            new SubmitUserTurn(deps.UserId, deps.IdempotencyKey, "fitness"),
            deps.Session,
            deps.Llm,
            deps.Assembler,
            deps.Sanitizer,
            deps.Idempotency,
            deps.PlanGen,
            deps.Time,
            NullLogger<OnboardingTurnHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Assert
        var appended = CollectAppendedEvents(deps.Session);
        var captures = appended.OfType<AnswerCaptured>().ToArray();
        var expectedCaptureCount = expectAnswerCaptured ? 1 : 0;
        captures.Should().HaveCount(expectedCaptureCount);

        // When NeedsClarification=true the handler must still stage
        // ClarificationRequested (the alternative audit-trail event).
        if (needsClarification)
        {
            appended.OfType<ClarificationRequested>().Should().ContainSingle();
        }
    }

    [Fact]
    public async Task Handle_HappyPath_AssistantBlocks_UseCamelCasePropertyNames()
    {
        // Arrange — drive a happy-path Ask response through the WireSerializerOptions
        // path (CamelCase). Asserts the serialized JsonDocument carries lowercase
        // 'type' / 'text' so the SPA Zod schemas keep round-tripping.
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
            deps.Sanitizer,
            deps.Idempotency,
            deps.PlanGen,
            deps.Time,
            NullLogger<OnboardingTurnHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Assert — the serialized assistant blocks must use camelCase keys.
        response.AssistantBlocks.ValueKind.Should().Be(JsonValueKind.Array);
        var firstBlock = response.AssistantBlocks.EnumerateArray().First();

        // Camelcase keys present.
        firstBlock.TryGetProperty("type", out _).Should()
            .BeTrue(because: "WireSerializerOptions.CamelCase must produce lowercase 'type'");
        firstBlock.TryGetProperty("text", out _).Should()
            .BeTrue(because: "WireSerializerOptions.CamelCase must produce lowercase 'text'");

        // PascalCase keys absent — would indicate the camelCase policy was dropped.
        firstBlock.TryGetProperty("Type", out _).Should()
            .BeFalse(because: "PascalCase 'Type' would indicate a regression in the wire format");
        firstBlock.TryGetProperty("Text", out _).Should()
            .BeFalse(because: "PascalCase 'Text' would indicate a regression in the wire format");
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
            deps.Sanitizer,
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
            deps.Sanitizer,
            deps.Idempotency,
            deps.PlanGen,
            deps.Time,
            NullLogger<OnboardingTurnHandler>.Instance,
            TestContext.Current.CancellationToken);

        // Assert — record was called with the response we are about to return.
        deps.Idempotency.Received(1).Record(deps.IdempotencyKey, response);
    }

    private static OnboardingTurnResponseDto BuildAskResponseDto() => new(
        Kind: OnboardingTurnKind.Ask,
        AssistantBlocks: JsonSerializer.SerializeToElement(Array.Empty<AnthropicContentBlock>()),
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

    /// <summary>
    /// Walks every Append(streamId, params object[]) call recorded against the
    /// session's Events substitute and flattens the appended events into one
    /// list — lets per-test assertions filter via .OfType&lt;T&gt;() instead of
    /// fighting NSubstitute's expression-tree limits.
    /// </summary>
    private static List<object> CollectAppendedEvents(Marten.IDocumentSession session)
    {
        var collected = new List<object>();
        foreach (var call in session.Events.ReceivedCalls())
        {
            if (!string.Equals(call.GetMethodInfo().Name, "Append", StringComparison.Ordinal))
            {
                continue;
            }

            var args = call.GetArguments();
            if (args.Length < 2 || args[1] is not object[] events)
            {
                continue;
            }

            collected.AddRange(events);
        }

        return collected;
    }

    private static OnboardingTurnOutput BuildOutputWithExtraction(
        double confidence,
        bool needsClarification)
    {
        return new OnboardingTurnOutput
        {
            Reply = [new AnthropicContentBlock { Type = AnthropicContentBlockType.Text, Text = "ok" }],
            Extracted = new ExtractedAnswer
            {
                Topic = OnboardingTopic.PrimaryGoal,
                Confidence = confidence,
                NormalizedPrimaryGoal = new PrimaryGoalAnswer { Goal = PrimaryGoal.GeneralFitness, Description = "test" },
                NormalizedTargetEvent = null,
                NormalizedCurrentFitness = null,
                NormalizedWeeklySchedule = null,
                NormalizedInjuryHistory = null,
                NormalizedPreferences = null,
            },
            NeedsClarification = needsClarification,
            ClarificationReason = needsClarification ? "needs more detail" : null,
            ReadyForPlan = false,
        };
    }

    private static OnboardingTurnOutput BuildReadyForPlanOutput() => new()
    {
        Reply = [new AnthropicContentBlock { Type = AnthropicContentBlockType.Text, Text = "all set" }],
        Extracted = null,
        NeedsClarification = false,
        ClarificationReason = null,
        ReadyForPlan = true,
    };

    private static OnboardingView BuildFullView(Guid userId) => new()
    {
        Id = userId,
        UserId = userId,
        Status = OnboardingStatus.InProgress,
        PrimaryGoal = new PrimaryGoalAnswer { Goal = PrimaryGoal.GeneralFitness, Description = "fitness" },
        TargetEvent = null,
        CurrentFitness = new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = 30,
            LongestRecentRunKm = 12,
            RecentRaceDistanceKm = null,
            RecentRaceTimeIso = null,
            Description = "moderate",
        },
        WeeklySchedule = new WeeklyScheduleAnswer
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
        },
        InjuryHistory = new InjuryHistoryAnswer
        {
            HasActiveInjury = false,
            ActiveInjuryDescription = string.Empty,
            PastInjurySummary = "none",
        },
        Preferences = new PreferencesAnswer
        {
            PreferredUnits = PreferredUnits.Kilometers,
            PreferTrail = false,
            ComfortableWithIntensity = true,
            Description = "ok",
        },
        OutstandingClarifications = [],
    };

    private static PlanEventSequence BuildFakePlanSequence(Guid userId)
    {
        var generatedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var macro = new PlanGenerated(
            PlanId: Guid.Parse("00000000-0000-0000-0000-000000000111"),
            UserId: userId,
            Macro: new RunCoach.Api.Modules.Coaching.Models.Structured.MacroPlanOutput
            {
                TotalWeeks = 4,
                GoalDescription = "test",
                Phases = Array.Empty<RunCoach.Api.Modules.Coaching.Models.Structured.PlanPhaseOutput>(),
                Rationale = string.Empty,
                Warnings = string.Empty,
            },
            GeneratedAt: generatedAt,
            PromptVersion: "v1",
            ModelId: "test-model",
            PreviousPlanId: null);

        var meso = new RunCoach.Api.Modules.Coaching.Models.Structured.MesoWeekOutput
        {
            WeekNumber = 1,
            PhaseType = RunCoach.Api.Modules.Coaching.Models.Structured.PhaseType.Base,
            WeeklyTargetKm = 20,
            IsDeloadWeek = false,
            Sunday = MesoRest(),
            Monday = MesoRest(),
            Tuesday = MesoRest(),
            Wednesday = MesoRest(),
            Thursday = MesoRest(),
            Friday = MesoRest(),
            Saturday = MesoRest(),
            WeekSummary = "rest",
        };

        var mesos = new MesoCycleCreated[]
        {
            new(1, meso),
            new(2, meso with { WeekNumber = 2 }),
            new(3, meso with { WeekNumber = 3 }),
            new(4, meso with { WeekNumber = 4 }),
        };

        var micro = new FirstMicroCycleCreated(
            new RunCoach.Api.Modules.Coaching.Models.Structured.MicroWorkoutListOutput
            {
                Workouts = Array.Empty<RunCoach.Api.Modules.Coaching.Models.Structured.WorkoutOutput>(),
            });

        return new PlanEventSequence(macro, mesos, micro);
    }

    private static RunCoach.Api.Modules.Coaching.Models.Structured.MesoDaySlotOutput MesoRest() => new()
    {
        SlotType = RunCoach.Api.Modules.Coaching.Models.Structured.DaySlotType.Rest,
        WorkoutType = null,
        Notes = "rest",
    };

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
            Sanitizer = Substitute.For<IPromptSanitizer>();
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

        public IPromptSanitizer Sanitizer { get; }

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
                .Returns(_ => (queue.Count > 0 ? queue.Dequeue() : outputs[^1], AnthropicUsage.Zero));
        }
    }
}
