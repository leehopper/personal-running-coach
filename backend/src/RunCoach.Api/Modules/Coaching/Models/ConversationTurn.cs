namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// A single turn in a coaching conversation, containing a user message
/// and the coach's response.
/// </summary>
public sealed record ConversationTurn(
    string UserMessage,
    string CoachMessage);
