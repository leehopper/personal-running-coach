namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// Result of <see cref="IContextAssembler.ComposeForAdaptationAsync"/> — the
/// system prompt + user message bytes for the Level-2 plan-restructure call
/// (Slice 3 § Unit 5 / DEC-012).
/// </summary>
/// <param name="SystemPrompt">
/// The byte-stable adaptation system prompt loaded from the versioned prompt
/// store. Identical across every adaptation call for a given prompt version,
/// so the caller can pair it with Anthropic prompt caching at call time
/// (DEC-047 / R-067).
/// </param>
/// <param name="UserMessage">
/// The rendered adaptation context: plan context (current micro week + meso
/// weekly targets), the deterministic escalation level / safety tier /
/// deviation summary (echo-back only — never recomputed by the LLM), and the
/// triggering logged workout inside a nonce-delimited spotlight section per
/// R-068 / DEC-059. The nonce is fresh per call, so this value is
/// intentionally not byte-stable across calls.
/// </param>
public sealed record AdaptationPromptComposition(
    string SystemPrompt,
    string UserMessage);
