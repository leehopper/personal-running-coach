using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Tests.Modules.Coaching.Models.Structured;

public sealed class MacroPlanOutputValidatorTests
{
    private static readonly WorkoutType[] EasyWorkout = [WorkoutType.Easy];

    [Fact]
    public void Validate_PhaseWeeksSumMismatch_RejectsRegardlessOfAnchor()
    {
        // Arrange — TotalWeeks 12 but phases sum to 10.
        var macro = BuildMacro(totalWeeks: 12, 6, 4);

        // Act
        var actual = MacroPlanOutputValidator.Validate(macro, PlanHorizon.NoAnchor());

        // Assert
        actual.IsValid.Should().BeFalse();
        actual.Violation.Should().Be(MacroPlanOutputValidationViolation.PhaseSumMismatch);
    }

    [Fact]
    public void Validate_LiveShape_SixteenWeeksForNineWeekRace_RejectsHorizon()
    {
        // Arrange — the recorded live finding: TotalWeeks 16, race week 9.
        var macro = BuildMacro(totalWeeks: 16, 8, 4, 2, 2);
        var horizon = PlanHorizon.Anchored(new DateOnly(2026, 8, 8), targetTotalWeeks: 9);

        // Act
        var actual = MacroPlanOutputValidator.Validate(macro, horizon);

        // Assert
        actual.IsValid.Should().BeFalse();
        actual.Violation.Should().Be(MacroPlanOutputValidationViolation.HorizonMismatch);
    }

    [Theory]
    [InlineData(9)] // exact
    [InlineData(8)] // one week short
    [InlineData(10)] // one week long
    public void Validate_WithinOneWeekOfTarget_Passes(int totalWeeks)
    {
        var macro = BuildMacro(totalWeeks);
        var horizon = PlanHorizon.Anchored(new DateOnly(2026, 8, 8), targetTotalWeeks: 9);

        MacroPlanOutputValidator.Validate(macro, horizon).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(7)] // two short
    [InlineData(11)] // two long
    public void Validate_BeyondOneWeekOfTarget_Rejects(int totalWeeks)
    {
        var macro = BuildMacro(totalWeeks);
        var horizon = PlanHorizon.Anchored(new DateOnly(2026, 8, 8), targetTotalWeeks: 9);

        var actual = MacroPlanOutputValidator.Validate(macro, horizon);

        actual.IsValid.Should().BeFalse();
        actual.Violation.Should().Be(MacroPlanOutputValidationViolation.HorizonMismatch);
    }

    [Fact]
    public void Validate_NoAnchor_ConsistentPhases_Passes()
    {
        var macro = BuildMacro(totalWeeks: 16, 10, 6);

        MacroPlanOutputValidator.Validate(macro, PlanHorizon.NoAnchor()).IsValid.Should().BeTrue();
    }

    // Builds a macro whose phases sum to totalWeeks, or to the explicit phaseWeeks when given.
    private static MacroPlanOutput BuildMacro(int totalWeeks, params int[] phaseWeeks)
    {
        var weeks = phaseWeeks.Length > 0 ? phaseWeeks : new[] { totalWeeks };
        var phases = weeks.Select(w => new PlanPhaseOutput
        {
            PhaseType = PhaseType.Base,
            Weeks = w,
            WeeklyDistanceStartKm = 30,
            WeeklyDistanceEndKm = 40,
            IntensityDistribution = "80/20",
            AllowedWorkoutTypes = EasyWorkout,
            TargetPaceEasySecPerKm = 360,
            TargetPaceFastSecPerKm = 300,
            Notes = "n",
            IncludesDeload = false,
        }).ToArray();

        return new MacroPlanOutput
        {
            TotalWeeks = totalWeeks,
            GoalDescription = "g",
            Phases = phases,
            Rationale = "r",
            Warnings = "w",
        };
    }
}
