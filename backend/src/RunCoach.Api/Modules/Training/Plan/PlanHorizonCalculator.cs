namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Pure, stateless computation of a <see cref="PlanHorizon"/> from the plan's Sunday
/// anchor and an optional target-event date. Mirrors the deterministic week arithmetic of
/// <see cref="PlanCalendar"/>: race-week index = floor((raceDate - planStartDate) / 7) + 1.
/// No LLM, no I/O — the horizon is structured data, computed here, then both pinned into
/// the macro prompt and enforced by <c>MacroPlanOutputValidator</c>.
/// </summary>
public static class PlanHorizonCalculator
{
    /// <summary>
    /// Maximum plannable horizon. An event further out falls back to the general-fitness
    /// horizon (no anchoring); the above-macro multi-macrocycle concept is a deferred
    /// follow-up.
    /// </summary>
    public const int MaxAnchorWeeks = 52;

    private const int DaysPerWeek = 7;

    /// <summary>
    /// Computes the horizon. Returns <see cref="PlanHorizon.NoAnchor"/> when
    /// <paramref name="raceDate"/> is null, before the plan start, inside the current
    /// training week (week 1), or beyond <see cref="MaxAnchorWeeks"/>. Otherwise returns
    /// <see cref="PlanHorizon.Anchored"/> with the race-week index as the target total weeks.
    /// </summary>
    /// <param name="planStartDate">The plan's week-1 day-0 anchor (a Sunday).</param>
    /// <param name="raceDate">The target event date, or null when none exists.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="planStartDate"/> is not a Sunday (the week-1 day-0 anchor).
    /// </exception>
    public static PlanHorizon Compute(DateOnly planStartDate, DateOnly? raceDate)
    {
        if (planStartDate.DayOfWeek != DayOfWeek.Sunday)
        {
            throw new ArgumentException(
                $"planStartDate must be a Sunday (week 1, day 0); got {planStartDate:O} ({planStartDate.DayOfWeek}).",
                nameof(planStartDate));
        }

        if (raceDate is null || raceDate.Value < planStartDate)
        {
            return PlanHorizon.NoAnchor();
        }

        var dayOffset = raceDate.Value.DayNumber - planStartDate.DayNumber;
        var raceWeek = (dayOffset / DaysPerWeek) + 1;

        if (raceWeek <= 1 || raceWeek > MaxAnchorWeeks)
        {
            return PlanHorizon.NoAnchor();
        }

        return PlanHorizon.Anchored(raceDate.Value, raceWeek);
    }
}
