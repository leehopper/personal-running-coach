using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// Source-generated <c>[LoggerMessage]</c> partials for <see cref="ConversationStreamService"/>
/// (CA1848). Kept in a dedicated partial file so the orchestration body holds no static
/// members interleaved with its instance methods (SA1204).
/// </summary>
public sealed partial class ConversationStreamService
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Conversation safety gate Red short-circuit user={UserId} category={Category}: scripted turn emitted, no LLM")]
    private static partial void LogRedShortCircuit(ILogger logger, Guid userId, ReferralCategory category);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Conversation Amber referral surfaced user={UserId} category={Category}: scripted referral turn persisted alongside the answer")]
    private static partial void LogAmberReferral(ILogger logger, Guid userId, ReferralCategory category);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Conversation intent classifier failed user={UserId}; surfacing an error frame, intent never guessed")]
    private static partial void LogClassifierFailed(ILogger logger, Guid userId, Exception ex);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Conversation workout-log card emitted user={UserId} prescriptionMatched={Matched}: nothing committed pending Confirm")]
    private static partial void LogWorkoutCard(ILogger logger, Guid userId, bool matched);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Conversation answer stream failed user={UserId} retryable={Retryable}: errored marker persisted, partial discarded")]
    private static partial void LogAnswerStreamFailed(ILogger logger, Guid userId, bool retryable);
}
