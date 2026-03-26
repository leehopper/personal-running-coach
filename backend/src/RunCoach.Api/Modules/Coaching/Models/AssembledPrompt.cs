using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// The fully assembled prompt payload produced by the ContextAssembler.
/// Sections are returned as structured data so the caller can construct
/// the API call (e.g., system prompt vs user messages).
/// </summary>
public sealed record AssembledPrompt(
    string SystemPrompt,
    ImmutableArray<PromptSection> StartSections,
    ImmutableArray<PromptSection> MiddleSections,
    ImmutableArray<PromptSection> EndSections,
    int EstimatedTokenCount);
