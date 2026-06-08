using JasperFx.Events;
using Marten.Events.Aggregation;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Inline single-stream projection that materializes the conversation read-model
/// (<see cref="ConversationLogView"/>) from the per-user Plan stream (Slice 3
/// Unit 2, DEC-079). NET-NEW. It subscribes to the same stream as
/// <see cref="Training.Plan.PlanProjection"/>: the stream-creation
/// <see cref="PlanGenerated"/> seeds an empty per-plan log, and each
/// <see cref="PlanAdaptedFromLog"/> / <see cref="SafetySignalRaised"/> appends one
/// turn. Other plan events on the stream (meso/micro) are not handled and are
/// skipped. Marten 9's JasperFx source generator dispatches the convention methods
/// below, which is why this class is <c>partial</c>.
/// </summary>
/// <remarks>
/// Each turn is keyed by the Marten event id (<see cref="IEvent.Id"/>), so a
/// projection rebuild re-applies the same events and upserts the same turns —
/// exactly one turn per event, no duplicates. The conversation log and the plan
/// read model both commit inline in the same Marten transaction as the event
/// append, so the explanation is atomic with the plan change (DEC-060). Registered
/// with <c>ProjectionLifecycle.Inline</c> in
/// <see cref="Infrastructure.MartenConfiguration"/>.
/// </remarks>
public sealed partial class ConversationProjection : SingleStreamProjection<ConversationLogView, Guid>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationProjection"/> class.
    /// Pins the projection name so the schema-artifact / projection-state row name
    /// survives rename refactors.
    /// </summary>
    public ConversationProjection()
    {
        Name = "ConversationProjection";
    }

    /// <summary>
    /// Seeds an empty conversation log when the Plan stream is created. Keyed by the
    /// plan id (the stream id), so reads resolve via the runner's active plan id.
    /// </summary>
    /// <param name="event">The stream-creation event.</param>
    /// <returns>The initial, empty <see cref="ConversationLogView"/> for the stream.</returns>
    public static ConversationLogView Create(PlanGenerated @event)
    {
        return new ConversationLogView
        {
            PlanId = @event.PlanId,
            UserId = @event.UserId,
        };
    }

    /// <summary>
    /// Appends an assistant-adaptation turn for a <see cref="PlanAdaptedFromLog"/>
    /// event. Accepts the <see cref="IEvent{T}"/> wrapper to read the event id +
    /// timestamp metadata the turn carries.
    /// </summary>
    /// <param name="event">The adaptation event with Marten metadata.</param>
    /// <param name="view">The conversation log being mutated.</param>
    public static void Apply(IEvent<PlanAdaptedFromLog> @event, ConversationLogView view)
    {
        Upsert(view, ConversationTurnView.FromAdaptation(@event.Id, @event.Timestamp, @event.Data));
    }

    /// <summary>
    /// Appends a system-safety turn for a <see cref="SafetySignalRaised"/> event.
    /// Decoupled from any plan change — a safety signal can be the only event for a
    /// triggering log.
    /// </summary>
    /// <param name="event">The safety event with Marten metadata.</param>
    /// <param name="view">The conversation log being mutated.</param>
    public static void Apply(IEvent<SafetySignalRaised> @event, ConversationLogView view)
    {
        Upsert(view, ConversationTurnView.FromSafety(@event.Id, @event.Timestamp, @event.Data));
    }

    private static void Upsert(ConversationLogView view, ConversationTurnView turn)
    {
        // Find-or-replace by the source event id keeps the projection idempotent
        // under replay — re-applying the same event overwrites its single turn
        // rather than appending a duplicate.
        var existing = view.Turns.FindIndex(t => t.TriggeringPlanEventId == turn.TriggeringPlanEventId);
        if (existing >= 0)
        {
            view.Turns[existing] = turn;
        }
        else
        {
            view.Turns.Add(turn);
        }
    }
}
