using FluentAssertions;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Tests.Modules.Training.Plan;

public sealed class PlanHorizonCalculatorTests
{
    // planStartDate is the Sunday on/before "today". 2026-06-07 is a Sunday.
    private static readonly DateOnly PlanStart = new(2026, 6, 7);

    [Fact]
    public void Compute_NoRaceDate_ReturnsNoAnchor()
    {
        // Arrange / Act
        var actual = PlanHorizonCalculator.Compute(PlanStart, raceDate: null);

        // Assert
        actual.IsAnchored.Should().BeFalse();
        actual.TargetTotalWeeks.Should().BeNull();
    }

    [Fact]
    public void Compute_RaceNineWeeksOut_AnchorsToNineWeeks()
    {
        // Arrange — a date inside week 9 from the Sunday anchor.
        var raceDate = PlanStart.AddDays((8 * 7) + 2);

        // Act
        var actual = PlanHorizonCalculator.Compute(PlanStart, raceDate);

        // Assert
        actual.IsAnchored.Should().BeTrue();
        actual.TargetTotalWeeks.Should().Be(9);
        actual.RaceDate.Should().Be(raceDate);
    }

    [Fact]
    public void Compute_RaceInCurrentWeek_ReturnsNoAnchor()
    {
        var raceDate = PlanStart.AddDays(3); // still week 1
        PlanHorizonCalculator.Compute(PlanStart, raceDate).IsAnchored.Should().BeFalse();
    }

    [Fact]
    public void Compute_RaceInPast_ReturnsNoAnchor()
    {
        var raceDate = PlanStart.AddDays(-5);
        PlanHorizonCalculator.Compute(PlanStart, raceDate).IsAnchored.Should().BeFalse();
    }

    [Fact]
    public void Compute_RaceBeyondMaxWeeks_ReturnsNoAnchor()
    {
        var raceDate = PlanStart.AddDays(60 * 7); // 60 weeks out, beyond the 52 ceiling
        PlanHorizonCalculator.Compute(PlanStart, raceDate).IsAnchored.Should().BeFalse();
    }

    [Fact]
    public void Compute_RaceExactlyAtMaxWeeks_Anchors()
    {
        var raceDate = PlanStart.AddDays(((PlanHorizonCalculator.MaxAnchorWeeks - 1) * 7) + 1);

        var actual = PlanHorizonCalculator.Compute(PlanStart, raceDate);

        actual.IsAnchored.Should().BeTrue();
        actual.TargetTotalWeeks.Should().Be(PlanHorizonCalculator.MaxAnchorWeeks);
    }
}
