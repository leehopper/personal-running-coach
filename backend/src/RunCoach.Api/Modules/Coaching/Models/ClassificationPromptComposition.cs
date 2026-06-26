using System.Collections.Immutable;
using System.Linq;
using RunCoach.Api.Modules.Coaching.Sanitization;

namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// Result of <see cref="IContextAssembler.ComposeForClassificationAsync"/> — the
/// classifier system prompt plus the user message carrying today's date and the
/// sanitized, delimiter-wrapped runner message (Slice 4B / DEC-085 D3).
/// </summary>
/// <param name="SystemPrompt">
/// The byte-stable classifier system prompt loaded from
/// <c>Prompts/conversation-classifier.v1.yaml</c>.
/// </param>
/// <param name="UserMessage">
/// The composed user message: today's date plus the sanitized, spotlight-wrapped
/// runner message (the single sanitization owner is the assembler).
/// </param>
/// <param name="Findings">
/// PII-free sanitization audit trail describing which patterns matched and whether
/// they were neutralized vs. log-only. Empty when nothing tripped.
/// </param>
public sealed record ClassificationPromptComposition(
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
