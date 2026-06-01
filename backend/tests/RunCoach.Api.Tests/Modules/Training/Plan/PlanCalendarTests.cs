using FluentAssertions;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Tests.Modules.Training.Plan;

/// <summary>
/// Pure-function tests over <see cref="PlanCalendar"/> — the deterministic
/// <c>PlanStartDate</c> calendar anchor (slice-2b Unit 1 / DEC-076). Covers the
/// start-of-week (Sunday) computation that anchors a generated plan and the
/// <c>OccurredOn → (week, day)</c> slot mapping that Unit 3 uses for
/// server-authoritative prescription snapshotting. No Marten or database.
/// </summary>
public sealed class PlanCalendarTests
{
    [Theory]
    [InlineData(2026, 6, 10, 2026, 6, 7)] // Wednesday → preceding Sunday
    [InlineData(2026, 6, 7, 2026, 6, 7)] // Sunday → itself (idempotent)
    [InlineData(2026, 6, 13, 2026, 6, 7)] // Saturday (end of week) → that week's Sunday
    [InlineData(2026, 6, 23, 2026, 6, 21)] // Tuesday of the next-but-one week → 2026-06-21 Sunday
    public void StartOfTrainingWeek_ReturnsSundayOnOrBeforeDate(
        int year,
        int month,
        int day,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        // Arrange
        var date = new DateOnly(year, month, day);
        var expectedStart = new DateOnly(expectedYear, expectedMonth, expectedDay);

        // Act
        var actualStart = PlanCalendar.StartOfTrainingWeek(date);

        // Assert
        actualStart.DayOfWeek.Should().Be(DayOfWeek.Sunday, because: "every training week opens on a Sunday");
        actualStart.Should().Be(expectedStart);
    }

    [Theory]
    [InlineData(2026, 6, 7, 8, 1, 0)] // first day → week 1, Sunday
    [InlineData(2026, 6, 18, 8, 2, 4)] // 11 days in → week 2, Thursday
    [InlineData(2026, 6, 13, 8, 1, 6)] // last day of week 1 → week 1, Saturday
    [InlineData(2026, 6, 14, 8, 2, 0)] // first day of week 2 → week 2, Sunday
    [InlineData(2026, 7, 26, 8, 8, 0)] // 49 days in → week 8 (final week), Sunday
    public void ResolveSlot_OnPlanDate_ReturnsExpectedWeekAndDay(
        int occurredYear,
        int occurredMonth,
        int occurredDay,
        int weekCount,
        int expectedWeek,
        int expectedDay)
    {
        // Arrange
        var planStartDate = new DateOnly(2026, 6, 7);
        var occurredOn = new DateOnly(occurredYear, occurredMonth, occurredDay);

        // Act
        var actualSlot = PlanCalendar.ResolveSlot(planStartDate, occurredOn, weekCount);

        // Assert
        actualSlot.Should().NotBeNull();
        actualSlot!.Value.WeekNumber.Should().Be(expectedWeek);
        actualSlot.Value.DayOfWeek.Should().Be(expectedDay);
    }

    [Theory]
    [InlineData(2026, 6, 5)] // before plan start (2026-06-05 < 2026-06-07) → off-plan
    [InlineData(2026, 8, 23)] // 77 days in → week 12, beyond the 8 generated weeks → off-plan
    public void ResolveSlot_OffPlanDate_ReturnsNull(int year, int month, int day)
    {
        // Arrange — an 8-week plan anchored at 2026-06-07.
        var planStartDate = new DateOnly(2026, 6, 7);
        var occurredOn = new DateOnly(year, month, day);
        const int weekCount = 8;

        // Act
        var actualSlot = PlanCalendar.ResolveSlot(planStartDate, occurredOn, weekCount);

        // Assert
        actualSlot.Should().BeNull();
    }

    [Fact]
    public void ResolveSlot_DayBeyondFinalWeek_ReturnsNull()
    {
        // Arrange — week 9 of an 8-week plan is exactly one week past the end.
        var planStartDate = new DateOnly(2026, 6, 7);
        var occurredOn = planStartDate.AddDays(8 * 7); // first day of week 9
        const int weekCount = 8;

        // Act
        var actualSlot = PlanCalendar.ResolveSlot(planStartDate, occurredOn, weekCount);

        // Assert
        actualSlot.Should().BeNull();
    }

    [Fact]
    public void ResolveSlot_NonPositiveWeekCount_Throws()
    {
        // Arrange
        var planStartDate = new DateOnly(2026, 6, 7);
        var occurredOn = new DateOnly(2026, 6, 7);

        // Act
        var act = () => PlanCalendar.ResolveSlot(planStartDate, occurredOn, weekCount: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ResolveSlot_NonSundayPlanStartDate_Throws()
    {
        // Arrange — a Wednesday anchor violates the week-1/day-0 = Sunday invariant
        // that the week/day math relies on. The guard surfaces this loudly instead
        // of returning a plausible-but-wrong slot (e.g. for a defaulted 0001-01-01).
        var planStartDate = new DateOnly(2026, 6, 10); // Wednesday
        var occurredOn = new DateOnly(2026, 6, 10);

        // Act
        var act = () => PlanCalendar.ResolveSlot(planStartDate, occurredOn, weekCount: 8);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
