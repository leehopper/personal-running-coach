// Marten event records for the per-user Plan stream (DEC-047, spec 13 § Unit 2).
//
// Convention: events are positional records with stable property order so the
// System.Text.Json representation is byte-stable across deployments. The macro,
// meso, and micro payloads reuse the existing structured-output records from
// `Modules/Coaching/Models/Structured/` verbatim - the LLM emits them and the
// caller hands them straight to `session.Events.StartStream<Plan>(planId, events)`
// without re-projection.
//
// Slice 1 lands three event types: `PlanGenerated` (stream-creation),
// `MesoCycleCreated` (x4 - one per week 1-4), `FirstMicroCycleCreated` (week 1
// detailed workouts). Future slices add `PlanAdaptedFromLog` (Slice 3) and
// `PlanRestructuredFromConversation` (Slice 4) as additive `Apply` methods on
// `PlanProjection`; no schema break.
//
// `PreviousPlanId` ships in `PlanGenerated` from day one - Unit 5's regenerate
// handler threads it onto the new plan's stream-creation event so the projection
// retains audit linkage to the prior plan without a schema bump.
//
// All event records are co-located in this file so the Plan stream's wire schema
// is reviewable in one place. The StyleCop SA1402 / SA1649 single-type-per-file
// rules are suppressed locally for this aggregate file only - other modules
// continue to follow the one-type-per-file convention.
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Stream-creation event. Emitted exactly once per Plan stream when
/// `IPlanGenerationService` finishes the macro tier of the structured-output
/// chain. Carries the periodized macro plan plus prompt + model provenance so
/// regeneration audits can reconstruct what the LLM saw.
/// </summary>
/// <param name="PlanId">The plan's id; doubles as the per-user-plan stream id.</param>
/// <param name="UserId">The runner's user id - the plan's owner.</param>
/// <param name="Macro">The periodized macro plan emitted by the macro-tier LLM call.</param>
/// <param name="GeneratedAt">Wall-clock time the plan was generated.</param>
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
/// initial onboarding-driven generation. Unit 5 (Slice 1) reuses this slot
/// without a schema bump.
/// </param>
public sealed record PlanGenerated(
    Guid PlanId,
    Guid UserId,
    MacroPlanOutput Macro,
    DateTimeOffset GeneratedAt,
    string PromptVersion,
    string ModelId,
    Guid? PreviousPlanId);

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

/// <summary>
/// Records the detailed workout list for the first week of the plan. Slice 1
/// only generates micro-detail for week 1; weeks 2-4 carry only meso-tier
/// structure until subsequent slices land just-in-time micro generation.
/// </summary>
/// <param name="Micro">The week-1 workout list from the micro-tier LLM call.</param>
public sealed record FirstMicroCycleCreated(
    MicroWorkoutListOutput Micro);
