namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// A prior interactive turn fed into the conversation Q&amp;A context (Slice 4B). A
/// decoupled prompt-input shape over the persisted <c>InteractiveTurnView</c>: the
/// caller (the streaming endpoint) loads recent non-errored turns from the user-scoped
/// <c>ConversationView</c>, filters out errored coach markers (empty content), orders
/// them chronologically, and maps each to this pair so <c>ContextAssembler</c> need not
/// depend on the Marten read-model shape.
/// </summary>
/// <param name="Participant">Whether the turn was authored by the runner or the coach.</param>
/// <param name="Content">The turn's text content (never an errored/empty coach marker).</param>
public sealed record ConversationContextTurn(ConversationParticipant Participant, string Content);
