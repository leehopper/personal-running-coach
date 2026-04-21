# JWT bearer "wired today, consumed later" in ASP.NET Core 10

## Direct answer to the primary question

**Keep `AddJwtBearer` registered, but invert the permissive-flag logic: set `Validate*` and `RequireSignedTokens` to `true` unconditionally and let the handler fail closed when no keys are configured. Gate `ValidateOnStart()` on `!IsDevelopment()`.** The current "`ValidateIssuerSigningKey = hasSigningKey`" pattern is not exploitable today — but it inverts the "secure by default" principle and becomes a latent landmine the moment someone adds an `Authority` URL, an HMAC secret, or a bearer-only endpoint. The 2026 idiom is *strict validation always, presence of keys optional in dev/test, startup fails fast in prod*.

A defensible alternative — equally idiomatic and slightly cleaner — is to **not register `AddJwtBearer` at all until the iOS shim actually needs it**, per YAGNI. Deleting the registration later is still a purely additive PR because `AuthenticationBuilder.AddJwtBearer(...)` and the `CookieOrBearer` policy's scheme list are both mechanical edits. Pick the strict-always option if you want the `CookieOrBearer` policy to stay shape-stable across the iOS transition; pick the gated option if you value fewer dormant moving parts.

The developer's instinct that "permissive validation can't do harm when no endpoint uses it" is **narrowly correct today** (thanks to `RequireSignedTokens = true` defaulting to true and `IssuerSigningKey = null` producing `IDX10500` fail-closed), but it's the wrong *default posture* to codify. The permissive pattern's risk is not current exploitability — it's that it silently re-enables dangerous behavior under future config edits.

---

## Sub-question 1 — Conditional vs. always-register

**Recommendation: always-register, with strict validation flags and env-gated fail-fast.** The Microsoft Learn docs (`configure-jwt-bearer-authentication?view=aspnetcore-10.0`) and Andrew Lock's walkthrough ("A look behind the JWT bearer authentication middleware in ASP.NET Core") both show that `AddJwtBearer` defers signing-key and metadata resolution to `JwtBearerPostConfigureOptions.PostConfigure`, which runs only the first time the scheme is *invoked*. **Startup cost of a dormant registration is negligible.** The handler is lazy and the DI surface is three services (the options configurer, post-configurer, and the named handler), none of which execute work at boot.

No canonical 2024–2026 post from Andrew Lock, Milan Jovanović, Nick Chapsas, or Khalid Abuhakmeh explicitly addresses the "register Bearer as opt-in for a future client" pattern — it's a niche shape. The community samples split two ways: **eShop, Clean Architecture, and the `dotnet user-jwts` workflow always register Bearer** because they ship a dev-populated `Authentication:Schemes:Bearer` section in `appsettings.Development.json`; nothing in the framework treats "Bearer registered without keys" as an error state. **Conditional registration (gating the `AddJwtBearer` call itself)** is a valid YAGNI-driven choice when the scheme truly serves no current purpose, but it requires the `CookieOrBearer` policy's scheme list to be gated in lockstep — otherwise `AuthorizationPolicy.AuthenticationSchemes` references a non-existent scheme and `IAuthenticationSchemeProvider` throws `InvalidOperationException` on the first protected request.

**For RunCoach's stated constraint ("iOS shim lands as a purely additive change"), always-register wins.** Keeping the scheme in the policy list means the iOS PR touches only `appsettings.Production.json` plus one controller — not `Program.cs` and not the authorization policy shape.

## Sub-question 2 — Options validation posture

**Use the `[OptionsValidator]` source generator (stable in .NET 8+, AOT-safe) with conditional `ValidateOnStart()`.** Microsoft Learn's "Compile-time options validation source generation" page makes this the canonical replacement for `ValidateDataAnnotations()` — it rewrites the `Required`/`MinLength`/`Range` attributes into reflection-free validators so `PublishAot` stops warning IL2025/IL3050. Andrew Lock's "Adding validation to strongly typed configuration objects in .NET 6" (still the definitive reference) and Milan Jovanović's 2024 Options-pattern post converge on the same shape. Kevin Smith's "Failing fast with invalid configuration in .NET" (Feb 2023) shows the env-aware delegate variant, but gating `ValidateOnStart` itself is cleaner and what the aspnetcore team's samples use.

