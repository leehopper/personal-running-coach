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
    /// Array used instead of ImmutableArray for JSON deserialization compatibility with constrained decoding.
    /// </summary>
    [Description("The list of detailed workout prescriptions for the training week.")]
    public required WorkoutOutput[] Workouts { get; init; }
}
