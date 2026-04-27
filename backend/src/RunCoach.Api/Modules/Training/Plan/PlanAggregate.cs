// Marten event records for the per-user Plan stream (DEC-047, spec 13 § Unit 2).
//
// Convention: events are positional records with stable property order so the
// System.Text.Json representation is byte-stable across deployments. The macro,
// meso, and micro payloads reuse the existing structured-output records from
// `Modules/Coaching/Models/Structured/` verbatim - the LLM emits them and the
// caller hands them straight to `session.Events.StartStream<Plan>(planId, events)`
// without re-projection.
//
// Slice 1 lands three event types: `PlanGenerated` (stream-creation),
// `MesoCycleCreated` (x4 - one per week 1-4), `FirstMicroCycleCreated` (week 1
// detailed workouts). Future slices add `PlanAdaptedFromLog` (Slice 3) and
// `PlanRestructuredFromConversation` (Slice 4) as additive `Apply` methods on
// `PlanProjection`; no schema break.
//
// `PreviousPlanId` ships in `PlanGenerated` from day one - Unit 5's regenerate
// handler threads it onto the new plan's stream-creation event so the projection
// retains audit linkage to the prior plan without a schema bump.
//
// Event records are each in their own file per the one-type-per-file convention:
// PlanGenerated.cs, MesoCycleCreated.cs, FirstMicroCycleCreated.cs.

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Marker type identifying the per-plan event stream's aggregate shape. Passed
/// as the generic argument to <c>session.Events.StartStream&lt;PlanAggregate&gt;</c>
/// so Marten records the aggregate type in <c>mt_streams.aggregate</c>
/// independently of any read-model projection. Keeping the marker distinct
/// from <see cref="Models.PlanProjectionDto"/> (the inline projection's
/// read-model document) means projection refactors do not perturb the event
/// store metadata per DEC-060: handler bodies emit events into aggregate
/// streams; projections own EF state. The type carries no fields — it is a
/// pure type-system tag — and is never instantiated.
/// </summary>
#pragma warning disable S2094 // Empty class — intentional Marten stream marker
public sealed class PlanAggregate
{
}
#pragma warning restore S2094
