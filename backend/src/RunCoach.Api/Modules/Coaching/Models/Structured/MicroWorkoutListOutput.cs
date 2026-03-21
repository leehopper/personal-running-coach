using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Structured output record for a list of detailed workout prescriptions.
/// Root level: 1 property, nesting depth 3 (MicroWorkoutListOutput -> WorkoutOutput -> WorkoutSegmentOutput).
/// </summary>
public sealed record MicroWorkoutListOutput
{
    /// <summary>
    /// Gets the list of detailed workout prescriptions.
    /// </summary>
    [Description("The list of detailed workout prescriptions for the training week.")]
    public required WorkoutOutput[] Workouts { get; init; }
}
