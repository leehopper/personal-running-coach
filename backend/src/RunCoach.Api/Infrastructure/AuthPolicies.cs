namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Names of authorization policies registered in <c>Program.cs</c>. Referenced
/// by <c>[Authorize(Policy = ...)]</c> on controllers so the string never has
/// to be retyped — a typo here surfaces as a compile error instead of a
/// silent 403.
/// </summary>
public static class AuthPolicies
{
    /// <summary>
    /// Accepts either the ASP.NET Core Identity application cookie (browser
    /// SPA) or a JWT bearer token (future iOS shim). Every business endpoint
    /// introduced in later slices is authorized against this policy so the
    /// mobile client lands as a purely additive change.
    /// </summary>
    public const string CookieOrBearer = "CookieOrBearer";
}
