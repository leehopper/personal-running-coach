using System.Collections.Immutable;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Profiles;

/// <summary>
/// A complete test profile containing user data, goal state, and training history.
/// This is the top-level fixture used by the context assembler and eval suite.
/// </summary>
/// <param name="UserProfile">The runner's biographical and preference data.</param>
/// <param name="GoalState">Current goal with fitness estimate and derived training paces.</param>
/// <param name="TrainingHistory">Simulated workout history (empty for beginners).</param>
public sealed record TestProfile(
    UserProfile UserProfile,
    GoalState GoalState,
    ImmutableArray<WorkoutSummary> TrainingHistory);
