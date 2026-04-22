using System.ComponentModel.DataAnnotations;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Strongly-typed configuration for the JWT bearer scheme registered as an
/// opt-in, non-default handler reserved for the future iOS shim (DEC-033).
/// Bound from the <c>Auth:Jwt</c> section: user-secrets locally, environment
/// variables in CI/prod (never committed).
/// </summary>
/// <remarks>
/// Slice 0 has no endpoint that accepts the bearer scheme. Absent values are
/// tolerated in Development and CI because <c>ValidateOnStart</c> is only
/// called outside <c>IsDevelopment()</c>; the JWT handler's strict-always
/// validation (R-057) then rejects every incoming token with
/// <c>IDX10500</c> until real config lands. In Production / Staging,
/// startup fails fast if any required value is missing.
/// </remarks>
public sealed record JwtAuthOptions
{
    /// <summary>
    /// Gets the configuration section name, kept alongside the type so
    /// binding sites reference the constant instead of a magic string.
    /// </summary>
    public const string SectionName = "Auth:Jwt";

    /// <summary>
    /// Gets the token issuer (<c>iss</c> claim).
    /// </summary>
    [Required]
    [MinLength(1)]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Gets the expected audience (<c>aud</c> claim).
    /// </summary>
    [Required]
    [MinLength(1)]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Gets the symmetric HMAC signing key. Must be ≥ 32 bytes for HS256 to
    /// satisfy RFC 7518 §3.2.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the key identifier emitted in the JWT <c>kid</c> header. Optional
    /// today, load-bearing for the two-overlapping-keys rotation story when
    /// the iOS shim lands.
    /// </summary>
    public string KeyId { get; init; } = string.Empty;
}
