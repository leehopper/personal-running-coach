using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using RunCoach.Api.Infrastructure;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Slice-2b workout-logging endpoints. Every state-changing route is gated by
/// both <see cref="AuthPolicies.CookieOrBearer"/> and
/// <see cref="RequireAntiforgeryTokenAttribute"/> per the repo's DEC-055 contract.
/// </summary>
[ApiController]
[Route("api/v1/workouts/logs")]
[Authorize(Policy = AuthPolicies.CookieOrBearer)]
public sealed class WorkoutLogsController(IWorkoutLogService service) : ControllerBase
{
    private const string MissingUserType = "https://runcoach.app/problems/missing-user-claim";
    private const string InvalidLogType = "https://runcoach.app/problems/invalid-workout-log";

    /// <summary>
    /// POST /api/v1/workouts/logs — persist a workout log with a
    /// server-authoritative prescription snapshot (DEC-076) and EF-native
    /// idempotency on the request's idempotency key (DEC-077). A replayed key
    /// returns the original log id without creating a duplicate.
    /// </summary>
    /// <param name="request">The create-workout-log request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new (or replayed) log id; 400 on an invalid request.</returns>
    [HttpPost]
    [RequireAntiforgeryToken]
    [ProducesResponseType(typeof(CreateWorkoutLogResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateLog(
        [FromBody] CreateWorkoutLogRequestDto request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetUserId(out var userId))
        {
            return MissingUserClaim();
        }

        try
        {
            var workoutLogId = await service.CreateAsync(userId, request, ct);
            return Created(
                $"/api/v1/workouts/logs/{workoutLogId}",
                new CreateWorkoutLogResponseDto(workoutLogId));
        }
        catch (ArgumentException ex)
        {
            // Domain-invariant violations from value-object construction (negative
            // distance/duration, invalid split) surface as a 400 ProblemDetails
            // rather than an unhandled 500.
            return Problem(
                type: InvalidLogType,
                title: "The workout log request is invalid.",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
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
