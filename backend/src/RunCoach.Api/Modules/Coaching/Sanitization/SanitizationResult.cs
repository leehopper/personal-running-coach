using System.Collections.Generic;

namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// Result of a single <see cref="IPromptSanitizer.SanitizeAsync"/> call.
/// </summary>
/// <param name="Sanitized">
/// The wrapped, normalized text ready for prompt assembly. Includes the
/// per-section containment delimiter (e.g. <c>&lt;CURRENT_USER_INPUT id="..."&gt;…&lt;/CURRENT_USER_INPUT&gt;</c>).
/// Deterministic for the same input + section, except for the per-turn
/// <c>id="…"</c> nonce which is appended on the non-cached tail to preserve
/// Anthropic prompt-cache prefix stability per DEC-047.
/// </param>
/// <param name="Neutralized">
/// True if any content was actually stripped (Unicode-strip or DAN-family
/// neutralize on <see cref="PromptSection.CurrentUserMessage"/>). False when
/// findings exist in log-only mode without modification.
/// </param>
/// <param name="Findings">
/// Ordered list of detector hits. PII-free — see <see cref="SanitizationFinding"/>.
/// </param>
public readonly record struct SanitizationResult(
    string Sanitized,
    bool Neutralized,
    IReadOnlyList<SanitizationFinding> Findings);
