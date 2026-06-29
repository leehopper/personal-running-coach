using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Request body for <c>POST /api/v1/conversation/logs/confirm</c> (Slice 4B PR5 / DEC-085 D4):
/// the runner's explicit, button-driven confirmation of the parsed workout card. Carries the
/// advisory <see cref="StructuredLogDraft"/> the classifier extracted plus the card's client
/// message id. The EF-row idempotency key is DERIVED server-side from <see cref="ClientMessageId"/>
/// (DEC-077) — the body never carries it — and the server resolves the prescription itself;
/// neither is trusted from the client.
/// </summary>
/// <param name="Draft">The confirmed workout draft (runner actuals in their stated units).</param>
/// <param name="ClientMessageId">
/// The card's client message id. Three distinct idempotency mechanisms key off it: the durable
/// conversation turn, the derived EF-row idempotency key, and the derived ack coach-turn id.
/// </param>
public sealed record ConfirmConversationalLogRequestDto(
    [property: JsonRequired] StructuredLogDraft Draft,
    [property: JsonRequired] Guid ClientMessageId);
