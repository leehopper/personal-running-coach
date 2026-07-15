using JasperFx.Events;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// One persisted turn in a runner's <b>interactive</b> conversation read-model
/// (Slice 4B Unit 3, DEC-085), projected from exactly one user-scoped
/// <c>Conversation</c> stream event. Distinct from the plan-scoped
/// <see cref="ConversationTurnView"/> (proactive adaptation/safety turns).
/// <see cref="TurnId"/> is the per-turn idempotency/replay key, so the projection
/// upserts exactly one entry per logical message.
/// </summary>
public sealed record InteractiveTurnView
{
    /// <summary>Gets the per-turn id — the client GUID for a user turn, the server-derived GUID for a coach turn.</summary>
    public required Guid TurnId { get; init; }

    /// <summary>Gets whether the runner or the coach authored this turn.</summary>
    public required ConversationParticipant Participant { get; init; }

    /// <summary>Gets the turn text. Empty for an errored coach turn (never a partial-as-complete reply).</summary>
    public required string Content { get; init; }

    /// <summary>Gets a value indicating whether this coach turn is an errored marker (the stream died mid-flight). Always false for a user turn.</summary>
    public required bool IsErrored { get; init; }

    /// <summary>Gets the wall-clock time the source event was appended (Marten event timestamp).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the source event's per-stream Marten version (1-based, strictly increasing
    /// with append order) — the deterministic tiebreaker for turns sharing a
    /// <see cref="CreatedAt"/>.
    /// </summary>
    public required long EventVersion { get; init; }

    /// <summary>
    /// Gets the structured actuals of a confirmed conversational log (Slice 3, DEC-091),
    /// set only on the confirm-ack coach turn; <see langword="null"/> for every user turn
    /// and every other coach turn.
    /// </summary>
    public LoggedRunSummary? LoggedRun { get; init; }

    /// <summary>Builds a runner-authored turn from a <see cref="UserMessagePosted"/> event and its Marten metadata.</summary>
    /// <param name="event">The user-message event with Marten metadata.</param>
    /// <returns>The projected user turn.</returns>
    public static InteractiveTurnView FromUser(IEvent<UserMessagePosted> @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return new InteractiveTurnView
        {
            TurnId = @event.Data.TurnId,
            Participant = ConversationParticipant.User,
            Content = @event.Data.Content,
            IsErrored = false,
            CreatedAt = @event.Timestamp,
            EventVersion = @event.Version,
            LoggedRun = null,
        };
    }

    /// <summary>Builds a coach-authored turn from a <see cref="CoachMessagePosted"/> event and its Marten metadata.</summary>
    /// <param name="event">The coach-message event with Marten metadata.</param>
    /// <returns>The projected coach turn. Content is forced empty for an errored marker.</returns>
    public static InteractiveTurnView FromCoach(IEvent<CoachMessagePosted> @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return new InteractiveTurnView
        {
            TurnId = @event.Data.TurnId,
            Participant = ConversationParticipant.Coach,

            // Defense-in-depth: an errored marker never renders partial coaching advice
            // as complete, regardless of what content the event happens to carry.
            Content = @event.Data.IsErrored ? string.Empty : @event.Data.Content,
            IsErrored = @event.Data.IsErrored,
            CreatedAt = @event.Timestamp,
            EventVersion = @event.Version,
            LoggedRun = @event.Data.LoggedRun,
        };
    }
}