Known gotcha: `ValidateOnStart` is hosted-service-driven and only fires when `IHost.StartAsync` runs — see dotnet/aspnetcore #56453. If nothing resolves `IOptions<JwtAuthOptions>.Value` in CI, validation silently skips. That's fine for tests; it's the reason the env-gate works.

## Sub-question 3 — Security audit of permissive `TokenValidationParameters`

**Alg=none is not accepted today.** The controlling flag is `TokenValidationParameters.RequireSignedTokens`, which defaults to `true` and is **independent of `ValidateIssuerSigningKey`** (per Microsoft Learn's `RequireSignedTokens` page, v8.15.0, and the `TokenValidationParameters.cs` source in the AzureAD identitymodel repo). `JwtSecurityTokenHandler.ValidateSignature` and `JsonWebTokenHandler.ValidateJWSAsync` both throw `SecurityTokenInvalidSignatureException` when a signature is missing and `RequireSignedTokens = true`. An attacker-crafted `alg:none` token is rejected *before* the signing-key validators run, regardless of how you set `ValidateIssuer`, `ValidateAudience`, or `ValidateIssuerSigningKey`.

**Null-key behavior is fail-closed.** With `IssuerSigningKey = null` and no `Authority`/metadata URL, a signed bearer token fails `JsonWebTokenHandler.ValidateSignature` with `SecurityTokenSignatureKeyNotFoundException: IDX10500: Signature validation failed. No security keys were provided to validate the signature.` `JwtBearerHandler` surfaces this as `AuthenticateResult.Fail(...)`, not success, not `NoResult`. The `ValidateIssuerSigningKey = false` flag only skips the *key-lifetime/trust* post-check; it does not skip signature validation itself. This distinction is captured in AzureAD/identitymodel issues #332 and #972 and PR #1158 (which fixed a historical null-key `ArgumentNullException` path).

**Relevant CVEs 2024–2026** (ensure patched versions):

| CVE | Date | Package | Fixed in |
|---|---|---|---|
| CVE-2024-21319 | 2024-01-09 | `Microsoft.IdentityModel.JsonWebTokens`, `System.IdentityModel.Tokens.Jwt` — JWE compression-ratio DoS | 5.7.0 / 6.34.0 / **7.1.2** |
| CVE-2024-21643 | 2024-01 | `Microsoft.IdentityModel.Protocols.SignedHttpRequest` — `jku` SSRF/RCE | 6.34.0 / 7.1.2 |
| CVE-2024-30105 | 2024-07-09 | `System.Text.Json` (runtime dep) — DoS via `DeserializeAsyncEnumerable` | 8.0.4 / runtime 8.0.7 |

No public CVE indicts "permissive `TokenValidationParameters`" as a library bug — it's treated as a configuration defect. The `RequireSignedTokens = true` default has held across 5.x → 8.x. Target `Microsoft.IdentityModel.*` ≥ 7.1.2 (ideally current 8.x); `Microsoft.AspNetCore.Authentication.JwtBearer` is at 10.0.6 on NuGet.

**Policy evaluation is safe by iteration semantics, not by short-circuit.** `PolicyEvaluator.AuthenticateAsync` iterates *every* scheme in `policy.AuthenticationSchemes` with no short-circuit; successful schemes' identities are merged via `SecurityHelper.MergeUserPrincipal`. A failing scheme does not abort the loop — it's simply skipped in the merge. Consequence for `CookieOrBearer`:

- **Bogus `Authorization: Bearer` + no cookie** → JwtBearer fails (`IDX10500`), Cookie finds nothing, no principal, 401. Safe.
- **Bogus Bearer + valid cookie** → JwtBearer fails, Cookie succeeds, merged principal is the cookie user. The bogus Bearer is inert. Safe.
- **Malformed/missing Authorization header** → JwtBearer returns `NoResult()` (not `Fail()`), Cookie succeeds, normal flow.

The handler cannot mint a principal today because no signing key is resolvable. There's no path by which a malicious `Authorization` header subverts the policy in the current configuration.

**Latent risk assessment — where the current permissive flags *become* dangerous:**

1. **Authority auto-discovery regression.** If someone sets `options.Authority = "https://login.microsoftonline.com/common"` without also setting `ValidIssuer`/`ValidAudience`, JwtBearer auto-fetches JWKS and validates signatures. With `ValidateIssuer = false` and `ValidateAudience = false` still silently gated on `!string.IsNullOrEmpty(...)`, any token signed by that authority would be accepted — the canonical confused-deputy / audience-confusion attack that RFC 8725 §3.9 and the OWASP JWT Cheat Sheet explicitly warn against.
2. **Signing-key-only config.** Set `IssuerSigningKey` (e.g., a leaked sample HMAC secret) without issuer/audience → the scheme accepts any token signed with that key, including attacker-generated ones.
3. **`ValidateIssuerSigningKey = false` surviving past config rollout** removes the key-trust post-check as a defense layer.
4. **Future endpoint adoption.** The moment anyone writes `[Authorize(AuthenticationSchemes = "Bearer")]` or expands `CookieOrBearer` to a sensitive endpoint, latent misconfiguration becomes live exposure.

**RFC 8725 (§2.1, §3.1, §3.9) and OWASP treat "absence of config implies absence of validation" as an anti-pattern.** The fix is to make validation flags unconditional constants, not expressions over config presence.

## Sub-question 4 — Integration-test posture

**Leave `AddJwtBearer` registered in the test host. Do not introduce a `TestAuthHandler` for cookie-authenticated endpoints.** Microsoft Learn's "Integration tests in ASP.NET Core" (aspnetcore-10.0) treats `ConfigureTestServices` as a tool for *targeted overrides*, not surgical removal. Ripping the scheme out by hunting `ServiceDescriptor`s is brittle — `AddJwtBearer` registers multiple options-configure callbacks, a `PostConfigureOptions<JwtBearerOptions>`, the handler, and the scheme entry in `AuthenticationOptions`. dotnet/aspnetcore #45608 documents the ordering trap where `ConfigureTestServices` overrides fail to beat a `DefaultAuthenticateScheme` set in `Program.cs`.

**Does permissive-validation leak into test behavior?** No. With `Auth:Jwt` unset in CI, the scheme registers with no `IssuerSigningKey`, no `Authority`, no metadata URL. Every token is rejected with `IDX10500`. Tests that only send cookies never invoke the JWT handler. Tests cannot accidentally authenticate with bogus tokens.

**Three canonical test-host patterns** (pick by context):

1. **Leave it alone** (today's RunCoach): cookie-only tests, JWT dormant. No overrides needed.
2. **`PostConfigure<JwtBearerOptions>` with a test key** (Stebet 2019, Renato Golia Aug-2025, Craig Wardman 2024): when you start writing iOS-path tests, inject a known `SymmetricSecurityKey` and a pre-built `OpenIdConnectConfiguration` so the handler doesn't try to hit a discovery endpoint. Works with `JsonWebTokenHandler`.
3. **`TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>`** (Joao Grassi pattern): only use when the test is about *authorization policy semantics*, not auth wiring. Warning: a `TestAuthHandler` that replaces the default scheme will hijack cookie tests too.

**`ValidateOnStart` in tests:** gate it on `!IsDevelopment()` in `Program.cs`, then run tests under `UseEnvironment("Testing")`. This is the Milan Jovanović / Anton Martyniuk / eShop pattern. Don't disable fail-fast unconditionally; don't call `ValidateOnStart` on a genuinely empty section.

## Sub-question 5 — Key material convention for first-party issuance in 2026

**For RunCoach's single-deployment, backend-issues/backend-verifies, no-federation scenario: HS256 with `SymmetricSecurityKey` is idiomatic, RFC 8725 §3.5-compliant, and the right call.** The "RS256 is universally better" drumbeat (Auth0, SuperTokens, Supabase) is correct for *multi-party / federated* systems but over-applies to single-trust-boundary topology. WorkOS's 2024 post states it directly: *"Use HS256 when your signing and verification happen in the same trust boundary, typically a single application or a small cluster of services that already share secrets through a secure channel."* OpenIddict's docs say the same: *"symmetric keys are always chosen first [for access tokens], except for identity tokens."* The iOS app is a token *carrier*, not a verifier; the backend is the only verifier.

**Do NOT use `AddBearerToken` for this.** Per Andrew Lock's November 2023 post "Should you use the .NET 8 Identity API endpoints?" and Tore Nestenius's "BearerToken: The new Authentication handler in .NET 8": `AddBearerToken` produces **opaque, Data-Protection-encrypted tokens — not JWTs**. They cannot be parsed by any JWT library, cannot be decoded by iOS inspection tooling or Postman, and are intentionally coupled to `MapIdentityApi<TUser>()` — a non-standard implementation of the deprecated OAuth 2.0 Resource Owner Password Credentials grant. Lock's verdict: *"I strongly suggest you don't use these bearer tokens."* For an iOS shim wanting inspectable JWT claims, use `AddJwtBearer` + issue JWTs yourself via `JsonWebTokenHandler`.

**`JsonWebTokenHandler` is the 2026 issuance path, not `JwtSecurityTokenHandler`.** Since .NET 8, `JwtBearerOptions.UseTokenHandlers = true` is the default (dotnet/aspnetcore #49469). `JsonWebTokenHandler` is ~30% faster, AOT-friendly (no Newtonsoft dep), and supports async validation and last-known-good OIDC metadata. Breaking-change note: event callbacks now receive `JsonWebToken`, not `JwtSecurityToken` — cast accordingly. Also set `JsonWebTokenHandler.MapInboundClaims = false` (or clear the default map) to stop the SOAP-style claim rewriting and work with raw JWT claim names (`sub`, `role`).

**Rotation:** both algorithms use the same plumbing — a collection on `TokenValidationParameters.IssuerSigningKeys`, each with `KeyId` set, tokens carrying `kid` in the header. Microsoft ships **no built-in key-rotation for `AddJwtBearer` in the first-party issuer role.** For HS256, roll your own with two overlapping keys; rotate every 60–180 days per Curity/OpenIddict/Zalando convergence; store in Azure Key Vault / `IDataProtectionProvider`-protected storage, never appsettings. If you ever need JWKS or federation, adopt `NetDevPack.Security.Jwt` (auto-rotates, exposes JWKS, defaults to ECDSA P-256).

**.NET 10 note:** no new first-party JWT issuance helper was introduced. Headline auth features are passkeys/WebAuthn in ASP.NET Identity, C# 14 extension members on `ClaimsPrincipal`, and new auth metrics (per Auth0's "What's New for Authentication and Authorization in .NET 10", 2025). `AddJwtBearer` and `JsonWebTokenHandler` remain the building blocks.

---

## Concrete code

### `Program.cs` — strict-always posture (recommended)

```csharp
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var authBuilder = builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme);
authBuilder.AddIdentityCookies(/* … */);

// Always register — keeps the iOS shim PR purely additive.
authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtAuthOptions>>((bearer, jwt) =>
    {
        var o = jwt.Value;
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            // Strict ALWAYS — absence of config means "no keys to validate against",
            // which makes the handler fail closed (IDX10500), not fail open.
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens      = true,

            // Pin algorithms — closes the HS/RS key-confusion attack class.
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },

            // Issuer/audience/key are null when config absent; handler rejects
            // all tokens with IDX10500. When config lands, they populate.
            ValidIssuer      = o.Issuer,
            ValidAudience    = o.Audience,
            IssuerSigningKey = string.IsNullOrEmpty(o.SigningKey)
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey))
                    { KeyId = o.KeyId ?? "current" },

            ClockSkew = TimeSpan.FromSeconds(30), // default 5min is too generous
        };
    });
```

### `JwtAuthOptions` with source-generated validator + env-gated fail-fast

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

public sealed class JwtAuthOptions
{
    public const string SectionName = "Auth:Jwt";

    [Required, MinLength(1)]  public required string Issuer     { get; init; }
    [Required, MinLength(1)]  public required string Audience   { get; init; }
    [Required, MinLength(32)] public required string SigningKey { get; init; }
    public string? KeyId { get; init; }
}

[OptionsValidator]  // .NET 8+ source generator — AOT-safe, reflection-free
public sealed partial class JwtAuthOptionsValidator : IValidateOptions<JwtAuthOptions> { }
```

```csharp
// Program.cs
var jwtOpts = builder.Services
    .AddOptions<JwtAuthOptions>()
    .BindConfiguration(JwtAuthOptions.SectionName);

builder.Services.AddSingleton<IValidateOptions<JwtAuthOptions>, JwtAuthOptionsValidator>();

// Fail-fast in prod/staging; tolerate absence in dev/CI/test.
if (!builder.Environment.IsDevelopment())
    jwtOpts.ValidateOnStart();
```

### Test-host posture (today, cookie-only)

```csharp
public sealed class RunCoachWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");  // skips ValidateOnStart branch
        // No auth overrides. Cookie path works. JWT registered but rejects
        // all tokens (IDX10500, no keys) — exactly what we want.
    }
}
```

### Test-host posture (later, when iOS-path tests arrive — add without disturbing cookie tests)

```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.UseEnvironment("Testing");
    builder.ConfigureTestServices(services =>
    {
        services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // Short-circuit OIDC metadata; inject deterministic test key.
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = TestJwt.Issuer
                };
                options.Configuration.SigningKeys.Add(TestJwt.SigningKey);
                options.TokenValidationParameters.ValidIssuer      = TestJwt.Issuer;
                options.TokenValidationParameters.ValidAudience    = TestJwt.Audience;
                options.TokenValidationParameters.IssuerSigningKey = TestJwt.SigningKey;
            });
    });
}
```

---

## Security audit findings

| Finding | Verdict | Evidence |
|---|---|---|
| Current permissive-when-absent config exploitable today | **No.** `RequireSignedTokens = true` default + `IssuerSigningKey = null` forces `IDX10500` fail-closed on every token. `ValidateIssuerSigningKey = false` only skips key-trust post-check, not signature validation itself. | identitymodel #332, #972, PR #1158; Microsoft Learn `RequireSignedTokens` docs (v8.15.0) |
| Alg=none accepted | **No.** `RequireSignedTokens` is independent of `ValidateIssuerSigningKey`; alg=none rejected before key validation. | Same sources; AzureAD `TokenValidationParameters.cs` |
| Malicious `Authorization: Bearer` can subvert `CookieOrBearer` | **No.** `PolicyEvaluator` iterates all schemes and merges successful identities. Bearer failure does not block cookie success. | aspnetcore `PolicyEvaluator.cs`; Microsoft Learn `limitingidentitybyscheme?view=aspnetcore-10.0` |
| Permissive pattern is good to codify long-term | **No — latent landmine.** Becomes exploitable under future `Authority =`, HMAC-only config, or new bearer endpoint. Inverts RFC 8725 §2.1 / OWASP "secure by default." | RFC 8725 §3.1, §3.9; OWASP JWT Cheat Sheet |
| Package versions | Keep `Microsoft.IdentityModel.*` ≥ 7.1.2 (ideally 8.x); `System.Text.Json` / runtime patched against CVE-2024-30105. | GHSA-59j7-ghrg-fj52, GHSA-rv9j-c866-gp5h, GHSA-hh2w-p6rv-4g7w |

**Bottom line:** the existing code is not currently exploitable, but the specific pattern of gating `Validate*` flags on `!string.IsNullOrEmpty(config)` is the wrong default. Replace with unconditional strict flags; let missing config produce fail-closed behavior automatically via `IDX10500`. This preserves "wired today, consumed later" without inheriting the latent-landmine shape.

---

## References

**Microsoft Learn (.NET 10)**
- Configure JWT bearer authentication — https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0
- Authorize with a specific scheme (updated 2026-02-03) — https://learn.microsoft.com/en-us/aspnet/core/security/authorization/limitingidentitybyscheme?view=aspnetcore-10.0
- Policy schemes — https://learn.microsoft.com/en-us/aspnet/core/security/authentication/policyschemes?view=aspnetcore-10.0
- Integration tests in ASP.NET Core — https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0
- Compile-time options validation source generation (`[OptionsValidator]`) — https://learn.microsoft.com/en-us/dotnet/core/extensions/options-validation-generator
- `OptionsBuilderExtensions.ValidateOnStart` — https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.optionsbuilderextensions.validateonstart?view=net-10.0-pp
- `TokenValidationParameters.RequireSignedTokens` (v8.15.0) — https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters.requiresignedtokens
- Breaking change: security token events return JsonWebToken (.NET 8) — https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/8.0/securitytoken-events

**Andrew Lock (andrewlock.net)**
- A look behind the JWT bearer authentication middleware in ASP.NET Core — https://andrewlock.net/a-look-behind-the-jwt-bearer-authentication-middleware-in-asp-net-core/
- Adding validation to strongly typed configuration objects in .NET 6 — https://andrewlock.net/adding-validation-to-strongly-typed-configuration-objects-in-dotnet-6/
- Introducing the Identity API endpoints (.NET 8 preview) — https://andrewlock.net/exploring-the-dotnet-8-preview-introducing-the-identity-api-endpoints/
- Should you use the .NET 8 Identity API endpoints? (Nov 2023) — https://andrewlock.net/should-you-use-the-dotnet-8-identity-api-endpoints/
- Supporting integration tests with WebApplicationFactory in .NET 6 — https://andrewlock.net/exploring-dotnet-6-part-6-supporting-integration-tests-with-webapplicationfactory-in-dotnet-6/

**Other community sources (2024–2026)**
- Tore Nestenius, BearerToken: the new Authentication handler in .NET 8 — https://nestenius.se/net/bearertoken-the-new-authentication-handler-in-net-8/
- Renato Golia, Testing ASP.NET Core endpoints with fake JWT tokens and WebApplicationFactory (2025-08) — https://renatogolia.com/2025/08/01/testing-aspnet-core-endpoints-with-fake-jwt-tokens-and-webapplicationfactory/
- Stefán Jökull Sigurðarson, Mocking JWT tokens in ASP.NET Core integration tests — https://stebet.net/mocking-jwt-tokens-in-asp-net-core-integration-tests/
- Craig Wardman, Mocking JWT Tokens when Integration Testing ASP.NET WebAPI (2024) — https://www.craigwardman.com/blog/mocking-jwt-tokens-when-integration-testing-asp-net-webapi
- Anton Martyniuk, ASP.NET Core Integration Testing Best Practices (2024) — https://antondevtips.com/blog/asp-net-core-integration-testing-best-practises
- Milan Jovanović, Options Pattern Validation in ASP.NET Core with FluentValidation — https://www.milanjovanovic.tech/blog/options-pattern-validation-in-aspnetcore-with-fluentvalidation
- Kevin Smith, Failing fast with invalid configuration in .NET (2023-02) — https://kevsoft.net/2023/02/24/failing-fast-with-invalid-configuration-in-dotnet.html
- Auth0, .NET 10: What's New for Authentication and Authorization (2025) — https://auth0.com/blog/authentication-authorization-enhancements-dotnet-10/
- WorkOS, RS256 vs HS256: deep dive into JWT signing algorithms (2024) — https://workos.com/blog/rs256-vs-hs256-jwt-signing-algorithms
- Kévin Chalet, Can you use ASP.NET Core Identity API endpoints with OpenIddict? (2023-10) — https://kevinchalet.com/2023/10/04/can-you-use-the-asp-net-core-identity-api-endpoints-with-openiddict/
- OpenIddict docs, Encryption and signing credentials — https://documentation.openiddict.com/configuration/encryption-and-signing-credentials.html
- Curity, JWT Security Best Practices — https://curity.io/resources/learn/jwt-best-practices/
- Zalando, Automated JSON Web Key rotation (2025-01) — https://engineering.zalando.com/posts/2025/01/automated-json-web-key-rotation.html
- Joao Grassi, Integration tests for permission-protected API endpoints — https://blog.joaograssi.com/posts/2021/asp-net-core-testing-permission-protected-api-endpoints/

**GitHub**
- dotnet/aspnetcore #49469 — Move aspnetcore to JsonWebToken/JsonWebTokenHandler — https://github.com/dotnet/aspnetcore/issues/49469
- dotnet/aspnetcore #52075 — .NET 8 stricter JwtBearer signature validation — https://github.com/dotnet/aspnetcore/issues/52075
- dotnet/aspnetcore #45608 — Cannot override authentication in Mvc.Testing (ordering trap) — https://github.com/dotnet/aspnetcore/issues/45608
- dotnet/aspnetcore #56453 — ValidateOnStart doesn't run without hosted-service trigger (2024-05) — https://github.com/dotnet/aspnetcore/issues/56453
- AzureAD/identitymodel — TokenValidationParameters.cs — https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/dev/src/Microsoft.IdentityModel.Tokens/TokenValidationParameters.cs
- identitymodel #332, #972, PR #1158 — RequireSignedTokens semantics and null-key fix — https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/issues/332 ; .../issues/972 ; .../pull/1158

**Security advisories**
- GHSA-59j7-ghrg-fj52 / CVE-2024-21319 (2024-01-09) — JWT DoS — https://github.com/dotnet/aspnetcore/security/advisories/GHSA-59j7-ghrg-fj52
- GHSA-rv9j-c866-gp5h / CVE-2024-21643 — SignedHttpRequest `jku` SSRF/RCE — https://github.com/advisories/GHSA-rv9j-c866-gp5h
- GHSA-hh2w-p6rv-4g7w / CVE-2024-30105 (2024-07-09) — System.Text.Json DoS — https://github.com/advisories/GHSA-hh2w-p6rv-4g7w

**Specs**
- RFC 8725 — JSON Web Token Best Current Practices — https://datatracker.ietf.org/doc/html/rfc8725
- draft-ietf-oauth-rfc8725bis-04 (2026-03) — https://datatracker.ietf.org/doc/draft-ietf-oauth-rfc8725bis/
- OWASP JWT Cheat Sheet — https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html

**NuGet**
- `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.6 — https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer
- `System.IdentityModel.Tokens.Jwt` 8.17.0 — https://www.nuget.org/packages/system.identitymodel.tokens.jwt/

**Caveats**
- No single Microsoft 2026 policy document explicitly addresses the "registered but unexercised scheme" case; the recommendation above integrates the handler lifecycle (Andrew Lock), the `PolicyEvaluator` semantics (aspnetcore source), and current library defaults (`RequireSignedTokens = true`).
- `JsonWebTokenHandler` is undergoing a result-based validation API overhaul (marked `Experimental` in 8.x); this memo describes behavior of the mainline `ValidateTokenAsync` path used by `JwtBearerHandler` in .NET 8/9/10.
- The HS256-is-fine-for-first-party position is contested by vendor blogs (Auth0, SuperTokens, Supabase) that prefer RS256 as a universal default; their reasoning is sound for multi-tenant/multi-party use cases but over-applies to single-deployment scenarios. WorkOS's and OpenIddict's context-dependent positions align with this memo.