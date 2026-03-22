namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Strongly-typed settings for the coaching LLM adapter.
/// Mapped from the "Anthropic" configuration section.
/// </summary>
public sealed record CoachingLlmSettings
{
    /// <summary>
    /// The configuration section name in appsettings/user-secrets.
    /// </summary>
    public const string SectionName = "Anthropic";

    /// <summary>
    /// Gets the Anthropic API key. Must be provided via user-secrets or
    /// environment variables — never committed to source.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Claude model identifier (e.g., "claude-sonnet-4-5-20250514").
    /// Defaults to the value from coaching-v1.yaml but can be overridden.
    /// </summary>
    public string ModelId { get; init; } = "claude-sonnet-4-5-20250514";

    /// <summary>
    /// Gets temperature for generation. Lower values produce more deterministic output.
    /// Defaults to 0.3 per coaching-v1.yaml.
    /// </summary>
    public double Temperature { get; init; } = 0.3;

    /// <summary>
    /// Gets maximum tokens for the response.
    /// Defaults to 4096 per coaching-v1.yaml.
    /// </summary>
    public int MaxTokens { get; init; } = 4096;

    /// <summary>
    /// Gets maximum number of retries for failed requests (rate limits, transient errors).
    /// The Anthropic SDK handles retries with exponential backoff.
    /// Defaults to 3.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Gets request timeout in seconds. Defaults to 120 seconds (2 minutes)
    /// to accommodate longer plan generation responses.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// Gets the model identifier for LLM-as-judge calls (e.g., safety rubric evaluation).
    /// Defaults to Claude Haiku 4.5 for cost-effective judging.
    /// </summary>
    public string JudgeModelId { get; init; } = "claude-haiku-4-5-20251001";
}
