using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Onboarding;

namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Assembles the full prompt payload from user data, enforcing positional
/// layout and token budget. Loads system prompts from versioned YAML files
/// via <see cref="Prompts.IPromptStore"/> and renders context templates
/// with <see cref="Prompts.PromptRenderer"/>.
///
/// The assembled prompt is split into a static prefix (coaching persona,
/// safety rules, semantic output guidance) suitable for Anthropic prompt
/// caching, and a dynamic suffix (rendered athlete context, conversation
/// history) that changes per request.
/// </summary>
public interface IContextAssembler
{
    /// <summary>
    /// Builds the full prompt payload from the provided input data.
    /// Loads the system prompt from the configured YAML prompt store.
    /// Applies positional layout (stable prefix, variable middle, conversational end)
    /// and enforces the token budget by truncating/summarizing as needed.
    /// </summary>
    /// <param name="input">All input data for prompt assembly.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assembled prompt with structured sections and token estimate.</returns>
    Task<AssembledPrompt> AssembleAsync(ContextAssemblerInput input, CancellationToken ct = default);

    /// <summary>
    /// Composes the system prompt + user message for a single onboarding turn
    /// (Slice 1 § Unit 1 R01.7 / R01.11). The system prompt is byte-stable
    /// across all six topics so Anthropic's prompt-prefix cache hits from
    /// turn 2 onward (R-067 / DEC-058 — also DEC-047). The current topic is
    /// placed in the user message, never the system prompt.
    /// </summary>
    /// <param name="view">The runner's in-flight onboarding projection.</param>
    /// <param name="currentTopic">
    /// The topic the deterministic next-topic selector chose for this turn.
    /// Placed in the user message — never the system prompt.
    /// </param>
    /// <param name="userInput">
    /// The runner's raw free-text input. Sanitized inside the assembler per
    /// R-068 / DEC-059 (Unicode normalization + regex-tier detection +
    /// Spotlighting containment delimiters).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The composed onboarding prompt — system + user + sanitization audit trail.</returns>
    Task<OnboardingPromptComposition> ComposeForOnboardingAsync(
        OnboardingView view,
        OnboardingTopic currentTopic,
        string userInput,
        CancellationToken ct = default);

    /// <summary>
    /// Composes the stable-prefix prompt for the macro/meso/micro plan-generation
    /// chain (Slice 1 § Unit 2 R02.4 + § Unit 5 R05.4). Reads the captured
    /// profile from the <paramref name="profileSnapshot"/> projection — NOT by
    /// replaying the onboarding stream — and renders it into a byte-stable user
    /// message. When <paramref name="intent"/> is supplied, its sanitized
    /// free-text is appended at the END of the user message under the stable
    /// label <c>[Regeneration intent provided by user]</c> so the prefix above
    /// it stays byte-identical across initial-generation and regenerate calls.
    /// </summary>
    /// <param name="profileSnapshot">
    /// The completed <see cref="OnboardingView"/> projection — the captured
    /// answers across the six topics serve as the profile snapshot for plan
    /// generation. Per Slice 1 spec the snapshot is read directly; the chain
    /// does NOT re-replay the onboarding event stream on each macro/meso/micro
    /// call.
    /// </param>
    /// <param name="intent">
    /// Optional regeneration intent supplied by the runner when invoking
    /// Settings → Plan (Slice 1 § Unit 5). The free-text MUST already be
    /// sanitized by the caller via
    /// <c>IPromptSanitizer.SanitizeAsync(intent.FreeText, PromptSection.RegenerationIntentFreeText, ct)</c>.
    /// This method does NOT re-sanitize.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The composed plan-generation prompt — system + base user message.</returns>
    Task<PlanGenerationPromptComposition> ComposeForPlanGenerationAsync(
        OnboardingView profileSnapshot,
        RegenerationIntent? intent,
        CancellationToken ct = default);

    /// <summary>
    /// Estimates the token count for a given text using the character ratio method
    /// (characters / 4 with a 10% safety margin).
    /// </summary>
    /// <param name="text">The text to estimate tokens for.</param>
    /// <returns>The estimated token count.</returns>
    int EstimateTokens(string text);
}
