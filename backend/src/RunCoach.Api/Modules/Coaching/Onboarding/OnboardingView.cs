using Marten.Metadata;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// In-flight projection of the per-user onboarding stream (spec 13 § Unit 1, R01.4).
/// Built inline by <see cref="OnboardingProjection"/> from the eight onboarding events
/// so the deterministic completion gate, the next-topic selector, and the regenerate
/// handler can read current onboarding state without replaying the stream on every read.
/// </summary>
/// <remarks>
/// Implements <see cref="ITenanted"/> so Marten conjoined tenancy auto-populates
/// <see cref="TenantId"/> with the per-user tenant id (the runner's user id stringified)
/// established by Slice 0's tenant-resolution middleware. The setter is required by the
/// interface; the projection does not assign it.
/// </remarks>
public sealed class OnboardingView : ITenanted
{
    /// <summary>Gets or sets the stream id (the runner's user id; equal to <see cref="UserId"/>).</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the runner's user id.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the conjoined-tenancy tenant id auto-populated by Marten from the
    /// session's tenant context. Always equal to <see cref="UserId"/> stringified.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status of the stream.
    /// </summary>
    public OnboardingStatus Status { get; set; } = OnboardingStatus.NotStarted;

    /// <summary>
    /// Gets or sets the topic the assistant most recently asked about, or <see langword="null"/>
    /// before the first <see cref="TopicAsked"/> event.
    /// </summary>
    public OnboardingTopic? CurrentTopic { get; set; }

    /// <summary>Gets or sets the captured PrimaryGoal answer. Null until the topic is captured.</summary>
    public PrimaryGoalAnswer? PrimaryGoal { get; set; }

    /// <summary>Gets or sets the captured TargetEvent answer. Null when not race-training or not yet captured.</summary>
    public TargetEventAnswer? TargetEvent { get; set; }

    /// <summary>Gets or sets the captured CurrentFitness answer.</summary>
    public CurrentFitnessAnswer? CurrentFitness { get; set; }

    /// <summary>Gets or sets the captured WeeklySchedule answer.</summary>
    public WeeklyScheduleAnswer? WeeklySchedule { get; set; }

    /// <summary>Gets or sets the captured InjuryHistory answer.</summary>
    public InjuryHistoryAnswer? InjuryHistory { get; set; }

    /// <summary>Gets or sets the captured Preferences answer.</summary>
    public PreferencesAnswer? Preferences { get; set; }

    /// <summary>
    /// Gets or sets the topics with an outstanding clarification request that has not yet
    /// been resolved by a subsequent <see cref="AnswerCaptured"/>. The deterministic
    /// completion gate fails while this list is non-empty.
    /// </summary>
    public IReadOnlyList<OnboardingTopic> OutstandingClarifications { get; set; } = Array.Empty<OnboardingTopic>();

    /// <summary>
    /// Gets or sets the timestamp the stream was opened (set by <see cref="OnboardingStarted"/>).
    /// </summary>
    public DateTimeOffset? OnboardingStartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp onboarding completed (set by <see cref="OnboardingCompleted"/>).
    /// </summary>
    public DateTimeOffset? OnboardingCompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user's currently-active plan id. Set by <see cref="PlanLinkedToUser"/>
    /// per DEC-060 / R-069 — read by Slice 1 Unit 5's regenerate handler to derive the
    /// previous-plan link without an extra EF round-trip.
    /// </summary>
    public Guid? CurrentPlanId { get; set; }

    /// <summary>
    /// Gets or sets the projection's monotonically-increasing version number, incremented
    /// on each apply so optimistic concurrency / replay tooling can detect drift.
    /// </summary>
    public int Version { get; set; }
}
