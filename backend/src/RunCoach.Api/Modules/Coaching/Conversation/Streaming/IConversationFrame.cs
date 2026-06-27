namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// A single frame in the streaming Q&amp;A response (Slice 4B PR4). The
/// <see cref="IConversationStreamService"/> yields these in order; the controller
/// serializes each as an SSE frame (<c>event: {EventName}\ndata: {json}\n\n</c>) via
/// <see cref="SseWriter"/>. <see cref="EventName"/> is an <b>explicit</b> interface
/// implementation on every frame so it carries the SSE event name without becoming a
/// serialized JSON property (System.Text.Json ignores explicit interface members), and
/// so a new frame type cannot be added without supplying its event name.
/// </summary>
public interface IConversationFrame
{
    /// <summary>Gets the SSE event name for this frame (e.g. <c>token</c>, <c>done</c>).</summary>
    string EventName { get; }
}
