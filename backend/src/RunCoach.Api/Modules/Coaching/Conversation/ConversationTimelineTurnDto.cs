namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// One turn in the composed conversation timeline (Slice 4B Unit 3, DEC-085) — a
/// discriminated union over the interactive user/coach turns and the proactive
/// adaptation/safety turns, unioned oldest-first by <c>GET /api/v1/conversation/timeline</c>.
/// <see cref="Kind"/> says which payload is populated: <see cref="Interactive"/> for
/// <see cref="ConversationTimelineTurnKind.User"/>/<see cref="ConversationTimelineTurnKind.Coach"/>,
/// <see cref="Proactive"/> (the existing <see cref="ConversationTurnDto"/> shape, reused)
/// for <see cref="ConversationTimelineTurnKind.Adaptation"/>/<see cref="ConversationTimelineTurnKind.Safety"/>.
/// The non-active payload is null. (The OpenAPI schema filter cannot express that
/// per-member nullability on a <c>$ref</c>, so the frontend hand-writes the discriminated
/// model; the generated Zod is a tripwire only.)
/// </summary>
/// <param name="Kind">Which of the four turn variants this is — the render discriminator.</param>
/// <param name="TurnId">The stable per-turn id (the interactive turn id, or the proactive turn's source event id).</param>
/// <param name="CreatedAt">The turn's timestamp — the union's oldest-first ordering key.</param>
/// <param name="Interactive">The interactive payload when <see cref="Kind"/> is User/Coach; otherwise null.</param>
/// <param name="Proactive">The proactive adaptation/safety payload when <see cref="Kind"/> is Adaptation/Safety; otherwise null.</param>
public sealed record ConversationTimelineTurnDto(
    ConversationTimelineTurnKind Kind,
    Guid TurnId,
    DateTimeOffset CreatedAt,
    InteractiveTurnDto? Interactive,
    ConversationTurnDto? Proactive);
