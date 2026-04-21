namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Strongly-typed configuration for the JWT bearer scheme registered as an
/// opt-in, non-default handler reserved for the future iOS shim. Bound from
/// the <c>Auth:Jwt</c> section: user-secrets locally, environment variables
/// in CI/prod (never committed). In Slice 0 no endpoint accepts the bearer
/// scheme, so absent configuration is tolerated — the scheme still registers
/// so the iOS addition lands as a purely additive change.
/// </summary>
public sealed record JwtAuthOptions
{
    /// <summary>
    /// Configuration section name, kept alongside the type so binding sites
    /// reference the constant instead of a magic string.
    /// </summary>
    public const string SectionName = "Auth:Jwt";

    /// <summary>
    /// Gets token issuer (<c>iss</c> claim). When absent, issuer validation is
    /// disabled — acceptable while no endpoint accepts bearer tokens.
    /// </summary>
    public string? Issuer { get; init; }

    /// <summary>
    /// Gets expected audience (<c>aud</c> claim). When absent, audience validation
    /// is disabled.
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// Gets symmetric HMAC signing key. Must be ≥ 32 bytes for HS256 to satisfy
    /// RFC 7518 §3.2. When absent, signature validation is disabled — the
    /// handler still registers but any bearer token that reaches it will
    /// fail authentication, which is fine until the iOS shim wires a real
    /// token-issuance endpoint.
    /// </summary>
    public string? SigningKey { get; init; }
}
