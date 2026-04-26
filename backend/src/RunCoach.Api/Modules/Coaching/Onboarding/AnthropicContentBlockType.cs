namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Discriminator for the closed <see cref="AnthropicContentBlock"/> Pattern B record.
/// Values are explicitly numbered for stable Anthropic-grammar-cache encoding.
/// </summary>
public enum AnthropicContentBlockType
{
    /// <summary>Plain text block. The runner-visible payload lives in <c>Text</c>.</summary>
    Text = 0,

    /// <summary>Anthropic extended-thinking block. Pass-through-only at MVP-0.</summary>
    Thinking = 1,
}
