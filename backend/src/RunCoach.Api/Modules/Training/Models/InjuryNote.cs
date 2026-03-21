namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// A recorded injury or physical issue in the runner's history.
/// </summary>
public sealed record InjuryNote(
    string Description,
    DateOnly DateReported,
    string Status);
