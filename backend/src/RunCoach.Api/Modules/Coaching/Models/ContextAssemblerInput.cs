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
    string CurrentUserMessage);
