# Don't put JWTs in the browser at all

## Executive summary and primary recommendation

**For RunCoach Slice 0, reject the cycle plan's localStorage + bearer default and reject every pattern that hands a JWT to React. Instead, use the ASP.NET Core Identity application cookie as the browser session, with `AddJwtBearer` registered as a non-default opt-in scheme reserved for the future iOS shim.** The cookie is `HttpOnly; Secure; SameSite=Lax` with the `__Host-` prefix; CSRF is handled by ASP.NET Core's built-in antiforgery middleware exposing an `XSRF-TOKEN` cookie that the SPA echoes in an `X-XSRF-TOKEN` header on state-changing requests; refresh-survival is handled by Identity's sliding expiration — **no refresh token is issued to the browser in MVP-0**.

The framing that makes this decision obvious is usually missed. The IETF `draft-ietf-oauth-browser-based-apps-26` (Parecki, De Ryck, Waite; December 2025) **explicitly carves a same-origin SPA+API out of OAuth scope entirely** — Section 1 directs such apps to "rely on OpenID Connect for federated user authentication, after which the application maintains the user's authentication state" via a traditional session. Microsoft's own .NET 10 Learn article "Use Identity to secure a Web API backend for SPAs" says verbatim: *"We recommend using cookies for browser-based applications, because, by default, the browser automatically handles them without exposing them to JavaScript."* OWASP ASVS v5 (May 2025) and the current Session Management, HTML5, and CSRF Cheat Sheets all line up behind httpOnly cookies over any JavaScript-accessible storage. The question "where do I store the JWT" has a 2026 answer that isn't listed in the five candidate options as written: **you don't issue a JWT to the browser**.

This recommendation satisfies every constraint in the brief. It's the lowest XSS exposure short of a full separate BFF project. It's a single-cookie migration target, so it's maximally "good enough now, no rewrite later." It future-proofs the iOS shim through ASP.NET Core's multi-scheme authorization (`[Authorize(AuthenticationSchemes = "Cookies,Bearer")]`) so the same controllers serve both clients without duplication. And it sidesteps every foot-gun in the brief (refresh tokens in localStorage, mutex storms on parallel 401s, cross-tab token-sharing over BroadcastChannel) because the browser never handles a token in the first place.

## Storage-decision matrix

The five candidate patterns, evaluated against the brief's axes. "Recommended" is the April 2026 verdict for a same-origin React 19 + .NET 10 stack with an eventual iOS client.

