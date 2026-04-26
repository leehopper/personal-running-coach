using RunCoach.Api.Modules.Coaching.Models;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Wolverine command dispatched by <see cref="PlanRenderingController"/> for every
/// inbound <c>POST /api/v1/plan/regenerate</c> request. Per Slice 1 § Unit 5 R05.1
/// / DEC-057 / DEC-060 the matching handler is <see cref="RegeneratePlanHandler"/>
/// — a plain Wolverine handler (NOT an <c>[AggregateHandler]</c> — there is no
/// aggregate to fetch since regeneration creates a NEW Plan stream) that performs
/// every per-call side-effect atomically through the single Marten
/// <see cref="Marten.IDocumentSession"/> Wolverine's transactional middleware
/// brackets around the handler body: idempotency check + load onboarding view +
/// inline plan generation + Plan stream creation + <c>PlanLinkedToUser</c> append +
/// idempotency record — all on the same session, no <c>RunCoachDbContext</c>
/// injection, no second Postgres transaction.
/// </summary>
/// <param name="UserId">The authenticated runner's id; doubles as the per-user
/// onboarding stream identity from which the handler reads the prior
/// <c>CurrentPlanId</c>.</param>
/// <param name="Intent">
/// Optional regeneration intent. The free-text payload was sanitized + delimiter-wrapped
/// by the controller via
/// <c>IPromptSanitizer.SanitizeAsync(rawFreeText, PromptSection.RegenerationIntentFreeText, ct)</c>
/// BEFORE the command was dispatched, so the handler and downstream context-assembler
/// hand the value through unchanged. Null when the runner did not supply an intent.
/// </param>
/// <param name="IdempotencyKey">
/// Client-generated idempotency key — typically <c>crypto.randomUUID()</c> the
/// frontend re-sends on retry. The handler's first action is to short-circuit
/// duplicate submissions via <see cref="Coaching.Idempotency.IIdempotencyStore.SeenAsync{TResponse}"/>.
/// </param>
public sealed record RegeneratePlanCommand(
    Guid UserId,
    RegenerationIntent? Intent,
    Guid IdempotencyKey);
