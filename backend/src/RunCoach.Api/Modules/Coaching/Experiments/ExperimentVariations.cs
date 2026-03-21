using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Defines all experiment variations for the 4 context injection experiments.
/// Each variation is a parameterized <see cref="ExperimentConfig"/> that
/// the experiment runner uses to configure the ContextAssembler.
/// </summary>
public static class ExperimentVariations
{
    /// <summary>
    /// Gets all variations for Experiment 1: Token Budget.
    /// Compares plan quality at ~8K, ~12K, and ~15K total context tokens.
    /// </summary>
    public static ImmutableArray<ExperimentConfig> TokenBudget { get; } =
    [
        new ExperimentConfig
        {
            VariationId = "token-8k",
            Category = ExperimentCategory.TokenBudget,
            Description = "Reduced budget ~8K tokens. Layer 2 only, max 2 weeks history, max 3 conversation turns.",
            TotalTokenBudget = 8_000,
            SummarizationMode = SummarizationMode.WeeklySummaryOnly,
            MaxLayer2Weeks = 2,
            MaxConversationTurns = 3,
        },
        new ExperimentConfig
        {
            VariationId = "token-12k",
            Category = ExperimentCategory.TokenBudget,
            Description = "Medium budget ~12K tokens. Mixed summarization, 1 week L1 + 3 weeks L2, 5 conversation turns.",
            TotalTokenBudget = 12_000,
            MaxLayer1Weeks = 1,
            MaxLayer2Weeks = 3,
            MaxConversationTurns = 5,
        },
        new ExperimentConfig
        {
            VariationId = "token-15k",
            Category = ExperimentCategory.TokenBudget,
            Description = "Full budget ~15K tokens (baseline). Mixed summarization, 2 weeks L1 + 4 weeks L2, 10 conversation turns.",
            TotalTokenBudget = 15_000,
        },
    ];

    /// <summary>
    /// Gets all variations for Experiment 2: Positional Placement.
    /// Compares profile-at-start vs. profile-at-end vs. profile-in-middle.
    /// </summary>
    public static ImmutableArray<ExperimentConfig> PositionalPlacement { get; } =
    [
        new ExperimentConfig
        {
            VariationId = "position-start",
            Category = ExperimentCategory.PositionalPlacement,
            Description = "Profile at START (baseline). Stable prefix with high attention per U-curve research.",
            ProfilePlacement = ProfilePlacement.Start,
        },
        new ExperimentConfig
        {
            VariationId = "position-middle",
            Category = ExperimentCategory.PositionalPlacement,
            Description = "Profile in MIDDLE. Low attention zone per U-curve research. Hypothesis: worse profile data usage.",
            ProfilePlacement = ProfilePlacement.Middle,
        },
        new ExperimentConfig
        {
            VariationId = "position-end",
            Category = ExperimentCategory.PositionalPlacement,
            Description = "Profile at END. Recency attention zone. Tests whether proximity to user message compensates.",
            ProfilePlacement = ProfilePlacement.End,
        },
    ];

    /// <summary>
    /// Gets all variations for Experiment 3: Summarization Level.
    /// Compares per-workout history (Layer 1) vs. weekly summary (Layer 2).
    /// </summary>
    public static ImmutableArray<ExperimentConfig> SummarizationLevel { get; } =
    [
        new ExperimentConfig
        {
            VariationId = "summarize-per-workout",
            Category = ExperimentCategory.SummarizationLevel,
            Description = "Per-workout detail (Layer 1) for all history. Maximum detail, highest token cost.",
            SummarizationMode = SummarizationMode.PerWorkoutOnly,
        },
        new ExperimentConfig
        {
            VariationId = "summarize-weekly",
            Category = ExperimentCategory.SummarizationLevel,
            Description = "Weekly summaries (Layer 2) for all history. Minimal detail, lowest token cost.",
            SummarizationMode = SummarizationMode.WeeklySummaryOnly,
        },
        new ExperimentConfig
        {
            VariationId = "summarize-mixed",
            Category = ExperimentCategory.SummarizationLevel,
            Description = "Mixed (baseline). Layer 1 for recent weeks, Layer 2 for older weeks.",
            SummarizationMode = SummarizationMode.Mixed,
        },
    ];

    /// <summary>
    /// Gets all variations for Experiment 4: Conversation History.
    /// Compares 0 turns vs. 5 turns of prior conversation context.
    /// </summary>
    public static ImmutableArray<ExperimentConfig> ConversationHistory { get; } =
    [
        new ExperimentConfig
        {
            VariationId = "conversation-0",
            Category = ExperimentCategory.ConversationHistory,
            Description = "No conversation history (cold start). Tests plan quality without prior context.",
            ConversationTurns = 0,
        },
        new ExperimentConfig
        {
            VariationId = "conversation-5",
            Category = ExperimentCategory.ConversationHistory,
            Description = "5 turns of conversation history. Tests whether prior context improves plan coherence.",
            ConversationTurns = 5,
        },
    ];

    /// <summary>
    /// Gets all experiment variations across all 4 experiments.
    /// </summary>
    public static ImmutableArray<ExperimentConfig> All { get; } =
        [.. TokenBudget, .. PositionalPlacement, .. SummarizationLevel, .. ConversationHistory];
}
