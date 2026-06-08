namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Response envelope for <c>GET /api/v1/conversation/turns</c> — the runner's
/// adaptation + safety turns for their active plan, newest-first (Slice 3 Unit 2,
/// DEC-079). <see cref="Turns"/> is empty when the runner has no active plan or no
/// turns have been appended yet (e.g. before PR5 wires production adaptation).
/// </summary>
/// <param name="Turns">The conversation turns, ordered newest-first.</param>
public sealed record ConversationTurnsResponseDto(
    IReadOnlyList<ConversationTurnDto> Turns)
{
    /// <summary>Gets the empty response — no turns to show.</summary>
    public static ConversationTurnsResponseDto Empty { get; } = new([]);
}
