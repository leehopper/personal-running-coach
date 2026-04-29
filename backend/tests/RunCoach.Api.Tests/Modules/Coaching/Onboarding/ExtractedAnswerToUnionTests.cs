using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Unit tests for <see cref="ExtractedAnswer.ToUnion"/>.
/// Validates that the Pattern-B oneOf invariant is enforced at the LLM-output boundary:
/// exactly one <c>Normalized*</c> slot must be non-null and must match <see cref="ExtractedAnswer.Topic"/>.
/// </summary>
public sealed class ExtractedAnswerToUnionTests
{
    [Fact]
    public void ToUnion_PrimaryGoal_MatchingSlot_ReturnsPrimaryGoalExtraction()
    {
        // Arrange
        var value = new PrimaryGoalAnswer { Goal = PrimaryGoal.GeneralFitness, Description = "Stay fit." };
        var dto = BuildDto(OnboardingTopic.PrimaryGoal, normalizedPrimaryGoal: value);

        // Act
        var result = dto.ToUnion();

        // Assert
        result.Should().BeOfType<PrimaryGoalExtraction>()
            .Which.Value.Should().Be(value);
        result.Confidence.Should().Be(dto.Confidence);
        result.Topic.Should().Be(OnboardingTopic.PrimaryGoal);
    }

    [Fact]
    public void ToUnion_TargetEvent_MatchingSlot_ReturnsTargetEventExtraction()
    {
        // Arrange
        var value = new TargetEventAnswer
        {
            EventName = "City Marathon",
            DistanceKm = 42.195,
            EventDateIso = "2026-10-01",
            TargetFinishTimeIso = null,
        };
        var dto = BuildDto(OnboardingTopic.TargetEvent, normalizedTargetEvent: value);

        // Act
        var result = dto.ToUnion();

        // Assert
        result.Should().BeOfType<TargetEventExtraction>()
            .Which.Value.Should().Be(value);
        result.Confidence.Should().Be(dto.Confidence);
        result.Topic.Should().Be(OnboardingTopic.TargetEvent);
    }

    [Fact]
    public void ToUnion_CurrentFitness_MatchingSlot_ReturnsCurrentFitnessExtraction()
    {
        // Arrange
        var value = new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = 40.0,
            LongestRecentRunKm = 15.0,
            RecentRaceDistanceKm = null,
            RecentRaceTimeIso = null,
            Description = "Running comfortably at easy pace.",
        };
        var dto = BuildDto(OnboardingTopic.CurrentFitness, normalizedCurrentFitness: value);

        // Act
        var result = dto.ToUnion();

