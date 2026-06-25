namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Command to persist the coach's reply as the <b>second</b> of the two-write
/// conversation turn pair (Slice 4B Unit 3, DEC-085). Handled by
/// <see cref="PostCoachConversationTurnHandler"/> as a tenant-scoped Wolverine
/// handler. The SSE endpoint (PR4) dispatches this <b>once on completion</b> (or with
/// <see cref="IsErrored"/> set when the stream died mid-flight). Idempotent on the
/// server-derived turn id (<see cref="ConversationTurnId.DeriveCoachTurnId"/> over
/// <see cref="ClientMessageId"/>), so a duplicate completion never double-appends.
/// </summary>
/// <param name="UserId">The runner's user id — the <c>Conversation</c> stream id and the Wolverine tenant id.</param>
/// <param name="ClientMessageId">
/// The originating user turn's client message id. The coach turn id is derived from
/// it deterministically; the coach write is idempotent on that derived id.
/// </param>
/// <param name="Content">The coach's complete reply text. Ignored (forced empty) when <see cref="IsErrored"/> is true.</param>
/// <param name="IsErrored">True to persist an errored-turn marker (the stream failed mid-flight); false for a complete reply.</param>
public sealed record PostCoachConversationTurn(
    Guid UserId,
    Guid ClientMessageId,
    string Content,
    bool IsErrored);
