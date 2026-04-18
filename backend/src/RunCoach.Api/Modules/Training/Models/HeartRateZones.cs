namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// Heart-rate zone bands derived from max HR and optional resting HR.
/// Boundaries are in beats per minute (bpm), rounded to the nearest integer.
/// Repetition is always null — Daniels assigns no HR target to R-zone workouts.
/// </summary>
public sealed record HeartRateZones(
    IntRange Easy,
    IntRange Marathon,
    IntRange Threshold,
    IntRange Interval,
    IntRange? Repetition);
