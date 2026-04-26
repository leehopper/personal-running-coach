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
}
