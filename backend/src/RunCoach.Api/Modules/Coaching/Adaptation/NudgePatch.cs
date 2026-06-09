using System.ComponentModel;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Coaching.Adaptation;

/// <summary>
/// The typed slot for an <see cref="Training.Adaptation.AdaptationKind.Nudge"/>: a small
/// deterministic-style reschedule of one or two workouts within the current micro week.
/// Reuses the int-unit <see cref="WorkoutOutput"/> shape so the revised workouts slot
/// straight back into <c>PlanProjectionDto.MicroWorkoutsByWeek[WeekNumber]</c>.
/// </summary>
public sealed record NudgePatch
{
    /// <summary>
    /// Gets the 1-based plan week whose micro-cycle workouts are revised — the current
    /// training week (the only week for which detailed workouts exist today).
    /// </summary>
    [Description("The 1-based plan week whose micro-cycle workouts are revised. This is the current training week.")]
    public required int WeekNumber { get; init; }

    /// <summary>
    /// Gets the replacement workouts for the current micro week, each keyed by its
    /// <see cref="WorkoutOutput.DayOfWeek"/> (0=Sunday..6=Saturday). A nudge moves or
    /// swaps one or two workouts and must never stack two key workouts on consecutive days.
    /// </summary>
    [Description("Replacement workouts for the current micro week, each keyed by its day-of-week (0=Sunday..6=Saturday). A nudge reschedules one or two workouts and never stacks two hard workouts on consecutive days.")]
    public required WorkoutOutput[] RevisedWorkouts { get; init; }
}
