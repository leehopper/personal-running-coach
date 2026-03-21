using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Observations from the conversation history experiment.
/// </summary>
public sealed record ConversationHistoryObservations
{
    /// <summary>
    /// Gets average token usage per variation ID.
    /// </summary>
    public required ImmutableDictionary<string, double> AverageTokensByVariation { get; init; }

    /// <summary>
    /// Gets a value indicating whether adding conversation history increases token usage.
    /// </summary>
    public required bool ConversationAddsTokens { get; init; }

    /// <summary>
    /// Gets the approximate number of additional tokens from 5 conversation turns.
    /// </summary>
    public required int AdditionalTokensFromConversation { get; init; }
}
