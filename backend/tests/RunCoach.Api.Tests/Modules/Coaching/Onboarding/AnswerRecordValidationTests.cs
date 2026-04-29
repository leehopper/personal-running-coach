using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Range-validation tests for numeric fields on onboarding answer records and
/// <see cref="ExtractedAnswer"/>. Each validated field gets one in-range (succeeds)
/// test and one or more out-of-range (throws) tests. Construction is tested directly
/// (not via <c>with</c> expressions) because C# record copy-constructors assign backing
/// fields directly and do not re-invoke the init accessor on the mutated property.
/// </summary>
public sealed class AnswerRecordValidationTests
{
    // WeeklyScheduleAnswer.MaxRunDaysPerWeek (1-7 inclusive)
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    public void WeeklyScheduleAnswer_MaxRunDaysPerWeek_InRange_Succeeds(int value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new WeeklyScheduleAnswer
        {
            MaxRunDaysPerWeek = value,
            TypicalSessionMinutes = 60,
            Monday = true,
            Tuesday = false,
            Wednesday = true,
            Thursday = false,
            Friday = true,
            Saturday = false,
            Sunday = true,
            Description = string.Empty,
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8)]
    public void WeeklyScheduleAnswer_MaxRunDaysPerWeek_OutOfRange_Throws(int value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new WeeklyScheduleAnswer
        {
            MaxRunDaysPerWeek = value,
            TypicalSessionMinutes = 60,
            Monday = true,
            Tuesday = false,
            Wednesday = true,
            Thursday = false,
            Friday = true,
            Saturday = false,
            Sunday = true,
            Description = string.Empty,
        };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("MaxRunDaysPerWeek");
    }

    // WeeklyScheduleAnswer.TypicalSessionMinutes (> 0)
    [Theory]
    [InlineData(1)]
    [InlineData(45)]
    [InlineData(120)]
    public void WeeklyScheduleAnswer_TypicalSessionMinutes_InRange_Succeeds(int value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new WeeklyScheduleAnswer
        {
            MaxRunDaysPerWeek = 4,
            TypicalSessionMinutes = value,
            Monday = true,
            Tuesday = false,
            Wednesday = true,
            Thursday = false,
            Friday = true,
            Saturday = false,
            Sunday = true,
            Description = string.Empty,
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void WeeklyScheduleAnswer_TypicalSessionMinutes_OutOfRange_Throws(int value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new WeeklyScheduleAnswer
        {
            MaxRunDaysPerWeek = 4,
            TypicalSessionMinutes = value,
            Monday = true,
            Tuesday = false,
            Wednesday = true,
            Thursday = false,
            Friday = true,
            Saturday = false,
            Sunday = true,
            Description = string.Empty,
        };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("TypicalSessionMinutes");
    }

    // TargetEventAnswer.DistanceKm (> 0)
    [Theory]
    [InlineData(0.1)]
    [InlineData(5.0)]
    [InlineData(42.195)]
    public void TargetEventAnswer_DistanceKm_InRange_Succeeds(double value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new TargetEventAnswer
        {
            EventName = "Local 10K",
            DistanceKm = value,
            EventDateIso = "2026-06-01",
            TargetFinishTimeIso = null,
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.001)]
    [InlineData(-42.0)]
    public void TargetEventAnswer_DistanceKm_OutOfRange_Throws(double value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new TargetEventAnswer
        {
            EventName = "Local 10K",
            DistanceKm = value,
            EventDateIso = "2026-06-01",
            TargetFinishTimeIso = null,
        };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("DistanceKm");
    }

    // CurrentFitnessAnswer.TypicalWeeklyKm (>= 0)
    [Theory]
    [InlineData(0.0)]
    [InlineData(30.0)]
    [InlineData(150.0)]
    public void CurrentFitnessAnswer_TypicalWeeklyKm_InRange_Succeeds(double value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = value,
            LongestRecentRunKm = 12.0,
            RecentRaceDistanceKm = null,
            RecentRaceTimeIso = null,
            Description = "Comfortable at easy pace.",
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(-10.0)]
    public void CurrentFitnessAnswer_TypicalWeeklyKm_OutOfRange_Throws(double value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = value,
            LongestRecentRunKm = 12.0,
            RecentRaceDistanceKm = null,
            RecentRaceTimeIso = null,
            Description = "Comfortable at easy pace.",
        };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("TypicalWeeklyKm");
    }

    // CurrentFitnessAnswer.LongestRecentRunKm (>= 0)
    [Theory]
    [InlineData(0.0)]
    [InlineData(12.0)]
    [InlineData(42.195)]
    public void CurrentFitnessAnswer_LongestRecentRunKm_InRange_Succeeds(double value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = 30.0,
            LongestRecentRunKm = value,
            RecentRaceDistanceKm = null,
            RecentRaceTimeIso = null,
            Description = "Comfortable at easy pace.",
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(-5.0)]
    public void CurrentFitnessAnswer_LongestRecentRunKm_OutOfRange_Throws(double value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = 30.0,
            LongestRecentRunKm = value,
            RecentRaceDistanceKm = null,
            RecentRaceTimeIso = null,
            Description = "Comfortable at easy pace.",
        };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("LongestRecentRunKm");
    }

