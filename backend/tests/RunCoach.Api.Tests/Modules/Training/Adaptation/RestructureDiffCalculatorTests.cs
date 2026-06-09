using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Plan.Models;

namespace RunCoach.Api.Tests.Modules.Training.Adaptation;

/// <summary>
/// Unit tests for <see cref="RestructureDiffCalculator"/> (Slice 3 § Unit 5,
/// DEC-079): the deterministic projection-space mapping from a validated LLM
/// <see cref="RestructurePlan"/> proposal to the <see cref="PlanAdaptationDiff"/>
/// the event carries. The calculator must only emit changes the projection's
/// apply method will honor: 1-based weeks only, current-week micro edits only,
/// no synthesized weeks, and never a removal.
/// </summary>
public sealed class RestructureDiffCalculatorTests
{
    [Fact]
    public void Calculate_MapsWeeklyTargetEditsAndCurrentWeekWorkoutEditsIntoTheDiff()
    {
        // Arrange — week 2's target drops 30 -> 24 and Tuesday (day 2) of the
        //   current week is revised from Tempo to Easy.
        var plan = BuildPlan();
        var revisedTuesday = BuildWorkout(2, WorkoutType.Easy);
        var proposal = BuildProposal(
            targets: [new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = 24 }],
            workouts: [revisedTuesday]);

        // Act
        var actual = RestructureDiffCalculator.Calculate(proposal, plan, currentWeekNumber: 1);

