namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// The interactive-turn payload of a <see cref="ConversationTimelineTurnDto"/> — the
/// text of a user message or a coach reply (Slice 4B Unit 3, DEC-085). Present only
/// when the wrapper's <see cref="ConversationTimelineTurnDto.Kind"/> is
/// <see cref="ConversationTimelineTurnKind.User"/> or
/// <see cref="ConversationTimelineTurnKind.Coach"/>. The wrapper carries the id and
/// timestamp; this carries only the kind-specific fields.
/// </summary>
/// <param name="Content">The turn text. Empty for an errored coach turn.</param>
/// <param name="IsErrored">True when this coach turn is an errored marker (the stream died mid-flight); always false for a user turn.</param>
public sealed record InteractiveTurnDto(
    string Content,
    bool IsErrored);
