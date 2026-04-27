using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Records that a single mesocycle (calendar week) of detailed week structure
/// was generated. Slice 1 emits exactly four of these per Plan stream covering
/// weeks 1-4 immediately after <see cref="PlanGenerated"/>; later slices may
/// append further weeks as the runner progresses.
/// </summary>
/// <param name="WeekIndex">
/// The 1-based index of this week within the plan (1, 2, 3, 4 in Slice 1).
/// </param>
/// <param name="Meso">The week-template output from the meso-tier LLM call.</param>
public sealed record MesoCycleCreated(
    int WeekIndex,
    MesoWeekOutput Meso);
