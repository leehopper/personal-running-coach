using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// A phase within a periodized macro training plan, as returned by structured output.
/// Nesting depth: 1 (child of MacroPlanOutput).
/// </summary>
public sealed record PlanPhaseOutput
{
    /// <summary>
    /// Gets the periodization phase type (e.g., Base, Build, Peak, Taper, Recovery).
    /// </summary>
    [Description("The periodization phase type: Base, Build, Peak, Taper, or Recovery.")]
    public required PhaseType PhaseType { get; init; }

    /// <summary>
    /// Gets the number of weeks in this phase.
    /// </summary>
    [Description("The number of weeks in this training phase.")]
    public required int Weeks { get; init; }

    /// <summary>
    /// Gets the target weekly distance in kilometers at the start of the phase.
    /// </summary>
    [Description("Target weekly distance in kilometers at the start of this phase.")]
    public required int WeeklyDistanceStartKm { get; init; }

    /// <summary>
    /// Gets the target weekly distance in kilometers at the end of the phase.
    /// </summary>
    [Description("Target weekly distance in kilometers at the end of this phase.")]
    public required int WeeklyDistanceEndKm { get; init; }

    /// <summary>
    /// Gets the intensity distribution description (e.g., "80/20 easy/hard").
    /// </summary>
    [Description("The intensity distribution for this phase, such as '80/20 easy/hard'.")]
    public required string IntensityDistribution { get; init; }

    /// <summary>
    /// Gets the list of workout types allowed in this phase.
    /// Array used instead of ImmutableArray for System.Text.Json deserialization compatibility.
    /// </summary>
    [Description("The workout types allowed during this phase.")]
    public required WorkoutType[] AllowedWorkoutTypes { get; init; }

    /// <summary>
    /// Gets the target easy pace in seconds per kilometer for this phase.
    /// </summary>
    [Description("Target easy pace in seconds per kilometer for this phase.")]
    public required int TargetPaceEasySecPerKm { get; init; }

    /// <summary>
    /// Gets the target fast pace in seconds per kilometer for hard workouts in this phase.
    /// </summary>
    [Description("Target fast pace in seconds per kilometer for hard workouts in this phase.")]
    public required int TargetPaceFastSecPerKm { get; init; }

    /// <summary>
    /// Gets the coaching notes explaining the purpose and focus of this phase.
    /// </summary>
    [Description("Coaching notes explaining the purpose and focus of this phase.")]
    public required string Notes { get; init; }

    /// <summary>
    /// Gets a value indicating whether gets a flag indicating whether this phase includes a deload week.
    /// </summary>
    [Description("Whether this phase includes a deload week for recovery.")]
    public required bool IncludesDeload { get; init; }
}
