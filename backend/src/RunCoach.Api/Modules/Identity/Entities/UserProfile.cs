using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Identity.Entities;

/// <summary>
/// Per-user training profile populated by the onboarding flow (spec 13 § Unit 1,
/// requirement R01.2 / R01.3). Lives in a 1:1 relationship with
/// <see cref="ApplicationUser"/> via a shared primary key (<see cref="UserId"/>
/// is both PK and FK — no surrogate id, no nullable optional row). Slot columns
/// are JSONB-typed via owned-entity <c>ToJson</c> mapping (configured in
/// <see cref="UserProfileConfiguration"/>) and remain null until the
/// corresponding onboarding topic is captured. The slot answer record types are
/// canonically defined under <c>Modules.Coaching.Onboarding.Models</c> — both
/// the EF column shape and the LLM constrained-decoding schema use the same
/// records to avoid drift.
/// </summary>
/// <remarks>
/// State updates flow exclusively through the
/// <c>UserProfileFromOnboardingProjection</c> apply method per DEC-060 / R-069,
/// which runs as a Marten transaction participant on the same Postgres
/// connection as the event append — atomic by construction. Wolverine
/// <c>[AggregateHandler]</c> bodies must not mutate this entity directly
/// (DEC-060 prohibition on dual-write).
/// </remarks>
[Table("UserProfile")]
public class UserProfile
{
    /// <summary>
    /// Gets or sets shared primary / foreign key with <see cref="ApplicationUser.Id"/>.
    /// Cascade-deletes alongside the parent user.
    /// </summary>
    [Key]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }

    /// <summary>Gets or sets navigation to the parent <see cref="ApplicationUser"/>.</summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Gets or sets top-level training goal captured from the <c>PrimaryGoal</c> onboarding
    /// topic. Null until the topic is answered. Stored as the categorical
    /// <see cref="PrimaryGoal"/> enum scalar — <see cref="PrimaryGoalAnswer"/>'s
    /// free-text description lives only on the onboarding event stream and is
    /// not duplicated on the projection row.
    /// </summary>
    public PrimaryGoal? PrimaryGoal { get; set; }

    /// <summary>
    /// Gets or sets race / event target. Only populated when
    /// <see cref="PrimaryGoal"/> = <see cref="Models.PrimaryGoal.RaceTraining"/>;
    /// otherwise null.
    /// </summary>
    public TargetEventAnswer? TargetEvent { get; set; }

    /// <summary>Gets or sets recent training-load + effort baseline.</summary>
    public CurrentFitnessAnswer? CurrentFitness { get; set; }

    /// <summary>Gets or sets weekly availability + run-day cap.</summary>
    public WeeklyScheduleAnswer? WeeklySchedule { get; set; }

    /// <summary>Gets or sets injury concerns + recovery context.</summary>
    public InjuryHistoryAnswer? InjuryHistory { get; set; }

    /// <summary>Gets or sets uX + display preferences (units, etc.).</summary>
    public PreferencesAnswer? Preferences { get; set; }

    /// <summary>
    /// Gets or sets set by <c>OnboardingCompleted</c> projection apply. Null while
    /// onboarding is in progress; non-null gates entry to the home surface
    /// (the frontend redirects authenticated-but-not-onboarded users to
    /// <c>/onboarding</c>).
    /// </summary>
    public DateTimeOffset? OnboardingCompletedAt { get; set; }

    /// <summary>
    /// Gets or sets points at the user's currently active <c>Plan</c> stream id. Set by
    /// <c>UserProfileFromOnboardingProjection</c> when applying the
    /// <c>PlanLinkedToUser</c> event — atomic with the Marten event append
    /// per DEC-060 / R-069. Null until the first plan is generated.
    /// </summary>
    public Guid? CurrentPlanId { get; set; }

    /// <summary>Gets or sets uTC creation timestamp (set by EF on insert).</summary>
    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>Gets or sets uTC last-modified timestamp (updated on each projection apply).</summary>
    public DateTimeOffset ModifiedOn { get; set; }
}
