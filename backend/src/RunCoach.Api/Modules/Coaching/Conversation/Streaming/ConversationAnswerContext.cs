using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Plan.Models;

namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// The grounded Q&amp;A context for a streamed answer (Slice 4B PR4): the runner's active
/// plan (or <c>null</c> when none), recent logged workouts, and recent interactive turns.
/// Loaded by <see cref="IConversationContextLoader"/> and fed to
/// <c>ContextAssembler.ComposeForConversationAsync</c>.
/// </summary>
/// <param name="Plan">The runner's active plan projection, or <c>null</c> when there is no active plan.</param>
/// <param name="RecentLogs">Recent logged workouts (the assembler orders them newest-first).</param>
/// <param name="RecentTurns">Recent non-errored interactive turns, oldest-first, excluding the current user turn.</param>
public sealed record ConversationAnswerContext(
    PlanProjectionDto? Plan,
    IReadOnlyList<LoggedWorkoutDetail> RecentLogs,
    IReadOnlyList<ConversationContextTurn> RecentTurns);
