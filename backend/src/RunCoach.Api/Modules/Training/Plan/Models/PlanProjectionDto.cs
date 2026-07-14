using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Plan.Models;

/// <summary>
/// Inline-projected read model for a Plan stream (spec 13 § Unit 2, R02.3).
/// Materialized by <see cref="PlanProjection"/> from the Plan stream's event
/// types and rendered directly by the frontend via
/// <c>GET /api/v1/plan/current</c> — no further server-side shaping.
/// Apply methods (<see cref="PlanGenerated"/>, <see cref="MesoCycleCreated"/>,
/// <see cref="FirstMicroCycleCreated"/>) are additive — each new event type
/// extends the projection without modifying existing applied properties.
/// </summary>
/// <remarks>
/// <para>
/// The document is keyed on <see cref="PlanId"/> rather than <see cref="UserId"/>
/// so a runner can have multiple plans in their history (the prior stream is
/// retained as audit trail on regeneration). The active plan is resolved via
/// <c>RunnerOnboardingProfile.CurrentPlanId</c>.
/// </para>
/// <para>
/// The <see cref="MicroWorkoutsByWeek"/> dictionary is keyed by 1-based week
/// index so additional week entries can be attached additively without changing
/// the access path for previously populated weeks.
/// </para>
/// </remarks>
public sealed record PlanProjectionDto
{
    /// <summary>Gets or sets the plan id (also the Marten stream id for the plan stream).</summary>
    public Guid PlanId { get; set; }

    /// <summary>Gets or sets the runner's user id - the plan's owner.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the wall-clock time the plan was generated.</summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>
    /// Gets or sets the calendar date on which the plan's week 1, day 0 (Sunday)
    /// begins — the start-of-week of the generation date. Threaded through from
    /// <see cref="PlanGenerated.PlanStartDate"/> verbatim; the frontend derives the
    /// current week from this anchor relative to today (DEC-076).
    /// </summary>
    public DateOnly PlanStartDate { get; set; }

    /// <summary>
    /// Gets or sets the prior plan id when this plan came from the regenerate-from-settings
    /// flow, otherwise <see langword="null"/>. Threaded through from
    /// <see cref="PlanGenerated.PreviousPlanId"/> verbatim.
    /// </summary>
    public Guid? PreviousPlanId { get; set; }

    /// <summary>
    /// Gets or sets the name of the goal race or event, or <see langword="null"/> for a
    /// general-fitness plan with no target event. Threaded through from
    /// <see cref="PlanGenerated.TargetEventName"/> verbatim.
    /// </summary>
    public string? TargetEventName { get; set; }

    /// <summary>
    /// Gets or sets the target event's distance in kilometers, or <see langword="null"/> for a
    /// general-fitness plan. Threaded through from
    /// <see cref="PlanGenerated.TargetEventDistanceKm"/> verbatim.
    /// </summary>
    public double? TargetEventDistanceKm { get; set; }

    /// <summary>
    /// Gets or sets the target event's calendar date, or <see langword="null"/> for a
    /// general-fitness plan or an unparseable onboarding date. Threaded through from
    /// <see cref="PlanGenerated.TargetEventDate"/> verbatim.
    /// </summary>
    public DateOnly? TargetEventDate { get; set; }

    /// <summary>
    /// Gets or sets the semantic version of the coaching prompt YAML that
    /// produced the plan (e.g. <c>"coaching-v1"</c>).
    /// </summary>
    public string PromptVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Anthropic model identifier (e.g. <c>"claude-sonnet-4-5"</c>) that
    /// generated the plan.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the periodized macro plan rendered as the home page's macro phase strip.
    /// Set by the <see cref="PlanGenerated"/> apply method; never null after the projection
    /// has consumed the stream-creation event.
    /// </summary>
    public MacroPlanOutput? Macro { get; set; }

    /// <summary>
    /// Gets or sets the detailed weekly templates emitted by the meso tier, in week-index
    /// order. The frontend's <c>MesoWeekBlock</c> renders this directly.
    /// </summary>
    public IReadOnlyList<MesoWeekOutput> MesoWeeks { get; set; } = Array.Empty<MesoWeekOutput>();

    /// <summary>
    /// Gets or sets the per-week detailed workout lists keyed by 1-based week index.
    /// Entries are attached additively so callers that access a specific week key
    /// remain unaffected when new week entries are added.
    /// </summary>
    public IReadOnlyDictionary<int, MicroWorkoutListOutput> MicroWorkoutsByWeek { get; set; }
        = new Dictionary<int, MicroWorkoutListOutput>();
}
