using RunCoach.Api.Modules.Coaching.Models;

namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Assembles the full prompt payload from user data, enforcing positional
/// layout and token budget per the context-injection-v1.yaml specification.
/// </summary>
public interface IContextAssembler
{
    /// <summary>
    /// Builds the full prompt payload from the provided input data.
    /// Applies positional layout (stable prefix, variable middle, conversational end)
    /// and enforces the token budget by truncating/summarizing as needed.
    /// </summary>
    /// <param name="input">All input data for prompt assembly.</param>
    /// <returns>The assembled prompt with structured sections and token estimate.</returns>
    AssembledPrompt Assemble(ContextAssemblerInput input);

    /// <summary>
    /// Estimates the token count for a given text using the character ratio method
    /// (characters / 4 with a 10% safety margin).
    /// </summary>
    /// <param name="text">The text to estimate tokens for.</param>
    /// <returns>The estimated token count.</returns>
    int EstimateTokens(string text);
}
