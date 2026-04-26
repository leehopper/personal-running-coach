using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Normalized answer for the PrimaryGoal topic. Captures the runner's primary training goal
/// (race training, general fitness, etc.) plus a free-text description supplied by the runner.
/// </summary>
public sealed record PrimaryGoalAnswer
{
    /// <summary>
    /// Gets the categorical primary goal selected from the closed PrimaryGoal enum.
    /// </summary>
    [Description("Categorical primary goal: RaceTraining, GeneralFitness, ReturnToRunning, BuildVolume, or BuildSpeed.")]
    public required PrimaryGoal Goal { get; init; }

    /// <summary>
    /// Gets the runner-supplied free-text description that informed the categorical mapping.
    /// </summary>
    [Description("Runner-supplied free-text description that informed the categorical mapping.")]
    public required string Description { get; init; }
}
