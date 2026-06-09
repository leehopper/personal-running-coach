using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Adaptation;

/// <summary>
/// A revised weekly volume target inside a <see cref="RestructurePlan"/>. Targets a meso
/// week by 1-based index and carries the replacement weekly volume in kilometers, matching
/// the int-unit <c>MesoWeekOutput.WeeklyTargetKm</c>.
/// </summary>
public sealed record WeeklyTargetEdit
{
    /// <summary>
    /// Gets the 1-based meso week whose weekly volume target is revised.
    /// </summary>
    [Description("The 1-based meso week whose weekly volume target is revised.")]
    public required int WeekNumber { get; init; }

    /// <summary>
    /// Gets the revised weekly volume target in kilometers for this week.
    /// </summary>
    [Description("The revised weekly volume target in kilometers for this week.")]
    public required int WeeklyTargetKm { get; init; }
}
