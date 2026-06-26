namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Event recorded when the coach's reply to an interactive turn completes (Slice 4B
/// Unit 3, DEC-085). Appended to the user-scoped <c>Conversation</c> stream as the
/// <b>second</b> of two separate idempotent writes — the user turn is durable first
/// (<see cref="UserMessagePosted"/>), the coach turn appends only once the stream
/// finishes. The co-transactional <c>IIdempotencyStore</c> + single-Wolverine-
/// <c>SaveChanges</c> model cannot span a multi-second stream, so these are two
/// writes, not one transaction.
/// </summary>
/// <param name="UserId">The runner's user id — also the <c>Conversation</c> stream id.</param>
/// <param name="TurnId">
/// The <b>server-derived</b> turn id, deterministic from the user turn's client
/// message id (see <see cref="ConversationTurnId.DeriveCoachTurnId"/>). Doubles as
/// the idempotency key for the coach-turn write: a duplicate completion for the same
/// user turn re-derives the same id and short-circuits, so the coach turn is never
/// appended twice.
/// </param>
/// <param name="Content">
/// The coach's complete reply text. <b>Empty</b> when <paramref name="IsErrored"/>
/// is <see langword="true"/> — a stream that died mid-flight never persists its
/// partial text as a complete turn (a HARD safety rule: truncated coaching advice
/// must not render as complete).
/// </param>
/// <param name="IsErrored">
/// <see langword="true"/> for an errored-turn marker (the stream failed mid-flight);
/// <see langword="false"/> for a normal, complete reply.
/// </param>
public sealed record CoachMessagePosted(
    Guid UserId,
    Guid TurnId,
    string Content,
    bool IsErrored);
