using System.Text.Json;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Response payload for POST /api/v1/onboarding/turns. Carries either the next assistant turn
/// (Kind = Ask), the completion signal with a generated plan id (Kind = Complete), or a terminal
/// rejection envelope (Kind = Error).
/// </summary>
/// <remarks>
/// <para>
/// The DTO is logically a discriminated union but is encoded as a flat record so the
/// frontend Zod schema can read it without a custom converter. Producers should
/// always use the static <see cref="Ask"/> / <see cref="Complete"/> / <see cref="Error"/> factories — the
/// positional constructor remains public only for System.Text.Json deserialization.
/// </para>
/// </remarks>
/// <param name="Kind">Discriminator: Ask, Complete, or Error.</param>
/// <param name="AssistantBlocks">
/// Anthropic content blocks from the assistant turn, carried as a <see cref="JsonElement"/> so
/// non-text block types (thinking, tool_use) round-trip to the frontend without lossy projection.
/// Use <see cref="JsonElement"/> rather than <see cref="JsonDocument"/> because <c>JsonElement</c>
/// is a struct that does not hold pooled memory and therefore does not require disposal. Producers
/// must call <c>doc.RootElement.Clone()</c> to obtain an independent element that survives the
/// source document's lifetime.
/// </param>
/// <param name="Topic">
/// The current topic the assistant is asking about. Null when <paramref name="Kind"/> is Complete or Error.
/// </param>
/// <param name="SuggestedInputType">
/// The input control the chat surface should render for the next user reply.
/// Null when <paramref name="Kind"/> is Complete or Error.
/// </param>
/// <param name="Progress">
/// Topic-completion progress for the UI indicator.
/// </param>
/// <param name="PlanId">
/// The generated plan id when <paramref name="Kind"/> is Complete; null otherwise.
/// </param>
/// <param name="ErrorMessage">User-facing error text when <paramref name="Kind"/> is Error; null otherwise.</param>
public sealed record OnboardingTurnResponseDto(
    OnboardingTurnKind Kind,
    JsonElement AssistantBlocks,
    OnboardingTopic? Topic,
    SuggestedInputType? SuggestedInputType,
    OnboardingProgressDto Progress,
    Guid? PlanId,
    string? ErrorMessage = null)
{
    private static readonly JsonElement EmptyAssistantBlocks = InitializeEmptyAssistantBlocks();

    /// <summary>
    /// Constructs an <see cref="OnboardingTurnKind.Error"/>-shaped response (F3). Carries a
    /// user-facing message and no plan; assistant blocks are empty and progress is zeroed
    /// (the client renders only the message + a retry affordance on this kind). HTTP status is 200.
    /// </summary>
    /// <param name="errorMessage">User-facing description of why plan generation was rejected.</param>
    /// <returns>A well-formed Error response.</returns>
    public static OnboardingTurnResponseDto Error(string errorMessage) =>
        new(
            Kind: OnboardingTurnKind.Error,
            AssistantBlocks: EmptyAssistantBlocks,
            Topic: null,
            SuggestedInputType: null,
            Progress: new OnboardingProgressDto(0, 1),
            PlanId: null,
            ErrorMessage: errorMessage);

    /// <summary>
    /// Constructs an <see cref="OnboardingTurnKind.Ask"/>-shaped response. Validates
    /// that the per-Kind invariant holds (topic + suggestedInputType present, planId absent).
    /// </summary>
    /// <param name="assistantBlocks">The assistant turn content blocks.</param>
    /// <param name="topic">The topic being asked about.</param>
    /// <param name="suggestedInputType">The input control the SPA should render.</param>
    /// <param name="progress">Topic-completion progress for the UI.</param>
    /// <returns>A well-formed Ask response.</returns>
    public static OnboardingTurnResponseDto Ask(
        JsonElement assistantBlocks,
        OnboardingTopic topic,
        SuggestedInputType suggestedInputType,
        OnboardingProgressDto progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return new OnboardingTurnResponseDto(
            Kind: OnboardingTurnKind.Ask,
            AssistantBlocks: assistantBlocks,
            Topic: topic,
            SuggestedInputType: suggestedInputType,
            Progress: progress,
            PlanId: null);
    }

    /// <summary>
    /// Constructs an <see cref="OnboardingTurnKind.Complete"/>-shaped response. Validates
    /// that the per-Kind invariant holds (planId present, topic + suggestedInputType absent).
    /// </summary>
    /// <param name="assistantBlocks">The assistant turn content blocks.</param>
    /// <param name="progress">Topic-completion progress for the UI (typically 6/6).</param>
    /// <param name="planId">The generated plan id.</param>
    /// <returns>A well-formed Complete response.</returns>
    public static OnboardingTurnResponseDto Complete(
        JsonElement assistantBlocks,
        OnboardingProgressDto progress,
        Guid planId)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (planId == Guid.Empty)
        {
            throw new ArgumentException("PlanId must not be Guid.Empty for a Complete response.", nameof(planId));
        }

        return new OnboardingTurnResponseDto(
            Kind: OnboardingTurnKind.Complete,
            AssistantBlocks: assistantBlocks,
            Topic: null,
            SuggestedInputType: null,
            Progress: progress,
            PlanId: planId);
    }

    // Clones the empty-array element out of a disposed JsonDocument: the clone is an independent
    // JsonElement that survives the document, while disposing the document returns its pooled
    // buffer instead of leaking it for the app lifetime.
    private static JsonElement InitializeEmptyAssistantBlocks()
    {
        using var doc = JsonDocument.Parse("[]");
        return doc.RootElement.Clone();
    }
}
