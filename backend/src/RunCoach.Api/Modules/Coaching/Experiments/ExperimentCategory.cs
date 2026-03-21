namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// The experiment category grouping related variations.
/// </summary>
public enum ExperimentCategory
{
    /// <summary>
    /// Experiment 1: Compare plan quality at different token budgets.
    /// </summary>
    TokenBudget,

    /// <summary>
    /// Experiment 2: Compare profile positional placement strategies.
    /// </summary>
    PositionalPlacement,

    /// <summary>
    /// Experiment 3: Compare training history summarization levels.
    /// </summary>
    SummarizationLevel,

    /// <summary>
    /// Experiment 4: Compare conversation history turn counts.
    /// </summary>
    ConversationHistory,
}
