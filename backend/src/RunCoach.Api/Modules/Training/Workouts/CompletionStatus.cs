namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// How fully the runner completed a logged workout. Values are explicitly
/// numbered so reordering members does not shift the stored integer encoding.
/// </summary>
public enum CompletionStatus
{
    /// <summary>The workout was completed as intended.</summary>
    Complete = 0,

    /// <summary>The workout was started but cut short (distance/duration below plan).</summary>
    Partial = 1,

    /// <summary>The workout was not done.</summary>
    Skipped = 2,
}
