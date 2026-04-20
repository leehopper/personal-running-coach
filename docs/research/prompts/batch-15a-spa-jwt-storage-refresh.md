# Research Prompt: Batch 15a — R-044

# SPA JWT Storage and Refresh-Token Strategy for Modern React (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: What is the current (2026) best-practice pattern for storing JWTs and managing token lifecycle in a React 19 + Vite SPA that calls a same-origin .NET 10 API, balancing security (XSS, CSRF), UX (refresh-survival, multi-tab), and the project's "personal-use first, public-beta later" trajectory?

## Context

I'm building Slice 0 (Foundation) of an AI-powered running coach (RunCoach):

- Backend: ASP.NET Core 10 with `Microsoft.AspNetCore.Identity` + JWT bearer auth, hosted at the same origin as the SPA in dev (Docker Compose) and likely in production.
- Frontend: React 19 + TypeScript + Vite, Redux Toolkit + RTK Query for HTTP, React Router v7. No tokens currently stored; this is a clean-slate decision.
- Target users now: solo developer (the builder) using it on personal runs.
- Target users later (MVP-1): friends and testers, then public beta. Public exposure inherits OWASP scrutiny.

The cycle plan's pragmatic default is **localStorage + bearer header**. The cycle plan also pre-flagged JWT rotation / refresh-token strategy as an explicit research trigger ("Slice 0 scope decision"). This prompt resolves both questions together because they're inseparable — storage location dictates refresh-token mechanics.

## Research Question

**Primary:** What is the current (2026) industry-standard pattern for storing access tokens and refresh tokens in a React SPA that shares an origin with its API, given OWASP guidance has shifted away from localStorage over the past 3 years?

**Sub-questions (must be actionable):**

1. **Storage mechanics — recommend one of the following primary patterns and justify:**
   - (a) localStorage + bearer header (the historical default)
   - (b) httpOnly + Secure + SameSite=Lax cookie (server-set; SPA never reads token)
   - (c) In-memory access token + httpOnly refresh-token cookie (BFF-lite — access lives only in JS memory; refresh handled by server cookie)
   - (d) Full BFF (Backend-for-Frontend) where the server holds session state and the SPA holds nothing
   - (e) Other current-best pattern not listed

   For the recommendation, cover: XSS exposure, CSRF exposure (and required mitigations), refresh-survival behavior, multi-tab sync, SSO compatibility, mobile/iOS-shim future-compatibility (RunCoach plans an iOS app per DEC-033 — does the chosen pattern bridge cleanly?).

2. **Refresh-token strategy:**
   - Should we issue refresh tokens at all in MVP-0, given the cycle plan's deferred-items list defers "refresh tokens / token rotation / server-side revocation" to pre-public-release?
   - If we adopt pattern (c) or (d), is a refresh-token endpoint *implicitly required* even for MVP-0, since access tokens in memory die on tab close?
   - If we keep the cycle plan's "long-lived ~30d JWT, no refresh" baseline, what storage pattern is least bad?
   - For each combination of (storage pattern × refresh strategy), what's the migration path when we later add refresh + rotation?

