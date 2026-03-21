namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// Training pace zones derived from VDOT calculation.
/// </summary>
public sealed record TrainingPaces(
    PaceRange EasyPaceRange,
    TimeSpan? MarathonPace,
    TimeSpan? ThresholdPace,
    TimeSpan? IntervalPace,
    TimeSpan? RepetitionPace);
