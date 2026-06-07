using System.Collections.Immutable;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// All input data required by the ContextAssembler to build a prompt payload.
/// Bundles user profile, goal state, fitness data, and optional history/conversation.
/// </summary>
public sealed record ContextAssemblerInput(
    UserProfile UserProfile,
    GoalState GoalState,
    FitnessEstimate FitnessEstimate,
    TrainingPaces TrainingPaces,
    ImmutableArray<WorkoutSummary> TrainingHistory,
    ImmutableArray<ConversationTurn> ConversationHistory,
    string CurrentUserMessage)
{
    /// <summary>
    /// Gets the recent real logged workouts (DEC-076 / slice-2b Unit 5),
    /// rendered into the training-history block as compact Layer-1 one-liners
    /// with inlined metrics + notes. Defaults to empty so existing callers
    /// (test profiles supplying only <see cref="TrainingHistory"/>) produce
    /// byte-identical prompts. A bounded recent window — the caller supplies
    /// only the logs worth surfacing; they are never collapsed into weekly
    /// summaries.
    /// </summary>
    public ImmutableArray<LoggedWorkoutDetail> RecentLoggedWorkouts { get; init; } = [];
}
