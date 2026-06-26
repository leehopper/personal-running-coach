namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Response from the two interactive-turn persistence handlers
/// (<see cref="PostUserConversationTurn"/> / <see cref="PostCoachConversationTurn"/>),
/// Slice 4B Unit 3. Carries the persisted turn id back to the caller (the SSE
/// endpoint, PR4). Recorded by <c>IIdempotencyStore</c> so a retried write returns
/// the byte-identical prior response without re-appending. Not a wire DTO — never
/// surfaced directly on a controller.
/// </summary>
/// <param name="TurnId">
/// The persisted turn id — the client message id for a user turn, the server-derived
/// id for a coach turn.
/// </param>
public sealed record ConversationTurnPostedResponse(Guid TurnId);
