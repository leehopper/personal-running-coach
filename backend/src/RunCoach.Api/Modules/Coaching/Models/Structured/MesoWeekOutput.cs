using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Structured output record for a weekly training template.
/// Root level: 12 properties (5 scalar + 7 named day slots), nesting depth 2 (MesoWeekOutput -> MesoDaySlotOutput).
/// Seven named properties instead of a Days array so constrained decoding structurally guarantees exactly 7 slots.
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
    /// Gets a value indicating whether this is a deload (recovery) week.
    /// </summary>
    [Description("Whether this is a deload week with reduced volume for recovery.")]
    public required bool IsDeloadWeek { get; init; }

    /// <summary>Gets the activity plan for Sunday.</summary>
    [Description("Activity plan for Sunday.")]
    public required MesoDaySlotOutput Sunday { get; init; }

    /// <summary>Gets the activity plan for Monday.</summary>
    [Description("Activity plan for Monday.")]
    public required MesoDaySlotOutput Monday { get; init; }

    /// <summary>Gets the activity plan for Tuesday.</summary>
    [Description("Activity plan for Tuesday.")]
    public required MesoDaySlotOutput Tuesday { get; init; }

    /// <summary>Gets the activity plan for Wednesday.</summary>
    [Description("Activity plan for Wednesday.")]
    public required MesoDaySlotOutput Wednesday { get; init; }

    /// <summary>Gets the activity plan for Thursday.</summary>
    [Description("Activity plan for Thursday.")]
    public required MesoDaySlotOutput Thursday { get; init; }

    /// <summary>Gets the activity plan for Friday.</summary>
    [Description("Activity plan for Friday.")]
    public required MesoDaySlotOutput Friday { get; init; }

    /// <summary>Gets the activity plan for Saturday.</summary>
    [Description("Activity plan for Saturday.")]
    public required MesoDaySlotOutput Saturday { get; init; }

    /// <summary>
    /// Gets the coaching summary for this week.
    /// </summary>
    [Description("Coaching summary explaining the focus and goals for this training week.")]
    public required string WeekSummary { get; init; }

    /// <summary>
    /// Enumerates the seven named day slots in Sunday-to-Saturday order.
    /// </summary>
    public IEnumerable<(DayOfWeek DayOfWeek, MesoDaySlotOutput Slot)> EnumerateDays()
    {
        yield return (DayOfWeek.Sunday, Sunday);
        yield return (DayOfWeek.Monday, Monday);
        yield return (DayOfWeek.Tuesday, Tuesday);
        yield return (DayOfWeek.Wednesday, Wednesday);
        yield return (DayOfWeek.Thursday, Thursday);
        yield return (DayOfWeek.Friday, Friday);
        yield return (DayOfWeek.Saturday, Saturday);
    }
}
