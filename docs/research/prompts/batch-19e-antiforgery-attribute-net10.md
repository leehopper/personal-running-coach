# Research Prompt: Batch 19e — R-062

# Antiforgery attribute selection for MVC controllers on ASP.NET Core 10 — `[ValidateAntiForgeryToken]` vs `[RequireAntiforgeryToken]` vs the `UseAntiforgery` middleware (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

**Research Topic:** For an ASP.NET Core 10 app whose JSON API lives in MVC controllers (not Minimal APIs), whose session is a `__Host-`-prefixed Identity application cookie, and whose anti-CSRF posture is the built-in double-submit cookie via `AddAntiforgery()` + `UseAntiforgery()` (header `X-XSRF-TOKEN`, cookie `__Host-Xsrf`, plus a SPA-readable `__Host-Xsrf-Request` written by `/xsrf`), what is the canonical 2026 attribute to place on state-changing endpoints — `[ValidateAntiForgeryToken]` (MVC filter) or `[RequireAntiforgeryToken]` (endpoint metadata consumed by the `UseAntiforgery` middleware) — and what `AddControllers*` registration shape does each require?

## Context

RunCoach's Slice 0 auth module just shipped the following shape:

- **Parent task #44 / spec §Unit 2 prescribes** `[RequireAntiforgeryToken]` and explicitly warns that `[ValidateAntiForgeryToken]` is "broken with `UseAntiforgery` in .NET 10" — but without a linked primary source.
- **T02.4 (commit `bdb5383`) shipped `[ValidateAntiForgeryToken]`** on `AuthController.Register` and `AuthController.Logout` with `[IgnoreAntiforgeryToken]` on `AuthController.Xsrf`, using `Microsoft.AspNetCore.Mvc` namespace attributes.
- **T02.5 (commit `7506c8a`) hit a runtime bug** the moment the integration-test matrix exercised a real POST: `InvalidOperationException: No service for type 'Microsoft.AspNetCore.Mvc.ViewFeatures.Filters.ValidateAntiforgeryTokenAuthorizationFilter' has been registered.` The fix was to switch `builder.Services.AddControllers()` → `builder.Services.AddControllersWithViews()` so `AddViews()`'s filter registrations are present.
- **The test matrix now covers** happy-path, missing-token (400), and cookie-lifecycle flows. All 13 AuthControllerTests pass; full assembly suite is 601 / 601.

The current wiring therefore works, but it may be an anti-pattern: we're carrying view-engine services the app does not use, and we ignored the parent task's explicit guidance. The `[RequireAntiforgeryToken]` alternative lives in `Microsoft.AspNetCore.Antiforgery` (not MVC) and provides `IAntiforgeryMetadata` that the `UseAntiforgery` middleware consumes directly. The question is which is correct for this stack in 2026, with primary sources.

The parent task's language ("broken with `UseAntiforgery` in .NET 10") suggests there was a specific regression or interaction that made the MVC filter path unreliable. The research should confirm whether that claim holds on .NET 10.0.6 (the current pin) and, if so, under what circumstances.

## Research Question

**Primary:** In ASP.NET Core 10 MVC controllers served behind `app.UseAntiforgery()`, which attribute is the canonical anti-CSRF gate for unsafe methods — `Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute` or `Microsoft.AspNetCore.Http.RequireAntiforgeryTokenAttribute` — and what is the minimum service registration each requires?

**Sub-questions:**

