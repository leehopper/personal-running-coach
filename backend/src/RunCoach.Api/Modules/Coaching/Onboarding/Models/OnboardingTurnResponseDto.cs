using System.Text.Json;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Response payload for POST /api/v1/onboarding/turns. Carries either the next assistant turn
/// (Kind = Ask) or the completion signal with a generated plan id (Kind = Complete).
/// </summary>
/// <param name="Kind">Discriminator: Ask or Complete.</param>
/// <param name="AssistantBlocks">
/// Anthropic content blocks from the assistant turn, captured opaquely as a <see cref="JsonElement"/>
/// so non-text block types (thinking, tool_use) round-trip to the frontend without lossy projection.
/// The element is an independent clone (no shared pooled buffer); callers do not need to dispose it.
/// </param>
/// <param name="Topic">
/// The current topic the assistant is asking about. Null when <paramref name="Kind"/> is Complete.
/// </param>
/// <param name="SuggestedInputType">
/// The input control the chat surface should render for the next user reply.
/// Null when <paramref name="Kind"/> is Complete.
/// </param>
/// <param name="Progress">
/// Topic-completion progress for the UI indicator.
/// </param>
/// <param name="PlanId">
/// The generated plan id when <paramref name="Kind"/> is Complete; null otherwise.
/// </param>
public sealed record OnboardingTurnResponseDto(
    OnboardingTurnKind Kind,
    JsonElement AssistantBlocks,
    OnboardingTopic? Topic,
    SuggestedInputType? SuggestedInputType,
    OnboardingProgressDto Progress,
    Guid? PlanId);
