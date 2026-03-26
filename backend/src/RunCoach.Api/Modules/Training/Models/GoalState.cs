namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// The runner's current training goal, including optional race target
/// and current fitness estimate.
/// </summary>
public sealed record GoalState(
    string GoalType,
    RaceGoal? TargetRace,
    FitnessEstimate CurrentFitnessEstimate);
