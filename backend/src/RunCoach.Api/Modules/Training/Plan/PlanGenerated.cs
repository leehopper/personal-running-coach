using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Stream-creation event. Emitted exactly once per Plan stream when
/// <c>IPlanGenerationService</c> finishes the macro tier of the structured-output
/// chain. Carries the periodized macro plan plus prompt + model provenance so
/// regeneration audits can reconstruct what the LLM saw.
/// </summary>
/// <param name="PlanId">The plan's id; doubles as the per-user-plan stream id.</param>
/// <param name="UserId">The runner's user id - the plan's owner.</param>
/// <param name="Macro">The periodized macro plan emitted by the macro-tier LLM call.</param>
/// <param name="GeneratedAt">Wall-clock time the plan was generated.</param>
/// <param name="PlanStartDate">
/// The calendar date on which the plan's week 1, day 0 (Sunday, matching
/// <see cref="Coaching.Models.Structured.WorkoutOutput.DayOfWeek"/> 0 = Sunday)
/// begins — the start-of-week (Sunday) of the generation date. Lets a logged
/// run's date map deterministically to a <c>(week, day)</c> slot server-side
/// (slice-2b Unit 1 / DEC-076); the regenerate flow re-anchors week 1 to the
/// regeneration week because it shares this construction site.
/// </param>
/// <param name="PromptVersion">
/// The semantic version of the coaching prompt YAML that produced the plan
/// (e.g. <c>"coaching-v1"</c>) - lets future slices replay or A/B against
/// historic prompt revisions.
/// </param>
/// <param name="ModelId">
/// The Anthropic model identifier (e.g. <c>"claude-sonnet-4-5"</c>) that
/// generated the plan, captured for audit + cost analysis.
/// </param>
/// <param name="PreviousPlanId">
/// The prior <see cref="PlanId"/> when this generation came from the
/// regenerate-from-settings flow, otherwise <see langword="null"/> for the
/// initial onboarding-driven generation. The regenerate handler reuses this
/// slot without a schema bump.
/// </param>
public sealed record PlanGenerated(
    Guid PlanId,
    Guid UserId,
    MacroPlanOutput Macro,
    DateTimeOffset GeneratedAt,
    DateOnly PlanStartDate,
    string PromptVersion,
    string ModelId,
    Guid? PreviousPlanId);
