using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Request payload for POST /api/v1/onboarding/answers/revise. Allows the runner to overwrite
/// a previously captured answer for a specific topic, appending a fresh AnswerCaptured event
/// to the onboarding stream so the audit trail is preserved.
/// </summary>
/// <param name="Topic">The topic whose answer should be revised.</param>
/// <param name="NormalizedValue">
/// The replacement answer payload encoded as a JSON document. The handler validates the shape
/// against the topic-specific record (e.g. <see cref="PrimaryGoalAnswer"/>) before appending.
/// </param>
public sealed record ReviseAnswerRequestDto(
    [property: JsonRequired] OnboardingTopic Topic,
    [property: JsonRequired] JsonDocument NormalizedValue);
