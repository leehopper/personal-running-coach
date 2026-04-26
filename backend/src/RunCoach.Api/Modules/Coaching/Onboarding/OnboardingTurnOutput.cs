using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Pattern B (R-067 / DEC-058) structured-output schema for a single onboarding turn.
/// A single byte-stable schema with six nullable typed Normalized* slots plus a Topic
/// discriminator on the nested <see cref="ExtractedAnswer"/>. Schema bytes stay constant
/// across all six topics so Anthropic's grammar cache + prompt-prefix cache hit from
/// turn 2 onward (DEC-047's ~70% input-token reduction).
/// </summary>
/// <remarks>
/// The Pattern-B-Invariant - exactly one Normalized* slot is non-null AND it matches
/// <c>Extracted.Topic</c> - is enforced at runtime by <c>OnboardingTurnOutputValidator</c>
/// (T01.6) because Anthropic constrained decoding rejects <c>oneOf</c>. Numerical bounds
/// (e.g. <c>MaxRunDaysPerWeek</c> 1-7, <c>Confidence</c> 0.0-1.0) are documented in the
/// system prompt and the property [Description] attributes; they are NOT expressed via
/// <c>minimum</c>/<c>maximum</c> because Anthropic rejects those keywords with HTTP 400.
/// </remarks>
public sealed record OnboardingTurnOutput
{
    /// <summary>
    /// Gets the assistant content blocks the chat surface should render to the runner.
    /// </summary>
    [Description("Assistant content blocks for the chat surface to render to the runner.")]
    public required AnthropicContentBlock[] Reply { get; init; }

    /// <summary>
    /// Gets the structured extraction of the runner's latest input, when one was extracted
    /// confidently for the current topic. Null when no answer could be extracted (e.g. the
    /// runner asked a clarifying question of their own).
    /// </summary>
    [Description("Structured extraction of the runner's latest input. Null when no answer could be confidently extracted from the turn.")]
    public required ExtractedAnswer? Extracted { get; init; }

    /// <summary>
    /// Gets a value indicating whether the runner's input was ambiguous and the assistant
    /// requested clarification. When true, the handler appends a ClarificationRequested event.
    /// </summary>
    [Description("Whether the runner's input was ambiguous and the assistant requested clarification.")]
    public required bool NeedsClarification { get; init; }

    /// <summary>
    /// Gets the human-readable reason for clarification when <see cref="NeedsClarification"/>
    /// is true. Null when no clarification was requested.
    /// </summary>
    [Description("Human-readable reason for clarification. Null when no clarification was requested.")]
    public required string? ClarificationReason { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assistant believes the runner has provided enough
    /// information across all topics for plan generation to proceed. This is an additive
    /// precondition only - the handler also requires the deterministic completion gate
    /// to be satisfied.
    /// </summary>
    [Description("Whether the assistant believes the runner has provided enough information for plan generation. Additive only - the handler also requires the deterministic completion gate to pass.")]
    public required bool ReadyForPlan { get; init; }
}
