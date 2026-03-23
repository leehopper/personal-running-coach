using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// A segment within a structured workout, as returned by structured output.
/// Nesting depth: 2 (child of WorkoutOutput, grandchild of MicroWorkoutListOutput).
/// </summary>
public sealed record WorkoutSegmentOutput
{
    /// <summary>
    /// Gets the type of this segment (e.g., Warmup, Work, Recovery, Cooldown).
    /// </summary>
    [Description("The segment type: Warmup, Work, Recovery, or Cooldown.")]
    public required SegmentType SegmentType { get; init; }

    /// <summary>
    /// Gets the duration of this segment in minutes.
    /// </summary>
    [Description("Duration of this segment in minutes.")]
    public required int DurationMinutes { get; init; }

    /// <summary>
    /// Gets the target pace in seconds per kilometer for this segment.
    /// </summary>
    [Description("Target pace in seconds per kilometer for this segment.")]
    public required int TargetPaceSecPerKm { get; init; }

    /// <summary>
    /// Gets the intensity profile for this segment.
    /// </summary>
    [Description("The intensity profile: Easy, Moderate, Threshold, VO2Max, or Repetition.")]
    public required IntensityProfile Intensity { get; init; }

    /// <summary>
    /// Gets the number of repetitions if this is an interval segment, or 1 for continuous.
    /// </summary>
    [Description("Number of repetitions for interval segments, or 1 for continuous efforts.")]
    public required int Repetitions { get; init; }

    /// <summary>
    /// Gets coaching notes for this segment.
    /// </summary>
    [Description("Coaching notes for this segment, such as effort cues or technique reminders.")]
    public required string Notes { get; init; }
}
