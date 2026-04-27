using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Training.Plan.Models;

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
/// The <c>POST /regenerate</c> endpoint (Unit 5) lands on this same controller in a
/// later task; Slice 1 ships only the read endpoint.
/// </para>
/// <para>
/// Authorization mirrors <see cref="Modules.Coaching.Onboarding.OnboardingController"/>'s
/// <see cref="AuthPolicies.CookieOrBearer"/> policy. Reads do not require an
/// antiforgery token (idempotent GETs are out of scope for ASP.NET Core's
/// antiforgery middleware).
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/plan")]
[Authorize(Policy = AuthPolicies.CookieOrBearer)]
public sealed partial class PlanRenderingController(
    IDocumentSession session,
    RunCoachDbContext db,
    ILogger<PlanRenderingController> logger) : ControllerBase
{
    private const string MissingUserType = "https://runcoach.app/problems/missing-user-claim";

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

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Plan rendering miss: UserProfile.CurrentPlanId set but PlanProjectionDto absent user={UserId} planId={PlanId}")]
    private static partial void LogProjectionMiss(ILogger logger, Guid userId, Guid planId);

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
