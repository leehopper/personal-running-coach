namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// A terminal error frame (Slice 4B PR4) emitted as <c>event: error</c> when the
/// classifier or the answer stream fails (a DEC-073 Transient/Permanent escape, or a
/// free-text-incomplete finish). The partial answer, if any, is discarded — never
/// reconciled into the timeline — and the client is expected to re-send with a
/// <b>fresh</b> client message id (D5).
/// </summary>
/// <param name="Message">A user-facing, non-leaking failure message.</param>
/// <param name="Retryable">Whether re-sending the request is likely to help (Transient / max-tokens) or not (Permanent).</param>
/// <param name="RetryAfterSeconds">The server-advised back-off in seconds when known (a pre-stream 429 hint); <c>null</c> otherwise.</param>
public sealed record ErrorFrame(string Message, bool Retryable, int? RetryAfterSeconds) : IConversationFrame
{
    string IConversationFrame.EventName => "error";
}
