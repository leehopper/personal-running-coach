namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// An assessment of the runner's current fitness level, including
/// estimated VDOT, derived training paces, and assessment basis.
/// </summary>
public sealed record FitnessEstimate(
    decimal? EstimatedVdot,
    TrainingPaces TrainingPaces,
    string FitnessLevel,
    string AssessmentBasis,
    DateOnly AssessedOn);