1. **Attribute selection.** For a pure JSON API hosted in MVC controllers (no Razor views, no Razor Pages) using the `UseAntiforgery` middleware — which attribute does Microsoft's current guidance (Learn / dotnet/aspnetcore samples / ASP.NET Core team posts) recommend? What primary source (not a blog) supports the recommendation?
2. **`[RequireAntiforgeryToken]` on MVC controllers.** `Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryTokenAttribute` is documented for Minimal APIs. Does placing it on an MVC controller action emit the same `IAntiforgeryMetadata` such that `UseAntiforgery` validates the request, without requiring any MVC filter registration? Any caveats around filter ordering, `[AllowAnonymous]`, or `[IgnoreAntiforgeryToken]` carve-outs?
3. **`[IgnoreAntiforgeryToken]` carve-out.** If we switch to `[RequireAntiforgeryToken]` globally, how do we exempt the `GET /xsrf` endpoint from validation? Does `[IgnoreAntiforgeryToken]` (MVC) produce the corresponding `IAntiforgeryMetadata(required: false)`, or is a different mechanism required (e.g., `DisableAntiforgery()` endpoint convention)?
4. **`AddControllersWithViews()` side-effects.** The current T02.5 workaround pulls in view-engine services (Razor view engine, `IViewEngine`, tag helpers, partial views, antiforgery filter registrations). Are any of those lazily initialized such that the cost is zero, or does the registration graph eagerly resolve services we don't want? Any security-surface expansion (e.g., Razor-rendered error pages) we should be aware of?
5. **`[ValidateAntiForgeryToken]` "broken" claim.** Is the parent task's claim that `[ValidateAntiForgeryToken]` is broken with `UseAntiforgery` in .NET 10 accurate? Our tests show it works with `AddControllersWithViews()`. Is there a specific scenario (dual validation by both the MVC filter AND the middleware, ordering issue, etc.) where the two interact incorrectly?
6. **Dual-validation concern.** With both `[ValidateAntiForgeryToken]` (MVC filter validates in the filter pipeline) AND `app.UseAntiforgery()` (middleware validates before the endpoint), is the token being validated twice per request? If so, is that a correctness, performance, or log-noise concern?
7. **Canonical test-host pattern.** For `WebApplicationFactory<Program>`-based integration tests (xUnit v3 + Testcontainers Postgres, per R-046), what is the canonical way to attach a real antiforgery token — call `GET /xsrf` and echo the SPA-readable cookie into `X-XSRF-TOKEN`, or use `IAntiforgery.GetAndStoreTokens()` via a scope resolved from `Factory.Services`? Either suffices; the recommendation should match the shape T03.x frontend will use, not just what compiles.

## Why It Matters

Three concrete consequences, in order of likelihood:

- **Every state-changing auth endpoint.** `POST /register`, `POST /logout`, and every authenticated POST / PUT / DELETE in later slices routes through this attribute. Picking the wrong one now means every new controller carries the anti-pattern forward and we retrofit the entire surface later.
- **Surface-area minimization.** `AddControllersWithViews()` brings Razor view engine + tag helpers + partial views into DI. For a JSON-only API this is dead weight, and possibly an unnecessary security surface (e.g., if a Razor view is ever accidentally introduced by a future contributor, it picks up the view-engine machinery automatically).
- **Parent-task fidelity.** The cycle-plan / parent-task document explicitly prescribed `[RequireAntiforgeryToken]`. T02.4 diverged from that without surfacing the decision; T02.5 papered over the divergence with `AddControllersWithViews()`. If the parent's guidance turns out correct, the fidelity gap is the right thing to close before the T02 PR merges. If the parent's guidance turns out wrong or incomplete, the decision-log should capture the revised recommendation so Slice 1+ controllers don't re-encounter the same confusion.

## Deliverables

- **Primary recommendation** on which attribute to use for MVC controllers on .NET 10 + `UseAntiforgery()`, citing a primary source (Microsoft Learn page, `aspnetcore` samples, ASP.NET Core team PR / issue, or `AntiforgeryMiddleware.cs` source).
- **Wiring sketch** — the exact `AddControllers*` call, attribute placement, and carve-out mechanism for `GET /xsrf`.
- **Verdict on the "broken" claim** — confirm / reject the parent task's assertion that `[ValidateAntiForgeryToken]` is broken with `UseAntiforgery` in .NET 10, with source.
- **Verdict on dual-validation** — is the token validated twice, and does it matter?
- **Test-host recommendation** — the `HttpClient` + token acquisition pattern that matches whatever the SPA will do in T03.x.
- **Migration delta** — concrete diff for AuthController + Program.cs to go from the current `[ValidateAntiForgeryToken]` + `AddControllersWithViews()` shape to the recommended shape, if they differ.

---

## Current Repo State (for the research agent to inspect if useful)

- `backend/src/RunCoach.Api/Program.cs` — current `AddControllersWithViews()` + `AddAntiforgery()` + `app.UseAntiforgery()` wiring (commit `7506c8a`).
- `backend/src/RunCoach.Api/Modules/Identity/AuthController.cs` — current `[ValidateAntiForgeryToken]` + `[IgnoreAntiforgeryToken]` shape.
- `backend/tests/RunCoach.Api.Tests/Modules/Identity/AuthControllerTests.cs` — the test matrix pinning the current contract; cases 5 and 13 exercise the missing-antiforgery-token branch.
- `docs/research/artifacts/batch-19a-httpsredirection-webapplicationfactory.md` — R-056; related Slice 0 auth posture.
- `docs/decisions/decision-log.md` — DEC-054 captures the `/xsrf` SPA-readable cookie decision; the eventual DEC for this topic should link into that chain.