| Pattern | XSS exposure | CSRF exposure | Refresh-survival | Multi-tab | iOS-future | Migration cost from here | 2026 verdict |
|---|---|---|---|---|---|---|---|
| **(a)** localStorage + bearer | **Catastrophic** — single XSS exfiltrates tokens for offline abuse | Low (no auto-attach) | Perfect (token in storage) | Free (`storage` event) | Native fit | N/A (start point) | **Rejected** by OWASP ASVS v5, BCP draft-26, De Ryck, Duende, Curity |
| **(b-session)** Identity cookie (recommended) | **Low** — token not readable from JS; session abuse bounded to tab lifetime | Real; mitigated by built-in antiforgery + SameSite=Lax + `__Host-` | Perfect (sliding expiration on cookie) | Free (shared cookie jar) | Works via **second** `Bearer` scheme on same API | — | **Recommended** by Microsoft .NET 10 docs, OWASP, BCP scope carve-out |
| **(b-jwt-cookie)** JWT as httpOnly cookie | Low, same as above | Same as above | Same | Same | Same dual-scheme trick | Trivial (just change what's inside the cookie) | Acceptable but adds no value over (b-session) for same-origin |
| **(c)** In-memory access + httpOnly refresh cookie | Medium — access token stealable from memory during a session; XSS can also trigger silent refresh | Real on `/refresh` endpoint; needs Strict cookie + CORS lockdown + custom header | Tab close = refresh round-trip on reopen (~200–500 ms flash of unauth) | Requires `BroadcastChannel` + mutex to prevent refresh storms | Dual-scheme works | ~6 files / 1–1.5 days to get here | Listed **least secure** of three architectures in BCP §6.3; pointless for same-origin |
| **(d)** Full BFF (YARP proxy, opaque session) | Lowest — tokens never leave backend | Same mitigations as (b) | Perfect | Free | iOS bypasses BFF, talks to bearer-protected API directly | ~15–20 files / 4–8 days | Endorsed by BCP §6.1 for OAuth SPAs; overkill for same-origin when Identity cookie already *is* a BFF session |
| **(e)** DPoP / sender-constrained tokens | Better than (a)/(c), worse than (b)/(d); does not block XSS-driven new Auth Code flow with attacker-owned key | — | — | — | iOS can DPoP natively | Unnecessary complexity for this stack | Additive hardening, not a substitute for (b) or (d) |

The honest read of the matrix: patterns (b-session) and (d) tie on security; (b-session) wins on operational simplicity because ASP.NET Core Identity already ships it. (c) is the option most tutorials show and is strictly worse than (b-session) for your same-origin case. (a) is the cycle plan's default and is the one you must not ship past MVP-0.

## Detailed analysis per pattern

### (a) localStorage + `Authorization: Bearer`

OWASP's HTML5 Security Cheat Sheet has stated — and still states — *"Do not store session identifiers in local storage as the data is always accessible by JavaScript. Cookies can mitigate this risk using the httpOnly flag."* ASVS v5 V8.2.2 (May 2025) fails any verification that puts sensitive secrets in Web Storage. The OAuth 2.0 Security BCP (RFC 9700, January 2025) mandates that refresh tokens for public browser clients MUST be sender-constrained or rotated, and the browser-based-apps draft explicitly lists single-execution and persistent token theft as the two primary threats that localStorage enables (§5.1.1, §5.1.2). Practitioner consensus from Philippe De Ryck, Duende, Curity, and even Auth0's own later posts has moved firmly against this pattern since 2023.

The one defensible pro-localStorage argument, made by Randall Degges and a handful of HN commenters, is that **if** an attacker has XSS then they can already abuse your session by issuing `fetch` with httpOnly cookies attached — so httpOnly only protects against *persistent, offline* token theft, not session abuse. This is technically correct and De Ryck concedes it. The response is that persistent offline theft is exactly the scenario where damage scales — an attacker harvests tokens from thousands of victims, keeps using them for weeks, pivots laterally. Bounding damage to tab-lifetime session abuse is a real mitigation, not a theatrical one.

For RunCoach specifically, pattern (a) also breaks your iOS future unhelpfully: it forces the browser to use the same bearer-token shape iOS will use, meaning you'll end up with the worst of both worlds — a long-lived JWT on a JS-accessible surface AND needing to build a refresh endpoint anyway.

### (b) HttpOnly cookie — Identity session (the recommendation)

For a **same-origin** React + .NET 10 setup, this collapses to the normal ASP.NET Core Identity cookie flow. You register `AddIdentityCookies()`, the cookie is set `HttpOnly; Secure; SameSite=Lax` with a `__Host-` prefix; the SPA's `fetch` calls send the cookie automatically because it's first-party; and `[Authorize]` on any endpoint just works. There is no token in JavaScript, no refresh endpoint, no mutex, no `BroadcastChannel`. The BCP's own language carves this scenario out of OAuth entirely — "Such scenarios can rely on OpenID Connect for federated user authentication, after which the application maintains the user's authentication state. Such a scenario … is not within scope of this specification."

The critical .NET 10 change that makes this finally painless for SPAs is the **cookie-auth-handler 401-for-API-endpoints breaking change** (documented at `/dotnet/core/compatibility/aspnet-core/10/cookie-authentication-api-endpoints`). Previously, unauthenticated requests to cookie-protected endpoints returned a 302 redirect to `/Account/Login`, which is terrible for SPAs — fetch followed the redirect, tried to parse an HTML login page as JSON, and threw. Workarounds (setting `X-Requested-With`, overriding `OnRedirectToLogin`) were tutorial-tier boilerplate. In .NET 10, the cookie handler automatically returns **401/403** for endpoints that implement `IDisableCookieRedirectMetadata` — which applies to Minimal API JSON endpoints, SignalR, and `[ApiController]` controllers. Your RTK Query `baseQueryWithReauth` gets clean 401s out of the box.

### (b') JWT inside an httpOnly cookie

Functionally identical to (b) for the browser side — `HttpOnly` + `Secure` + `SameSite=Lax` means the JWT is not JavaScript-readable and is auto-attached to same-origin requests. This is what some teams do when they already have a JWT pipeline (e.g., for M2M) and want cookie delivery for the browser. For RunCoach, it's a pointless indirection: Identity already issues a session cookie; cramming a JWT inside adds parse overhead, doubles the signing-key surface, and gains nothing.

### (c) In-memory access + httpOnly refresh cookie ("BFF-lite")

This is the pattern most 2021–2023 tutorials show, and the brief lists it as a serious candidate. In 2026 it is the **weakest** of the three BCP architectures (ranked §6.3, explicitly last) when OAuth is actually needed, and it is actively inferior to (b) for a same-origin setup. Three concrete problems:

First, **XSS can issue a silent refresh with the attacker's own DPoP key** (BCP §5.1.3, documented by De Ryck at Identiverse 2024 and cited in Duende's April 2024 blog on refresh-token reuse). Rotation alone doesn't stop this — the attacker just waits until the legitimate session goes idle, then uses their harvested key. Duende's conclusion: *"There is no way to securely store and handle access tokens in the browser."*

