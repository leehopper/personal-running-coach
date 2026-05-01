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

    [Theory]
    [InlineData(OnboardingTopic.PrimaryGoal)]
    [InlineData(OnboardingTopic.TargetEvent)]
    [InlineData(OnboardingTopic.CurrentFitness)]
    [InlineData(OnboardingTopic.WeeklySchedule)]
    [InlineData(OnboardingTopic.InjuryHistory)]
    [InlineData(OnboardingTopic.Preferences)]
    public void Validate_ReturnsValid_WhenAnyMatchingSlotIsTheOnlyNonNullSlot(OnboardingTopic topic)
    {
        // Arrange — exercise the SlotMatchesTopic positive path for every topic.
        var output = BuildOutputForTopic(topic);

        // Act
        var actual = OnboardingTurnOutputValidator.Validate(output, topic);

        // Assert
        var expected = (IsValid: true, Violation: OnboardingTurnOutputValidationViolation.None, NonNullSlotCount: 1);
        actual.IsValid.Should().Be(expected.IsValid);
        actual.Violation.Should().Be(expected.Violation);
        actual.NonNullSlotCount.Should().Be(expected.NonNullSlotCount);
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

    private static OnboardingTurnOutput BuildOutputForTopic(OnboardingTopic topic) => new()
    {
        Reply = new[] { TextBlock("Got it.") },
        Extracted = new ExtractedAnswer
        {
            Topic = topic,
            Confidence = 0.92,
            NormalizedPrimaryGoal = topic == OnboardingTopic.PrimaryGoal
                ? new PrimaryGoalAnswer
                {
                    Goal = PrimaryGoal.RaceTraining,
                    Description = "training for a goal",
                }
                : null,
            NormalizedTargetEvent = topic == OnboardingTopic.TargetEvent
                ? new TargetEventAnswer
                {
                    EventName = "City Marathon",
                    DistanceKm = 42.195,
                    EventDateIso = "2026-10-12",
                    TargetFinishTimeIso = null,
                }
                : null,
            NormalizedCurrentFitness = topic == OnboardingTopic.CurrentFitness
                ? new CurrentFitnessAnswer
                {
                    TypicalWeeklyKm = 40,
                    LongestRecentRunKm = 18,
                    RecentRaceDistanceKm = null,
                    RecentRaceTimeIso = null,
                    Description = "consistent base for the past month",
                }
                : null,
            NormalizedWeeklySchedule = topic == OnboardingTopic.WeeklySchedule
                ? new WeeklyScheduleAnswer
                {
                    MaxRunDaysPerWeek = 5,
                    TypicalSessionMinutes = 60,
                    Monday = true,
                    Tuesday = true,
                    Wednesday = false,
                    Thursday = true,
                    Friday = false,
                    Saturday = true,
                    Sunday = true,
                    Description = "no early mornings",
                }
                : null,
            NormalizedInjuryHistory = topic == OnboardingTopic.InjuryHistory
                ? new InjuryHistoryAnswer
                {
                    HasActiveInjury = false,
                    ActiveInjuryDescription = string.Empty,
                    PastInjurySummary = "mild plantar fasciitis last spring",
                }
                : null,
            NormalizedPreferences = topic == OnboardingTopic.Preferences
                ? new PreferencesAnswer
                {
                    PreferredUnits = PreferredUnits.Kilometers,
                    PreferTrail = false,
                    ComfortableWithIntensity = true,
                    Description = "loves tempo runs",
                }
                : null,
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
