using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using RunCoach.Api.Infrastructure;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Read-only conversation surface for the "Explain-the-change" panel (Slice 3
/// Unit 2, DEC-079). Exposes <c>GET /api/v1/conversation/turns</c> which resolves
/// the runner's active plan (via <c>RunnerOnboardingProfile.CurrentPlanId</c> on the
/// EF projection, mirroring <see cref="Training.Plan.PlanRenderingController"/>) and
/// returns its <see cref="ConversationLogView"/> turns newest-first. The read takes
/// no antiforgery token; it is user-scoped via the per-request tenanted Marten
/// session, so a caller only ever sees their own turns. The endpoint returns 200
/// with an empty list when the runner has no active plan or no turns yet (PR5 wires
/// production appends).
/// </summary>
[ApiController]
[Route("api/v1/conversation")]
[Authorize(Policy = AuthPolicies.CookieOrBearer)]
public sealed class ConversationController(
    IDocumentStore store,
    RunCoachDbContext db) : ControllerBase
{
    private const string MissingUserType = "https://runcoach.app/problems/missing-user-claim";

    /// <summary>GET /api/v1/conversation/turns — read the runner's adaptation + safety turns, newest-first.</summary>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>200 with the turns (possibly empty); 401 when the user claim is missing or malformed.</returns>
    [HttpGet("turns")]
    [ProducesResponseType(typeof(ConversationTurnsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTurns(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingUserClaim();
        }

        // Resolve the active plan id off the EF projection (read-only; AsNoTracking).
        // The conversation log is keyed by plan id, so turns for the active plan are
        // the panel's scope — a regenerated plan starts a fresh log.
        var currentPlanId = await EntityFrameworkQueryableExtensions
            .SingleOrDefaultAsync(
                db.RunnerOnboardingProfiles
                    .AsNoTracking()
                    .Where(p => p.UserId == userId)
                    .Select(p => p.CurrentPlanId),
                ct)
            .ConfigureAwait(false);

        if (currentPlanId is null)
        {
            return Ok(ConversationTurnsResponseDto.Empty);
        }

        // Conjoined tenancy: the DI session has no tenant, so open a per-request
        // tenanted session — same pattern as PlanRenderingController.GetCurrent.
        await using var session = store.LightweightSession(userId.ToString());
        var log = await session
            .LoadAsync<ConversationLogView>(currentPlanId.Value, ct)
            .ConfigureAwait(false);

        if (log is null)
        {
            return Ok(ConversationTurnsResponseDto.Empty);
        }

        // Newest-first. CreatedAt is the Marten event timestamp; two turns appended in
        // one transaction (EventAppendMode.Rich) share a single transaction_timestamp(),
        // so the per-stream EventVersion is the deterministic tiebreaker that keeps the
        // panel order stable rather than relying on the stable-sort's incidental order.
        var turns = log.Turns
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.EventVersion)
            .Select(MapTurn)
            .ToArray();

        return Ok(new ConversationTurnsResponseDto(turns));
    }

    /// <summary>
    /// GET /api/v1/conversation/timeline — the runner's composed conversation
    /// (Slice 4B Unit 3, DEC-085): their user-scoped interactive turns unioned with
    /// the current plan's proactive adaptation/safety turns, oldest-first for a chat
    /// composer. The interactive turns survive plan regeneration; the proactive turns
    /// reset with the plan. User-scoped via the per-request tenanted Marten session;
    /// no antiforgery (read). Returns the interactive turns alone until the current
    /// plan has proactive turns (or none until PR4 wires the interactive appends).
    /// </summary>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>200 with the oldest-first timeline (possibly empty); 401 when the user claim is missing or malformed.</returns>
    [HttpGet("timeline")]
    [ProducesResponseType(typeof(ConversationTimelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTimeline(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingUserClaim();
        }

        // Conjoined tenancy: one per-request tenanted session loads both the
        // user-scoped interactive stream (keyed by user id) and the plan-scoped
        // proactive log (keyed by the active plan id) — both under the same tenant.
        await using var session = store.LightweightSession(userId.ToString());

        // Interactive turns — keyed by user id, so they persist across plan regens.
        var conversation = await session
            .LoadAsync<ConversationView>(userId, ct)
            .ConfigureAwait(false);
        IReadOnlyList<InteractiveTurnView> interactive = conversation?.Turns ?? [];

        // Proactive turns for the runner's active plan — keyed by plan id, reset on regen.
        var currentPlanId = await EntityFrameworkQueryableExtensions
            .SingleOrDefaultAsync(
                db.RunnerOnboardingProfiles
                    .AsNoTracking()
                    .Where(p => p.UserId == userId)
                    .Select(p => p.CurrentPlanId),
                ct)
            .ConfigureAwait(false);

        IReadOnlyList<ConversationTurnView> proactive = [];
        if (currentPlanId is not null)
        {
            var log = await session
                .LoadAsync<ConversationLogView>(currentPlanId.Value, ct)
                .ConfigureAwait(false);
            proactive = log?.Turns ?? [];
        }

        // Union oldest-first. CreatedAt (the Marten event timestamp) is the cross-stream
        // ordering key; the per-stream EventVersion then the TurnId are deterministic
        // tiebreakers for turns that share a transaction_timestamp().
        var turns = interactive
            .Select(MapInteractiveTurn)
            .Concat(proactive.Select(MapProactiveTurn))
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.EventVersion)
            .ThenBy(x => x.TurnId)
            .Select(x => x.Dto)
            .ToArray();

        return Ok(new ConversationTimelineDto(turns));
    }

    private static ConversationTurnDto MapTurn(ConversationTurnView turn) =>
        new(
            turn.TriggeringPlanEventId,
            turn.Role,
            turn.Content,
            turn.EscalationLevel,
            turn.SafetyTier,
            turn.ReferralCategory,
            turn.AdaptationKind,
            turn.Diff,
            turn.TriggeringWorkoutLogId,
            turn.CreatedAt);

    private static (DateTimeOffset CreatedAt, long EventVersion, Guid TurnId, ConversationTimelineTurnDto Dto)
        MapInteractiveTurn(InteractiveTurnView turn) =>
        (turn.CreatedAt, turn.EventVersion, turn.TurnId,
            new ConversationTimelineTurnDto(
                turn.Participant == ConversationParticipant.User
                    ? ConversationTimelineTurnKind.User
                    : ConversationTimelineTurnKind.Coach,
                turn.TurnId,
                turn.CreatedAt,
                new InteractiveTurnDto(turn.Content, turn.IsErrored),
                Proactive: null));

    private static (DateTimeOffset CreatedAt, long EventVersion, Guid TurnId, ConversationTimelineTurnDto Dto)
        MapProactiveTurn(ConversationTurnView turn) =>
        (turn.CreatedAt, turn.EventVersion, turn.TriggeringPlanEventId,
            new ConversationTimelineTurnDto(
                turn.Role == ConversationRole.AssistantAdaptation
                    ? ConversationTimelineTurnKind.Adaptation
                    : ConversationTimelineTurnKind.Safety,
                turn.TriggeringPlanEventId,
                turn.CreatedAt,
                Interactive: null,
                MapTurn(turn)));

    private bool TryGetUserId(out Guid userId)
    {
        // Cookie auth writes ClaimTypes.NameIdentifier; raw JWTs carry the subject
        // as `sub` (Program.cs disables inbound claim mapping) — mirrors the
        // OnboardingController / PlanRenderingController fallback.
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
