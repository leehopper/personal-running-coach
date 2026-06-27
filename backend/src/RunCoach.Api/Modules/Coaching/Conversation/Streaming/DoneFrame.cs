namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// The terminal success frame (Slice 4B PR4) emitted as <c>event: done</c> after a coach
/// turn (answer, scripted safety turn, or clarification) completes. The workout-log
/// confirmation card is NOT terminated by a done frame: that branch emits a <c>card</c>
/// frame and commits nothing (no coach turn, so no persisted turn id), pending an explicit
/// Confirm (PR5). Carries the persisted <see cref="TurnId"/> — load-bearing for the client's
/// reconcile-once: the live bubble is folded into the timeline cache keyed on this id.
/// </summary>
/// <param name="TurnId">The persisted coach/safety turn id (server-derived for the coach reply).</param>
public sealed record DoneFrame(Guid TurnId) : IConversationFrame
{
    string IConversationFrame.EventName => "done";
}
