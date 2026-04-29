using System.Text.Json;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Records the assistant's reply turn with the typed Anthropic content blocks it produced.
/// </summary>
/// <param name="ContentBlocks">
/// Typed Anthropic content blocks the assistant produced (text + optional thinking blocks).
/// Stored opaquely so non-text block types do not lossy-project.
/// </param>
/// <param name="RecordedAt">Wall-clock time the turn was recorded.</param>
public sealed record AssistantTurnRecorded(
    JsonDocument ContentBlocks,
    DateTimeOffset RecordedAt);
