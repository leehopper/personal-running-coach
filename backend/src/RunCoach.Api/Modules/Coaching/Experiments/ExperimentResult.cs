namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Captures the full result of a single experiment variation run.
/// Includes the configuration, assembled prompt metadata, and the LLM response
/// for comparison across variations.
/// </summary>
public sealed record ExperimentResult
{
    /// <summary>
    /// Gets the experiment variation ID (e.g., "token-8k", "position-end").
    /// </summary>
    public required string VariationId { get; init; }

    /// <summary>
    /// Gets the experiment category.
    /// </summary>
    public required ExperimentCategory Category { get; init; }

    /// <summary>
    /// Gets the description of this variation.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the profile name used for this run.
    /// </summary>
    public required string ProfileName { get; init; }

    /// <summary>
    /// Gets the estimated token count of the assembled prompt.
    /// </summary>
    public required int EstimatedPromptTokens { get; init; }

    /// <summary>
    /// Gets the number of sections in the assembled prompt.
    /// </summary>
    public required int SectionCount { get; init; }

    /// <summary>
    /// Gets the number of start sections.
    /// </summary>
    public required int StartSectionCount { get; init; }

    /// <summary>
    /// Gets the number of middle sections.
    /// </summary>
    public required int MiddleSectionCount { get; init; }

    /// <summary>
    /// Gets the number of end sections.
    /// </summary>
    public required int EndSectionCount { get; init; }

    /// <summary>
    /// Gets the raw LLM response text. Null if not yet executed (dry run).
    /// </summary>
    public string? LlmResponse { get; init; }

    /// <summary>
    /// Gets the timestamp when this result was captured.
    /// </summary>
    public required string Timestamp { get; init; }

    /// <summary>
    /// Gets optional error message if the run failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets a value indicating whether gets whether this result contains a JSON plan block in the response.
    /// </summary>
    public bool HasJsonPlan { get; init; }
}
