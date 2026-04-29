using System.Text.Json;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Records that a normalized answer was captured for a specific topic. Closed-shape payload
/// so the projection can apply without runtime type discovery.
/// </summary>
/// <param name="Topic">The topic the answer applies to.</param>
/// <param name="NormalizedPayload">
/// The normalized answer record serialized to a JsonDocument. The projection deserializes
/// to the topic-specific answer record (e.g. <see cref="PrimaryGoalAnswer"/>) when applying.
/// </param>
/// <param name="Confidence">The assistant's confidence in the extraction (0.0-1.0).</param>
/// <param name="CapturedAt">Wall-clock time the answer was captured.</param>
public sealed record AnswerCaptured(
    OnboardingTopic Topic,
    JsonDocument NormalizedPayload,
    double Confidence,
    DateTimeOffset CapturedAt);
