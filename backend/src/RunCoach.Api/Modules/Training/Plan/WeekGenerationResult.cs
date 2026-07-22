namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Result of <c>IPlanGenerationService.GenerateWeekAsync</c> — the rolling-horizon extension seam
/// (DEC-090). Wraps the events generated for one target plan week.
/// </summary>
/// <param name="Meso">
/// The meso-tier event for the target week, non-null when this call generated it (the
/// both-tiers-missing case); <see langword="null"/> when an existing meso week was supplied and
/// only the micro tier was generated (micro-only backfill).
/// </param>
/// <param name="Micro">
/// The micro-tier event for the target week. Always non-null — the method is only called when at
/// least the micro tier is missing.
/// </param>
public sealed record WeekGenerationResult(MesoCycleCreated? Meso, MicroCycleCreated? Micro);
