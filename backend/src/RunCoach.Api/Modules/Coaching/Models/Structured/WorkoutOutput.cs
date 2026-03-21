using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// A detailed workout prescription, as returned by structured output.
/// Nesting depth: 1 (child of MicroWorkoutListOutput), contains WorkoutSegmentOutput at depth 2.
/// </summary>
public sealed record WorkoutOutput
{
    /// <summary>
    /// Gets the day of the week for this workout (0 = Sunday, 1 = Monday, etc.).
    /// </summary>
    [Description("The day of the week as an integer: 0 = Sunday, 1 = Monday, ..., 6 = Saturday.")]
    public required int DayOfWeek { get; init; }

    /// <summary>
    /// Gets the type of workout.
    /// </summary>
    [Description("The type of workout: Easy, LongRun, Tempo, Interval, Repetition, Recovery, or CrossTrain.")]
    public required WorkoutType WorkoutType { get; init; }

    /// <summary>
    /// Gets the title or name of the workout.
    /// </summary>
    [Description("A descriptive title for this workout, such as 'Easy Aerobic Run' or 'Threshold Intervals'.")]
    public required string Title { get; init; }

    /// <summary>
    /// Gets the target distance in kilometers.
    /// </summary>
    [Description("Target total distance for this workout in kilometers.")]
    public required int TargetDistanceKm { get; init; }

    /// <summary>
    /// Gets the target duration in minutes.
    /// </summary>
    [Description("Target total duration for this workout in minutes.")]
    public required int TargetDurationMinutes { get; init; }

    /// <summary>
    /// Gets the target easy pace in seconds per kilometer.
    /// </summary>
    [Description("Target easy pace in seconds per kilometer for easy portions of this workout.")]
    public required int TargetPaceEasySecPerKm { get; init; }

    /// <summary>
    /// Gets the target fast pace in seconds per kilometer for hard portions.
    /// </summary>
    [Description("Target fast pace in seconds per kilometer for hard portions of this workout.")]
    public required int TargetPaceFastSecPerKm { get; init; }

    /// <summary>
    /// Gets the structured segments of the workout.
    /// </summary>
    [Description("The structured segments that make up this workout (warmup, work intervals, cooldown, etc.).")]
    public required WorkoutSegmentOutput[] Segments { get; init; }

    /// <summary>
    /// Gets the warmup instructions.
    /// </summary>
    [Description("Specific warmup instructions for this workout.")]
    public required string WarmupNotes { get; init; }

    /// <summary>
    /// Gets the cooldown instructions.
    /// </summary>
    [Description("Specific cooldown instructions for this workout.")]
    public required string CooldownNotes { get; init; }

    /// <summary>
    /// Gets the coaching notes for this workout.
    /// </summary>
    [Description("Coaching notes explaining the purpose and execution guidance for this workout.")]
    public required string CoachingNotes { get; init; }

    /// <summary>
    /// Gets the perceived effort level on a 1-10 scale.
    /// </summary>
    [Description("Expected perceived effort on a 1-10 scale, where 1 is very easy and 10 is maximal.")]
    public required int PerceivedEffort { get; init; }
}
