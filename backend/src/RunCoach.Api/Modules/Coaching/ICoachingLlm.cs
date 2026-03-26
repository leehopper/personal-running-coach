namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Thin abstraction over an LLM API call for coaching plan generation.
/// Implementations handle model-specific details (API client, authentication,
/// retry logic, response extraction).
/// </summary>
public interface ICoachingLlm
{
    /// <summary>
    /// Sends a system prompt and user message to the LLM and returns the
    /// generated text response.
    /// </summary>
    /// <param name="systemPrompt">The coaching system prompt with persona, safety rules, and context.</param>
    /// <param name="userMessage">The assembled user message with profile data and request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated text response from the LLM.</returns>
    Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct);

    /// <summary>
    /// Sends a system prompt and user message to the LLM and returns a
    /// structured response deserialized to <typeparamref name="T"/>.
    /// Uses Anthropic constrained decoding to guarantee schema-compliant JSON.
    /// </summary>
    /// <typeparam name="T">The structured output record type.</typeparam>
    /// <param name="systemPrompt">The coaching system prompt.</param>
    /// <param name="userMessage">The assembled user message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized structured response.</returns>
    Task<T> GenerateStructuredAsync<T>(string systemPrompt, string userMessage, CancellationToken ct);
}
