namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// Represents a pace range with minimum and maximum pace per kilometer.
/// </summary>
public sealed record PaceRange(TimeSpan MinPerKm, TimeSpan MaxPerKm);
