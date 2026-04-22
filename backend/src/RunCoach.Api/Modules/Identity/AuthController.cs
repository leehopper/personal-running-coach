using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Modules.Identity.Entities;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace RunCoach.Api.Modules.Identity;

/// <summary>
/// Slice 0 auth endpoints — hand-rolled rather than <c>MapIdentityApi&lt;TUser&gt;()</c>
/// so the contract stays under project control and lands the
/// <see cref="AuthPolicies.CookieOrBearer"/> dual-scheme seam on day one.
/// Login is timing-safe per DEC-053 (FindByEmailAsync + cached dummy-hash
/// VerifyHashedPassword on the unknown-user branch); register translates
/// <see cref="IdentityResult.Errors"/> via <see cref="IdentityResultExtensions"/>
/// per DEC-052; the <c>/xsrf</c> endpoint writes the SPA-readable
/// <c>__Host-Xsrf-Request</c> cookie per DEC-054.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6960:Controllers should not have mixed responsibilities", Justification = "Spec §Unit 2 mandates a single AuthController holding the five auth endpoints (xsrf, register, login, me, logout).")]
public sealed partial class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IAntiforgery antiforgery,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string InvalidCredentialsType =
        "https://runcoach.app/problems/invalid-credentials";

    // Additional SPA-readable antiforgery cookie written alongside the
    // framework-managed `__Host-Xsrf` cookie (configured via
    // AntiforgeryOptions.Cookie.Name in T02.2). The SPA reads this value and
    // echoes it in the X-XSRF-TOKEN request header. Named `__Host-Xsrf-Request`
    // rather than the Angular-convention `XSRF-TOKEN` to match the `__Host-`
    // posture already on `__Host-RunCoach` and `__Host-Xsrf` (DEC-054).
    private const string RequestTokenCookieName = "__Host-Xsrf-Request";

    // Single PBKDF2-HMAC-SHA512 / 100k-iteration V3 hash computed once at
    // type load — the unknown-user login branch burns one VerifyHashedPassword
    // call against this to equalize the ~40–80 ms hash timing (DEC-053 / R-059).
    // Re-hashing per request would flip the timing leak rather than close it.
    private static readonly string DummyHash =
        new PasswordHasher<ApplicationUser>().HashPassword(
            new ApplicationUser(),
            "__runcoach_dummy__");

    [HttpGet("xsrf")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Xsrf()
    {
        IssueAntiforgeryTokens();
        return NoContent();
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [RequireAntiforgeryToken]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return result.ToRegistrationActionResult(this, request);
        }

        var response = new AuthResponse(user.Id, user.Email!);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            // Burn one PBKDF2 pass on the supplied password so the wall-clock
            // response time matches the real-user failure branch (SignInManager
            // runs VerifyHashedPassword on a found user). Output is discarded —
            // the JIT cannot elide the call because PBKDF2 is an extern
            // P/Invoke. See DEC-053.
            _ = passwordHasher.VerifyHashedPassword(
                new ApplicationUser(),
                DummyHash,
                request.Password);
            LogLoginFailedUnknownEmail(logger);
            return InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: true,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            IssueAntiforgeryTokens();
            return Ok(new AuthResponse(user.Id, user.Email!));
        }

        // IsLockedOut / IsNotAllowed / RequiresTwoFactor collapse to the same
        // generic 401 body so no content side-channel re-opens the enumeration
        // channel that timing parity closed.
        LogLoginFailed(logger, user.Id, result);
        return InvalidCredentials();
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthPolicies.CookieOrBearer)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new AuthResponse(user.Id, user.Email!));
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthPolicies.CookieOrBearer)]
    [RequireAntiforgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        IssueAntiforgeryTokens();
        return NoContent();
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Login failed: unknown email (timing-masked)")]
    private static partial void LogLoginFailedUnknownEmail(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Login failed for {UserId}: {Result}")]
    private static partial void LogLoginFailed(ILogger logger, Guid userId, SignInResult result);

    private ObjectResult InvalidCredentials() =>
        Problem(
            type: InvalidCredentialsType,
            title: "Invalid email or password.",
            statusCode: StatusCodes.Status401Unauthorized);

    // Writes fresh `__Host-Xsrf` (framework, HttpOnly) + `__Host-Xsrf-Request`
    // (SPA-readable) cookies bound to the current authentication identity.
    // Called from `/xsrf` (initial app-boot seed) and from `/login` / `/logout`
    // immediately after the auth state changes. Antiforgery tokens are bound
    // to the Identity security stamp at issue time; rotating in lockstep with
    // sign-in / sign-out prevents stale tokens from failing the next unsafe
    // request, which otherwise forces the SPA to call `/xsrf` explicitly
    // between every auth transition and the first state-changing request.
    private void IssueAntiforgeryTokens()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);

        Response.Cookies.Append(
            RequestTokenCookieName,
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
            });
    }
}
