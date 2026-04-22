namespace RunCoach.Api.Modules.Identity.Contracts;

/// <summary>
/// Response body for <c>POST /api/v1/auth/register</c>, <c>POST /api/v1/auth/login</c>,
/// and <c>GET /api/v1/auth/me</c>. No token field — the session IS the
/// <c>__Host-RunCoach</c> application cookie set by <c>SignInManager</c>.
/// </summary>
public sealed record AuthResponse(Guid UserId, string Email);