Second, **multi-tab requires real engineering**. Each tab has its own memory; when a new tab opens it must call `/refresh` to get an access token. Without a mutex (`async-mutex` or `navigator.locks`), two tabs opening within ~50 ms both refresh, the second request invalidates the first's rotated token, and the first tab is silently logged out. With a `BroadcastChannel` token-sharing message you can avoid this, but now you're maintaining a distributed-systems-lite protocol in your auth layer.

Third, **every F5 shows a 200–500 ms "flash of unauth."** The access token is in memory only, so page refresh triggers a `/refresh` round-trip before any protected data loads. Users notice, and it's a UX loss for zero security gain compared to (b).

The only scenario where (c) is correct is when your API is cross-origin with no shared-domain option — which is not your situation.

### (d) Full BFF / Token Handler Pattern

The BFF pattern (Philippe De Ryck, Curity Token Handler, Duende.BFF) is what the IETF draft-26 ranks most secure (§6.1). A confidential-client backend holds OAuth tokens server-side and exchanges them for an opaque session cookie; the SPA is a dumb renderer. Curity splits this into an "OAuth Agent" that handles the dance and an "OAuth Proxy" that swaps the session cookie for an access token inbound to the API.

**Here's the realization that matters for RunCoach:** your stack already is a BFF. ASP.NET Core Identity is acting as the session layer; the .NET API is acting as the resource layer; both are in the same process. If you ever add external OIDC (Google, Microsoft login), ASP.NET Core's OpenID Connect middleware does the code-exchange server-to-server and drops a session cookie — tokens never touch React. There's no separate project to spin up, no YARP proxy to configure, no Redis session store. Pattern (b) **is** pattern (d) once you notice ASP.NET Core already fills every BFF role in a single host.

A *separate* BFF project becomes justified only when your SPA is on a CDN edge while your API lives in a different administrative zone — which contradicts the brief's same-origin Docker Compose setup.

### (e) DPoP, WebAuthn/passkeys, other 2026-era options

**DPoP (RFC 9449, September 2023)** sender-constrains access tokens via a non-exportable WebCrypto key. It is genuinely useful hardening for pattern (c) and is recommended for FAPI 2.0 profiles. The BCP draft §5.1.3 and §9.2 position it as *additive* to BFF, not a substitute — because XSS can still initiate a fresh authorization code flow with the attacker's own DPoP key. For your stack, DPoP is overkill in MVP-0 and unnecessary if you ship (b).

**Passkeys in ASP.NET Core Identity** shipped in .NET 10 (`learn.microsoft.com/.../passkeys/?view=aspnetcore-10.0`). It's a first-party implementation scoped to Identity, not a general FIDO2 library. Only the Blazor template wires it up by default; for a React SPA you'd manually expose `/PasskeyCreationOptions` and `/PasskeyRequestOptions` and call `SignInManager.SignInWithPasskeyAsync`. Passkeys solve **login** (phishing-resistant credential) but don't change the **session-storage** question — the cookie comes out the same either way. Defer for MVP-0; revisit for public beta as a differentiator.