        // Assert
        result.Should().BeOfType<CurrentFitnessExtraction>()
            .Which.Value.Should().Be(value);
        result.Confidence.Should().Be(dto.Confidence);
        result.Topic.Should().Be(OnboardingTopic.CurrentFitness);
    }

    [Fact]
    public void ToUnion_WeeklySchedule_MatchingSlot_ReturnsWeeklyScheduleExtraction()
    {
        // Arrange
        var value = new WeeklyScheduleAnswer
        {
            MaxRunDaysPerWeek = 4,
            TypicalSessionMinutes = 60,
            Monday = true,
            Tuesday = false,
            Wednesday = true,
            Thursday = false,
            Friday = true,
            Saturday = true,
            Sunday = false,
            Description = string.Empty,
        };
        var dto = BuildDto(OnboardingTopic.WeeklySchedule, normalizedWeeklySchedule: value);

        // Act
        var result = dto.ToUnion();

        // Assert
        result.Should().BeOfType<WeeklyScheduleExtraction>()
            .Which.Value.Should().Be(value);
        result.Confidence.Should().Be(dto.Confidence);
        result.Topic.Should().Be(OnboardingTopic.WeeklySchedule);
    }

    [Fact]
    public void ToUnion_InjuryHistory_MatchingSlot_ReturnsInjuryHistoryExtraction()
    {
        // Arrange
        var value = new InjuryHistoryAnswer
        {
            HasActiveInjury = false,
            ActiveInjuryDescription = string.Empty,
            PastInjurySummary = "Occasional shin splints a few years ago.",
        };
        var dto = BuildDto(OnboardingTopic.InjuryHistory, normalizedInjuryHistory: value);

        // Act
        var result = dto.ToUnion();

        // Assert
        result.Should().BeOfType<InjuryHistoryExtraction>()
            .Which.Value.Should().Be(value);
        result.Confidence.Should().Be(dto.Confidence);
        result.Topic.Should().Be(OnboardingTopic.InjuryHistory);
    }

    [Fact]
    public void ToUnion_Preferences_MatchingSlot_ReturnsPreferencesExtraction()
    {
        // Arrange
        var value = new PreferencesAnswer
        {
            PreferredUnits = PreferredUnits.Kilometers,
            PreferTrail = false,
            ComfortableWithIntensity = true,
            Description = string.Empty,
        };
        var dto = BuildDto(OnboardingTopic.Preferences, normalizedPreferences: value);

        // Act
        var result = dto.ToUnion();

        // Assert
        result.Should().BeOfType<PreferencesExtraction>()
            .Which.Value.Should().Be(value);
        result.Confidence.Should().Be(dto.Confidence);
        result.Topic.Should().Be(OnboardingTopic.Preferences);
    }

    [Fact]
    public void ToUnion_AllSlotsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var dto = BuildDto(OnboardingTopic.PrimaryGoal);

        // Act
        var act = () => dto.ToUnion();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*exactly 1*");
    }

    [Fact]
    public void ToUnion_MultipleNonNullSlots_ThrowsInvalidOperationException()
    {
        // Arrange — both PrimaryGoal and InjuryHistory slots are set
        var dto = new ExtractedAnswer
        {
            Topic = OnboardingTopic.PrimaryGoal,
            Confidence = 0.9,
            NormalizedPrimaryGoal = new PrimaryGoalAnswer { Goal = PrimaryGoal.RaceTraining, Description = "Run a marathon." },
            NormalizedTargetEvent = null,
            NormalizedCurrentFitness = null,
            NormalizedWeeklySchedule = null,
            NormalizedInjuryHistory = new InjuryHistoryAnswer
            {
                HasActiveInjury = false,
                ActiveInjuryDescription = string.Empty,
                PastInjurySummary = string.Empty,
            },
            NormalizedPreferences = null,
        };

        // Act
        var act = () => dto.ToUnion();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*exactly 1*");
    }

    [Fact]
    public void ToUnion_TopicPrimaryGoal_ButOnlyTargetEventSlotSet_ThrowsInvalidOperationException()
    {
        // Arrange — Topic says PrimaryGoal but only NormalizedTargetEvent is populated
        var dto = new ExtractedAnswer
        {
            Topic = OnboardingTopic.PrimaryGoal,
            Confidence = 0.8,
            NormalizedPrimaryGoal = null,
            NormalizedTargetEvent = new TargetEventAnswer
            {
                EventName = "Spring 5K",
                DistanceKm = 5.0,
                EventDateIso = "2026-05-01",
                TargetFinishTimeIso = null,
            },
            NormalizedCurrentFitness = null,
            NormalizedWeeklySchedule = null,
            NormalizedInjuryHistory = null,
            NormalizedPreferences = null,
        };

        // Act
        var act = () => dto.ToUnion();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    private static ExtractedAnswer BuildDto(
        OnboardingTopic topic,
        PrimaryGoalAnswer? normalizedPrimaryGoal = null,
        TargetEventAnswer? normalizedTargetEvent = null,
        CurrentFitnessAnswer? normalizedCurrentFitness = null,
        WeeklyScheduleAnswer? normalizedWeeklySchedule = null,
        InjuryHistoryAnswer? normalizedInjuryHistory = null,
        PreferencesAnswer? normalizedPreferences = null,
        double confidence = 0.85) =>
        new()
        {
            Topic = topic,
            Confidence = confidence,
            NormalizedPrimaryGoal = normalizedPrimaryGoal,
            NormalizedTargetEvent = normalizedTargetEvent,
            NormalizedCurrentFitness = normalizedCurrentFitness,
            NormalizedWeeklySchedule = normalizedWeeklySchedule,
            NormalizedInjuryHistory = normalizedInjuryHistory,
            NormalizedPreferences = normalizedPreferences,
        };
}
