# Research Prompt: Batch 15b — R-045

# `MapIdentityApi<TUser>()` vs Custom AuthController for ASP.NET Core 10 + SPA

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a new ASP.NET Core 10 API serving a React 19 SPA, should the authentication endpoints (register, login, logout, /me, refresh) be implemented via `app.MapIdentityApi<TUser>()` (Microsoft's built-in Identity API endpoints introduced in .NET 8) or via a hand-rolled `AuthController`?

## Context

I'm building Slice 0 (Foundation) of an AI-powered running coach (RunCoach):

- Backend: ASP.NET Core 10 with `Microsoft.AspNetCore.Identity`, JWT bearer auth, EF Core on Postgres.
- Frontend: React 19 + Vite SPA. Same-origin in dev (Docker Compose) and likely in production.
- Multi-tenant from day one (per-user isolation across every business entity).
- Slice 0 ships register/login/logout/me/error-envelope; later slices (1–4) add onboarding, plan generation, workout logging, conversation.
- Trajectory: solo dev now → friends/testers (MVP-1) → public beta later.

The Slice 0 spec currently assumes a custom `AuthController` under `Modules/Identity/AuthController.cs` with hand-rolled register/login/logout/me endpoints. Microsoft's recommended path since .NET 8 has been `app.MapIdentityApi<TUser>()`, which auto-wires a complete set of Identity API endpoints. I need to decide which path Slice 0 takes — the choice shapes every later slice's auth surface.

This prompt is paired with R-044 (SPA JWT storage). The two decisions interact: `MapIdentityApi` ships with both bearer and cookie modes, so the storage choice partly dictates the auth-endpoint choice.

## Research Question

**Primary:** For an ASP.NET Core 10 API + React SPA built today, which approach to auth endpoints is current best practice — `MapIdentityApi<TUser>()` or a custom controller — and what are the durable trade-offs?

**Sub-questions (must be actionable):**

1. **What does `MapIdentityApi` actually expose in .NET 8 / 9 / 10?** Enumerate the endpoints, their request/response shapes, the default configuration knobs, and any meaningful changes between releases. Include the endpoint paths Microsoft chose and whether they can be customized (versioned `/api/v1/` prefix, renamed routes).

2. **Customization ceiling.** What can `MapIdentityApi` not do that a custom controller can? Specifically:
   - Can the password policy be tightened (12-char + upper/lower/digit, etc.)?
   - Can the response shape be swapped to RFC 7807 ProblemDetails / ValidationProblemDetails (the project's chosen error envelope)?
   - Can register flow be intercepted to populate a related entity (`UserProfile` lands in Slice 1)?
   - Can the `/me` response be enriched (DB-backed lookup vs claims-only)?
   - Can endpoints be selectively excluded (e.g., we want register/login but not the built-in confirm-email flow)?

3. **Token mode interaction with R-044.** `MapIdentityApi` supports `useBearerOnly`, cookie-based, or both. For each token-storage option in R-044 (localStorage + bearer, httpOnly cookie, in-memory + httpOnly refresh-cookie, full BFF), how cleanly does `MapIdentityApi` support the pattern? Where does it fight you?

4. **Refresh-token behavior.** `MapIdentityApi` ships a `/refresh` endpoint. What's the default lifetime, rotation behavior, and revocation story? Does enabling it conflict with the cycle plan's "long-lived 30-day JWT, no refresh" baseline, or is it complementary?

5. **Testability.** How does each path test? With `MapIdentityApi`, can integration tests hit the endpoints via `WebApplicationFactory` + Testcontainers as cleanly as a custom controller? Are there assertion patterns that get harder?

6. **Module-first organization fit.** RunCoach uses `Modules/{Domain}/` per `backend/CLAUDE.md`. `MapIdentityApi` is registered in `Program.cs` (top-level). Can it be visually housed in `Modules/Identity/` via an extension method (`app.MapIdentityModule()` that internally calls `MapIdentityApi`), or does it always look like a top-level wiring detail?

7. **Migration cost.** If we pick `MapIdentityApi` now and later need a custom flow that Microsoft hasn't shipped (e.g., custom email verification, social login, MFA), what's the cost of carving out the endpoint into a custom controller? Conversely, if we pick custom now and later want to adopt Microsoft's wiring, what changes?

8. **Hybrid pattern.** Is there a maintained idiom for using `MapIdentityApi` for the boring 80% (register/login/refresh) and a custom controller for the 20% (`/me` with DB enrichment, custom logout side-effects)? If so, what's the wiring pattern and are there gotchas?

9. **Real-world adoption.** Search GitHub, .NET blogs, Microsoft's own samples (`eShop`, `dotnet/aspnetcore` samples), and Andrew Lock / Steve Gordon / Khalid Abuhakmeh posts for actual production usage. Which approach are non-trivial .NET 10 SPAs running in 2026?

10. **Module-first vs MapIdentityApi style mismatch.** RunCoach's controllers are `[ApiController]`-decorated, route-prefixed, and live in `Modules/`. `MapIdentityApi` produces minimal API endpoints with a different style. Is the style mismatch a real cost or cosmetic?

## Why It Matters

- **Slice 0 sets the auth surface contract** — every later slice (onboarding, plan, logging, conversation) consumes the auth endpoints. Changing the contract mid-MVP is expensive.
- **Microsoft's recommendation has shifted** — pre-.NET 8 the answer was "build a controller." Post-.NET 8, `MapIdentityApi` is the documented default. We should not pick the older pattern by default just because it's familiar.
- **Maintenance burden** — `MapIdentityApi` evolves with .NET releases (new auth scenarios get added without us writing them). A custom controller freezes us at whatever we ship.
- **R-044 coupling** — token storage and endpoint shape decisions are entangled; this prompt's outcome partially determines what R-044's recommended pattern can practically use.

## Deliverables

- **A concrete recommendation** with one chosen path (MapIdentityApi / custom / hybrid) and the explicit rationale.
- **A capability matrix** comparing MapIdentityApi vs custom across: customization, ProblemDetails support, token modes, refresh, testability, module-first fit, migration cost.
- **A ProblemDetails compatibility verdict** — can MapIdentityApi return RFC 7807 envelopes for its 400/401/409 responses, or does it use its own shape? If the latter, what's the workaround?
- **A wiring sketch** — minimal `Program.cs` excerpt showing the recommended registration pattern, plus the equivalent in `Modules/Identity/` if the hybrid approach is recommended.
- **Migration path** — if we adopt the recommendation now and need to switch later, what changes? Include scope estimate (file count, contract-break risk).
- **Compatibility notes** with .NET 10 specifically — anything that landed in .NET 9 or .NET 10 that affects this decision (e.g., new password-policy options, ProblemDetails defaults).
- **Citations** — current Microsoft Learn docs, dotnet/aspnetcore sample code, and 2025–2026 community sources.

## Out of Scope

- Whether to use ASP.NET Core Identity at all vs. a third-party IdP (Auth0, Keycloak, Clerk). The cycle plan locks in Identity; switching is not on the table.
- Mobile-specific auth flows (the iOS shim is post-MVP-0).
- MFA, password reset, email verification — explicitly deferred to pre-public-release per the cycle plan. Touch only if MapIdentityApi's bundled defaults force one of them on.
- Social login / OAuth providers — out of scope for MVP-0.
