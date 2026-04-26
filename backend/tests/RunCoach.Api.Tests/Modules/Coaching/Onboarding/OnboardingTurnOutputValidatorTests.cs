using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Validates the Pattern-B-Invariant enforcement per R-067 / DEC-058. Anthropic
/// constrained decoding cannot express the "exactly one Normalized* slot is
/// non-null AND it matches Topic" invariant (the spec rejects <c>oneOf</c>),
/// so the backend enforces it post-deserialization.
/// </summary>
public sealed class OnboardingTurnOutputValidatorTests
{
    [Fact]
    public void Validate_ReturnsValid_WhenPrimaryGoalSlotMatchesTopic()
    {
        // Arrange
        var output = BuildOutputWithPrimaryGoal(PrimaryGoal.RaceTraining);

        // Act
        var result = OnboardingTurnOutputValidator.Validate(output, OnboardingTopic.PrimaryGoal);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violation.Should().Be(OnboardingTurnOutputValidationViolation.None);
        result.NonNullSlotCount.Should().Be(1);
    }

    [Fact]
    public void Validate_ReturnsValid_WhenExtractedIsNull()
    {
        // Arrange — LLM chose to ask a clarifying question instead of committing.
        var output = new OnboardingTurnOutput
        {
            Reply = new[] { TextBlock("Could you tell me a bit more?") },
            Extracted = null,
            NeedsClarification = true,
            ClarificationReason = "ambiguous goal",
            ReadyForPlan = false,
        };

        // Act
        var result = OnboardingTurnOutputValidator.Validate(output, OnboardingTopic.PrimaryGoal);

        // Assert — invariant is vacuously satisfied when no extraction is reported.
        result.IsValid.Should().BeTrue();
        result.NonNullSlotCount.Should().Be(0);
    }

    [Fact]
    public void Validate_ReturnsNoNormalizedSlot_WhenAllSlotsAreNull()
    {
        // Arrange
        var output = new OnboardingTurnOutput
        {
            Reply = new[] { TextBlock("Got it.") },
            Extracted = new ExtractedAnswer
            {
                Topic = OnboardingTopic.PrimaryGoal,
                Confidence = 0.8,
                NormalizedPrimaryGoal = null,
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
        var result = OnboardingTurnOutputValidator.Validate(output, OnboardingTopic.PrimaryGoal);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(OnboardingTurnOutputValidationViolation.NoNormalizedSlot);
    }

    [Fact]
    public void Validate_ReturnsMultipleNormalizedSlots_WhenTwoSlotsAreNonNull()
    {
        // Arrange
        var output = new OnboardingTurnOutput
        {
            Reply = new[] { TextBlock("Got it.") },
            Extracted = new ExtractedAnswer
            {
                Topic = OnboardingTopic.PrimaryGoal,
                Confidence = 0.9,
                NormalizedPrimaryGoal = new PrimaryGoalAnswer
                {
                    Goal = PrimaryGoal.RaceTraining,
                    Description = "marathon",
                },
                NormalizedTargetEvent = new TargetEventAnswer
                {
                    EventName = "City Marathon",
                    DistanceKm = 42.195,
                    EventDateIso = "2026-10-12",
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

        // Act
        var result = OnboardingTurnOutputValidator.Validate(output, OnboardingTopic.PrimaryGoal);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(OnboardingTurnOutputValidationViolation.MultipleNormalizedSlots);
        result.NonNullSlotCount.Should().Be(2);
    }

    [Fact]
    public void Validate_ReturnsSlotTopicMismatch_WhenSlotDoesNotMatchTopic()
    {
        // Arrange — Topic says PrimaryGoal but only NormalizedTargetEvent is populated.
        var output = new OnboardingTurnOutput
        {
            Reply = new[] { TextBlock("Got it.") },
            Extracted = new ExtractedAnswer
            {
                Topic = OnboardingTopic.PrimaryGoal,
                Confidence = 0.9,
                NormalizedPrimaryGoal = null,
                NormalizedTargetEvent = new TargetEventAnswer
                {
                    EventName = "City Marathon",
                    DistanceKm = 42.195,
                    EventDateIso = "2026-10-12",
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

        // Act
        var result = OnboardingTurnOutputValidator.Validate(output, OnboardingTopic.PrimaryGoal);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(OnboardingTurnOutputValidationViolation.SlotTopicMismatch);
    }

    [Fact]
    public void Validate_ReturnsClarificationWithoutReason_WhenFlagSetButReasonMissing()
    {
        // Arrange
        var output = new OnboardingTurnOutput
        {
            Reply = new[] { TextBlock("?") },
            Extracted = null,
            NeedsClarification = true,
            ClarificationReason = "   ",
            ReadyForPlan = false,
        };

        // Act
        var result = OnboardingTurnOutputValidator.Validate(output, OnboardingTopic.PrimaryGoal);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(OnboardingTurnOutputValidationViolation.ClarificationWithoutReason);
    }

    [Fact]
    public void Validate_ThrowsArgumentNullException_WhenOutputIsNull()
    {
        // Arrange + Act
        var act = () => OnboardingTurnOutputValidator.Validate(null!, OnboardingTopic.PrimaryGoal);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static OnboardingTurnOutput BuildOutputWithPrimaryGoal(PrimaryGoal goal) => new()
    {
        Reply = new[] { TextBlock("Got it.") },
        Extracted = new ExtractedAnswer
        {
            Topic = OnboardingTopic.PrimaryGoal,
            Confidence = 0.92,
            NormalizedPrimaryGoal = new PrimaryGoalAnswer
            {
                Goal = goal,
                Description = "training for a goal",
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

    private static AnthropicContentBlock TextBlock(string text) => new()
    {
        Type = AnthropicContentBlockType.Text,
        Text = text,
    };
}
