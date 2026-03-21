namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// A generic numeric range with minimum and maximum decimal values.
/// Used for distance ranges, volume targets, and similar bounded quantities.
/// </summary>
public sealed record DecimalRange(decimal Min, decimal Max);
