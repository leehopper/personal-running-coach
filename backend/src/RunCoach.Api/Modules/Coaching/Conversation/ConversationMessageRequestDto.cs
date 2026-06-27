using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Request body for <c>POST /api/v1/conversation/messages</c> (Slice 4B PR4) — the
/// streaming Q&amp;A endpoint. Carries the runner's free-text message and a
/// client-generated message id that keys the durable-first user-turn idempotency marker
/// (and, derived server-side, the coach reply's). A re-send after a mid-stream failure
/// uses a <b>fresh</b> <see cref="ClientMessageId"/> (D5), which derives a fresh coach id
/// so the original errored turn is left intact.
/// </summary>
/// <param name="Message">The runner's free-text message. Sanitized server-side before it reaches the safety gate, the classifier, or the answer assembly.</param>
/// <param name="ClientMessageId">
/// Client-generated message id (typically <c>crypto.randomUUID()</c>). Marked
/// <see cref="JsonRequiredAttribute"/> so System.Text.Json refuses to under-post a
/// default <see cref="Guid"/> when the field is omitted.
/// </param>
public sealed record ConversationMessageRequestDto(
    [property: JsonRequired] string Message,
    [property: JsonRequired] Guid ClientMessageId);
