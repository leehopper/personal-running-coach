namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Describes a single experiment variation for context injection testing.
/// Each config defines a unique combination of token budget, section ordering,
/// summarization mode, and conversation turn count.
/// </summary>
public sealed record ExperimentConfig
{
    /// <summary>
    /// Gets the unique identifier for this variation (e.g., "token-8k", "position-end").
    /// </summary>
    public required string VariationId { get; init; }

    /// <summary>
    /// Gets the experiment category this variation belongs to.
    /// </summary>
    public required ExperimentCategory Category { get; init; }

    /// <summary>
    /// Gets a human-readable description of what this variation tests.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the total token budget for prompt assembly.
    /// </summary>
    public int TotalTokenBudget { get; init; } = 15_000;

    /// <summary>
    /// Gets the positional placement strategy for profile data.
    /// </summary>
    public ProfilePlacement ProfilePlacement { get; init; } = ProfilePlacement.Start;

    /// <summary>
    /// Gets the summarization mode for training history.
    /// </summary>
    public SummarizationMode SummarizationMode { get; init; } = SummarizationMode.Mixed;

    /// <summary>
    /// Gets the number of conversation history turns to include.
    /// </summary>
    public int ConversationTurns { get; init; }

    /// <summary>
    /// Gets the maximum conversation turns allowed before truncation.
    /// </summary>
    public int MaxConversationTurns { get; init; } = 10;

    /// <summary>
    /// Gets the maximum weeks of Layer 1 (per-workout) history.
    /// </summary>
    public int MaxLayer1Weeks { get; init; } = 2;

    /// <summary>
    /// Gets the maximum weeks of Layer 2 (weekly summary) history.
    /// </summary>
    public int MaxLayer2Weeks { get; init; } = 4;
}
