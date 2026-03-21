using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Structured output record for a weekly training template.
/// Root level: 6 properties, nesting depth 2 (MesoWeekOutput -> MesoDayOutput).
/// </summary>
public sealed record MesoWeekOutput
{
    /// <summary>
    /// Gets the week number within the current phase.
    /// </summary>
    [Description("The week number within the current training phase (1-based).")]
    public required int WeekNumber { get; init; }

    /// <summary>
    /// Gets the phase type this week belongs to.
    /// </summary>
    [Description("The periodization phase this week belongs to: Base, Build, Peak, Taper, or Recovery.")]
    public required PhaseType PhaseType { get; init; }

    /// <summary>
    /// Gets the target weekly distance in kilometers.
    /// </summary>
    [Description("Target total weekly running distance in kilometers.")]
    public required int WeeklyTargetKm { get; init; }

    /// <summary>
    /// Gets whether this is a deload (recovery) week.
    /// </summary>
    [Description("Whether this is a deload week with reduced volume for recovery.")]
    public required bool IsDeloadWeek { get; init; }

    /// <summary>
    /// Gets the seven day slots for the week.
    /// </summary>
    [Description("The seven day slots for this week, one per day from Sunday to Saturday.")]
    public required MesoDayOutput[] Days { get; init; }

    /// <summary>
    /// Gets the coaching summary for this week.
    /// </summary>
    [Description("Coaching summary explaining the focus and goals for this training week.")]
    public required string WeekSummary { get; init; }
}
