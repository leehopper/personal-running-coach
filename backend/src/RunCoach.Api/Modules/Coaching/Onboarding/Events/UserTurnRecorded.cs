using System.Text.Json;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Records the runner's user turn with the typed Anthropic content blocks they submitted.
/// </summary>
/// <param name="ContentBlocks">
/// Typed Anthropic content blocks the runner supplied (text only at MVP-0; future tool_use
/// blocks round-trip via the JsonDocument). Stored opaquely so non-text block types do not
/// lossy-project.
/// </param>
/// <param name="RecordedAt">Wall-clock time the turn was recorded.</param>
public sealed record UserTurnRecorded(
    JsonDocument ContentBlocks,
    DateTimeOffset RecordedAt);
