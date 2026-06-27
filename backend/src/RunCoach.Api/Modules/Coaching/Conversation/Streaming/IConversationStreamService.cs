namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// Orchestrates a single streaming Q&amp;A turn for <c>POST /api/v1/conversation/messages</c>
/// (Slice 4B PR4, the integration gate). Persists the user turn durable-first, runs the
/// deterministic safety gate, classifies intent, and routes to a streamed answer, a
/// confirmation card, a clarification, or a scripted safety turn — yielding the response
/// as an ordered sequence of <see cref="IConversationFrame"/>. The controller is a thin
/// transport that serializes each frame as an SSE event; all coaching/persistence logic
/// lives here so the controller stays an entry point only.
/// </summary>
public interface IConversationStreamService
{
    /// <summary>Streams the coach's response to one runner message as ordered frames.</summary>
    /// <param name="userId">The authenticated runner (also the Marten tenant + conversation stream id).</param>
    /// <param name="message">The runner's raw free-text message (sanitized internally; never pre-sanitized by the caller).</param>
    /// <param name="clientMessageId">The client-generated message id keying the durable-first user turn.</param>
    /// <param name="ct">The request-aborted token; a client disconnect is surfaced as a clean abort, never a fault.</param>
    /// <returns>The ordered response frames (token / safety / card / error / done).</returns>
    IAsyncEnumerable<IConversationFrame> StreamReplyAsync(
        Guid userId, string message, Guid clientMessageId, CancellationToken ct);
}