## Refresh-token strategy decision for MVP-0

**No refresh token is issued to the browser in MVP-0.** The sliding-expiration Identity cookie is the session; that's all you need.

Reasoning: a refresh token exists to let a short-lived access token be renewed without asking the user to re-authenticate. In pattern (b) there is no access token in the browser — there's just an Identity cookie whose absolute + sliding lifetime you control. Set `ExpireTimeSpan = TimeSpan.FromHours(8)` with `SlidingExpiration = true`, or raise to 7–14 days for personal-use convenience; the cookie renews on activity, users stay logged in across browser restarts, and there is no separate refresh endpoint to secure.

**If the cycle plan's "long-lived ~30d JWT, no refresh" baseline is kept anyway**, the least-bad storage for such a JWT is an httpOnly cookie (pattern b'), not localStorage. A 30-day JWT in localStorage is the worst combination in the matrix — long abuse window AND exfiltratable.

**The iOS shim is where refresh tokens become necessary**, because a native app can't rely on cookie-jar lifetime in the same way and wants shorter access-token TTLs for Keychain hygiene. When the iOS scheme lands (DEC-033), add a `POST /api/auth/refresh` endpoint that reads a bearer refresh token from the request body (iOS doesn't need a cookie) and rotates it, with per-rotation hash storage in SQL and reuse-detection-revokes-family semantics per RFC 9700 §4.14. The browser never uses that endpoint.

**Migration combinations and their later cost:** From (b) with no refresh token, adding iOS bearer + refresh later is purely additive on the server (new endpoints, new scheme on existing authorization policies) and touches **zero** lines of browser code. From (a) localStorage with a 30-day JWT, same iOS addition costs about a day of client work to remove localStorage and another day to re-test every protected route. The "don't do refresh now" decision is structurally free to reverse later; the "use localStorage now" decision is structurally expensive.

## CSRF posture for the recommended pattern

SameSite=Lax is a browser default across Chrome, Firefox, and Edge since 2020–2021. For a same-origin SPA+API the residual CSRF attack surface is small but not zero. Remaining vectors: top-level `GET` navigation to any state-changing endpoint (so **never use GET for state changes** — this is the second principle of the OWASP CSRF Cheat Sheet), sibling subdomain attacks if any subdomain is reachable to an attacker (mitigated by the **`__Host-` cookie prefix**, which forces `Secure` + host-only + `Path=/` and makes subdomain poisoning impossible), method-override attacks via `X-HTTP-Method-Override` or `?_method=` (disable these), pre-auth CSRF against the login form itself (still needs a token), and the "Lax+POST" 2-minute window that Chrome has been retiring.

The 2024–2025 rewrite of the OWASP CSRF Cheat Sheet is explicit about 2026 direction: *"First, check if your framework has built-in CSRF protection and use it."* For stateful apps (anything using Identity) the **synchronizer token pattern is primary**; the naive double-submit cookie pattern is now labeled **DISCOURAGED**; only the **signed, session-bound HMAC double-submit variant** is recommended for stateless contexts. The cheat sheet names .NET's built-in antiforgery by name as a preferred option.

For RunCoach this means: use `AddAntiforgery(o => o.HeaderName = "X-XSRF-TOKEN")`, expose a `GET /api/auth/xsrf` endpoint that calls `IAntiforgery.GetAndStoreTokens()` and sets a non-HttpOnly `XSRF-TOKEN` cookie, and have React read that cookie and echo it as `X-XSRF-TOKEN` on state-changing requests. RTK Query's `fetchBaseQuery` supports this cleanly through `prepareHeaders` — pull the cookie value via `document.cookie.match(/XSRF-TOKEN=([^;]+)/)?.[1]` and set the header. `credentials: 'include'` ensures the Identity cookie rides along.

