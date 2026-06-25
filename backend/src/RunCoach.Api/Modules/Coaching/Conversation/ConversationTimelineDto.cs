namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Response envelope for <c>GET /api/v1/conversation/timeline</c> (Slice 4B Unit 3,
/// DEC-085) — the runner's composed conversation, unioning their user-scoped
/// interactive turns with the current plan's proactive adaptation/safety turns into
/// one <b>oldest-first</b> ordered list (a chat composer wants chronological flow;
/// the sibling <c>GET /conversation/turns</c> stays newest-first and untouched).
/// </summary>
/// <param name="Turns">The composed turns, ordered oldest-first.</param>
public sealed record ConversationTimelineDto(
    IReadOnlyList<ConversationTimelineTurnDto> Turns)
{
    /// <summary>Gets the empty timeline — no turns yet.</summary>
    public static ConversationTimelineDto Empty { get; } = new([]);
}
