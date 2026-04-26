using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Training.Plan.Models;
using Wolverine;
using IPromptSanitizer = RunCoach.Api.Modules.Coaching.Sanitization.IPromptSanitizer;
using SanitizationPromptSection = RunCoach.Api.Modules.Coaching.Sanitization.PromptSection;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Slice 1 read-only plan rendering surface (spec 13 § Unit 2, R02.4 / DEC-057).
/// Exposes <c>GET /api/v1/plan/current</c> which materializes the projection of
/// the user's currently-active <c>Plan</c> stream so the home surface can render
/// the macro phase strip + four meso weeks + this week's micro detail with zero
/// LLM cost. The active plan is resolved by reading
/// <see cref="Modules.Identity.Entities.UserProfile.CurrentPlanId"/> from the
/// EF projection (set atomically via the
/// <c>UserProfileFromOnboardingProjection</c> apply method when
/// <c>PlanLinkedToUser</c> commits) and loading
/// <see cref="PlanProjectionDto"/> directly off the
/// <see cref="IDocumentSession"/>.
/// </summary>
/// <remarks>
/// <para>
/// The <c>POST /regenerate</c> endpoint (Unit 5) ships alongside <c>GET /current</c>:
/// it sanitizes the optional regeneration intent free-text in-line, dispatches a
/// <see cref="RegeneratePlanCommand"/> to the Wolverine
/// <see cref="RegeneratePlanHandler"/>, and surfaces the resulting
/// <see cref="RegeneratePlanResponse"/> verbatim. The handler runs every
/// per-call side-effect on a single Marten session under Wolverine's
/// transactional middleware per DEC-057 / DEC-060.
/// </para>
/// <para>
/// Authorization mirrors <see cref="Modules.Coaching.Onboarding.OnboardingController"/>'s
/// <see cref="AuthPolicies.CookieOrBearer"/> policy. Reads do not require an
/// antiforgery token (idempotent GETs are out of scope for ASP.NET Core's
/// antiforgery middleware); <c>POST /regenerate</c> requires
/// <c>[RequireAntiforgeryToken]</c> per the Slice 0 pattern.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/plan")]
[Authorize(Policy = AuthPolicies.CookieOrBearer)]
public sealed partial class PlanRenderingController(
    IDocumentSession session,
    IMessageBus bus,
    IPromptSanitizer sanitizer,
    RunCoachDbContext db,
    ILogger<PlanRenderingController> logger) : ControllerBase
{
    private const string MissingUserType = "https://runcoach.app/problems/missing-user-claim";
    private const string OnboardingNotCompleteType = "https://runcoach.app/problems/onboarding-not-complete";

    /// <summary>GET /api/v1/plan/current — read the user's active plan projection.</summary>
    /// <remarks>
    /// Returns 404 when the runner has not generated a plan yet (i.e. their
    /// <c>UserProfile</c> row is missing OR <c>CurrentPlanId</c> is null OR the
    /// referenced Plan stream has not yet projected a <see cref="PlanProjectionDto"/>
    /// document — defensively handled although the projection runs inline with
    /// the stream-creation event so the third case is only reachable across an
    /// in-flight upgrade where a stale stream exists without a re-projected
    /// document). Returns 200 with the byte-stable projection document
    /// otherwise — re-issuing the call returns the same payload because the
    /// document is persisted, not regenerated.
    /// </remarks>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>The plan projection document or 404.</returns>
    [HttpGet("current")]
    [ProducesResponseType(typeof(PlanProjectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingUserClaim();
        }

        // Resolve the active plan id off the EF projection. AsNoTracking keeps
        // this read-only path out of EF's change tracker — the controller never
        // mutates the row. The projection of `PlanLinkedToUser` runs inside
        // Marten's transaction (per DEC-060 / R-069) so by the time the home
        // surface calls this endpoint after onboarding, `CurrentPlanId` is
        // guaranteed to be set if any Plan stream exists for the user.
        var currentPlanId = await EntityFrameworkQueryableExtensions
            .SingleOrDefaultAsync(
                db.UserProfiles
                    .AsNoTracking()
                    .Where(p => p.UserId == userId)
                    .Select(p => p.CurrentPlanId),
                ct)
            .ConfigureAwait(false);

        if (currentPlanId is null)
        {
            return NotFound();
        }

        var plan = await session
            .LoadAsync<PlanProjectionDto>(currentPlanId.Value, ct)
            .ConfigureAwait(false);

        if (plan is null)
        {
            // The EF row points at a Plan stream that has not projected a
            // document. With inline projection this is unreachable in steady
            // state; log so an operational regression (e.g. someone flipping
            // the projection lifecycle to async) surfaces in observability.
            LogProjectionMiss(logger, userId, currentPlanId.Value);
            return NotFound();
        }

        return Ok(plan);
    }

    /// <summary>POST /api/v1/plan/regenerate — regenerate the runner's plan from their persisted profile.</summary>
    /// <remarks>
    /// <para>
    /// Per Slice 1 § Unit 5 R05.1: validates the runner has finished onboarding
    /// (<c>UserProfile.OnboardingCompletedAt</c> non-null) else returns 409
    /// ProblemDetails. Sanitizes the optional intent free-text via
    /// <see cref="IPromptSanitizer.SanitizeAsync"/> with
    /// <see cref="SanitizationPromptSection.RegenerationIntentFreeText"/> BEFORE dispatching
    /// the command — the sanitized + delimiter-wrapped text is what reaches the
    /// LLM via <c>ContextAssembler.ComposeForPlanGenerationAsync</c>, which
    /// does NOT re-sanitize.
    /// </para>
    /// <para>
    /// Dispatches a <see cref="RegeneratePlanCommand"/> to the Wolverine
    /// <see cref="RegeneratePlanHandler"/>; the handler performs idempotency
    /// check + Plan-stream creation + <c>PlanLinkedToUser</c> append +
    /// idempotency record on a single Marten session inside one transaction
    /// (DEC-057 / DEC-060). Failure of any LLM call rolls back the whole
    /// transaction, leaving <c>UserProfile.CurrentPlanId</c> pointing at the
    /// prior plan.
    /// </para>
    /// </remarks>
    /// <param name="request">The regenerate request body.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>200 with <see cref="RegeneratePlanResponse"/> on success; 409
    /// when onboarding is incomplete; 400 when the intent free-text exceeds the
    /// raw cap.</returns>
    [HttpPost("regenerate")]
    [Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryToken]
    [ProducesResponseType(typeof(RegeneratePlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Regenerate(
        [FromBody] RegeneratePlanRequestDto request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetUserId(out var userId))
        {
            return MissingUserClaim();
        }

        // Verify the runner finished onboarding before allowing regeneration.
        // Reading the EF UserProfile row from the controller (outside any
        // Wolverine handler body) is permitted by DEC-060 — only handler
        // bodies are forbidden from EF mutation. AsNoTracking keeps this
        // path out of EF's change tracker.
        var onboardingCompletedAt = await EntityFrameworkQueryableExtensions
            .SingleOrDefaultAsync(
                db.UserProfiles
                    .AsNoTracking()
                    .Where(p => p.UserId == userId)
                    .Select(p => p.OnboardingCompletedAt),
                ct)
            .ConfigureAwait(false);

        if (onboardingCompletedAt is null)
        {
            LogRegenerateRejectedOnboardingIncomplete(logger, userId);
            return Problem(
                type: OnboardingNotCompleteType,
                title: "Onboarding has not completed yet.",
                detail: "Finish onboarding before regenerating a plan.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Validate the raw free-text length against the wire-level cap before
        // sanitization. Sanitization wraps the input with a Spotlighting
        // delimiter that adds delimiter-overhead chars, so the
        // `RegenerationIntent` constructor's larger cap is sized to admit
        // RawMaxFreeTextLength + DelimiterOverhead (see RegenerationIntent.cs).
        RegenerationIntent? sanitizedIntent = null;
        if (request.Intent is not null)
        {
            var rawText = request.Intent.FreeText ?? string.Empty;
            if (rawText.Length > RegenerationIntent.RawMaxFreeTextLength)
            {
                return Problem(
                    title: "Regeneration intent is too long.",
                    detail: $"intent.freeText must be at most {RegenerationIntent.RawMaxFreeTextLength} characters; got {rawText.Length}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Sanitize once, in the controller, so the wrapped text is what
            // both the handler's audit log and the downstream context assembler
            // observe — ContextAssembler.ComposeForPlanGenerationAsync trusts
            // the wrapped bytes and does NOT re-sanitize.
            var sanitized = await sanitizer
                .SanitizeAsync(rawText, SanitizationPromptSection.RegenerationIntentFreeText, ct)
                .ConfigureAwait(false);

            sanitizedIntent = new RegenerationIntent(sanitized.Sanitized);
        }

        var response = await bus
            .InvokeAsync<RegeneratePlanResponse>(
                new RegeneratePlanCommand(userId, sanitizedIntent, request.IdempotencyKey),
                ct)
            .ConfigureAwait(false);

        return Ok(response);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Plan rendering miss: UserProfile.CurrentPlanId set but PlanProjectionDto absent user={UserId} planId={PlanId}")]
    private static partial void LogProjectionMiss(ILogger logger, Guid userId, Guid planId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Plan regeneration rejected: onboarding not complete user={UserId}")]
    private static partial void LogRegenerateRejectedOnboardingIncomplete(ILogger logger, Guid userId);

    private bool TryGetUserId(out Guid userId)
    {
        // Cookie auth writes ClaimTypes.NameIdentifier; raw JWTs carry the
        // subject as `sub`. Program.cs disables inbound claim mapping, so the
        // bearer-authenticated branch lands on `sub` only — mirrors the
        // OnboardingController + AuthController fallback.
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(raw) || !Guid.TryParse(raw, out userId))
        {
            userId = Guid.Empty;
            return false;
        }

        return true;
    }

    private ObjectResult MissingUserClaim() =>
        Problem(
            type: MissingUserType,
            title: "Authenticated user id was missing or malformed.",
            statusCode: StatusCodes.Status401Unauthorized);
}
