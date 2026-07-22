using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Kinds of invariant violation <see cref="MesoWeekOutputValidator"/> can detect on a
/// deserialized <see cref="MesoWeekOutput"/> generated for a specific target week.
/// </summary>
public enum MesoWeekOutputValidationViolation
{
    /// <summary>No violation — the week template is valid.</summary>
    None = 0,

    /// <summary>
    /// <see cref="MesoWeekOutput.WeekNumber"/> disagrees with the expected 1-based week
    /// index. This is the load-bearing check: it prevents
    /// <see cref="PlanProjection.Apply(MesoCycleCreated, PlanProjectionDto)"/>'s hard
    /// <see cref="InvalidOperationException"/> from aborting the whole extension
    /// transaction (DEC-090 D8).
    /// </summary>
    WeekNumberMismatch = 1,

    /// <summary>
    /// The week's seven day slots contain zero <see cref="DaySlotType.Run"/> slots — a
    /// meso week that prescribes no runs cannot seed any micro workouts and is not a
    /// trainable running week.
    /// </summary>
    NoRunDay = 2,
}
