// Marten event records for the per-user onboarding stream (DEC-047). Eight events total.
//
// Convention: events are positional records with stable property order so the System.Text.Json
// representation is byte-stable across deployments. Never use Dictionary<string, object> for
// payload slots - closed shapes only. UserTurnRecorded / AssistantTurnRecorded carry typed
// Anthropic content blocks via JsonDocument so non-text block types (thinking, tool_use)
// round-trip without lossy projection per DEC-047.
//
// Event records are each in their own file per the one-type-per-file convention:
// OnboardingStarted.cs, TopicAsked.cs, UserTurnRecorded.cs, AssistantTurnRecorded.cs,
// AnswerCaptured.cs, ClarificationRequested.cs, PlanLinkedToUser.cs, OnboardingCompleted.cs.

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Marker type identifying the per-user onboarding event stream's aggregate
/// shape. Passed as the generic argument to <c>session.Events.StartStream&lt;OnboardingAggregate&gt;</c>
/// so Marten records the aggregate type in <c>mt_streams.aggregate</c>
/// independently of any read-model projection. Keeping the marker distinct
/// from <see cref="OnboardingView"/> (the inline projection's read-model
/// document) means projection refactors do not perturb the event store
/// metadata per DEC-060: handler bodies emit events into aggregate streams;
/// projections own EF state. The type carries no fields — it is a pure
/// type-system tag — and is never instantiated.
/// </summary>
#pragma warning disable S2094 // Empty class — intentional Marten stream marker
public sealed class OnboardingAggregate
{
}
#pragma warning restore S2094
