namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// The Marten document read-model accumulating a runner's coaching conversation
/// turns for one plan (Slice 3 Unit 2, DEC-079). Keyed by <see cref="PlanId"/> (the
/// Plan stream id), it is materialized by <c>ConversationProjection</c> consuming
/// the same per-user Plan stream as <c>PlanProjection</c>: the stream-creation
/// <c>PlanGenerated</c> seeds an empty log, and each <c>PlanAdaptedFromLog</c> /
/// <c>SafetySignalRaised</c> appends one <see cref="ConversationTurnView"/>.
/// Scoping the log to the plan (not the user) keeps the panel showing explanations
/// for the currently-active plan; a regenerated plan starts a fresh log.
/// </summary>
public sealed record ConversationLogView
{
    /// <summary>Gets or sets the plan id (also the Marten stream id and document identity).</summary>
    public Guid PlanId { get; set; }

    /// <summary>Gets or sets the runner's user id — the plan's owner.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the turns in append (chronological) order. The read endpoint
    /// reverses to newest-first. Each turn is keyed by its source event id so a
    /// projection rebuild yields exactly one entry per event. Typed
    /// <see cref="IReadOnlyList{T}"/> (mutated by whole-collection reassignment in
    /// the projection) for parity with <c>PlanProjectionDto</c>'s read-model idiom.
    /// </summary>
    public IReadOnlyList<ConversationTurnView> Turns { get; set; } = [];
}
