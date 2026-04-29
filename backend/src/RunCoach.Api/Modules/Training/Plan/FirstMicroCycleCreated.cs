using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Records the detailed workout list for the first week of the plan. The
/// initial slice only generates micro-detail for week 1; weeks 2-4 carry only
/// meso-tier structure until subsequent slices land just-in-time micro
/// generation.
/// </summary>
/// <param name="Micro">The week-1 workout list from the micro-tier LLM call.</param>
public sealed record FirstMicroCycleCreated(
    MicroWorkoutListOutput Micro);
