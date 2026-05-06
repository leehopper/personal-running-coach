namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// Result of <see cref="IContextAssembler.ComposeForPlanGenerationAsync"/> —
/// the system prompt + base user message bytes used by the macro/meso/micro
/// plan-generation chain (Slice 1 § Unit 2 R02.4).
/// </summary>
/// <param name="SystemPrompt">
/// The byte-stable coaching system prompt loaded from the versioned prompt
/// store. Used as the cacheable prefix block for every call in the
/// macro/meso/micro chain so calls 2-6 hit Anthropic's prompt-prefix cache
/// (DEC-047 / R-067).
/// </param>
/// <param name="UserMessage">
/// The composed user message containing (a) the captured profile snapshot
/// rendered from the <c>OnboardingView</c>, and (b) the optional
/// <c>RegenerationIntent</c> block under the stable label
/// <c>[Regeneration intent provided by user]</c> at the END of the message.
/// The plan-generation service appends the per-tier (macro/meso/micro)
/// suffix AFTER this base — keeping these bytes stable across the six
/// chained calls.
/// </param>
public sealed record PlanGenerationPromptComposition(
    string SystemPrompt,
    string UserMessage);
