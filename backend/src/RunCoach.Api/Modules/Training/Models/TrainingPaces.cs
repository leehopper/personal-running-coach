namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// Training pace zones derived from a pace-zone index calculation.
/// All single-point zones are nullable so beginner profiles without race history
/// can omit speed-zone targets while still carrying an easy-pace range.
/// </summary>
public sealed record TrainingPaces(
    PaceRange? EasyPaceRange,
    Pace? MarathonPace,
    Pace? ThresholdPace,
    Pace? IntervalPace,
    Pace? RepetitionPace,
    Pace? FastRepetitionPace = null);
