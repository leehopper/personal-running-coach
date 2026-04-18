namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// An assessment of the runner's current fitness level, including
/// estimated pace-zone index, derived training paces, and assessment basis.
/// </summary>
public sealed record FitnessEstimate(
    decimal? EstimatedPaceZoneIndex,
    TrainingPaces TrainingPaces,
    string FitnessLevel,
    string AssessmentBasis,
    DateOnly AssessedOn);
