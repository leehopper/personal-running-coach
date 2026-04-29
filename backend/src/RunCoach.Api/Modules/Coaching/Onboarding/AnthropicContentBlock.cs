using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// A closed-shape Anthropic assistant content block carried inside the Pattern B
/// <see cref="OnboardingTurnOutput"/> Reply array. Closed-shape (no <c>object</c> fields)
/// keeps the schema bytes constant across turns so Anthropic's grammar cache + prompt-prefix
/// cache hit from turn 2 onward (R-067 / DEC-058).
/// </summary>
public sealed record AnthropicContentBlock
{
    /// <summary>
    /// Gets the block-type discriminator. Slice 1 produces only <c>Text</c>; <c>Thinking</c> is
    /// reserved for future use and rendered opaquely by the frontend.
    /// </summary>
    [Description("Content block type. Use 'Text' for runner-visible reply text. 'Thinking' is reserved for Anthropic extended thinking blocks.")]
    public required AnthropicContentBlockType Type { get; init; }

    /// <summary>
    /// Gets the runner-visible text payload for a Text block. Empty string for non-Text blocks.
    /// </summary>
    [Description("Runner-visible text payload for a Text block. Empty string for non-Text blocks.")]
    public required string Text { get; init; } = string.Empty;
}
