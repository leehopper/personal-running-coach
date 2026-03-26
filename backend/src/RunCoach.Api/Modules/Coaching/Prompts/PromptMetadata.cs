namespace RunCoach.Api.Modules.Coaching.Prompts;

/// <summary>
/// Optional metadata associated with a prompt template.
/// Provides descriptive and authorship information for prompt versioning.
/// </summary>
public sealed record PromptMetadata(
    string? Description,
    string? Author,
    string? CreatedAt);
