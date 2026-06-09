using System.ComponentModel;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Coaching.Adaptation;

/// <summary>
/// The typed slot for an <see cref="Training.Adaptation.AdaptationKind.Restructure"/>: a
/// week/load restructure that revises upcoming meso weekly targets and the current micro
/// week, plus the return-to-progression trajectory. Only the current micro week carries
/// detailed daily workouts today; upcoming weeks are adjusted at the meso
/// <c>WeeklyTargetKm</c> grain (Slice 3 does not generate fresh micro detail for future weeks).
/// </summary>
public sealed record RestructurePlan
{
    /// <summary>
    /// Gets the revised weekly volume targets (km) for upcoming meso weeks. Each entry
    /// replaces that week's <c>MesoWeekOutput.WeeklyTargetKm</c>.
    /// </summary>
    [Description("Revised weekly volume targets in kilometers for upcoming meso weeks. Each entry replaces that week's weekly target. May be empty when only the current week changes.")]
    public required WeeklyTargetEdit[] RevisedWeeklyTargets { get; init; }

    /// <summary>
    /// Gets the revised workouts for the current micro week, each keyed by its
    /// <see cref="WorkoutOutput.DayOfWeek"/> (0=Sunday..6=Saturday). May be empty when
    /// the restructure only adjusts upcoming weekly targets.
    /// </summary>
    [Description("Revised workouts for the current micro week, each keyed by its day-of-week (0=Sunday..6=Saturday). May be empty when only upcoming weekly targets change.")]
    public required WorkoutOutput[] RevisedCurrentWeekWorkouts { get; init; }

    /// <summary>
    /// Gets the return-to-progression trajectory: how and when load ramps back up after a
    /// reduction, so the runner sees the path back, not just the cut. Required (non-empty)
    /// for any load-reducing restructure — enforced by
    /// <see cref="PlanAdaptationOutputValidator"/>.
    /// </summary>
    [Description("The return-to-progression trajectory: how and when training load ramps back up after a reduction, so the runner sees the path back rather than just the cut. Required for any load-reducing restructure.")]
    public required string ForwardPath { get; init; }
}
