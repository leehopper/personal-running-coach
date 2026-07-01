using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using RunCoach.Api.Infrastructure;

namespace RunCoach.Api.Modules.Settings;

/// <summary>
/// Per-user settings endpoints (Slice 4C-units / DEC-086). The unit preference is
/// a frontend-display-only choice — these routes never touch stored km/SI data or
/// the km-native plan-gen prompt. The read is a safe GET (authenticated, no
/// antiforgery); the write is state-changing and gated by both
/// <see cref="AuthPolicies.CookieOrBearer"/> and
/// <see cref="RequireAntiforgeryTokenAttribute"/> per the repo's DEC-055 contract.
/// </summary>
[ApiController]
[Route("api/v1/settings")]
[Authorize(Policy = AuthPolicies.CookieOrBearer)]
public sealed partial class SettingsController(
    IUserSettingsService service,
    ILogger<SettingsController> logger) : ControllerBase
{
    private const string MissingUserType = "https://runcoach.app/problems/missing-user-claim";
    private const string InvalidUnitPreferenceType = "https://runcoach.app/problems/invalid-unit-preference";

    /// <summary>
    /// GET /api/v1/settings/units — the caller's preferred distance units. Returns
    /// 200 with the default (Kilometers) for a runner who has never set a
    /// preference; the settings page is reachable pre-onboarding, so this never 404s.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the unit preference.</returns>
    [HttpGet("units")]
    [ProducesResponseType(typeof(UnitPreferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnits(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            LogMissingUserClaim(logger);
            return MissingUserClaim();
        }

        var preferredUnits = await service.GetPreferredUnitsAsync(userId, ct).ConfigureAwait(false);
        return Ok(new UnitPreferenceDto(preferredUnits));
    }

    /// <summary>
    /// PUT /api/v1/settings/units — set the caller's preferred distance units
    /// (upsert, last-write-wins). Antiforgery-gated per DEC-055.
    /// </summary>
    /// <param name="request">The desired unit preference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the persisted preference; 400 on an undefined enum value.</returns>
    [HttpPut("units")]
    [RequireAntiforgeryToken]
    [ProducesResponseType(typeof(UnitPreferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PutUnits([FromBody] UnitPreferenceDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetUserId(out var userId))
        {
            LogMissingUserClaim(logger);
            return MissingUserClaim();
        }

        // JsonRequired enforces presence, not a defined value — an out-of-range
        // integer still deserializes to an undefined enum. Reject it explicitly.
        if (!Enum.IsDefined(request.PreferredUnits))
        {
            LogInvalidUnitPreference(logger, userId, (int)request.PreferredUnits);
            return Problem(
                type: InvalidUnitPreferenceType,
                title: "Invalid unit preference",
                detail: "preferredUnits must be a defined PreferredUnits value (0=Kilometers, 1=Miles).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await service.SetPreferredUnitsAsync(userId, request.PreferredUnits, ct).ConfigureAwait(false);
        return Ok(new UnitPreferenceDto(request.PreferredUnits));
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Settings request rejected: authenticated user id claim was missing or malformed.")]
    private static partial void LogMissingUserClaim(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Set-units rejected for user {UserId}: preferredUnits {PreferredUnits} is not a defined value.")]
    private static partial void LogInvalidUnitPreference(ILogger logger, Guid userId, int preferredUnits);

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
            title: "Missing user claim",
            detail: "The authenticated principal has no usable user id claim.",
            statusCode: StatusCodes.Status401Unauthorized);
}
