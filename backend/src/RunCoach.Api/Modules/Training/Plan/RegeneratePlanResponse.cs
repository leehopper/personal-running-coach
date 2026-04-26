namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Response shape returned by <see cref="RegeneratePlanHandler"/> and surfaced
/// verbatim by the <c>POST /api/v1/plan/regenerate</c> endpoint per Slice 1 §
/// Unit 5 R05.1. Carries the newly-generated plan id so the frontend can
/// immediately invalidate the <c>Plan</c> RTK-query tag and refetch the plan
/// projection without a second round-trip to discover the id.
/// </summary>
/// <remarks>
/// The shape is recorded by <see cref="Coaching.Idempotency.IIdempotencyStore.Record{TResponse}"/>
/// on the handler's session so a duplicate submission with the same
/// idempotency key returns the byte-identical payload — including the same
/// <see cref="PlanId"/> — without producing a second Plan stream.
/// </remarks>
/// <param name="PlanId">The id of the newly-generated Plan stream.</param>
/// <param name="Status">
/// Always <c>"generated"</c> on the success path per the spec wire contract;
/// modeled as a string so future slices can introduce richer status values
/// (e.g. <c>"queued"</c>) without breaking existing clients.
/// </param>
public sealed record RegeneratePlanResponse(
    Guid PlanId,
    string Status);
