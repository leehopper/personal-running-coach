using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// A single before/after change to one micro-week workout, keyed by 1-based
/// <see cref="WeekNumber"/> and <see cref="DayOfWeek"/> (0=Sunday..6=Saturday) —
/// the same keys the plan read model uses (<c>MicroWorkoutsByWeek</c> /
/// <c>WorkoutOutput.DayOfWeek</c>). <see cref="Before"/> is the prescription prior
/// to the adaptation; <see cref="After"/> is the revised workout the projection
/// applies and the panel renders. A null <see cref="After"/> denotes a removed
/// workout (not produced this slice); a null <see cref="Before"/> denotes an added
/// one. Part of the structured <see cref="PlanAdaptationDiff"/> (Unit 2).
/// </summary>
/// <param name="WeekNumber">The 1-based plan week the workout belongs to.</param>
/// <param name="DayOfWeek">The day of week, 0=Sunday..6=Saturday.</param>
/// <param name="Before">The workout before the adaptation, or null when added.</param>
/// <param name="After">The workout after the adaptation, or null when removed.</param>
public sealed record WorkoutChange(
    int WeekNumber,
    int DayOfWeek,
    WorkoutOutput? Before,
    WorkoutOutput? After);
