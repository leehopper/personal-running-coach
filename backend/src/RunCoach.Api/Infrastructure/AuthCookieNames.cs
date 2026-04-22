namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Single source of truth for the cookie + header names that form the
/// browser-auth contract across the cookie-auth stack, antiforgery stack,
/// Swagger helper, integration tests, and the SPA. A one-sided rename of
/// any of these literals silently breaks the authenticated request flow in
/// exactly one direction (browser rejects the cookie, or the server rejects
/// the header) — centralizing the values keeps renames single-commit.
///
/// All three cookie names use the RFC 6265bis <c>__Host-</c> prefix, which
/// the browser enforces against <c>Secure</c> + <c>Path=/</c> + no
/// <c>Domain</c>. The header name is the historical Angular / axios
/// double-submit convention the SPA echoes per DEC-054.
/// </summary>
public static class AuthCookieNames
{
    /// <summary>
    /// The Identity application session cookie. Written on successful
    /// login, rotated on logout, carries the encrypted authentication
    /// ticket. <c>HttpOnly=true</c>.
    /// </summary>
    public const string Session = "__Host-RunCoach";

    /// <summary>
    /// The framework-managed antiforgery cookie. Holds the server-bound
    /// antiforgery secret. <c>HttpOnly=true</c>.
    /// </summary>
    public const string Antiforgery = "__Host-Xsrf";

    /// <summary>
    /// The SPA-readable companion cookie in the antiforgery double-submit
    /// pair. <c>HttpOnly=false</c> by design (DEC-054) — the SPA reads
    /// this value and copies it into <see cref="AntiforgeryHeader"/> on
    /// unsafe requests.
    /// </summary>
    public const string AntiforgeryRequest = "__Host-Xsrf-Request";

    /// <summary>
    /// The HTTP header name the SPA uses to echo the
    /// <see cref="AntiforgeryRequest"/> cookie value. The antiforgery
    /// middleware matches the incoming header against the server-held
    /// token in <see cref="Antiforgery"/>.
    /// </summary>
    public const string AntiforgeryHeader = "X-XSRF-TOKEN";
}
