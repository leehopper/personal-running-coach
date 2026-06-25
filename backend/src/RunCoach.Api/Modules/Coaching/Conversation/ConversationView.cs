using Marten.Metadata;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// The Marten document read-model accumulating a runner's <b>interactive</b>
/// conversation turns (Slice 4B Unit 3, DEC-085). Keyed by <see cref="Id"/> = the
/// runner's user id (the <c>Conversation</c> stream id), so the conversation
/// <b>persists across plan regenerations</b> — RunCoach is "my coach", not "a coach
/// for this plan". Materialized inline by <see cref="InteractiveConversationProjection"/>
/// from <see cref="UserMessagePosted"/> / <see cref="CoachMessagePosted"/> events.
/// </summary>
/// <remarks>
/// Distinct from the plan-scoped <see cref="ConversationLogView"/> (keyed by
/// <c>PlanId</c>), which holds the proactive adaptation/safety turns and is left
/// untouched by this slice. A composed timeline read unions the two. Implements
/// <see cref="ITenanted"/> so Marten conjoined tenancy auto-populates
/// <see cref="TenantId"/> with the runner's user id (mirroring
/// <see cref="Onboarding.OnboardingView"/>, the only other user-keyed projection).
/// </remarks>
public sealed class ConversationView : ITenanted
{
    /// <summary>Gets or sets the document/stream id (the runner's user id; equal to <see cref="UserId"/>).</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the runner's user id.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the conjoined-tenancy tenant id auto-populated by Marten from the
    /// session's tenant context. Always equal to <see cref="UserId"/> stringified.
    /// The setter is required by <see cref="ITenanted"/>; the projection never assigns it.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the interactive turns in append (chronological) order. Each turn is
    /// keyed by its <see cref="InteractiveTurnView.TurnId"/> so the projection is
    /// idempotent under replay — exactly one turn per logical message. Typed
    /// <see cref="IReadOnlyList{T}"/> (mutated by whole-collection reassignment in the
    /// projection) for parity with the other read-model idioms.
    /// </summary>
    public IReadOnlyList<InteractiveTurnView> Turns { get; set; } = [];
}
