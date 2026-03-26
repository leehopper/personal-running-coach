namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// A target race the runner is training for.
/// </summary>
public sealed record RaceGoal(
    string? RaceName,
    string Distance,
    DateOnly RaceDate,
    TimeSpan? TargetTime,
    string Priority);
