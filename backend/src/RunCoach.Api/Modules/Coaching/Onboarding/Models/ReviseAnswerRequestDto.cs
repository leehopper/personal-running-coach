using System.Text.Json;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Request payload for POST /api/v1/onboarding/answers/revise. Allows the runner to overwrite
/// a previously captured answer for a specific topic, appending a fresh AnswerCaptured event
/// to the onboarding stream so the audit trail is preserved.
/// </summary>
/// <param name="Topic">The topic whose answer should be revised.</param>
/// <param name="NormalizedValue">
/// The replacement answer payload. The handler validates the shape against the topic-specific
/// record (e.g. <see cref="PrimaryGoalAnswer"/>) before appending.
/// Use <see cref="JsonElement"/> rather than <see cref="JsonDocument"/> because <c>JsonElement</c>
/// is a struct that does not hold pooled memory and therefore does not require disposal. Handlers
/// that need to write this value to the event stream must serialize via
/// <c>JsonSerializer.SerializeToDocument(NormalizedValue)</c> or equivalent.
/// </param>
public sealed record ReviseAnswerRequestDto(
    OnboardingTopic Topic,
    JsonElement NormalizedValue);
