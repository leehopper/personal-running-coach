using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// A single day slot within a weekly training template, as returned by structured output.
/// Nesting depth: 1 (child of MesoWeekOutput).
/// </summary>
public sealed record MesoDaySlotOutput
{
    /// <summary>
    /// Gets the type of activity assigned to this day slot.
    /// </summary>
    [Description("The type of activity for this day: Run, Rest, or CrossTrain.")]
    public required DaySlotType SlotType { get; init; }

    /// <summary>
    /// Gets the workout type if this is a run day, or null for rest/cross-train days.
    /// </summary>
    [Description("The workout type if this is a run day (e.g., Easy, LongRun, Tempo), or null for rest/cross-train.")]
    public WorkoutType? WorkoutType { get; init; }

    /// <summary>
    /// Gets the coaching note for this day slot.
    /// </summary>
    [Description("Brief coaching note for this day, such as emphasis or purpose.")]
    public required string Notes { get; init; }
}