3. **CSRF posture for cookie-based patterns:**
   - With same-site=Lax (default), what attacks remain possible on a same-origin SPA + API?
   - Is the double-submit token pattern still the recommendation in 2026, or have framework helpers (ASP.NET Core's `[ValidateAntiForgeryToken]`, etc.) become the norm for SPAs?
   - Does Fetch with `credentials: 'include'` + RTK Query's `prepareHeaders` cleanly support CSRF token injection?

4. **RTK Query specifics:**
   - For pattern (a) bearer header: `prepareHeaders` is the standard hook — confirm and link current docs.
   - For pattern (b)/(c)/(d) cookie: confirm `fetchBaseQuery` accepts `credentials: 'include'` and works with auto-refresh (`baseQueryWithReauth` pattern).
   - For in-memory + refresh: is the documented pattern still `mutex + 401 retry` in 2026?
   - Does Redux Toolkit have any new auth-related primitives (e.g., the `combineSlices` API or any 2025 release) that change the recommended approach?

5. **ASP.NET Core 10 server-side mechanics for the recommended pattern:**
   - For cookie-based patterns: does `app.UseAuthentication()` with `AddCookie()` interoperate cleanly with `AddJwtBearer()`, or is one preferred?
   - What's the current-best way to issue an httpOnly refresh-token cookie alongside (or instead of) a JWT bearer?
   - Is `IDataProtector` still the recommended primitive for refresh-token signing/encryption, or has anything replaced it in .NET 10?
   - Confirm any 2025/2026 changes to ASP.NET Core Identity that affect this (Identity API endpoints, `MapIdentityApi`, etc.).

6. **Multi-tab and refresh survival UX:**
   - For each pattern, what does "user opens a new tab" do? "User refreshes the page mid-session"? "User closes the browser and reopens an hour later"?
   - Are there libraries (e.g., `broadcast-channel`, `react-oidc-context`, `oidc-client-ts`) that solve this generically, and are they appropriate for a non-OIDC custom-Identity setup?

7. **Migration-friendliness for the project's trajectory:**
   - The cycle plan says "personal-use now, friends/testers in MVP-1, public beta later." Whichever pattern we pick now, what's the cost of migrating later? (e.g., if we pick localStorage now and later need httpOnly cookies for public beta, what changes?)
   - Is there a "good enough for now, no rewrite later" pattern, or are storage decisions effectively permanent?

## Why It Matters

- **Security floor:** localStorage is OWASP-flagged for token storage in modern guidance. If we ship the default and later go public, we either rewrite or carry XSS exposure into beta.
- **Product trajectory:** RunCoach has a planned iOS shim (DEC-033). The chosen web-token pattern dictates whether the iOS app shares an auth flow or runs a parallel one. A wrong choice now creates dual auth pipelines later.
- **Refresh-token timing:** "Add later" sounds cheap, but if the storage decision forces refresh-tokens early (e.g., in-memory access dies on tab close → refresh required), then the cycle plan's "defer refresh tokens" assumption may be wrong.
- **Slice 0 PR exposure:** Whatever lands in Slice 0 is the auth foundation every later slice (onboarding, plan generation, workout logging, conversation) builds on. Rework here is more expensive than rework anywhere else in MVP-0.

## Deliverables

- **A concrete recommendation** with one primary pattern and an explicit rationale for picking it over the alternatives.
- **A storage-decision matrix** (pattern × XSS exposure × CSRF posture × refresh-survival × multi-tab × iOS-future × migration cost) so the trade-offs are visible.
- **An explicit answer on refresh tokens for MVP-0** — yes/no, with reasoning. If yes, document the rotation strategy; if no, document what the pattern degrades to (e.g., user re-logs in every 30 days).
- **Library/version pins** for any client-side helpers (axios interceptors, oidc-client-ts, broadcast-channel, etc.) and confirmation that RTK Query (current version) supports the recommended pattern out of the box.
- **ASP.NET Core 10 wiring sketch** — minimal `Program.cs` excerpt showing the auth-middleware order and any DataProtection/cookie config required for the recommended pattern.
- **Migration path** — if we adopt the recommended pattern now and need to evolve it for MVP-1/beta, what changes? File-by-file scope estimate where possible.
- **Gotchas and security implications** — anti-patterns to avoid, common foot-guns (e.g., storing the refresh token in localStorage "temporarily"), and any version compatibility notes for React 19 / RTK 2.x / .NET 10.
- **Citations** — current OWASP cheat sheets (ASVS v5 if applicable), Microsoft docs (`MapIdentityApi` and current ASP.NET Core auth recommendations), Anthropic-independent sources (this is a security decision, not an LLM decision).

## Out of Scope

- Anything that requires a third-party identity provider (Auth0, Cognito, Clerk, Keycloak). The product uses ASP.NET Core Identity directly per the cycle plan; switching IdPs is not on the table.
- Mobile-specific token storage (iOS Keychain, Android Keystore). The RunCoach iOS shim is post-MVP-0 and will be researched separately.
- Multi-factor authentication, account lockout, password reset — explicitly out of Slice 0 scope.
- Any pattern that requires server-side session storage at scale (Redis-backed sessions, etc.) — the project is single-instance for MVP-0; if a recommended pattern requires distributed session state, flag it but treat as a tie-breaker against rather than for.