        // Assert
        var expectedTargetChange = new WeeklyTargetChange(2, 30, 24);
        actual.WeeklyTargetChanges.Should().ContainSingle().Which.Should().Be(expectedTargetChange);
        var workoutChange = actual.WorkoutChanges.Should().ContainSingle().Subject;
        workoutChange.WeekNumber.Should().Be(1);
        workoutChange.DayOfWeek.Should().Be(2);
        workoutChange.Before.Should().Be(plan.MicroWorkoutsByWeek[1].Workouts[1]);
        workoutChange.After.Should().Be(revisedTuesday);
    }

    [Fact]
    public void Calculate_WithAnEmptyProposal_ReturnsAnEmptyDiff()
    {
        // Act
        var actual = RestructureDiffCalculator.Calculate(BuildProposal(), BuildPlan(), currentWeekNumber: 1);

        // Assert
        actual.WorkoutChanges.Should().BeEmpty();
        actual.WeeklyTargetChanges.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_DropsTargetEditsForWeeksTheMesoTierNeverEmitted()
    {
        // Arrange — week 9 does not exist in MesoWeeks: the projection would
        //   skip it, so the diff must not claim it changed.
        var proposal = BuildProposal(
            targets: [new WeeklyTargetEdit { WeekNumber = 9, WeeklyTargetKm = 10 }]);

        // Act
        var actual = RestructureDiffCalculator.Calculate(proposal, BuildPlan(), currentWeekNumber: 1);

        // Assert
        actual.WeeklyTargetChanges.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_DropsNoOpTargetEditsThatLeaveTheWeekUnchanged()
    {
        // Arrange — week 3 already targets 35 km.
        var proposal = BuildProposal(
            targets: [new WeeklyTargetEdit { WeekNumber = 3, WeeklyTargetKm = 35 }]);

        // Act
        var actual = RestructureDiffCalculator.Calculate(proposal, BuildPlan(), currentWeekNumber: 1);

        // Assert
        actual.WeeklyTargetChanges.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Calculate_NeverEmitsANonOneBasedWeekNumber(int malformedWeek)
    {
        // Arrange — the projection THROWS on a week below 1 and fails the whole
        //   transaction, so a malformed LLM week index must be dropped here.
        var proposal = BuildProposal(
            targets: [new WeeklyTargetEdit { WeekNumber = malformedWeek, WeeklyTargetKm = 5 }]);

        // Act
        var actual = RestructureDiffCalculator.Calculate(proposal, BuildPlan(), currentWeekNumber: 1);

        // Assert
        actual.WeeklyTargetChanges.Should().BeEmpty();
        actual.WeeklyTargetChanges.Should().OnlyContain(change => change.WeekNumber >= 1);
    }

    [Fact]
    public void Calculate_CollapsesDuplicateWeekEditsLastWins()
    {
        // Arrange — two edits target week 2; the later one wins, mirroring the
        //   projection's sequential upsert semantics.
        var proposal = BuildProposal(
            targets:
            [
                new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = 26 },
                new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = 22 },
            ]);

        // Act
        var actual = RestructureDiffCalculator.Calculate(proposal, BuildPlan(), currentWeekNumber: 1);

        // Assert
        actual.WeeklyTargetChanges.Should().ContainSingle()
            .Which.Should().Be(new WeeklyTargetChange(2, 30, 22));
    }

    [Fact]
    public void Calculate_DropsAllWorkoutEditsWhenTheCurrentWeekCarriesNoMicroDetail()
    {
        // Arrange — only week 1 carries micro detail at MVP-0; a week-2 log's
        //   workout edits have nothing live to apply against and the adaptation
        //   never synthesizes future micro weeks.
        var proposal = BuildProposal(workouts: [BuildWorkout(2, WorkoutType.Easy)]);

        // Act
        var actual = RestructureDiffCalculator.Calculate(proposal, BuildPlan(), currentWeekNumber: 2);

        // Assert
        actual.WorkoutChanges.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_EmitsAnAdditionWithNullBeforeForADayTheWeekDidNotSchedule()
    {
        // Arrange — day 4 (Thursday) has no scheduled workout in the live week.
        var added = BuildWorkout(4, WorkoutType.Recovery);
        var proposal = BuildProposal(workouts: [added]);

        // Act
        var actual = RestructureDiffCalculator.Calculate(proposal, BuildPlan(), currentWeekNumber: 1);

        // Assert
        var change = actual.WorkoutChanges.Should().ContainSingle().Subject;
        change.Before.Should().BeNull();
        change.After.Should().Be(added);
    }

    [Fact]
    public void Calculate_NeverEmitsARemoval_DaysAbsentFromTheRevisedListAreUntouched()
    {
        // Arrange — the revised list is a sparse per-day edit set: revising only
        //   Tuesday must not emit null-After removals for the week's other days.
        var proposal = BuildProposal(workouts: [BuildWorkout(2, WorkoutType.Easy)]);

        // Act
        var actual = RestructureDiffCalculator.Calculate(proposal, BuildPlan(), currentWeekNumber: 1);

        // Assert
        actual.WorkoutChanges.Should().ContainSingle();
        actual.WorkoutChanges.Should().OnlyContain(change => change.After != null);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void Calculate_DropsWorkoutEditsWithAnOutOfRangeDayOfWeek(int malformedDay)
    {
        // Arrange
        var proposal = BuildProposal(workouts: [BuildWorkout(malformedDay, WorkoutType.Easy)]);

        // Act
        var actual = RestructureDiffCalculator.Calculate(proposal, BuildPlan(), currentWeekNumber: 1);

        // Assert
        actual.WorkoutChanges.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_CollapsesDuplicateDayEditsLastWins()
    {
        // Arrange — two revisions target day 2; the later one wins.
        var first = BuildWorkout(2, WorkoutType.Easy);
        var second = BuildWorkout(2, WorkoutType.Recovery);
        var proposal = BuildProposal(workouts: [first, second]);

        // Act
        var actual = RestructureDiffCalculator.Calculate(proposal, BuildPlan(), currentWeekNumber: 1);

        // Assert
        actual.WorkoutChanges.Should().ContainSingle()
            .Which.After.Should().Be(second);
    }

    private static RestructurePlan BuildProposal(
        WeeklyTargetEdit[]? targets = null,
        WorkoutOutput[]? workouts = null) =>
        new()
        {
            RevisedWeeklyTargets = targets ?? [],
            RevisedCurrentWeekWorkouts = workouts ?? [],
            ForwardPath = "Hold this week, then ramp back up.",
        };

    /// <summary>
    /// A plan with meso weeks 1-3 at 20/30/35 km and live micro detail for
    /// week 1 only (days 0, 2, 3, 6 scheduled — no Thursday).
    /// </summary>
    private static PlanProjectionDto BuildPlan() =>
        new()
        {
            PlanId = Guid.NewGuid(),
            MesoWeeks =
            [
                BuildMesoWeek(1, 20),
                BuildMesoWeek(2, 30),
                BuildMesoWeek(3, 35),
            ],
            MicroWorkoutsByWeek = new Dictionary<int, MicroWorkoutListOutput>
            {
                [1] = new()
                {
                    Workouts =
                    [
                        BuildWorkout(0, WorkoutType.Easy),
                        BuildWorkout(2, WorkoutType.Tempo),
                        BuildWorkout(3, WorkoutType.Easy),
                        BuildWorkout(6, WorkoutType.LongRun),
                    ],
                },
            },
        };

    private static MesoWeekOutput BuildMesoWeek(int weekNumber, int weeklyTargetKm)
    {
        var rest = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Rest,
            WorkoutType = null,
            Notes = string.Empty,
        };
        return new MesoWeekOutput
        {
            WeekNumber = weekNumber,
            PhaseType = PhaseType.Base,
            WeeklyTargetKm = weeklyTargetKm,
            IsDeloadWeek = false,
            Sunday = rest,
            Monday = rest,
            Tuesday = rest,
            Wednesday = rest,
            Thursday = rest,
            Friday = rest,
            Saturday = rest,
            WeekSummary = string.Empty,
        };
    }

    private static WorkoutOutput BuildWorkout(int dayOfWeek, WorkoutType type) =>
        new()
        {
            DayOfWeek = dayOfWeek,
            WorkoutType = type,
            Title = type.ToString(),
            TargetDistanceKm = 10,
            TargetDurationMinutes = 50,
            TargetPaceEasySecPerKm = 330,
            TargetPaceFastSecPerKm = 280,
            Segments = [],
            WarmupNotes = string.Empty,
            CooldownNotes = string.Empty,
            CoachingNotes = string.Empty,
            PerceivedEffort = 5,
        };
}
