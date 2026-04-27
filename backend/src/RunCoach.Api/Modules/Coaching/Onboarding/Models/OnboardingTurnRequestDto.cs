using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Request payload for POST /api/v1/onboarding/turns. The idempotency key allows the
/// frontend to safely retry on transient failures without producing duplicate events.
/// </summary>
/// <param name="IdempotencyKey">
/// Client-generated idempotency key (typically a <c>crypto.randomUUID()</c>) used to short-circuit
/// duplicate submissions via <c>IIdempotencyStore</c>.
/// </param>
/// <param name="Text">The user's free-text input for the current onboarding turn.</param>
public sealed record OnboardingTurnRequestDto(
    [property: JsonRequired] Guid IdempotencyKey,
    [property: JsonRequired] string Text);
