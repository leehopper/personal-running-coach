namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Command to persist a runner's interactive chat message as the <b>first</b> of the
/// two-write conversation turn pair (Slice 4B Unit 3, DEC-085). Handled by
/// <see cref="PostUserConversationTurnHandler"/> as a tenant-scoped Wolverine handler
/// (<c>InvokeForTenantAsync</c>) so the co-transactional <c>IIdempotencyStore</c> sees
/// a tenanted session. Dispatched <b>before</b> the coach reply stream opens, so the
/// user message is durable first.
/// </summary>
/// <param name="UserId">The runner's user id — the <c>Conversation</c> stream id and the Wolverine tenant id.</param>
/// <param name="ClientMessageId">The client-generated message id — the turn id and the idempotency key.</param>
/// <param name="Content">The runner's message text.</param>
public sealed record PostUserConversationTurn(
    Guid UserId,
    Guid ClientMessageId,
    string Content);
