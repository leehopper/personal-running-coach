// Marten event records for the per-user onboarding stream (DEC-047). Eight events total.
//
// Convention: events are positional records with stable property order so the System.Text.Json
// representation is byte-stable across deployments. Never use Dictionary<string, object> for
// payload slots - closed shapes only. UserTurnRecorded / AssistantTurnRecorded carry typed
// Anthropic content blocks via JsonDocument so non-text block types (thinking, tool_use)
// round-trip without lossy projection per DEC-047.
//
// All eight event records are co-located in this file so the onboarding stream's wire schema
// is reviewable in one place. The StyleCop SA1402 / SA1649 single-type-per-file rules are
// suppressed locally for this aggregate file only - other modules continue to follow the
// one-type-per-file convention.
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

using System.Text.Json;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Stream-creation event. Emitted exactly once per user when the first turn lands.
/// </summary>
/// <param name="UserId">The authenticated user's id; doubles as the per-user stream id.</param>
/// <param name="StartedAt">Wall-clock time the stream was opened.</param>
public sealed record OnboardingStarted(
    Guid UserId,
    DateTimeOffset StartedAt);

/// <summary>
/// Records that the deterministic next-topic selector advanced to a new topic for this turn.
/// </summary>
/// <param name="Topic">The topic the assistant is asking about on this turn.</param>
/// <param name="AskedAt">Wall-clock time the topic transition occurred.</param>
public sealed record TopicAsked(
    OnboardingTopic Topic,
    DateTimeOffset AskedAt);

/// <summary>
/// Records the runner's user turn with the typed Anthropic content blocks they submitted.
/// </summary>
/// <param name="ContentBlocks">
/// Typed Anthropic content blocks the runner supplied (text only at MVP-0; future tool_use
/// blocks round-trip via the JsonDocument). Stored opaquely so non-text block types do not
/// lossy-project.
/// </param>
/// <param name="RecordedAt">Wall-clock time the turn was recorded.</param>
public sealed record UserTurnRecorded(
    JsonDocument ContentBlocks,
    DateTimeOffset RecordedAt);

/// <summary>
/// Records the assistant's reply turn with the typed Anthropic content blocks it produced.
/// </summary>
/// <param name="ContentBlocks">
/// Typed Anthropic content blocks the assistant produced (text + optional thinking blocks).
/// Stored opaquely so non-text block types do not lossy-project.
/// </param>
/// <param name="RecordedAt">Wall-clock time the turn was recorded.</param>
public sealed record AssistantTurnRecorded(
    JsonDocument ContentBlocks,
    DateTimeOffset RecordedAt);

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

/// <summary>
/// Records that the assistant flagged the runner's input as ambiguous and asked for clarification.
/// </summary>
/// <param name="Topic">The topic the clarification applies to.</param>
/// <param name="Reason">Human-readable reason the assistant requested clarification.</param>
/// <param name="RequestedAt">Wall-clock time the clarification request was emitted.</param>
public sealed record ClarificationRequested(
    OnboardingTopic Topic,
    string Reason,
    DateTimeOffset RequestedAt);

/// <summary>
/// DEC-060 / R-069 event that drives the EF UserProfile.CurrentPlanId update via the
/// UserProfileFromOnboardingProjection apply method. Appended to the onboarding stream by
/// the terminal-branch handler immediately after the new Plan stream is staged, and again
/// by the regenerate handler each time a fresh plan is generated.
/// </summary>
/// <param name="UserId">The authenticated user's id.</param>
/// <param name="PlanId">The newly generated plan's id.</param>
public sealed record PlanLinkedToUser(
    Guid UserId,
    Guid PlanId);

/// <summary>
/// Stream-completion event. Emitted exactly once at the end of the terminal-branch handler
/// after plan generation succeeds. Carries the generated plan id for response correlation.
/// </summary>
/// <param name="PlanId">The generated plan's id.</param>
/// <param name="CompletedAt">Wall-clock time onboarding completed.</param>
public sealed record OnboardingCompleted(
    Guid PlanId,
    DateTimeOffset CompletedAt);
