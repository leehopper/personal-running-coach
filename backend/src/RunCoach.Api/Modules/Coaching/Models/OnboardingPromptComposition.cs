using System.Collections.Immutable;
using RunCoach.Api.Modules.Coaching.Sanitization;

namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// Result of <see cref="IContextAssembler.ComposeForOnboardingAsync"/> — the
/// system + user message bytes plus the sanitization audit trail produced
/// per R-068 / DEC-059.
/// </summary>
/// <param name="SystemPrompt">
/// The byte-stable onboarding system prompt loaded from
/// <c>Prompts/onboarding-v1.yaml</c>. Identical across all six topics so
/// Anthropic's prompt-prefix cache hits from turn 2 onward (DEC-047 / DEC-058).
/// </param>
/// <param name="UserMessage">
/// The composed user message containing (a) the captured-so-far slot summary
/// section, (b) the current topic name, and (c) the sanitized + delimiter-wrapped
/// raw runner input. Two replays for the same <c>(view, topic, userInput)</c>
/// produce byte-identical content (sanitizer nonce notwithstanding — the
/// nonce sits on the non-cached tail per DEC-047).
/// </param>
/// <param name="Findings">
/// PII-free sanitization audit trail describing which patterns matched and
/// whether they were neutralized vs. log-only. Empty when nothing tripped.
/// </param>
/// <param name="Neutralized">
/// True if the sanitizer stripped any content (Unicode-tag / zero-width /
/// DAN-family neutralize). False when findings exist but were all log-only.
/// </param>
public sealed record OnboardingPromptComposition(
    string SystemPrompt,
    string UserMessage,
    ImmutableArray<SanitizationFinding> Findings,
    bool Neutralized);
