using System.Collections.Immutable;
using System.Linq;
using RunCoach.Api.Modules.Coaching.Sanitization;

namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// Result of <see cref="IContextAssembler.ComposeForConversationAsync"/> — the
/// coaching system prompt plus the grounded Q&amp;A user message for a streamed
/// answer (Slice 4B / DEC-085). The answer reuses <c>coaching-system.v1</c> (the
/// register re-tuned in Slice 4A — no further prompt re-tune).
/// </summary>
/// <param name="SystemPrompt">
/// The byte-stable coaching system prompt loaded from <c>Prompts/coaching-system.v1.yaml</c>.
/// </param>
/// <param name="UserMessage">
/// The composed grounded context: the current plan summary, recent logged workouts
/// (newest-first, sanitized + spotlight-wrapped), recent interactive turns, and the
/// sanitized, spotlight-wrapped current runner message.
/// </param>
/// <param name="Findings">
/// PII-free sanitization audit trail for the current runner message. Empty when nothing tripped.
/// </param>
public sealed record ConversationPromptComposition(
    string SystemPrompt,
    string UserMessage,
    ImmutableArray<SanitizationFinding> Findings)
{
    /// <summary>
    /// Gets a value indicating whether the sanitizer stripped any content. Computed
    /// from <see cref="Findings"/> so it can never desynchronize from the audit trail.
    /// </summary>
    public bool Neutralized => Findings.Any(f => f.Stripped);
}
