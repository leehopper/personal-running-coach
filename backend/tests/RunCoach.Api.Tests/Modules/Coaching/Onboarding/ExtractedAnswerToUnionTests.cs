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
    // One row per topic: (topic, dto factory, expected extraction type, expected value)
    public static IEnumerable<object[]> HappyPathCases()
    {
        var primaryGoalValue = new PrimaryGoalAnswer { Goal = PrimaryGoal.GeneralFitness, Description = "Stay fit." };
        yield return
        [
            OnboardingTopic.PrimaryGoal,
            BuildDto(OnboardingTopic.PrimaryGoal, normalizedPrimaryGoal: primaryGoalValue),
            typeof(PrimaryGoalExtraction),
            (object)primaryGoalValue,
        ];

        var targetEventValue = new TargetEventAnswer
        {
            EventName = "City Marathon",
            DistanceKm = 42.195,
            EventDateIso = "2026-10-01",
            TargetFinishTimeIso = null,
        };
        yield return
        [
            OnboardingTopic.TargetEvent,
            BuildDto(OnboardingTopic.TargetEvent, normalizedTargetEvent: targetEventValue),
            typeof(TargetEventExtraction),
            (object)targetEventValue,
        ];

        var currentFitnessValue = new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = 40.0,
            LongestRecentRunKm = 15.0,
            RecentRaceDistanceKm = null,
            RecentRaceTimeIso = null,
            Description = "Running comfortably at easy pace.",
        };
        yield return
        [
            OnboardingTopic.CurrentFitness,
            BuildDto(OnboardingTopic.CurrentFitness, normalizedCurrentFitness: currentFitnessValue),
            typeof(CurrentFitnessExtraction),
            (object)currentFitnessValue,
        ];

        var weeklyScheduleValue = new WeeklyScheduleAnswer
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
        yield return
        [
            OnboardingTopic.WeeklySchedule,
            BuildDto(OnboardingTopic.WeeklySchedule, normalizedWeeklySchedule: weeklyScheduleValue),
            typeof(WeeklyScheduleExtraction),
            (object)weeklyScheduleValue,
        ];

        var injuryHistoryValue = new InjuryHistoryAnswer
        {
            HasActiveInjury = false,
            ActiveInjuryDescription = string.Empty,
            PastInjurySummary = "Occasional shin splints a few years ago.",
        };
        yield return
        [
            OnboardingTopic.InjuryHistory,
            BuildDto(OnboardingTopic.InjuryHistory, normalizedInjuryHistory: injuryHistoryValue),
            typeof(InjuryHistoryExtraction),
            (object)injuryHistoryValue,
        ];

        var preferencesValue = new PreferencesAnswer
        {
            PreferredUnits = PreferredUnits.Kilometers,
            PreferTrail = false,
            ComfortableWithIntensity = true,
            Description = string.Empty,
        };
        yield return
        [
            OnboardingTopic.Preferences,
            BuildDto(OnboardingTopic.Preferences, normalizedPreferences: preferencesValue),
            typeof(PreferencesExtraction),
            (object)preferencesValue,
        ];
    }

    [Theory]
    [MemberData(nameof(HappyPathCases))]
    public void ToUnion_MatchingSlot_ReturnsCorrectExtraction(
        OnboardingTopic topic,
        ExtractedAnswer dto,
        Type expectedExtractionType,
        object expectedValue)
    {
        // Arrange — dto and expected values are supplied by HappyPathCases.

        // Act
        var result = dto.ToUnion();

        // Assert
        result.Should().BeOfType(expectedExtractionType);
        result.Confidence.Should().Be(dto.Confidence);
        result.Topic.Should().Be(topic);

        // Verify the typed Value property matches the input answer record.
        var valueProperty = expectedExtractionType.GetProperty("Value");
        valueProperty.Should().NotBeNull(because: "every AnswerExtraction subtype exposes a Value property");
        valueProperty!.GetValue(result).Should().Be(expectedValue);
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