**Gotcha the .NET 10 docs bury**: `[ValidateAntiForgeryToken]` does **not** work on Minimal API endpoints, and does **not** integrate with the new `UseAntiforgery()` middleware on `[ApiController]` controllers either. Use `[RequireAntiforgeryToken]` (public but sparsely documented) or inject `IAntiforgery` and call `ValidateRequestAsync` manually. The middleware auto-enforces for any endpoint binding `IFormFile`, but JSON `POST`/`PUT`/`DELETE` must opt in explicitly.

`SameSite=Strict` on the session cookie buys near-zero extra protection in this setup (you already have antiforgery tokens) and costs real UX — users clicking a password-reset link from email land unauthenticated. **Stick with Lax.**

## RTK Query implementation details

As of April 2026 the pins are **Redux Toolkit 2.11.2** (Immer 11 under the hood, ~30% scripting-time reduction on RTK Query's 1000-component benchmark), **React 19.2.5**, **React Router v7.14.0**, **async-mutex 0.5.0**, and native `BroadcastChannel` (Safari 15.4+ makes the `broadcast-channel` npm polyfill unnecessary). RTK 2.x's new primitives (`combineSlices`, `createDynamicMiddleware`, the creator-callback `reducers` syntax, auto-`autoBatchEnhancer`) don't change auth patterns — they're orthogonal.

**For the recommended pattern (b), `prepareHeaders` only attaches the CSRF header.** The auth cookie attaches automatically given `credentials: 'include'`. The full `fetchBaseQuery` shape:

```ts
// api/baseQuery.ts
import { fetchBaseQuery } from '@reduxjs/toolkit/query'

const readCookie = (name: string) =>
  document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`))?.[1]

