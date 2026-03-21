namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// How training history is summarized in the prompt.
/// </summary>
public enum SummarizationMode
{
    /// <summary>
    /// Mixed: Layer 1 (per-workout) for recent weeks, Layer 2 (weekly summary) for older. Default.
    /// </summary>
    Mixed,

    /// <summary>
    /// Layer 1 only: per-workout detail for all history.
    /// </summary>
    PerWorkoutOnly,

    /// <summary>
    /// Layer 2 only: weekly summaries for all history.
    /// </summary>
    WeeklySummaryOnly,
}
