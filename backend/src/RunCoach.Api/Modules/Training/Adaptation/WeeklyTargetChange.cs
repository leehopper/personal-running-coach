namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// A before/after change to one meso week's volume target
/// (<c>MesoWeekOutput.WeeklyTargetKm</c>), keyed by 1-based <see cref="WeekNumber"/>.
/// The projection applies <see cref="AfterWeeklyTargetKm"/>; both values feed the
/// panel's before/after diff. Part of the structured <see cref="PlanAdaptationDiff"/>
/// (Unit 2) — this is the multi-week surface an adaptation edits, since today only
/// week 1 carries micro detail.
/// </summary>
/// <param name="WeekNumber">The 1-based plan week whose target changed.</param>
/// <param name="BeforeWeeklyTargetKm">The weekly target in km before the adaptation.</param>
/// <param name="AfterWeeklyTargetKm">The weekly target in km after the adaptation.</param>
public sealed record WeeklyTargetChange(
    int WeekNumber,
    int BeforeWeeklyTargetKm,
    int AfterWeeklyTargetKm);