export const baseQuery = fetchBaseQuery({
  baseUrl: '/api',
  credentials: 'include',                       // send Identity cookie
  prepareHeaders: (headers, { type }) => {
    if (type === 'mutation') {                   // only state-changing verbs
      const xsrf = readCookie('XSRF-TOKEN')
      if (xsrf) headers.set('X-XSRF-TOKEN', decodeURIComponent(xsrf))
    }
    return headers
  },
})
```

**No `baseQueryWithReauth` is needed in pattern (b).** Identity's sliding expiration renews the cookie on every request the server honors, so 401 means "genuinely logged out" and the right response is "dispatch `loggedOut`, send to login page" — not "call `/refresh` and retry." This is the primary operational win over pattern (c).

**If the iOS scheme is eventually added and the web also gains a bearer-refresh flow** (which the brief's DEC-033 doesn't actually require), the canonical 2026 `baseQueryWithReauth` is unchanged from what the Redux Toolkit "Customizing Queries" docs show: wrap in `async-mutex`, 401 → `/refresh` → retry. Known community gotchas per the GitHub issue tracker: RTK Query issue #3717 (retry happens even when `/refresh` returned 4xx — guard explicitly), discussion #2097 (circular-import deadlock when the auth slice imports the API — use string action types), and discussion #4180 (`prepareHeaders` re-applies the access token on the refresh call — branch on `endpoint` name). For pattern (b) none of these bite you.

**React 19 considerations:** the new `useActionState`, `useOptimistic`, and form-action props don't change auth; Strict Mode double-invocation is handled by RTK Query's cache subscription refcounting. React Router v7's Data-Mode protected routes are the clean pattern — a `ProtectedRoute` wrapper that renders `<Navigate to="/login" />` when the auth slice says not authenticated. One flagged-but-unverified item from the research: a third-party blog claims a CVE-2025-55182 ("React2Shell") affecting React 19.0.0–19.2.2 via Server Components, patched in 19.2.3. It did not surface on React's official blog or NVD in this research; since RunCoach is a pure SPA with no RSC, it's moot — but pin to 19.2.5 anyway.

**oidc-client-ts 3.5.0 / react-oidc-context 3.3.1** exist and are the official successors to the abandoned oidc-client-js. They are **not appropriate for RunCoach's current setup** — they target OIDC Authorization Code flows against external IdPs, which ASP.NET Core Identity isn't. Add them only if you later federate login via Google/Microsoft, and even then the server-side OIDC middleware handles the dance — the client libraries only matter for browser-side OAuth, which this stack deliberately avoids.

## ASP.NET Core 10 wiring sketch

This is the minimum `Program.cs` that implements the recommendation. Pin all `Microsoft.AspNetCore.*` and `Microsoft.EntityFrameworkCore.*` to the same .NET 10 servicing release (`10.0.5` at research time). Order matters: `UseCors` → `UseAuthentication` → `UseAuthorization` → `UseAntiforgery`, per the .NET 10 Middleware doc's explicit language *"UseAntiforgery must be placed after calls to UseAuthentication and UseAuthorization."*

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Identity: AddIdentityCore + AddIdentityCookies gives cookies as DEFAULT,
// avoiding AddIdentityApiEndpoints' opinionated bearer-default registration.
builder.Services
    .AddIdentityCore<ApplicationUser>(o => { o.User.RequireUniqueEmail = true;
                                             o.Password.RequiredLength   = 12; })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(o => {
        o.DefaultScheme          = IdentityConstants.ApplicationScheme;
        o.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    })
    .AddIdentityCookies(o => o.ApplicationCookie!.Configure(c => {
        c.Cookie.Name         = "__Host-RunCoach";       // __Host- forces Secure + host-only
        c.Cookie.HttpOnly     = true;
        c.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        c.Cookie.SameSite     = SameSiteMode.Lax;        // Lax: correct for same-origin SPA
        c.ExpireTimeSpan      = TimeSpan.FromDays(14);   // personal-use convenience
        c.SlidingExpiration   = true;
        // .NET 10: cookie handler auto-returns 401 for API endpoints; no OnRedirectToLogin override needed.
    }));

// JWT Bearer is registered but NOT default — opt-in per endpoint for the future iOS client.
builder.Services.AddAuthentication().AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o => {
    o.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = true,   ValidIssuer   = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});

builder.Services.AddAuthorization(o => {
    // The canonical "accepts either scheme" policy — use on every business endpoint from day 1.
    o.AddPolicy("CookieOrBearer", p => {
        p.AuthenticationSchemes = new[] {
            IdentityConstants.ApplicationScheme,
            JwtBearerDefaults.AuthenticationScheme };
        p.RequireAuthenticatedUser();
    });
});

builder.Services.AddAntiforgery(o => {
    o.HeaderName  = "X-XSRF-TOKEN";
    o.Cookie.Name = "__Host-Xsrf";
});

// CORS only matters in dev (Vite on :5173 talking to API on :5001).
// In prod SPA is served from wwwroot same-origin — AllowCredentials vanishes as a concern.
builder.Services.AddCors(o => o.AddPolicy("Spa", p => p
    .WithOrigins("https://localhost:5173")
    .AllowCredentials().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("Spa");
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Seeds the JS-readable XSRF-TOKEN cookie that RTK Query echoes back as X-XSRF-TOKEN.
app.MapGet("/api/auth/xsrf", (HttpContext ctx, IAntiforgery af) => {
    var tokens = af.GetAndStoreTokens(ctx);
    ctx.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!,
        new CookieOptions { HttpOnly = false, Secure = true, SameSite = SameSiteMode.Lax });
    return Results.NoContent();
});

app.MapControllers();
app.MapFallbackToFile("index.html");  // same-origin SPA hosting
app.Run();
```

**Data Protection in Docker Compose** is the single most common RunCoach-specific foot-gun here. The default DP-keys path is the container filesystem, which disappears on rebuild — every rebuild invalidates all cookies and every signed antiforgery token, so every restart logs everyone out and looks like a flaky bug. Mount a named volume to `/root/.aspnet/DataProtection-Keys` (or call `services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo("/keys")).SetApplicationName("RunCoach")` pointing at a mounted volume) from day 1.

## Multi-tab and refresh-survival UX analysis

In pattern (b) every user journey Just Works because the Identity cookie is the single source of truth and the browser cookie jar is shared across tabs. Opening a second tab: the cookie is sent on first request, authenticated. F5 mid-session: same. Close all tabs, reopen in an hour or a week: as long as the cookie's absolute or sliding expiration hasn't elapsed, authenticated. Logout in tab A with tab B open: tab B's cookie is gone after the logout response's `Set-Cookie: Max-Age=0`, so the next API call from tab B gets a 401 and RTK Query's error handler (`if (result.error?.status === 401) dispatch(loggedOut())`) kicks it to the login page. No `BroadcastChannel`, no `async-mutex`, no mutex storms.

