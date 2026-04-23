using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Binds <see cref="JwtAuthOptions"/> into <see cref="JwtBearerOptions"/>
/// with the strict-always validation posture R-057 prescribes. Pulled out
/// of Program.cs so startup code stays under the cognitive-complexity
/// ceiling (Sonar S3776) and so the bearer-setup contract is testable in
/// isolation when the iOS-shim workstream lands.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TokenValidationParameters"/> fields are set unconditionally;
/// when <see cref="JwtAuthOptions.SigningKey"/> is empty the handler fails
/// closed on every token with <c>IDX10500</c>. Gating <c>Validate*</c> on
/// config presence inverts "secure by default" and becomes a landmine
/// under future <c>Authority=</c> / HMAC-only edits
/// (RFC 8725 §2.1 / §3.9, OWASP JWT Cheat Sheet).
/// </para>
/// <para>
/// <c>ValidAlgorithms</c> pinned to HS256 closes the HS/RS key-confusion
/// attack class. <c>MapInboundClaims = false</c> keeps raw JWT claim
/// names (<c>sub</c> / <c>role</c>) instead of the SOAP-style claim
/// rewriting <c>JwtSecurityTokenHandler</c> inherits by default.
/// </para>
/// </remarks>
public sealed class JwtBearerOptionsSetup(IOptions<JwtAuthOptions> jwtAuthOptions)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        var o = jwtAuthOptions.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = BuildTokenValidationParameters(o);
    }

    private static TokenValidationParameters BuildTokenValidationParameters(JwtAuthOptions o) => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidIssuer = o.Issuer,
        ValidAudience = o.Audience,
        IssuerSigningKey = BuildSigningKey(o),
        ClockSkew = TimeSpan.FromSeconds(30),
    };

    private static SymmetricSecurityKey? BuildSigningKey(JwtAuthOptions o)
    {
        if (string.IsNullOrEmpty(o.SigningKey))
        {
            return null;
        }

        var keyId = string.IsNullOrEmpty(o.KeyId) ? "current" : o.KeyId;
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey))
        {
            KeyId = keyId,
        };
    }
}
