using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// Owns the read-side grounding for the streaming Q&amp;A endpoint (Slice 4B PR4): the
/// app-local date, the server-authoritative candidate-prescription resolution, and the
/// composed answer context (plan + recent logs + recent interactive turns). Extracted
/// from <see cref="ConversationStreamService"/> so the orchestrator depends on one
/// read-side collaborator rather than five raw data-access services (REVIEW.md DI rule).
/// </summary>
public interface IConversationContextLoader
{
    /// <summary>Gets the app-local "today" (the classifier resolves relative dates against it).</summary>
    DateOnly Today();

    /// <summary>
    /// Resolves the server-authoritative candidate prescription a run on
    /// <paramref name="occurredOn"/> would match (the confirmation card's candidate);
    /// <c>null</c> for an off-plan / unscheduled run.
    /// </summary>
    Task<WorkoutPrescriptionSnapshot?> ResolveCandidatePrescriptionAsync(
        Guid userId, DateOnly occurredOn, CancellationToken ct);

    /// <summary>
    /// Loads the grounded Q&amp;A context: the active plan, recent logged workouts, and
    /// recent interactive turns (excluding the just-persisted current user turn and any
    /// errored markers), on one per-request tenanted Marten session.
    /// </summary>
    Task<ConversationAnswerContext> LoadAnswerContextAsync(Guid userId, Guid clientMessageId, CancellationToken ct);
}
