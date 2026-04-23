# Research Prompt: Batch 19b — R-057

# Idiomatic "registered but dormant" `AddJwtBearer` pattern — Slice 0 holding posture before the iOS shim lands (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

**Research Topic:** For an ASP.NET Core 10 app that registers the JWT bearer scheme as an opt-in, non-default handler now (so the future iOS shim lands as a purely additive change), but has **no endpoint that accepts bearer tokens in the current slice and no production signing key configured yet** — what is the idiomatic 2026 pattern for the `AddJwtBearer` registration, `TokenValidationParameters` composition, and test-time posture? Compare against my current "bound `JwtAuthOptions` + permissive `Validate*` flags that short-circuit when config values are null" approach.

## Context

RunCoach's Slice 0 (T02.2, just shipped — commit `1b8eac8`) wires:

```csharp
var authBuilder = builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme);
authBuilder.AddIdentityCookies(...);           // browser SPA session
authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { }); // opt-in, iOS shim

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtAuthOptions>>((bearer, jwt) =>
    {
        var o = jwt.Value;
        var hasSigningKey = !string.IsNullOrEmpty(o.SigningKey);
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrEmpty(o.Issuer),
            ValidIssuer = o.Issuer,
            ValidateAudience = !string.IsNullOrEmpty(o.Audience),
            ValidAudience = o.Audience,
            ValidateIssuerSigningKey = hasSigningKey,
            IssuerSigningKey = hasSigningKey
                ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey!))
                : null,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

// CookieOrBearer authorization policy accepts either scheme + RequireAuthenticatedUser.
// No Slice 0 endpoint references the JWT scheme directly.
```

`Auth:Jwt` config is unset in `appsettings*.json` and in CI (tests don't provide it). Production config will arrive when the iOS shim lands (DEC-033, post-MVP-0). Per R-044 / DEC-044 the SPA uses the cookie, not a bearer — this registration exists purely so the iOS shim is an additive change.

My permissive-flag approach was a judgment call: "no endpoint exercises the scheme, so permissive validation can't do harm and the handler registration satisfies the CookieOrBearer policy." I'd like a sanity-check from current .NET idiom.

## Research Question

**Primary:** What is the 2026 idiomatic pattern for registering the JWT bearer scheme as "wired today, consumed later" in an ASP.NET Core 10 app — permissive conditional `TokenValidationParameters`, fail-fast `OptionsValidator`, or simply don't register it until config + endpoints both exist?

**Sub-questions:**

1. **Conditional registration.** Is the common pattern "register only when `Auth:Jwt` section is non-empty" (i.e., gate the `AddJwtBearer` call itself)? Or always-register with deferred validation?
2. **Config validation.** For production posture, should `JwtAuthOptions` use `ValidateDataAnnotations()` / `ValidateOnStart()` to fail startup when signing key is missing in production but tolerate absence in dev/test?
3. **`TokenValidationParameters` absent-config posture.** Is my "`ValidateIssuerSigningKey = hasSigningKey` with `IssuerSigningKey = null` when absent" approach safe, or is there a known-bad interaction (e.g., handler accepting unsigned tokens, JWT spec violation, silent security hole)?
4. **Integration-test contract.** Tests that hit endpoints under the `CookieOrBearer` policy via cookie auth — should the JWT scheme register at all in the test host, and if so with what config? Does the current permissive-validation posture leak into test-time behavior?
5. **Key material convention.** `SymmetricSecurityKey` for HS256 vs `RsaSecurityKey` for RS256 vs asymmetric keys sourced from JWKS URL — what's the 2026 default for first-party token issuance in a .NET 10 app that will issue tokens from an internal endpoint (iOS shim scenario)?

## Why It Matters

T02.5 writes the integration-test matrix for every auth endpoint. If the JWT registration posture leaks wrong-behavior into those tests, we encode an anti-pattern across the whole auth module. Getting this right once beats retrofitting every test when the iOS shim ships and real token issuance surfaces the bug.

## Deliverables

- **Primary recommendation** for the JWT registration posture with reasoning.
- **Concrete `AddJwtBearer` snippet** matching the recommendation (or "remove the registration entirely until iOS lands" if that's the call).
- **Options-validation snippet** for production fail-fast vs dev/test tolerance.
- **Test-host guidance** — what the `WebApplicationFactory` fixture should do about `Auth:Jwt`.
- **Security audit** — is my current permissive-validation posture risky under any scenario I haven't considered?

---

## Current Repo State

- `backend/src/RunCoach.Api/Program.cs` — T02.2 registration block (commit `1b8eac8`).
- `backend/src/RunCoach.Api/Infrastructure/JwtAuthOptions.cs` — current options shape.
- `docs/specs/12-spec-slice-0-foundation/12-spec-slice-0-foundation.md` — spec §Unit 2 lines 78-79 for the CookieOrBearer contract.
- `docs/research/artifacts/batch-15a-*` / equivalent — R-044 cookie-vs-JWT framing.
