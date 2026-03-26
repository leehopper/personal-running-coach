namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// A single named section within the assembled prompt payload.
/// </summary>
public sealed record PromptSection(
    string Key,
    string Content,
    int EstimatedTokens);
