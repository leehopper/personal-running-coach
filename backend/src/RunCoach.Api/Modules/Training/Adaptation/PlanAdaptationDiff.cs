namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// The structured before/after payload an adaptation carries (DEC-079; resolves the
/// Unit 2 open question in favor of structured data over a rendered diff string):
/// per-workout micro-week edits (<see cref="WorkoutChanges"/>) plus per-week meso
/// volume-target edits (<see cref="WeeklyTargetChanges"/>). Computed deterministically
/// from the projection delta so the "Show what changed" panel renders from structured
/// data, never parsed prose. Both collections are non-null; an absorb adaptation never
/// produces an event, so a persisted diff always carries at least one change in
/// practice.
/// </summary>
/// <param name="WorkoutChanges">The current-week micro workout swaps.</param>
/// <param name="WeeklyTargetChanges">The upcoming-week meso volume-target edits.</param>
public sealed record PlanAdaptationDiff(
    IReadOnlyList<WorkoutChange> WorkoutChanges,
    IReadOnlyList<WeeklyTargetChange> WeeklyTargetChanges)
{
    /// <summary>Gets an empty diff (no changes) — the canonical "nothing applied" value.</summary>
    public static PlanAdaptationDiff Empty { get; } = new([], []);
}
