using FluentAssertions;
using RunCoach.Api.Modules.Training.Computations;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Profiles;

namespace RunCoach.Api.Tests.Modules.Training.Profiles;

public class TestProfilesTests
{
    private readonly VdotCalculator _vdotCalc = new();
    private readonly PaceCalculator _paceCalc = new();

    [Fact]
    public void All_ContainsExactly5Profiles()
    {
        // Arrange & Act
        var all = TestProfiles.All;

        // Assert
        all.Should().HaveCount(5, because: "exactly 5 test profiles are defined for POC 1");
        all.Keys.Should().BeEquivalentTo("sarah", "lee", "maria", "james", "priya");
    }

    [Theory]
    [InlineData("sarah")]
    [InlineData("SARAH")]
    [InlineData("lee")]
    [InlineData("LEE")]
    [InlineData("maria")]
    [InlineData("james")]
    [InlineData("priya")]
    public void All_ProfileLookup_IsCaseInsensitive(string name)
    {
        // Arrange & Act
        var all = TestProfiles.All;

        // Assert
        all.Should().ContainKey(name);
    }

    [Fact]
    public void Sarah_HasCompleteUserProfile()
    {
        // Arrange & Act
        var sarah = TestProfiles.Sarah();

        // Assert
        sarah.UserProfile.Name.Should().Be("Sarah");
        sarah.UserProfile.Age.Should().Be(28);
        sarah.UserProfile.Gender.Should().Be("Female");
        sarah.UserProfile.RunningExperienceYears.Should().Be(0.5m);
        sarah.UserProfile.CurrentWeeklyDistanceKm.Should().Be(15m);
        sarah.UserProfile.RecentRaceTimes.Should().BeEmpty(because: "Sarah has no race history");
        sarah.UserProfile.InjuryHistory.Should().BeEmpty();
    }

    [Fact]
    public void Sarah_HasRaceGoalForFirst5K()
    {
        // Arrange & Act
        var sarah = TestProfiles.Sarah();

        // Assert
        sarah.GoalState.GoalType.Should().Be("RaceGoal");
        sarah.GoalState.TargetRace.Should().NotBeNull();
        sarah.GoalState.TargetRace!.Distance.Should().Be("5K");
        sarah.GoalState.TargetRace!.TargetTime.Should().BeNull(
            because: "beginner has no target time, just finish");
    }

    [Fact]
    public void Sarah_HasNullVdot_BecauseNoRaceHistory()
    {
        // Arrange & Act
        var sarah = TestProfiles.Sarah();

        // Assert
        sarah.GoalState.CurrentFitnessEstimate.EstimatedVdot.Should().BeNull(
            because: "no race history means VDOT cannot be computed");
        sarah.GoalState.CurrentFitnessEstimate.FitnessLevel.Should().Be("Beginner");
    }

    [Fact]
    public void Sarah_HasBeginnerEasyPaceRange_NoOtherPaces()
    {
        // Arrange & Act
        var sarah = TestProfiles.Sarah();
        var paces = sarah.GoalState.CurrentFitnessEstimate.TrainingPaces;

        // Assert
        paces.EasyPaceRange.Should().NotBeNull();
        paces.EasyPaceRange.MinPerKm.Should().Be(TimeSpan.FromSeconds(420));
        paces.EasyPaceRange.MaxPerKm.Should().Be(TimeSpan.FromSeconds(480));
        paces.MarathonPace.Should().BeNull();
        paces.ThresholdPace.Should().BeNull();
        paces.IntervalPace.Should().BeNull();
        paces.RepetitionPace.Should().BeNull();
    }

    [Fact]
    public void Sarah_HasNoTrainingHistory()
    {
        // Arrange & Act
        var sarah = TestProfiles.Sarah();

        // Assert
        sarah.TrainingHistory.Should().BeEmpty(because: "Sarah is a brand-new runner");
    }

