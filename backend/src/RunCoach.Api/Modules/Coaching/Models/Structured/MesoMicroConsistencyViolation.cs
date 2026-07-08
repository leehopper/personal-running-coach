namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Kinds of cross-layer inconsistency <see cref="MesoMicroConsistencyValidator"/> can detect
/// between a week-1 <see cref="MesoWeekOutput"/> template and the <see cref="MicroWorkoutListOutput"/>
/// that expands it (F-LIVE-2). The meso week is the source of truth; the micro layer must schedule
/// exactly one workout per meso <see cref="DaySlotType.Run"/> slot, on the same day, of the same
/// <see cref="WorkoutType"/>. Mirrors <see cref="MacroPlanOutputValidationViolation"/>.
/// </summary>
public enum MesoMicroConsistencyViolation
{
    /// <summary>No violation — the micro week faithfully expands the meso week-1 run schedule.</summary>
    None = 0,

    /// <summary>
    /// The number of micro run workouts does not equal the number of meso week-1 run slots
    /// (the finding's "3 vs 4 run days" symptom). A duplicate micro workout on the same day is
    /// reported here too, since it inflates the run-workout count above the meso run-day count.
    /// </summary>
    RunDayCountMismatch = 1,

    /// <summary>
    /// The run-day counts match but the day set differs — the micro layer scheduled a run on a day
    /// the meso week marks rest/cross-train, or omitted a day the meso week marks as a run.
    /// </summary>
    RunDaySetMismatch = 2,

    /// <summary>
    /// The run-day sets match but at least one day's <see cref="WorkoutType"/> disagrees between the
    /// layers (the finding's "tempo-vs-easy day swapped" symptom). Only checked for meso run slots
    /// that declare a non-null workout type.
    /// </summary>
    WorkoutTypeMismatch = 3,
}
