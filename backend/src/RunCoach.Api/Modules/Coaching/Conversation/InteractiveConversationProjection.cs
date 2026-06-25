using JasperFx.Events;
using Marten.Events.Aggregation;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Inline single-stream projection that materializes the runner's <b>interactive</b>
/// conversation read-model (<see cref="ConversationView"/>) from the user-scoped
/// <c>Conversation</c> stream (Slice 4B Unit 3, DEC-085). NET-NEW, keyed by user id
/// (the stream id) so the conversation survives plan regeneration. Mirrors the
/// only other user-keyed precedent, <see cref="Onboarding.OnboardingProjection"/>:
/// the first <see cref="UserMessagePosted"/> <c>Create</c>s the view, and every
/// subsequent <see cref="UserMessagePosted"/> / <see cref="CoachMessagePosted"/>
/// <c>Apply</c>s one turn. Marten 9's JasperFx source generator dispatches the
/// convention methods below, which is why this class is <c>partial</c>.
/// </summary>
/// <remarks>
/// Each turn is keyed by <see cref="InteractiveTurnView.TurnId"/>, so a projection
/// rebuild re-applies the same events and upserts the same turns — exactly one turn
/// per logical message, no duplicates. The user turn is durable-first and the coach
/// turn appends on completion, so the stream is always created by a
/// <see cref="UserMessagePosted"/>; a coach turn only ever appends to an existing
/// stream. Registered with <c>ProjectionLifecycle.Inline</c> in
/// <see cref="Infrastructure.MartenConfiguration"/>. This projection does <b>not</b>
/// touch the plan-scoped <see cref="ConversationLogView"/> / <see cref="ConversationProjection"/>.
/// </remarks>
public sealed partial class InteractiveConversationProjection : SingleStreamProjection<ConversationView, Guid>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InteractiveConversationProjection"/>
    /// class. Pins the projection name so the schema-artifact / projection-state row
    /// name survives rename refactors.
    /// </summary>
    public InteractiveConversationProjection()
    {
        Name = "InteractiveConversationProjection";
    }

    /// <summary>
    /// Seeds the conversation view on the first turn (always a
    /// <see cref="UserMessagePosted"/>, since the user turn is durable-first) and
    /// records that first turn. Accepts the <see cref="IEvent{T}"/> wrapper to read
    /// the stream id (= user id) plus the event timestamp/version the turn carries.
    /// </summary>
    /// <param name="event">The stream-creation user-message event with Marten metadata.</param>
    /// <returns>The initial <see cref="ConversationView"/> for the stream.</returns>
    public static ConversationView Create(IEvent<UserMessagePosted> @event)
    {
        var view = new ConversationView
        {
            Id = @event.Data.UserId,
            UserId = @event.Data.UserId,
        };
        Upsert(view, InteractiveTurnView.FromUser(@event));
        return view;
    }

    /// <summary>Appends a runner-authored turn for a subsequent <see cref="UserMessagePosted"/> event.</summary>
    /// <param name="event">The user-message event with Marten metadata.</param>
    /// <param name="view">The conversation view being mutated.</param>
    public static void Apply(IEvent<UserMessagePosted> @event, ConversationView view)
    {
        Upsert(view, InteractiveTurnView.FromUser(@event));
    }

    /// <summary>Appends a coach-authored (or errored) turn for a <see cref="CoachMessagePosted"/> event.</summary>
    /// <param name="event">The coach-message event with Marten metadata.</param>
    /// <param name="view">The conversation view being mutated.</param>
    public static void Apply(IEvent<CoachMessagePosted> @event, ConversationView view)
    {
        Upsert(view, InteractiveTurnView.FromCoach(@event));
    }

    private static void Upsert(ConversationView view, InteractiveTurnView turn)
    {
        // Find-or-replace by the per-turn id keeps the projection idempotent under
        // replay and against a duplicate coach append (the two-write idempotency at
        // the handler layer is the primary guard; this is the projection-side backstop).
        // Mutate by whole-collection reassignment (the read-model idiom) since Turns
        // is exposed as an IReadOnlyList.
        var turns = view.Turns.ToList();
        var existing = turns.FindIndex(t => t.TurnId == turn.TurnId);
        if (existing >= 0)
        {
            turns[existing] = turn;
        }
        else
        {
            turns.Add(turn);
        }

        view.Turns = turns;
    }
}
