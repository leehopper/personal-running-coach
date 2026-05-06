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

    /// <summary>
    /// Validates the block-type / text-payload contract: <c>Text</c> blocks must carry non-empty
    /// text, <c>Thinking</c> blocks must carry an empty string. The wire schema cannot express
    /// this dependency (DEC-058 keeps the schema closed-shape for grammar caching), so the
    /// invariant is enforced at the .NET boundary by callers (e.g. <c>OnboardingTurnOutputValidator</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the block's <see cref="Type"/> and <see cref="Text"/> combination is invalid.
    /// </exception>
    public void Validate()
    {
        switch (Type)
        {
            case AnthropicContentBlockType.Text:
                if (string.IsNullOrEmpty(Text))
                {
                    throw new InvalidOperationException(
                        "AnthropicContentBlock with Type=Text must carry a non-empty Text payload.");
                }

                break;

            case AnthropicContentBlockType.Thinking:
                if (!string.IsNullOrEmpty(Text))
                {
                    throw new InvalidOperationException(
                        "AnthropicContentBlock with Type=Thinking must carry an empty Text payload " +
                        "(the schema reserves Text for runner-visible Text blocks only).");
                }

                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown AnthropicContentBlockType '{Type}'.");
        }
    }
}
