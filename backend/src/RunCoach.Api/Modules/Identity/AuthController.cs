using System.IdentityModel.Tokens.Jwt;
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

    // Aliased for readability at the cookie-write site. The SPA-readable
    // antiforgery cookie name is owned by AuthCookieNames (DEC-054).
    private const string RequestTokenCookieName = AuthCookieNames.AntiforgeryRequest;

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
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return result.ToRegistrationActionResult(this);
        }

        var response = new AuthResponseDto(user.Id, user.Email!);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [RequireAntiforgeryToken]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
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
            return Ok(new AuthResponseDto(user.Id, user.Email!));
        }

        // IsLockedOut / IsNotAllowed / RequiresTwoFactor collapse to the same
        // generic 401 body so no content side-channel re-opens the enumeration
        // channel that timing parity closed.
        LogLoginFailed(logger, user.Id, result);
        return InvalidCredentials();
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthPolicies.CookieOrBearer)]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        // Cookie auth writes `ClaimTypes.NameIdentifier`; raw JWTs carry the
        // subject as `sub` (`JwtRegisteredClaimNames.Sub`). Program.cs disables
        // inbound claim mapping (so JWT claim types land verbatim rather than
        // being translated to the ClaimTypes.* longhand), which means the
        // bearer-authenticated branch of `CookieOrBearer` resolves through
        // `sub` only. Falling back preserves both call sites without widening
        // the claim-mapping surface.
        //
        // `Guid.TryParse` guards `UserManager.FindByIdAsync` against a
        // malformed claim value — `UserManager` calls
        // `ConvertIdFromString` internally, which throws `FormatException`
        // on non-GUID input BEFORE the EF Core store sees it. Without the
        // guard a signed token whose `sub` isn't a GUID escapes as an
        // unhandled 500 instead of the 401 the contract requires.
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out _))
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new AuthResponseDto(user.Id, user.Email!));
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
    //
    // `HttpOnly=false` on the `__Host-Xsrf-Request` cookie is intentional and
    // is the entire point of the SPA double-submit pattern (DEC-054): the
    // framework-managed `__Host-Xsrf` cookie is `HttpOnly=true` and holds the
    // server-bound secret; this companion is the token-echo channel the SPA
    // reads and copies into the `X-XSRF-TOKEN` request header. Making this
    // cookie `HttpOnly=true` would break every antiforgery-gated POST from
    // the browser. Both cookies carry `__Host-` + `Secure` + `Path=/` per
    // RFC 6265bis. This pattern defends against CSRF, not XSS — an injected
    // same-origin script can still issue a request and the browser will
    // attach the framework-managed antiforgery cookie automatically.
    // XSS mitigation is the SPA's problem (CSP, input sanitisation,
    // dependency hygiene), not this cookie's.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "S2092:\"HttpOnly\" should be set on cookie",
        Justification = "SPA-readable companion cookie in the antiforgery double-submit pattern. The framework-managed `__Host-Xsrf` cookie is HttpOnly=true; this one must be JS-readable so the SPA can echo the token into X-XSRF-TOKEN. See DEC-054.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "S3330:Creating cookies without the \"HttpOnly\" flag is security-sensitive",
        Justification = "SPA-readable companion cookie in the antiforgery double-submit pattern. The framework-managed `__Host-Xsrf` cookie is HttpOnly=true; this one must be JS-readable so the SPA can echo the token into X-XSRF-TOKEN. See DEC-054.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "cs/web/cookie-httponly-not-set",
        Justification = "SPA-readable companion cookie in the antiforgery double-submit pattern. See DEC-054.")]
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
