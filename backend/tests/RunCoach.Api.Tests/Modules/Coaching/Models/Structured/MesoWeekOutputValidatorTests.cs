using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Tests.Modules.Coaching.Models.Structured;

public sealed class MesoWeekOutputValidatorTests
{
    [Fact]
    public void Validate_MatchingWeekWithRunDay_Passes()
    {
        // Arrange
        const int expectedWeekIndex = 3;
        var meso = BuildMeso(expectedWeekIndex, includeRunDay: true);

        // Act
        var actual = MesoWeekOutputValidator.Validate(meso, expectedWeekIndex);

        // Assert
        actual.IsValid.Should().BeTrue();
        actual.Violation.Should().Be(MesoWeekOutputValidationViolation.None);
    }

    [Fact]
    public void Validate_WeekNumberMismatch_Rejects()
    {
        // Arrange — the meso payload's WeekNumber (2) disagrees with the expected target index (5).
        var meso = BuildMeso(weekNumber: 2, includeRunDay: true);
        const int expectedWeekIndex = 5;

        // Act
        var actual = MesoWeekOutputValidator.Validate(meso, expectedWeekIndex);

        // Assert
        actual.IsValid.Should().BeFalse();
        actual.Violation.Should().Be(MesoWeekOutputValidationViolation.WeekNumberMismatch);
    }

    [Fact]
    public void Validate_AllSevenSlotsRestOrCrossTrain_RejectsNoRunDay()
    {
        // Arrange — a week number that matches, but zero Run slots among the seven days.
        const int expectedWeekIndex = 4;
        var meso = BuildMesoWithoutRunDays(expectedWeekIndex);

        // Act
        var actual = MesoWeekOutputValidator.Validate(meso, expectedWeekIndex);

        // Assert
        actual.IsValid.Should().BeFalse();
        actual.Violation.Should().Be(MesoWeekOutputValidationViolation.NoRunDay);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(9)]
    public void Validate_MatchingWeekAcrossIndices_Passes(int weekIndex)
    {
        // Arrange
        var meso = BuildMeso(weekIndex, includeRunDay: true);

        // Act
        var actual = MesoWeekOutputValidator.Validate(meso, weekIndex);

        // Assert
        actual.IsValid.Should().BeTrue();
        actual.Violation.Should().Be(MesoWeekOutputValidationViolation.None);
    }

    // Builds a seven-slot week with Sunday as the sole Run slot (or Rest, when includeRunDay is
    // false) and every other day Rest — a small local fixture builder per spec 9a, independent of
    // the plan-gen test helpers.
    private static MesoWeekOutput BuildMeso(int weekNumber, bool includeRunDay)
    {
        var restSlot = new MesoDaySlotOutput { SlotType = DaySlotType.Rest, WorkoutType = null, Notes = "Rest." };
        var runSlot = new MesoDaySlotOutput { SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Easy, Notes = "Easy aerobic." };

        return new MesoWeekOutput
        {
            WeekNumber = weekNumber,
            PhaseType = PhaseType.Base,
            WeeklyTargetKm = 40,
            IsDeloadWeek = false,
            Sunday = includeRunDay ? runSlot : restSlot,
            Monday = restSlot,
            Tuesday = restSlot,
            Wednesday = restSlot,
            Thursday = restSlot,
            Friday = restSlot,
            Saturday = restSlot,
            WeekSummary = $"Week {weekNumber}.",
        };
    }

    // Builds a seven-slot week with only Rest and CrossTrain slots — no Run slot anywhere — so the
    // NoRunDay check fires even though CrossTrain days are present.
    private static MesoWeekOutput BuildMesoWithoutRunDays(int weekNumber)
    {
        var restSlot = new MesoDaySlotOutput { SlotType = DaySlotType.Rest, WorkoutType = null, Notes = "Rest." };
        var crossTrainSlot = new MesoDaySlotOutput { SlotType = DaySlotType.CrossTrain, WorkoutType = null, Notes = "Cross-train." };

        return new MesoWeekOutput
        {
            WeekNumber = weekNumber,
            PhaseType = PhaseType.Base,
            WeeklyTargetKm = 20,
            IsDeloadWeek = false,
            Sunday = crossTrainSlot,
            Monday = restSlot,
            Tuesday = crossTrainSlot,
            Wednesday = restSlot,
            Thursday = crossTrainSlot,
            Friday = restSlot,
            Saturday = crossTrainSlot,
            WeekSummary = $"Week {weekNumber}.",
        };
    }
}
