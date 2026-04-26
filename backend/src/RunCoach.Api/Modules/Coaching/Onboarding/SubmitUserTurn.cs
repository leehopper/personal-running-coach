namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Wolverine command dispatched by <c>OnboardingController.SubmitTurn</c> for every
/// inbound POST /api/v1/onboarding/turns request. Carries the authenticated user's
/// id (the per-user onboarding stream identity), the client-supplied idempotency
/// key, and the runner's free-text input. Per Slice 1 § Unit 1 / DEC-057 / DEC-060
/// the matching handler is <see cref="OnboardingTurnHandler"/> — a Wolverine
/// <c>[AggregateHandler]</c> that performs the per-turn work atomically through a
/// single Marten <c>IDocumentSession</c>: idempotency check + event emission +
/// (terminal branch only) inline plan generation + onboarding-completion events.
/// </summary>
/// <param name="UserId">
/// The authenticated runner's id; doubles as the onboarding aggregate stream id
/// (one onboarding stream per user — DEC-047).
/// </param>
/// <param name="IdempotencyKey">
/// Client-generated idempotency key — typically a <c>crypto.randomUUID()</c> the
/// frontend re-sends on retry. The handler's first action is to short-circuit
/// duplicate submissions via <c>IIdempotencyStore.SeenAsync</c>.
/// </param>
/// <param name="Text">The runner's raw free-text input for the current turn.</param>
public sealed record SubmitUserTurn(
    Guid UserId,
    Guid IdempotencyKey,
    string Text);
