using RunCoach.Api.Modules.Coaching.Models;

namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Assembles the full prompt payload from user data, enforcing positional
/// layout and token budget. Loads system prompts from versioned YAML files
/// via <see cref="Prompts.IPromptStore"/> and renders context templates
/// with <see cref="Prompts.PromptRenderer"/>.
///
/// The assembled prompt is split into a static prefix (coaching persona,
/// safety rules, semantic output guidance) suitable for Anthropic prompt
/// caching, and a dynamic suffix (rendered athlete context, conversation
/// history) that changes per request.
/// </summary>
public interface IContextAssembler
{
    /// <summary>
    /// Builds the full prompt payload from the provided input data.
    /// Loads the system prompt from the configured YAML prompt store.
    /// Applies positional layout (stable prefix, variable middle, conversational end)
    /// and enforces the token budget by truncating/summarizing as needed.
    /// </summary>
    /// <param name="input">All input data for prompt assembly.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assembled prompt with structured sections and token estimate.</returns>
    Task<AssembledPrompt> AssembleAsync(ContextAssemblerInput input, CancellationToken ct = default);

    /// <summary>
    /// Estimates the token count for a given text using the character ratio method
    /// (characters / 4 with a 10% safety margin).
    /// </summary>
    /// <param name="text">The text to estimate tokens for.</param>
    /// <returns>The estimated token count.</returns>
    int EstimateTokens(string text);
}