You'd **only** need `BroadcastChannel` in this pattern if you want the tab B UI to flip to "logged out" **immediately** on logout (before the next network call), which is a minor UX polish. A 10-line channel that broadcasts `{ kind: 'logout' }` on logout and dispatches `cleared()` on receive covers it — native API, no dependency. Compare with pattern (c) where the channel and the mutex are load-bearing infrastructure.

For the other patterns the journeys are mixed as captured in §2. The only pattern where the "30-day reopen" journey genuinely *feels* great is (a) or (b) with long cookie lifetime; (c) degrades to a login round-trip any time refresh tokens have expired.

## Migration path forward

The project trajectory is personal → friends/testers → public beta → iOS shim. Each transition under the recommended pattern:

**Personal → friends/testers (MVP-0 → MVP-1).** No architectural change. Tighten cookie lifetime from 14 days to maybe 3–7 days, verify HTTPS is enforced (in dev you may have been on `http://localhost` which silently breaks `Secure`), and write a tiny admin endpoint for revoking a user's cookie family by bumping their security stamp. **Scope: ~2 hours.**

**Friends/testers → public beta.** Add rate limiting on `/login`, add account lockout (ASP.NET Core Identity's built-in `UserLockoutEnabledByDefault = true` is one line), enforce email confirmation, optionally add passkeys via the .NET 10 passkey endpoints. None of this touches the storage/CSRF design. **Scope: ~1–2 days of Identity configuration, zero changes to React.**

**Public beta + iOS shim.** The dual-scheme authorization policy `CookieOrBearer` was declared in the Program.cs above on day 1, so every business controller already accepts both. You add: a `POST /api/auth/login-bearer` endpoint that calls `CheckPasswordSignInAsync` and returns `{ accessToken, refreshToken, expiresIn }` in the response body (no cookie); a `POST /api/auth/refresh-bearer` endpoint that accepts the refresh token in the body, validates against a `RefreshTokens` table storing SHA-256 hashes, rotates, detects reuse, revokes families; the iOS app stores these in Keychain. **Scope: ~3 new endpoints + 1 EF migration, ~2 days. Zero changes to the React SPA.**

**If you ever need to convert the SPA to pattern (d) full BFF** (realistic trigger: you want to host React on a CDN edge in a different domain from the API): add YARP as a reverse proxy, move the SPA behind it, everything else is already in place. Curity's writeups note this is "2–4 days for a team familiar with ASP.NET Core," which matches the §4c research estimate. Critically, the isolation of auth logic to `baseQuery.ts` and the Redux `authSlice` means the React side changes amount to deleting code, not rewriting.

The load-bearing claim here: **pattern (b) has migration cost zero to the only destination that actually makes sense (dual-scheme with added bearer for iOS) and cost ~2–4 days to the destination (full BFF) that your architecture probably never needs.** Compare: pattern (a) has cost ~1 day to (c) and ~4–8 days to (d), and neither of those transitions is strictly additive — both break every active session.

## Gotchas and anti-patterns

**Never store a refresh token in localStorage, even "temporarily."** Every RTK Query tutorial from 2019–2022 shows this, and it's the single most common production foot-gun. A refresh token in localStorage is strictly worse than an access token in localStorage — longer lifetime, broader abuse window, and it generally isn't rotated per request. If you find yourself writing `localStorage.setItem('refreshToken', …)` stop and reconsider.

**Don't mix `AddIdentityApiEndpoints` with a custom `AddJwtBearer` scheme both named "Bearer."** `AddIdentityApiEndpoints<TUser>()` registers `IdentityConstants.BearerScheme` (proprietary opaque tokens protected by `IDataProtector`, not JWTs — Andrew Lock covers this in "Should you use the .NET 8 Identity API endpoints?"). If you also call `AddJwtBearer()` without specifying a distinct scheme name, they collide. For RunCoach, either use `AddIdentityCore + AddIdentityCookies` as shown above (cleaner), or if you keep `MapIdentityApi` at all, call it with `?useCookies=true` only and register your own JWT scheme under a distinct name like `"JwtBearer"` for the iOS client.

**`[ValidateAntiForgeryToken]` is broken for `UseAntiforgery()` middleware integration.** It works for classic MVC but does not set `IAntiforgeryMetadata.RequiresValidation = true`, so the new middleware doesn't validate. Use `[RequireAntiforgeryToken]` (public but under-documented) or inject `IAntiforgery` and call `ValidateRequestAsync` by hand. Minimal API endpoints that bind `IFormFile` auto-enforce; JSON body endpoints do not.

**`.AllowCredentials()` + `.AllowAnyOrigin()` is illegal CORS.** In dev, enumerate origins explicitly. In prod, same-origin hosting means CORS is inert — but leave the dev config correct to avoid "works in Docker, fails in `npm run dev`" debugging sessions.

**Naive double-submit cookie is DISCOURAGED by OWASP's current CSRF Cheat Sheet.** If you implement CSRF protection by hand instead of using ASP.NET Core's antiforgery, make sure it's the signed, session-bound HMAC variant — the cheat sheet explicitly calls out that simply signing tokens without session binding "provides minimal protection." Using `AddAntiforgery()` gets you the synchronizer token pattern for free; don't reinvent.

**Don't rely on SameSite=Lax as your primary CSRF defense.** The current OWASP CSRF Cheat Sheet says SameSite "should co-exist with [a CSRF token] in order to protect the user in a more robust way" — it's explicitly defense-in-depth only. Top-level `GET` navigations still send Lax cookies, so any state-changing GET endpoint remains vulnerable. Never use GET for state changes.

**The `BroadcastChannel` npm package is unnecessary in 2026** for pure tab-to-tab messaging. Safari 15.4+ shipped native support in March 2022. The library is useful only if you also need Node/Deno/Worker coordination or LeaderElection. For RunCoach, use `new BroadcastChannel('auth')` directly.

**React 19 Strict Mode double-invocation** is a dev-only concern that RTK Query already handles via cache refcounting. Don't write a bootstrap `useEffect` that fetches user info on mount without an `AbortController` — Strict Mode will fire it twice and the second call's response racing the first can set stale state.

## Conclusion

The 2026 consensus is clearer than the cycle plan's framing implied: for a same-origin SPA + API, **don't issue a JWT to the browser**. The IETF browser-based-apps BCP (December 2025) carves this scenario out of OAuth entirely; Microsoft's .NET 10 docs explicitly recommend cookies for browser SPAs; OWASP ASVS v5 (May 2025) fails any verification that puts sensitive secrets in Web Storage; Philippe De Ryck and Duende make the practitioner case that browsers aren't suitable storage environments for tokens in the first place. Pattern (b) — the Identity application cookie with `__Host-` prefix, `SameSite=Lax`, and ASP.NET Core's built-in antiforgery — satisfies every axis in the brief simultaneously: lowest XSS exposure, adequate CSRF posture, perfect refresh-survival via sliding expiration, free multi-tab behavior, clean iOS future via dual-scheme authorization, and zero migration cost to the only destination that matters.

The non-obvious framing insight is that the "BFF-or-not" debate doesn't apply to you: your ASP.NET Core host already *is* a BFF because it renders the SPA, holds the session, and serves the API from one process. Adopting pattern (b) captures every BFF security property without the operational weight of a separate proxy project.

The MVP-0 decision therefore compresses to: keep the cookie lifetime generous for personal use, wire dual-scheme authorization from day 1 so the iOS shim lands additively, ship `UseAntiforgery()` now so CSRF posture is correct before the first non-developer user arrives, and mount a Docker volume for DataProtection keys before the second container rebuild eats your cookies. Every one of those is a one-liner; none of them is expensive to get right the first time; and none of them is expensive to walk back if a later threat model demands pattern (d). The cycle plan's pragmatic default was wrong in a specific, fixable way — localStorage + bearer is worse for your stack than the recommended alternative on every axis, including simplicity.