    [Fact]
    public void Lee_HasCompleteUserProfile()
    {
        // Arrange & Act
        var lee = TestProfiles.Lee();

        // Assert
        lee.UserProfile.Name.Should().Be("Lee");
        lee.UserProfile.Age.Should().Be(34);
        lee.UserProfile.Gender.Should().Be("Male");
        lee.UserProfile.RunningExperienceYears.Should().Be(3m);
        lee.UserProfile.CurrentWeeklyDistanceKm.Should().Be(40m);
        lee.UserProfile.RecentRaceTimes.Should().HaveCount(1);
        lee.UserProfile.RecentRaceTimes[0].Distance.Should().Be("10K");
    }

    [Fact]
    public void Lee_HasCorrectVdotFromRaceTime()
    {
        // Arrange
        var expectedVdot = _vdotCalc.CalculateVdot(
            new RaceTime("10K", TimeSpan.FromMinutes(48), new DateOnly(2026, 2, 15), null));

        // Act
        var lee = TestProfiles.Lee();

        // Assert
        lee.GoalState.CurrentFitnessEstimate.EstimatedVdot.Should().Be(
            expectedVdot!.Value,
            because: "Lee's VDOT should be computed from his 10K race time of 48:00");

        lee.GoalState.CurrentFitnessEstimate.EstimatedVdot!.Value.Should().BeApproximately(
            42.0m,
            0.5m,
            because: "10K in 48:00 corresponds to approximately VDOT 42");
    }

    [Fact]
    public void Lee_HasTrainingPacesDerivedFromVdot()
    {
        // Arrange
        var lee = TestProfiles.Lee();
        var expectedPaces = _paceCalc.CalculatePaces(lee.GoalState.CurrentFitnessEstimate.EstimatedVdot!.Value);

        // Act
        var actualPaces = lee.GoalState.CurrentFitnessEstimate.TrainingPaces;

        // Assert
        actualPaces.Should().Be(
            expectedPaces,
            because: "paces should be derived from the same VDOT via PaceCalculator");

        actualPaces.EasyPaceRange.Should().NotBeNull();
        actualPaces.MarathonPace.Should().NotBeNull();
        actualPaces.ThresholdPace.Should().NotBeNull();
        actualPaces.IntervalPace.Should().NotBeNull();
        actualPaces.RepetitionPace.Should().NotBeNull();
    }

    [Fact]
    public void Lee_HasSubHalfMarathonRaceGoal()
    {
        // Arrange & Act
        var lee = TestProfiles.Lee();

        // Assert
        lee.GoalState.GoalType.Should().Be("RaceGoal");
        lee.GoalState.TargetRace.Should().NotBeNull();
        lee.GoalState.TargetRace!.Distance.Should().Be("Half-Marathon");
        lee.GoalState.TargetRace!.TargetTime.Should().Be(
            TimeSpan.FromMinutes(105),
            because: "Lee's target is sub-1:45 HM");
    }

    [Fact]
    public void Lee_Has3WeeksTrainingHistory()
    {
        // Arrange & Act
        var lee = TestProfiles.Lee();

        // Assert — 4 workouts per week x 3 weeks = 12
        lee.TrainingHistory.Should().HaveCount(
            12,
            because: "Lee has 4 workouts per week for 3 weeks");
    }

    [Fact]
    public void Maria_HasCompleteUserProfile()
    {
        // Arrange & Act
        var maria = TestProfiles.Maria();

        // Assert
        maria.UserProfile.Name.Should().Be("Maria");
        maria.UserProfile.Age.Should().Be(42);
        maria.UserProfile.Gender.Should().Be("Female");
        maria.UserProfile.RunningExperienceYears.Should().Be(12m);
        maria.UserProfile.CurrentWeeklyDistanceKm.Should().Be(55m);
        maria.UserProfile.RecentRaceTimes.Should().HaveCount(3);
    }

