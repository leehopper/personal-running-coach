namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// The deterministic horizon decision for a plan generation. Either the plan is
/// <em>anchored</em> to a future target event (race week = the final week,
/// <see cref="TargetTotalWeeks"/>) or it is <em>not</em> (no event, an event in the
/// past / current week, or an event beyond the plannable ceiling — the general-fitness
/// horizon, where the validator skips event anchoring). Construct via
/// <see cref="NoAnchor"/> / <see cref="Anchored"/>.
/// </summary>
public sealed record PlanHorizon
{
    private PlanHorizon(bool isAnchored, DateOnly? raceDate, int? targetTotalWeeks)
    {
        IsAnchored = isAnchored;
        RaceDate = raceDate;
        TargetTotalWeeks = targetTotalWeeks;
    }

    /// <summary>Gets a value indicating whether the plan anchors to a target event.</summary>
    public bool IsAnchored { get; }

    /// <summary>Gets the target event date when <see cref="IsAnchored"/>; null otherwise.</summary>
    public DateOnly? RaceDate { get; }

    /// <summary>
    /// Gets the required total plan weeks (= race-week index) when <see cref="IsAnchored"/>;
    /// null otherwise.
    /// </summary>
    public int? TargetTotalWeeks { get; }

    /// <summary>The non-anchored horizon (general-fitness behavior).</summary>
    public static PlanHorizon NoAnchor() => new(isAnchored: false, raceDate: null, targetTotalWeeks: null);

    /// <summary>The anchored horizon for a future event.</summary>
    public static PlanHorizon Anchored(DateOnly raceDate, int targetTotalWeeks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(targetTotalWeeks, 2);
        return new PlanHorizon(isAnchored: true, raceDate: raceDate, targetTotalWeeks: targetTotalWeeks);
    }
}
