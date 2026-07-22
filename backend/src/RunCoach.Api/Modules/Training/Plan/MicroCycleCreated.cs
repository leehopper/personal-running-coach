using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Records the detailed workout list for an arbitrary week of the plan — the
/// rolling-horizon generalization of <see cref="FirstMicroCycleCreated"/>
/// (DEC-090). <see cref="FirstMicroCycleCreated"/> stays for week 1 forever,
/// since Marten streams are append-only; this event covers every week the
/// rolling-horizon extension seam generates beyond week 1.
/// </summary>
/// <param name="WeekIndex">The 1-based index of this week within the plan.</param>
/// <param name="Micro">The week's workout list from the micro-tier LLM call.</param>
public sealed record MicroCycleCreated(
    int WeekIndex,
    MicroWorkoutListOutput Micro);