    [Fact]
    public void Maria_HasCorrectVdotFromBestRaceResult()
    {
        // Arrange
        var maria = TestProfiles.Maria();
        var expectedVdot = _vdotCalc.CalculateVdot(maria.UserProfile.RecentRaceTimes);

        // Act
        var actualVdot = maria.GoalState.CurrentFitnessEstimate.EstimatedVdot;

        // Assert
        actualVdot.Should().Be(
            expectedVdot!.Value,
            because: "Maria's VDOT should be the best from her 3 race results");
    }

    [Fact]
    public void Maria_HasMaintenanceGoal_NoTargetRace()
    {
        // Arrange & Act
        var maria = TestProfiles.Maria();

        // Assert
        maria.GoalState.GoalType.Should().Be("Maintenance");
        maria.GoalState.TargetRace.Should().BeNull(because: "Maria has no current race goal");
    }

    [Fact]
    public void Maria_Has4WeeksTrainingHistory()
    {
        // Arrange & Act
        var maria = TestProfiles.Maria();

        // Assert — 6 workouts per week x 4 weeks = 24
        maria.TrainingHistory.Should().HaveCount(
            24,
            because: "Maria has 6 workouts per week for 4 weeks");
    }

    [Fact]
    public void Maria_TrainingHistory_IncludesVariety()
    {
        // Arrange & Act
        var maria = TestProfiles.Maria();
        var workoutTypes = maria.TrainingHistory.Select(w => w.WorkoutType).Distinct().ToList();

        // Assert
        workoutTypes.Should().Contain("Easy");
        workoutTypes.Should().Contain("Intervals");
        workoutTypes.Should().Contain("Tempo");
        workoutTypes.Should().Contain("LongRun");
    }

    [Fact]
    public void James_HasCompleteUserProfile()
    {
        // Arrange & Act
        var james = TestProfiles.James();

        // Assert
        james.UserProfile.Name.Should().Be("James");
        james.UserProfile.Age.Should().Be(38);
        james.UserProfile.Gender.Should().Be("Male");
        james.UserProfile.RunningExperienceYears.Should().Be(5m);
        james.UserProfile.CurrentWeeklyDistanceKm.Should().Be(10m);
    }

