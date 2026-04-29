using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Plan.Models;

/// <summary>
/// Inline-projected read model for a Plan stream (spec 13 § Unit 2, R02.3).
/// Materialized by <see cref="PlanProjection"/> from the Plan stream's event
/// types and rendered directly by the frontend via
/// <c>GET /api/v1/plan/current</c> - no further server-side shaping. Slice 1
/// adds <see cref="PlanGenerated"/> + four <see cref="MesoCycleCreated"/> +
/// <see cref="FirstMicroCycleCreated"/> apply methods; later slices (Slice 3
/// adaptation, Slice 4 conversation-driven changes) extend the projection with
/// additive applies, never breaking changes.
/// </summary>
/// <remarks>
/// <para>
/// The document is keyed on <see cref="PlanId"/> rather than <see cref="UserId"/>
/// so a runner can have multiple plans in their history (Unit 5 regenerate keeps
/// the prior stream as audit trail). The active plan is resolved via
/// <c>UserProfile.CurrentPlanId</c>.
/// </para>
/// <para>
/// The <see cref="MicroWorkoutsByWeek"/> dictionary is keyed by 1-based week
/// index so future slices can attach week-2/3/4 micro detail without breaking
/// the Slice 1 frontend - Slice 1 only ever populates the entry for week 1.
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
    /// Gets or sets the prior plan id when this plan came from the regenerate-from-settings
    /// flow, otherwise <see langword="null"/>. Threaded through from
    /// <see cref="PlanGenerated.PreviousPlanId"/> verbatim.
    /// </summary>
    public Guid? PreviousPlanId { get; set; }

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
    /// Gets or sets the four detailed weekly templates emitted by the meso tier (Slice 1
    /// always populates exactly four entries, in week-index order 1-4). The frontend's
    /// `MesoWeekBlock` renders this directly.
    /// </summary>
    public IReadOnlyList<MesoWeekOutput> MesoWeeks { get; set; } = Array.Empty<MesoWeekOutput>();

    /// <summary>
    /// Gets or sets the per-week detailed workout lists keyed by 1-based week index. Slice 1
    /// only ever populates the entry for week 1; later slices attach further weeks as the
    /// runner progresses without breaking the Slice 1 frontend's `microWorkoutsByWeek[1]`
    /// access path.
    /// </summary>
    public IReadOnlyDictionary<int, MicroWorkoutListOutput> MicroWorkoutsByWeek { get; set; }
        = new Dictionary<int, MicroWorkoutListOutput>();
}
