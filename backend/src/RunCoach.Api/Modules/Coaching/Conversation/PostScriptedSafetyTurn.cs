namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Wolverine command (Slice 4B PR4) to persist a scripted safety referral as a coach
/// turn on the user-scoped <c>Conversation</c> stream — the Amber injury / RED-S referral
/// that accompanies a chat answer. Distinct from <see cref="PostCoachConversationTurn"/>
/// because an Amber message persists BOTH this scripted referral and a streamed answer
/// off the same client message id: the caller supplies a server-derived
/// <see cref="TurnId"/> (<see cref="ConversationTurnId.DeriveSafetyTurnId"/>) so the two
/// turns occupy distinct idempotency keys and a re-send appends neither twice.
/// </summary>
/// <param name="UserId">The runner whose conversation stream the turn appends to (also the tenant).</param>
/// <param name="TurnId">The server-derived, deterministic referral turn id (the idempotency key).</param>
/// <param name="Content">The scripted referral copy (system-authored; sanitizer-bypassed by the caller).</param>
public sealed record PostScriptedSafetyTurn(Guid UserId, Guid TurnId, string Content);
