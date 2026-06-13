using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Plan.Models;

namespace RunCoach.Api.Tests.Modules.Training.Adaptation;

/// <summary>
/// Unit tests for <see cref="RestructureConsistencyCheck"/> (slice 3B F4): a
/// restructure that revises the current week's weekly target must make that target
/// equal the total distance of the week's RESULTING workouts (the sparse revisions
/// applied over the untouched days). The check is exact (whole km) and a no-op pass
/// when the proposal does not revise the current week's target or the week carries no
/// materialized micro detail.
/// </summary>
public sealed class RestructureConsistencyCheckTests
{
    [Fact]
    public void Evaluate_ProposedTargetMatchingOnlyEditedWorkouts_IsInconsistent()
    {
        // Arrange — the live-pass repro: the current week is Mon 7 / Wed 8 / Thu 6 /
        //   Sat 14; the proposal edits Mon->4, Wed->8, Sat->12 and leaves Thursday's
        //   6 km untouched, then sets the week target to 24 (= 4+8+12, the edited
        //   workouts only). The real week sums to 30, so the target contradicts it.
        var plan = Plan(1, Workout(1, 7), Workout(3, 8), Workout(4, 6), Workout(6, 14));
        var proposal = Proposal(
            targets: [Target(1, 24)],
            revised: [Workout(1, 4), Workout(3, 8), Workout(6, 12)]);

        // Act
        var actual = RestructureConsistencyCheck.Evaluate(proposal, plan, currentWeekNumber: 1);

        // Assert
        actual.IsConsistent.Should().BeFalse();
        actual.ProposedWeeklyTargetKm.Should().Be(24);
        actual.ResultingWorkoutSumKm.Should().Be(30);
    }

    [Fact]
    public void Evaluate_ProposedTargetMatchingTheResultingWeek_IsConsistent()
    {
        // Arrange — same edits, but the target accounts for the untouched Thursday.
        var plan = Plan(1, Workout(1, 7), Workout(3, 8), Workout(4, 6), Workout(6, 14));
        var proposal = Proposal(
            targets: [Target(1, 30)],
            revised: [Workout(1, 4), Workout(3, 8), Workout(6, 12)]);

        // Act
        var actual = RestructureConsistencyCheck.Evaluate(proposal, plan, currentWeekNumber: 1);

        // Assert
        actual.IsConsistent.Should().BeTrue();
        actual.ProposedWeeklyTargetKm.Should().Be(30);
        actual.ResultingWorkoutSumKm.Should().Be(30);
    }

    [Fact]
    public void Evaluate_TargetEditWithNoWorkoutEdits_ChecksAgainstTheUnchangedWeek()
    {
        // Arrange — the proposal cuts the week target to 24 km but revises no
        //   workouts, so the unchanged 35 km week contradicts the proposed target.
        var plan = Plan(1, Workout(1, 7), Workout(3, 8), Workout(4, 6), Workout(6, 14));
        var proposal = Proposal(targets: [Target(1, 24)], revised: []);

        // Act
        var actual = RestructureConsistencyCheck.Evaluate(proposal, plan, currentWeekNumber: 1);

        // Assert
        actual.IsConsistent.Should().BeFalse();
        actual.ResultingWorkoutSumKm.Should().Be(35);
    }

    [Fact]
    public void Evaluate_FullWeekRevisionSummingToTheTarget_IsConsistent()
    {
        // Arrange — every current-week day is revised and the target equals their sum.
        var plan = Plan(1, Workout(1, 7), Workout(3, 8), Workout(6, 14));
        var proposal = Proposal(
            targets: [Target(1, 20)],
            revised: [Workout(1, 5), Workout(3, 6), Workout(6, 9)]);

        // Act
        var actual = RestructureConsistencyCheck.Evaluate(proposal, plan, currentWeekNumber: 1);

        // Assert
        actual.IsConsistent.Should().BeTrue();
        actual.ResultingWorkoutSumKm.Should().Be(20);
    }

    [Fact]
    public void Evaluate_NoCurrentWeekTargetEdit_IsNotApplicable()
    {
        // Arrange — the proposal only revises upcoming weeks' targets; there is no
        //   current-week target to contradict.
        var plan = Plan(1, Workout(1, 7), Workout(3, 8));
        var proposal = Proposal(
            targets: [Target(2, 24), Target(3, 28)],
            revised: [Workout(1, 4)]);

        // Act
        var actual = RestructureConsistencyCheck.Evaluate(proposal, plan, currentWeekNumber: 1);

        // Assert
        actual.Should().Be(RestructureConsistencyResult.NotApplicable);
    }

    [Fact]
    public void Evaluate_CurrentWeekHasNoMaterializedMicroDetail_IsNotApplicable()
    {
        // Arrange — the triggering log is in week 2, but only week 1 carries micro
        //   detail at MVP-0, so there is no resulting week to sum.
        var plan = Plan(1, Workout(1, 7), Workout(3, 8));
        var proposal = Proposal(targets: [Target(2, 24)], revised: []);

        // Act
        var actual = RestructureConsistencyCheck.Evaluate(proposal, plan, currentWeekNumber: 2);

        // Assert
        actual.Should().Be(RestructureConsistencyResult.NotApplicable);
    }

    [Fact]
    public void Evaluate_DuplicateCurrentWeekTargetEdits_TakesTheLastWins()
    {
        // Arrange — two edits target the current week; the later one is checked,
        //   mirroring the diff calculator's last-wins upsert.
        var plan = Plan(1, Workout(1, 10), Workout(3, 10));
        var proposal = Proposal(
            targets: [Target(1, 99), Target(1, 20)],
            revised: []);

        // Act
        var actual = RestructureConsistencyCheck.Evaluate(proposal, plan, currentWeekNumber: 1);

        // Assert — 20 km (the last edit) against the unchanged 20 km week.
        actual.IsConsistent.Should().BeTrue();
        actual.ProposedWeeklyTargetKm.Should().Be(20);
    }

    private static PlanProjectionDto Plan(int weekNumber, params WorkoutOutput[] existing) =>
        new()
        {
            PlanId = Guid.NewGuid(),
            MicroWorkoutsByWeek = new Dictionary<int, MicroWorkoutListOutput>
            {
                [weekNumber] = new() { Workouts = existing },
            },
        };

    private static RestructurePlan Proposal(WeeklyTargetEdit[] targets, WorkoutOutput[] revised) =>
        new()
        {
            RevisedWeeklyTargets = targets,
            RevisedCurrentWeekWorkouts = revised,
            ForwardPath = "Hold this week, then ramp back up.",
        };

    private static WeeklyTargetEdit Target(int weekNumber, int weeklyTargetKm) =>
        new() { WeekNumber = weekNumber, WeeklyTargetKm = weeklyTargetKm };

    private static WorkoutOutput Workout(int dayOfWeek, int targetDistanceKm) =>
        new()
        {
            DayOfWeek = dayOfWeek,
            WorkoutType = WorkoutType.Easy,
            Title = "Workout",
            TargetDistanceKm = targetDistanceKm,
            TargetDurationMinutes = 40,
            TargetPaceEasySecPerKm = 330,
            TargetPaceFastSecPerKm = 280,
            Segments = [],
            WarmupNotes = string.Empty,
            CooldownNotes = string.Empty,
            CoachingNotes = string.Empty,
            PerceivedEffort = 4,
        };
}
