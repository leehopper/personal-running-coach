namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// An incremental text delta of a streamed coach answer or clarification (Slice 4B PR4).
/// Emitted as <c>event: token</c>. The client appends deltas into a live bubble and
/// reconciles into the timeline cache once on the terminal <see cref="DoneFrame"/>.
/// </summary>
/// <param name="Delta">The next chunk of answer text (already trademark-scrubbed by the adapter).</param>
public sealed record TokenFrame(string Delta) : IConversationFrame
{
    string IConversationFrame.EventName => "token";
}
