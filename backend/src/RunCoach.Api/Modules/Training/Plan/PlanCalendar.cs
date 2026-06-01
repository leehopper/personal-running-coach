namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Deterministic calendar mapping between a plan's <c>PlanStartDate</c> anchor
/// (week 1, day 0 = the Sunday the plan begins) and concrete run dates. Pure,
/// stateless, and timezone-free: MVP-0 treats <c>PlanStartDate</c> and a run's
/// <c>OccurredOn</c> as plain local training dates (slice-2b spec § Technical
/// Considerations — single-timezone solo user, deliberately not over-engineered).
/// </summary>
public static class PlanCalendar
{
    /// <summary>Number of calendar days in a training week.</summary>
    private const int DaysPerWeek = 7;

    /// <summary>
    /// Returns the Sunday on or before <paramref name="date"/> — the calendar
    /// anchor for a plan generated (or regenerated) on that date. Day 0 of every
    /// training week is Sunday, matching
    /// <see cref="Coaching.Models.Structured.WorkoutOutput.DayOfWeek"/> (0 = Sunday).
    /// Idempotent when <paramref name="date"/> already falls on a Sunday.
    /// </summary>
    /// <param name="date">The generation (or regeneration) date.</param>
    /// <returns>The Sunday that opens the week containing <paramref name="date"/>.</returns>
    public static DateOnly StartOfTrainingWeek(DateOnly date)
    {
        // System.DayOfWeek numbers Sunday as 0, so the enum value is exactly the
        // count of days to step back to reach this week's Sunday.
        var daysSinceSunday = (int)date.DayOfWeek;
        return date.AddDays(-daysSinceSunday);
    }

    /// <summary>
    /// Maps a run's date to its 1-based <see cref="PlanSlot"/> within a plan
    /// anchored at <paramref name="planStartDate"/> and spanning
    /// <paramref name="weekCount"/> generated weeks:
    /// <c>weekNumber = floor((occurredOn - planStartDate).Days / 7) + 1</c>,
    /// <c>dayOfWeek = (int)occurredOn.DayOfWeek</c>. Returns <see langword="null"/>
    /// (off-plan) when <paramref name="occurredOn"/> falls before the plan start
    /// or beyond the generated weeks.
    /// </summary>
    /// <param name="planStartDate">The plan's week-1, day-0 anchor (a Sunday).</param>
    /// <param name="occurredOn">The date the run actually occurred.</param>
    /// <param name="weekCount">The number of weeks the plan spans (1-based count).</param>
    /// <returns>The resolved slot, or <see langword="null"/> when off-plan.</returns>
    public static PlanSlot? ResolveSlot(DateOnly planStartDate, DateOnly occurredOn, int weekCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weekCount);

        if (occurredOn < planStartDate)
        {
            return null;
        }

        // occurredOn >= planStartDate, so the offset is non-negative and integer
        // division floors as the spec requires.
        var dayOffset = occurredOn.DayNumber - planStartDate.DayNumber;
        var weekNumber = (dayOffset / DaysPerWeek) + 1;
        if (weekNumber > weekCount)
        {
            return null;
        }

        return new PlanSlot(weekNumber, (int)occurredOn.DayOfWeek);
    }
}
