using System.Security.Claims;
using JasperFx.Events;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Training.Adaptation;
using Wolverine;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Slice-2b workout-logging endpoints. State-changing routes are gated by both
/// <see cref="AuthPolicies.CookieOrBearer"/> and
/// <see cref="RequireAntiforgeryTokenAttribute"/> per the repo's DEC-055 contract;
/// the read-only history query requires authentication but no antiforgery token (a
/// safe POST-for-query, DEC-055).
/// </summary>
[ApiController]
[Route("api/v1/workouts/logs")]
[Authorize(Policy = AuthPolicies.CookieOrBearer)]
public sealed partial class WorkoutLogsController(
    IWorkoutLogService service,
    IMessageBus bus,
    ILogger<WorkoutLogsController> logger) : ControllerBase
{
    private const string MissingUserType = "https://runcoach.app/problems/missing-user-claim";
    private const string InvalidLogType = "https://runcoach.app/problems/invalid-workout-log";
    private const string InvalidIdempotencyKeyType = "https://runcoach.app/problems/invalid-idempotency-key";
    private const string InvalidCursorType = "https://runcoach.app/problems/invalid-cursor";

    /// <summary>
    /// POST /api/v1/workouts/logs — persist a workout log with a
    /// server-authoritative prescription snapshot (DEC-076) and EF-native
    /// idempotency on the request's idempotency key (DEC-077). A replayed key
    /// returns the original log id without creating a duplicate. After the EF
    /// create commits, the controller synchronously dispatches
    /// <see cref="EvaluateAdaptationCommand"/> (Slice 3 § Unit 5) and surfaces
    /// the resulting <see cref="AdaptationResponseDto"/> envelope on the 201 body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two-transaction boundary, by design: the relational log insert commits in
    /// the service's EF transaction FIRST, then the Wolverine handler stages its
    /// Marten side-effects (events, signal state, idempotency marker) in a second,
    /// separate transaction. A crash between the two leaves a committed log with
    /// no adaptation — the designed recovery path is the client retrying the
    /// create: the replayed idempotency key returns the existing log id, the
    /// dispatch below runs unconditionally, and the handler's missing
    /// WorkoutLogId-keyed marker lets the evaluation run as if for the first
    /// time. When the adaptation DID commit, that same marker turns the
    /// re-dispatch into a no-op that replays the prior envelope.
    /// </para>
    /// <para>
    /// An adaptation failure never fails the create. The handler maps terminal
    /// coaching-LLM failures to a <c>Kind=Error</c> envelope itself; the one known
    /// failure shape that escapes the dispatch — a stream-version conflict that
    /// exhausts the handler's bounded retries — is caught here and mapped to a
    /// generic retryable <c>Kind=Error</c> envelope, still 201. Anything else is a
    /// genuine server fault and propagates as a 5xx.
    /// </para>
    /// </remarks>
    /// <param name="request">The create-workout-log request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new (or replayed) log id plus the adaptation
    /// envelope; 400 on an invalid request.</returns>
    [HttpPost]
    [RequireAntiforgeryToken]
    [ProducesResponseType(typeof(CreateWorkoutLogResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateLog(
        [FromBody] CreateWorkoutLogRequestDto request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetUserId(out var userId))
        {
            LogCreateRejectedMissingUser(logger);
            return MissingUserClaim();
        }

        if (request.IdempotencyKey == Guid.Empty)
        {
            // JsonRequired enforces presence, not a non-default value. An all-zeros
            // key would share one (UserId, Guid.Empty) index slot across every
            // request, so a second distinct run would be swallowed as a replay.
            // Reject it at the boundary, mirroring PlanRenderingController.Regenerate.
            LogCreateRejectedEmptyKey(logger, userId);
            return Problem(
                type: InvalidIdempotencyKeyType,
                title: "Invalid idempotency key",
                detail: "The supplied idempotencyKey must be a non-empty UUID.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        Guid workoutLogId;
        try
        {
            workoutLogId = await service.CreateAsync(userId, request, ct);
        }
        catch (ArgumentException ex)
        {
            // Domain-invariant violations from value-object construction (negative
            // distance/duration, invalid split) surface as a 400 ProblemDetails
            // rather than an unhandled 500. Scoped to the create only — once the
            // log has committed, nothing on the adaptation path may 400 it.
            LogCreateRejectedInvalid(logger, userId, request.IdempotencyKey, ex);
            return Problem(
                type: InvalidLogType,
                title: "The workout log request is invalid.",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Dispatch unconditionally — also on a DEC-077 replay that returned an
        // EXISTING log id. The handler's WorkoutLogId-keyed idempotency marker
        // makes a re-dispatch for an already-evaluated log a designed no-op,
        // and a marker miss (crash before the Marten commit) lets the replayed
        // create recover the missing evaluation.
        var adaptation = await EvaluateAdaptationAsync(workoutLogId, userId, ct);

        return Created(
            $"/api/v1/workouts/logs/{workoutLogId}",
            new CreateWorkoutLogResponseDto(workoutLogId, adaptation));
    }

    /// <summary>
    /// POST /api/v1/workouts/logs/query — one newest-first keyset page of the
    /// authenticated runner's logs (slice-2b Unit 4 / DEC-076 § C). A read, not a
    /// mutation: authenticated but no antiforgery token (DEC-055). Sort, paging, and
    /// the page-size limit all execute as a single SQL query; the response carries
    /// an opaque <c>nextCursor</c> for the next (older) page, or null when last.
    /// </summary>
    /// <param name="request">The query body: optional page size and opaque cursor.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page; 400 on a malformed cursor; 401 when unauthenticated.</returns>
    [HttpPost("query")]
    [ProducesResponseType(typeof(QueryWorkoutLogsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> QueryLogs(
        [FromBody] QueryWorkoutLogsRequestDto request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetUserId(out var userId))
        {
            LogQueryRejectedMissingUser(logger);
            return MissingUserClaim();
        }

        WorkoutLogCursor? cursor = null;
        if (!string.IsNullOrEmpty(request.Cursor))
        {
            if (!WorkoutLogCursorCodec.TryDecode(request.Cursor, out var decoded))
            {
                // A malformed cursor is client input — reject it at the boundary
                // rather than paging from a corrupted anchor, mirroring the
                // empty-idempotency-key guard on the create path.
                LogQueryRejectedInvalidCursor(logger, userId);
                return Problem(
                    type: InvalidCursorType,
                    title: "Invalid cursor",
                    detail: "The supplied cursor is malformed; omit it to start from the most recent log.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            cursor = decoded;
        }

        var page = await service.QueryAsync(userId, cursor, request.Limit, ct);
        return Ok(page);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Workout log create rejected: authenticated user id claim was missing or malformed.")]
    private static partial void LogCreateRejectedMissingUser(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Workout log create rejected for user {UserId}: idempotency key was an empty UUID.")]
    private static partial void LogCreateRejectedEmptyKey(ILogger logger, Guid userId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Workout log create rejected as invalid for user {UserId} with idempotency key {IdempotencyKey}.")]
    private static partial void LogCreateRejectedInvalid(
        ILogger logger, Guid userId, Guid idempotencyKey, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Adaptation evaluation conflicted after bounded retries for workout log {WorkoutLogId}, user {UserId}; create still answers 201 with a retryable error envelope.")]
    private static partial void LogAdaptationDispatchConflicted(
        ILogger logger, Guid workoutLogId, Guid userId, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Workout log query rejected: authenticated user id claim was missing or malformed.")]
    private static partial void LogQueryRejectedMissingUser(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Workout log query rejected for user {UserId}: the supplied cursor was malformed.")]
    private static partial void LogQueryRejectedInvalidCursor(ILogger logger, Guid userId);

    /// <summary>
    /// Synchronously evaluates the just-committed log for plan adaptation via the
    /// Wolverine inline pipeline. <c>InvokeForTenantAsync</c> sets the user id as
    /// Marten's conjoined tenant on the handler's auto-applied
    /// <c>IDocumentSession</c> — without it the handler's session has no TenantId
    /// and its multi-tenant document loads fail to resolve.
    /// </summary>
    private async Task<AdaptationResponseDto> EvaluateAdaptationAsync(
        Guid workoutLogId,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            return await bus.InvokeForTenantAsync<AdaptationResponseDto>(
                userId.ToString(),
                new EvaluateAdaptationCommand(workoutLogId, userId),
                ct);
        }
        catch (EventStreamUnexpectedMaxEventIdException ex)
        {
            // The handler retries a lost stream-version race a bounded number of
            // times before the conflict escapes here. The log row is already
            // committed, so the create must still answer 201 — the conflict maps
            // to a generic retryable Kind=Error envelope instead of a 5xx.
            //
            // The catch targets `EventStreamUnexpectedMaxEventIdException` (a
            // `JasperFx.ConcurrencyException`) because that is what a lost Rich
            // append-mode race on the existing plan stream actually throws:
            // Marten transforms the loser's (stream id, version) unique violation
            // into this type. `Marten.Exceptions.ConcurrentUpdateException` is a
            // different, unrelated hierarchy (session-level write collisions) and
            // never fires for this conflict.
            LogAdaptationDispatchConflicted(logger, workoutLogId, userId, ex);
            return new AdaptationResponseDto
            {
                Kind = AdaptationResponseKind.Error,
                ErrorMessage = "Your workout was saved, but your coach could not review it just now. Submitting the same log again will retry the review.",
                Retryable = true,
            };
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        // Cookie auth writes ClaimTypes.NameIdentifier; raw JWTs carry the subject
        // as `sub`. Program.cs disables inbound claim mapping (see AuthController).
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
