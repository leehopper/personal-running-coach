namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// A recorded race result used for fitness estimation and VDOT calculation.
/// </summary>
public sealed record RaceTime(
    string Distance,
    TimeSpan Time,
    DateOnly Date,
    string? Conditions);