    [Fact]
    public void James_HasActiveInjury()
    {
        // Arrange & Act
        var james = TestProfiles.James();

        // Assert
        james.UserProfile.InjuryHistory.Should().Contain(
            i => i.Status == "Active",
            because: "James has an active plantar fasciitis injury");

        james.UserProfile.InjuryHistory.Should().Contain(
            i => i.Description.Contains("Plantar fasciitis", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void James_HasReturnFromInjuryGoal()
    {
        // Arrange & Act
        var james = TestProfiles.James();

        // Assert
        james.GoalState.GoalType.Should().Be("ReturnFromInjury");
        james.GoalState.TargetRace.Should().BeNull(because: "injury return has no race target");
    }

    [Fact]
    public void James_HasPreInjuryVdot()
    {
        // Arrange
        var expectedVdot = _vdotCalc.CalculateVdot(
            new RaceTime("10K", new TimeSpan(0, 44, 0), new DateOnly(2025, 9, 20), null));

        // Act
        var james = TestProfiles.James();

        // Assert
        james.GoalState.CurrentFitnessEstimate.EstimatedVdot.Should().Be(
            expectedVdot!.Value,
            because: "James's VDOT is based on pre-injury 10K time");
    }

    [Fact]
    public void James_Has2WeeksLimitedTrainingHistory()
    {
        // Arrange & Act
        var james = TestProfiles.James();

        // Assert — 3 workouts per week x 2 weeks = 6
        james.TrainingHistory.Should().HaveCount(
            6,
            because: "James has 3 workouts per week for 2 weeks");
    }

    [Fact]
    public void James_TrainingHistory_AllEasyPaceOnly()
    {
        // Arrange & Act
        var james = TestProfiles.James();

        // Assert
        james.TrainingHistory.Should().OnlyContain(
            w => w.WorkoutType == "Easy",
            because: "James is restricted to easy running only while recovering");
    }

    [Fact]
    public void James_TrainingHistory_NoWorkoutExceeds20Minutes()
    {
        // Arrange & Act
        var james = TestProfiles.James();

        // Assert
        james.TrainingHistory.Should().OnlyContain(
            w => w.DurationMinutes <= 20,
            because: "James is cleared for maximum 20 minutes only");
    }

    [Fact]
    public void James_HasConstraints_LimitingActivity()
    {
        // Arrange & Act
        var james = TestProfiles.James();

        // Assert
        james.UserProfile.Preferences.Constraints.Should().NotBeEmpty();
        james.UserProfile.Preferences.AvailableTimePerRunMinutes.Should().Be(20);
    }

    [Fact]
    public void Priya_HasCompleteUserProfile()
    {
        // Arrange & Act
        var priya = TestProfiles.Priya();

        // Assert
        priya.UserProfile.Name.Should().Be("Priya");
        priya.UserProfile.Age.Should().Be(30);
        priya.UserProfile.Gender.Should().Be("Female");
        priya.UserProfile.RunningExperienceYears.Should().Be(7m);
        priya.UserProfile.CurrentWeeklyDistanceKm.Should().Be(60m);
        priya.UserProfile.RecentRaceTimes.Should().HaveCount(2);
    }

    [Fact]
    public void Priya_HasCorrectVdotFromBestRaceResult()
    {
        // Arrange
        var priya = TestProfiles.Priya();
        var expectedVdot = _vdotCalc.CalculateVdot(priya.UserProfile.RecentRaceTimes);

        // Act
        var actualVdot = priya.GoalState.CurrentFitnessEstimate.EstimatedVdot;

        // Assert
        actualVdot.Should().Be(
            expectedVdot!.Value,
            because: "Priya's VDOT should be the best from her 2 race results");
    }

    [Fact]
    public void Priya_HasMarathonRaceGoal()
    {
        // Arrange & Act
        var priya = TestProfiles.Priya();

        // Assert
        priya.GoalState.GoalType.Should().Be("RaceGoal");
        priya.GoalState.TargetRace.Should().NotBeNull();
        priya.GoalState.TargetRace!.Distance.Should().Be("Marathon");
        priya.GoalState.TargetRace!.TargetTime.Should().Be(
            TimeSpan.FromMinutes(195),
            because: "Priya's target is sub-3:15 marathon");
    }

    [Fact]
    public void Priya_HasMax4RunDaysConstraint()
    {
        // Arrange & Act
        var priya = TestProfiles.Priya();

        // Assert
        priya.UserProfile.Preferences.MaxRunDaysPerWeek.Should().Be(4);
        priya.UserProfile.Preferences.PreferredRunDays.Should().HaveCount(4);
        priya.UserProfile.Preferences.Constraints.Should().Contain(
            c => c.Contains("4 run days", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Priya_HasNeverBefore7AmConstraint()
    {
        // Arrange & Act
        var priya = TestProfiles.Priya();

        // Assert
        priya.UserProfile.Preferences.Constraints.Should().Contain(
            c => c.Contains("7:00 AM", StringComparison.OrdinalIgnoreCase) ||
                 c.Contains("7am", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Priya_Has3WeeksTrainingHistory()
    {
        // Arrange & Act
        var priya = TestProfiles.Priya();

        // Assert — 4 workouts per week x 3 weeks = 12
        priya.TrainingHistory.Should().HaveCount(
            12,
            because: "Priya has exactly 4 workouts per week for 3 weeks");
    }

    [Fact]
    public void Priya_TrainingHistory_Exactly4WorkoutsPerWeek()
    {
        // Arrange
        var priya = TestProfiles.Priya();
        var today = new DateOnly(2026, 3, 21);

        // Act — group workouts by which training week they belong to
        // Training weeks start at today - 21, today - 14, today - 7
        var weekGroups = priya.TrainingHistory
            .GroupBy(w => (today.DayNumber - w.Date.DayNumber) / 7)
            .ToList();

        // Assert
        weekGroups.Should().HaveCount(3, because: "3 weeks of training");
        weekGroups.Should().OnlyContain(
            g => g.Count() == 4,
            because: "Priya runs exactly 4 days per week");
    }

    [Fact]
    public void AllProfiles_HaveGoalState()
    {
        // Arrange & Act
        var all = TestProfiles.All;

        // Assert
        foreach (var (name, profile) in all)
        {
            profile.GoalState.Should().NotBeNull($"profile '{name}' must have a GoalState");
            profile.GoalState.CurrentFitnessEstimate.Should().NotBeNull(
                $"profile '{name}' must have a FitnessEstimate");
        }
    }

    [Fact]
    public void AllProfiles_HaveTrainingPaces()
    {
        // Arrange & Act
        var all = TestProfiles.All;

        // Assert
        foreach (var (name, profile) in all)
        {
            profile.GoalState.CurrentFitnessEstimate.TrainingPaces.Should().NotBeNull(
                $"profile '{name}' must have TrainingPaces");

            profile.GoalState.CurrentFitnessEstimate.TrainingPaces.EasyPaceRange.Should().NotBeNull(
                $"profile '{name}' must have at least an easy pace range");
        }
    }

    [Theory]
    [InlineData("lee")]
    [InlineData("maria")]
    [InlineData("james")]
    [InlineData("priya")]
    public void ProfilesWithRaceHistory_HaveComputedVdot(string name)
    {
        // Arrange & Act
        var profile = TestProfiles.All[name];

        // Assert
        profile.GoalState.CurrentFitnessEstimate.EstimatedVdot.Should().NotBeNull(
            $"profile '{name}' has race history and should have a computed VDOT");
    }

    [Theory]
    [InlineData("lee")]
    [InlineData("maria")]
    [InlineData("james")]
    [InlineData("priya")]
    public void ProfilesWithRaceHistory_HaveVdotDerivedPaces(string name)
    {
        // Arrange
        var profile = TestProfiles.All[name];
        var expectedPaces = _paceCalc.CalculatePaces(
            profile.GoalState.CurrentFitnessEstimate.EstimatedVdot!.Value);

        // Act
        var actualPaces = profile.GoalState.CurrentFitnessEstimate.TrainingPaces;

        // Assert
        actualPaces.Should().Be(
            expectedPaces,
            $"profile '{name}' paces should be derived from its VDOT via PaceCalculator");
    }

    [Theory]
    [InlineData("lee")]
    [InlineData("maria")]
    [InlineData("james")]
    [InlineData("priya")]
    public void NonBeginnerProfiles_HaveTrainingHistory(string name)
    {
        // Arrange & Act
        var profile = TestProfiles.All[name];

        // Assert
        profile.TrainingHistory.Should().NotBeEmpty(
            $"profile '{name}' should have simulated training history");
    }

    [Fact]
    public void TrainingHistory_HasValidDatesInPast()
    {
        // Arrange
        var today = new DateOnly(2026, 3, 21);
        var all = TestProfiles.All;

        // Act & Assert
        foreach (var (name, profile) in all)
        {
            foreach (var workout in profile.TrainingHistory)
            {
                workout.Date.Should().BeBefore(
                    today,
                    $"all training history for '{name}' should be in the past");
            }
        }
    }

    [Fact]
    public void TrainingHistory_HasPositiveDistanceAndDuration()
    {
        // Arrange & Act
        var all = TestProfiles.All;

        // Assert
        foreach (var (name, profile) in all)
        {
            foreach (var workout in profile.TrainingHistory)
            {
                workout.DistanceKm.Should().BeGreaterThan(
                    0,
                    $"workout on {workout.Date} for '{name}' should have positive distance");

                workout.DurationMinutes.Should().BeGreaterThan(
                    0,
                    $"workout on {workout.Date} for '{name}' should have positive duration");
            }
        }
    }
}
