using System.Collections.Immutable;
using System.Linq;
using RunCoach.Api.Modules.Coaching.Sanitization;

namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// Result of <see cref="IContextAssembler.ComposeForAckAsync"/> — the coaching system prompt
/// plus the user message for the short acknowledgment turn the coach streams after a
/// conversational-logging confirm commits and its adaptation has run (Slice 4B PR5 / DEC-085).
/// The ack reuses the active coaching system prompt (the gruff-direct register re-tuned in
/// Slice 4A — DEC-084) so its voice is inherited, not re-specified.
/// </summary>
/// <param name="SystemPrompt">
/// The byte-stable coaching system prompt resolved from the active prompt version.
/// </param>
/// <param name="UserMessage">
/// The composed ack instruction: the deterministic logged-run facts, the adaptation outcome
/// cue, and the runner's note (when present, sanitized + spotlight-wrapped as data). The
/// instruction forbids inventing the specific plan change — the plan diff is authoritative.
/// </param>
/// <param name="Findings">
/// PII-free sanitization audit trail for the runner's note. Empty when there was no note or
/// nothing tripped.
/// </param>
public sealed record AckPromptComposition(
    string SystemPrompt,
    string UserMessage,
    ImmutableArray<SanitizationFinding> Findings)
{
    /// <summary>
    /// Gets a value indicating whether the sanitizer stripped any note content. Computed from
    /// <see cref="Findings"/> so it can never desynchronize from the audit trail.
    /// </summary>
    public bool Neutralized => Findings.Any(f => f.Stripped);
}
