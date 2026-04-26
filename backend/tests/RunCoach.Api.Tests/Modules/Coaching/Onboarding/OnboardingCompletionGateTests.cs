using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Pure-function unit tests for <see cref="OnboardingCompletionGate"/> per
/// Slice 1 § Unit 1 R01.6 / completion-gate Gherkin scenarios. Covers the
/// race-training / non-race-training branch, missing-slot rejection, and the
/// outstanding-clarification veto.
/// </summary>
public class OnboardingCompletionGateTests
{
    [Fact]
    public void IsSatisfied_Returns_False_When_PrimaryGoal_Is_Null()
    {
        // Arrange
        var view = new OnboardingView { Status = OnboardingStatus.InProgress };

        // Act
        var actual = OnboardingCompletionGate.IsSatisfied(view);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfied_Returns_False_When_PrimaryGoal_Is_RaceTraining_But_TargetEvent_Is_Null()
    {
        // Arrange
        var view = BuildFullView(PrimaryGoal.RaceTraining);
        view.TargetEvent = null;

        // Act
        var actual = OnboardingCompletionGate.IsSatisfied(view);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfied_Returns_True_When_PrimaryGoal_Is_GeneralFitness_And_TargetEvent_Is_Null()
    {
        // Arrange — TargetEvent is N/A when goal is not race training.
        var view = BuildFullView(PrimaryGoal.GeneralFitness);
        view.TargetEvent = null;

        // Act
        var actual = OnboardingCompletionGate.IsSatisfied(view);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfied_Returns_False_When_OutstandingClarifications_Is_NonEmpty()
    {
        // Arrange
        var view = BuildFullView(PrimaryGoal.GeneralFitness);
        view.OutstandingClarifications = [OnboardingTopic.CurrentFitness];

        // Act
        var actual = OnboardingCompletionGate.IsSatisfied(view);

        // Assert
        actual.Should().BeFalse();
    }

    [Theory]
    [InlineData(PrimaryGoal.RaceTraining)]
    [InlineData(PrimaryGoal.GeneralFitness)]
    [InlineData(PrimaryGoal.ReturnToRunning)]
    [InlineData(PrimaryGoal.BuildVolume)]
    [InlineData(PrimaryGoal.BuildSpeed)]
    public void IsSatisfied_Returns_True_When_All_Slots_Filled_And_No_Outstanding_Clarifications(PrimaryGoal goal)
    {
        // Arrange
        var view = BuildFullView(goal);

        // Act
        var actual = OnboardingCompletionGate.IsSatisfied(view);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void NextTopic_Returns_PrimaryGoal_For_Empty_View()
    {
        // Arrange
        var view = new OnboardingView();

        // Act
        var actual = OnboardingCompletionGate.NextTopic(view);

        // Assert
        actual.Should().Be(OnboardingTopic.PrimaryGoal);
    }

    [Fact]
    public void NextTopic_Skips_TargetEvent_When_PrimaryGoal_Is_Not_RaceTraining()
    {
        // Arrange
        var view = new OnboardingView
        {
            PrimaryGoal = new PrimaryGoalAnswer { Goal = PrimaryGoal.GeneralFitness, Description = "fitness" },
        };

        // Act
        var actual = OnboardingCompletionGate.NextTopic(view);

        // Assert
        actual.Should().Be(OnboardingTopic.CurrentFitness);
    }

    [Fact]
    public void NextTopic_Returns_TargetEvent_When_PrimaryGoal_Is_RaceTraining()
    {
        // Arrange
        var view = new OnboardingView
        {
            PrimaryGoal = new PrimaryGoalAnswer { Goal = PrimaryGoal.RaceTraining, Description = "race" },
        };

        // Act
        var actual = OnboardingCompletionGate.NextTopic(view);

        // Assert
        actual.Should().Be(OnboardingTopic.TargetEvent);
    }

    [Fact]
    public void NextTopic_Returns_Null_When_All_Slots_Filled()
    {
        // Arrange
        var view = BuildFullView(PrimaryGoal.GeneralFitness);

        // Act
        var actual = OnboardingCompletionGate.NextTopic(view);

        // Assert
        actual.Should().BeNull();
    }

    [Fact]
    public void Progress_Reports_Five_Total_For_Non_RaceTraining()
    {
        // Arrange
        var view = new OnboardingView
        {
            PrimaryGoal = new PrimaryGoalAnswer { Goal = PrimaryGoal.GeneralFitness, Description = "fitness" },
        };

        // Act
        var (completed, total) = OnboardingCompletionGate.Progress(view);

        // Assert
        completed.Should().Be(1);
        total.Should().Be(5);
    }

    [Fact]
    public void Progress_Reports_Six_Total_For_RaceTraining()
    {
        // Arrange
        var view = new OnboardingView
        {
            PrimaryGoal = new PrimaryGoalAnswer { Goal = PrimaryGoal.RaceTraining, Description = "race" },
        };

        // Act
        var (completed, total) = OnboardingCompletionGate.Progress(view);

        // Assert
        completed.Should().Be(1);
        total.Should().Be(6);
    }

    private static OnboardingView BuildFullView(PrimaryGoal goal) => new()
    {
        Status = OnboardingStatus.InProgress,
        PrimaryGoal = new PrimaryGoalAnswer { Goal = goal, Description = "stub" },
        TargetEvent = goal == PrimaryGoal.RaceTraining
            ? new TargetEventAnswer
            {
                EventName = "Test 10K",
                DistanceKm = 10,
                EventDateIso = "2026-12-01",
                TargetFinishTimeIso = null,
            }
            : null,
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
            Description = "evenings only",
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
}