    // CurrentFitnessAnswer.RecentRaceDistanceKm (nullable; if non-null >= 0)
    [Fact]
    public void CurrentFitnessAnswer_RecentRaceDistanceKm_Null_Succeeds()
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = 30.0,
            LongestRecentRunKm = 12.0,
            RecentRaceDistanceKm = null,
            RecentRaceTimeIso = null,
            Description = "Comfortable at easy pace.",
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(10.0)]
    [InlineData(21.0975)]
    public void CurrentFitnessAnswer_RecentRaceDistanceKm_NonNullInRange_Succeeds(double value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = 30.0,
            LongestRecentRunKm = 12.0,
            RecentRaceDistanceKm = value,
            RecentRaceTimeIso = null,
            Description = "Comfortable at easy pace.",
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(-10.0)]
    public void CurrentFitnessAnswer_RecentRaceDistanceKm_NonNullOutOfRange_Throws(double value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = 30.0,
            LongestRecentRunKm = 12.0,
            RecentRaceDistanceKm = value,
            RecentRaceTimeIso = null,
            Description = "Comfortable at easy pace.",
        };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("RecentRaceDistanceKm");
    }

    // ExtractedAnswer.Confidence (0.0-1.0 inclusive)
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.6)]
    [InlineData(1.0)]
    public void ExtractedAnswer_Confidence_InRange_Succeeds(double value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new ExtractedAnswer
        {
            Topic = OnboardingTopic.PrimaryGoal,
            Confidence = value,
            NormalizedPrimaryGoal = new PrimaryGoalAnswer
            {
                Goal = PrimaryGoal.GeneralFitness,
                Description = "Just want to stay fit.",
            },
            NormalizedTargetEvent = null,
            NormalizedCurrentFitness = null,
            NormalizedWeeklySchedule = null,
            NormalizedInjuryHistory = null,
            NormalizedPreferences = null,
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(1.001)]
    [InlineData(-1.0)]
    [InlineData(2.0)]
    public void ExtractedAnswer_Confidence_OutOfRange_Throws(double value)
    {
        // Arrange (no setup needed)

        // Act
        var act = () => new ExtractedAnswer
        {
            Topic = OnboardingTopic.PrimaryGoal,
            Confidence = value,
            NormalizedPrimaryGoal = new PrimaryGoalAnswer
            {
                Goal = PrimaryGoal.GeneralFitness,
                Description = "Just want to stay fit.",
            },
            NormalizedTargetEvent = null,
            NormalizedCurrentFitness = null,
            NormalizedWeeklySchedule = null,
            NormalizedInjuryHistory = null,
            NormalizedPreferences = null,
        };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("Confidence");
    }
}
