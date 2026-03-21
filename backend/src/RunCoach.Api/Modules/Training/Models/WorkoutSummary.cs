namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// A summary of a completed workout, used for simulated training history
/// (Layer 1 per-workout data).
/// </summary>
public sealed record WorkoutSummary(
    DateOnly Date,
    string WorkoutType,
    decimal DistanceKm,
    int DurationMinutes,
    TimeSpan AveragePacePerKm,
    string? Notes);
