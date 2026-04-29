using System.Text.Json;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Compile-time + serialization assertions on the Pattern B (R-067 / DEC-058)
/// <see cref="OnboardingTurnOutput"/> record graph. The schema must round-trip cleanly
/// through System.Text.Json so the dictionary produced by AIJsonUtilities at startup
/// matches the runtime deserialized shape.
/// </summary>
public sealed class OnboardingTurnOutputShapeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    [Fact]
    public void OnboardingTurnOutput_RoundTrips_PrimaryGoalSlot()
    {
        // Arrange
        var expected = new OnboardingTurnOutput
        {
            Reply = new[]
            {
                new AnthropicContentBlock
                {
                    Type = AnthropicContentBlockType.Text,
                    Text = "Got it - sounds like a great goal.",
                },
            },
            Extracted = new ExtractedAnswer
            {
                Topic = OnboardingTopic.PrimaryGoal,
                Confidence = 0.92,
                NormalizedPrimaryGoal = new PrimaryGoalAnswer
                {
                    Goal = PrimaryGoal.RaceTraining,
                    Description = "Half marathon in October.",
                },
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

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<OnboardingTurnOutput>(json, JsonOptions);

        // Assert
        actual.Should().NotBeNull();
        actual!.Reply.Should().HaveCount(1);
        actual.Reply[0].Type.Should().Be(AnthropicContentBlockType.Text);
        actual.Extracted.Should().NotBeNull();
        actual.Extracted!.Topic.Should().Be(OnboardingTopic.PrimaryGoal);
        actual.Extracted.NormalizedPrimaryGoal.Should().NotBeNull();
        actual.Extracted.NormalizedPrimaryGoal!.Goal.Should().Be(PrimaryGoal.RaceTraining);
        actual.Extracted.NormalizedTargetEvent.Should().BeNull();
        actual.NeedsClarification.Should().BeFalse();
        actual.ReadyForPlan.Should().BeFalse();
    }

    [Fact]
    public void OnboardingTurnOutput_RoundTrips_NeedsClarificationBranch()
    {
        // Arrange — handler emits clarification branch with no Extracted slot populated.
        var expected = new OnboardingTurnOutput
        {
            Reply = new[]
            {
                new AnthropicContentBlock
                {
                    Type = AnthropicContentBlockType.Text,
                    Text = "Could you clarify whether you mean a 10K or a half marathon?",
                },
            },
            Extracted = null,
            NeedsClarification = true,
            ClarificationReason = "Distance was ambiguous between 10K and half marathon.",
            ReadyForPlan = false,
        };

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<OnboardingTurnOutput>(json, JsonOptions);

        // Assert
        actual.Should().NotBeNull();
        actual!.Extracted.Should().BeNull();
        actual.NeedsClarification.Should().BeTrue();
        actual.ClarificationReason.Should().Be("Distance was ambiguous between 10K and half marathon.");
    }

    [Fact]
    public void ExtractedAnswer_HasExactlySixNormalizedSlots()
    {
        // Arrange — the Pattern-B-Invariant assumes exactly six topic-discriminated slots.
        // Reflect the type so the count never drifts from the OnboardingTopic enum cardinality.
        var properties = typeof(ExtractedAnswer).GetProperties();

        // Act
        var normalizedSlotCount = properties.Count(p => p.Name.StartsWith("Normalized", StringComparison.Ordinal));

        // Assert
        normalizedSlotCount.Should().Be(6, "Pattern B requires exactly six Normalized* slots, one per OnboardingTopic");
        Enum.GetValues<OnboardingTopic>().Length.Should().Be(
            6,
            "OnboardingTopic enum cardinality must match the six Normalized* slots in ExtractedAnswer");
    }

    [Fact]
    public void OnboardingTurnOutput_PropertyOrder_IsStable()
    {
        // Arrange — declared property order drives Anthropic's grammar-cache schema bytes.
        // If the order ever changes the cache is invalidated; this test pins the order.
        var expectedRootOrder = new[]
        {
            "Reply",
            "Extracted",
            "NeedsClarification",
            "ClarificationReason",
            "ReadyForPlan",
        };

        // Act
        var actualRootOrder = typeof(OnboardingTurnOutput).GetProperties().Select(p => p.Name).ToArray();

        // Assert
        actualRootOrder.Should().Equal(expectedRootOrder);
    }

    [Fact]
    public void ExtractedAnswer_PropertyOrder_IsStable()
    {
        // Arrange
        var expectedOrder = new[]
        {
            "Topic",
            "Confidence",
            "NormalizedPrimaryGoal",
            "NormalizedTargetEvent",
            "NormalizedCurrentFitness",
            "NormalizedWeeklySchedule",
            "NormalizedInjuryHistory",
            "NormalizedPreferences",
        };

        // Act
        var actualOrder = typeof(ExtractedAnswer).GetProperties().Select(p => p.Name).ToArray();

        // Assert
        actualOrder.Should().Equal(expectedOrder);
    }

    [Fact]
    public void TopicAnswerRecords_ContainNoObjectFields()
    {
        // Arrange — Pattern B requires closed-shape answer records with no object fields so
        // Anthropic constrained decoding can compile the grammar.
        var topicAnswerTypes = new[]
        {
            typeof(PrimaryGoalAnswer),
            typeof(TargetEventAnswer),
            typeof(CurrentFitnessAnswer),
            typeof(WeeklyScheduleAnswer),
            typeof(InjuryHistoryAnswer),
            typeof(PreferencesAnswer),
        };

        // Act + Assert
        foreach (var type in topicAnswerTypes)
        {
            var objectProperties = type.GetProperties().Where(p => p.PropertyType == typeof(object)).ToArray();
            objectProperties.Should().BeEmpty(
                $"{type.Name} must be closed-shape; no System.Object properties allowed under Pattern B");
        }
    }

    [Fact]
    public void OnboardingAggregateEvents_AreAllSealedRecords()
    {
        // Arrange — Marten event types must be sealed records so their JSON shape is stable.
        var aggregateEventTypes = new[]
        {
            typeof(OnboardingStarted),
            typeof(TopicAsked),
            typeof(UserTurnRecorded),
            typeof(AssistantTurnRecorded),
            typeof(AnswerCaptured),
            typeof(ClarificationRequested),
            typeof(PlanLinkedToUser),
            typeof(OnboardingCompleted),
        };

        // Act + Assert — eight events total per spec proof artifact + DEC-060 / R-069.
        aggregateEventTypes.Should().HaveCount(8, "the onboarding stream declares eight Marten events");
        foreach (var type in aggregateEventTypes)
        {
            type.IsSealed.Should().BeTrue($"{type.Name} must be sealed for Marten serialization stability");
            type.Name.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void PlanLinkedToUser_CarriesUserIdAndPlanId()
    {
        // Arrange + Act — DEC-060 / R-069 event triggers UserProfile.CurrentPlanId update via projection.
        var expected = new PlanLinkedToUser(UserId: Guid.NewGuid(), PlanId: Guid.NewGuid());
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<PlanLinkedToUser>(json, JsonOptions);

        // Assert
        actual.Should().NotBeNull();
        actual!.UserId.Should().Be(expected.UserId);
        actual.PlanId.Should().Be(expected.PlanId);
    }

    [Fact]
    public void OnboardingTurnRequestDto_RoundTrips()
    {
        // Arrange
        var expected = new OnboardingTurnRequestDto(
            IdempotencyKey: Guid.NewGuid(),
            Text: "I want to run a half marathon in October.");

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<OnboardingTurnRequestDto>(json, JsonOptions);

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void OnboardingTurnResponseDto_AssistantBlocks_IsJsonElement_NotJsonDocument()
    {
        // Arrange — JsonElement is the required type for DTO properties; JsonDocument is
        // IDisposable and holds pooled memory. This test pins the property type so a
        // future edit cannot revert to JsonDocument without a test failure.
        var property = typeof(OnboardingTurnResponseDto)
            .GetProperty(nameof(OnboardingTurnResponseDto.AssistantBlocks));

        // Assert
        property.Should().NotBeNull();
        property!.PropertyType.Should().Be<System.Text.Json.JsonElement>(
            because: "DTO properties must use JsonElement (a non-owning struct) rather than JsonDocument (IDisposable, holds pooled memory)");
    }

    [Fact]
    public void ReviseAnswerRequestDto_NormalizedValue_IsJsonElement_NotJsonDocument()
    {
        // Arrange — same invariant on the inbound request DTO.
        var property = typeof(ReviseAnswerRequestDto)
            .GetProperty(nameof(ReviseAnswerRequestDto.NormalizedValue));

        // Assert
        property.Should().NotBeNull();
        property!.PropertyType.Should().Be<System.Text.Json.JsonElement>(
            because: "DTO properties must use JsonElement (a non-owning struct) rather than JsonDocument (IDisposable, holds pooled memory)");
    }

    [Fact]
    public void OnboardingTurnResponseDto_Ask_RoundTrips()
    {
        // Arrange — Ask branch: AssistantBlocks round-trips as JsonElement without disposal concerns.
        var blocksJson = """[{"type":"text","text":"What is your primary goal?"}]""";
        var blocksElement = JsonDocument.Parse(blocksJson).RootElement.Clone();
        var progress = new OnboardingProgressDto(Completed: 0, Total: 6);
        var expected = new OnboardingTurnResponseDto(
            Kind: OnboardingTurnKind.Ask,
            AssistantBlocks: blocksElement,
            Topic: OnboardingTopic.PrimaryGoal,
            SuggestedInputType: SuggestedInputType.Text,
            Progress: progress,
            PlanId: null);

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<OnboardingTurnResponseDto>(json, JsonOptions);

        // Assert
        actual.Should().NotBeNull();
        actual!.Kind.Should().Be(OnboardingTurnKind.Ask);
        actual.Topic.Should().Be(OnboardingTopic.PrimaryGoal);
        actual.PlanId.Should().BeNull();
        actual.AssistantBlocks.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public void OnboardingTurnResponseDto_Complete_RoundTrips()
    {
        // Arrange — Complete branch: no topic, no SuggestedInputType, PlanId set.
        var blocksJson = """[{"type":"text","text":"Your plan is ready!"}]""";
        var blocksElement = JsonDocument.Parse(blocksJson).RootElement.Clone();
        var planId = Guid.NewGuid();
        var progress = new OnboardingProgressDto(Completed: 6, Total: 6);
        var expected = new OnboardingTurnResponseDto(
            Kind: OnboardingTurnKind.Complete,
            AssistantBlocks: blocksElement,
            Topic: null,
            SuggestedInputType: null,
            Progress: progress,
            PlanId: planId);

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<OnboardingTurnResponseDto>(json, JsonOptions);

        // Assert
        actual.Should().NotBeNull();
        actual!.Kind.Should().Be(OnboardingTurnKind.Complete);
        actual.Topic.Should().BeNull();
        actual.SuggestedInputType.Should().BeNull();
        actual.PlanId.Should().Be(planId);
        actual.AssistantBlocks.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public void ReviseAnswerRequestDto_RoundTrips()
    {
        // Arrange — NormalizedValue carries the typed answer payload as a JsonElement.
        // The handler will re-serialize to JsonDocument before appending to the event stream.
        var answerJson = """{"goal":"RaceTraining","description":"Half marathon in October."}""";
        var answerElement = JsonDocument.Parse(answerJson).RootElement.Clone();
        var expected = new ReviseAnswerRequestDto(
            Topic: OnboardingTopic.PrimaryGoal,
            NormalizedValue: answerElement);

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<ReviseAnswerRequestDto>(json, JsonOptions);

        // Assert
        actual.Should().NotBeNull();
        actual!.Topic.Should().Be(OnboardingTopic.PrimaryGoal);
        actual.NormalizedValue.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
        actual.NormalizedValue.GetProperty("goal").GetString().Should().Be("RaceTraining");
    }
}
