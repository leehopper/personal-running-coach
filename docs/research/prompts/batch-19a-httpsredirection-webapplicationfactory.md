# Research Prompt: Batch 19a — R-056

# `UseHttpsRedirection` + `WebApplicationFactory` + `__Host-` cookie interaction on ASP.NET Core 10 (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

**Research Topic:** For an ASP.NET Core 10 + ASP.NET Core Identity app whose browser session is a `__Host-`-prefixed application cookie with `SecurePolicy = Always` + `SameSite = Lax` + `HttpOnly`, served to a Vite SPA on `https://localhost:5173`, and tested by an xUnit v3 `AssemblyFixture` running `WebApplicationFactory<Program>` against Testcontainers Postgres — what is the canonical 2026 pattern for `UseHttpsRedirection` placement / gating, and how do HTTPS redirect + `__Host-` cookie + `WebApplicationFactory`'s default HTTP test server compose without redirect-loops, silently-dropped cookies, or Secure/HTTPS contract violations in dev vs test vs prod?

## Context

RunCoach's Slice 0 (T02.2, just shipped — commit `1b8eac8`) wires the ASP.NET Core Identity application cookie with `SecurePolicy.Always`, `SameSite=Lax`, `HttpOnly`, and `__Host-RunCoach` name. The `__Host-` prefix + `SecurePolicy.Always` requires HTTPS end-to-end — browsers refuse to accept a Secure cookie over HTTP.

My Slice 0 choice was to gate `UseHttpsRedirection` on `!IsDevelopment()`:

- **Dev (`dotnet run`):** no redirect, so local dev without an HTTPS launch profile serves. Developers opt into HTTPS via `--launch-profile https`.
- **Test (`WebApplicationFactory<Program>`):** the fixture runs `UseEnvironment("Development")`, so redirect is off — the HTTP test handler works without redirect-loops. The tests assert `Set-Cookie` headers directly, not browser cookie acceptance.
- **Prod / Staging:** `UseHttpsRedirection` enforces HTTPS.

This choice works (all 588 tests green including the six SUT-host smoke tests) but I made it based on ASP.NET Core conventions + my own reasoning, not against a specific reference. R-044 (the DEC-044 cookie-session artifact, `batch-15a-jwt-auth-patterns-browser-clients.md`) recommended the cookie shape but did not explicitly cover `UseHttpsRedirection` placement or the `WebApplicationFactory` HTTP interaction.

Before T02.4 wires the actual login / logout endpoints that issue `Set-Cookie: __Host-RunCoach=...; Secure; HttpOnly; SameSite=Lax` headers, I want the HTTPS-redirect + test-host composition validated so T02.5's integration tests assert the right behavior.

## Research Question

**Primary:** What is the canonical pattern for `UseHttpsRedirection` placement, gating, and port configuration in an ASP.NET Core 10 app whose session relies on a `__Host-`-prefixed Secure + SameSite=Lax cookie, tested via `WebApplicationFactory<Program>` on Testcontainers Postgres?

**Sub-questions:**

1. **Gating pattern.** Is `!IsDevelopment()` the idiomatic gate, or is there a better split (e.g., `IsProduction()` only, or always-on with `HTTPS_PORT` configured, or a policy-based gate)? What do ASP.NET Core 10 templates do out of the box?
2. **`WebApplicationFactory` HTTP vs HTTPS.** What's the current (2026) guidance for testing auth flows that issue Secure cookies via `WebApplicationFactory`? Is HTTP-only test-host acceptable (tests assert `Set-Cookie` header shape directly), or should the fixture be configured for HTTPS?
3. **Redirect-loop pitfalls.** Under what configurations does `UseHttpsRedirection` + `WebApplicationFactory` redirect-loop or drop cookies silently? What's the fix pattern?
4. **Dev-without-HTTPS-cert reality.** Many devs run `dotnet run` without `dotnet dev-certs https --trust`. With `__Host-` + `Secure` cookies, the login flow is broken over HTTP in dev. Is the accepted solution "require HTTPS launch profile" or is there a lower-friction pattern?
5. **HSTS + `UseHsts()` interaction.** Should `UseHsts()` land in the same gate as `UseHttpsRedirection`? Any interaction with the `__Host-` prefix?

## Why It Matters

`UseHttpsRedirection` placement affects every integration test that hits an auth endpoint (T02.5 onwards) and the real browser login flow (T03.x). A wrong choice surfaces as either "tests redirect-loop" or "tests pass but real browser login silently fails because the cookie was set over HTTP and browser refused it." This is the kind of bug that lands in prod invisible until a user actually tries to log in.

## Deliverables

- **Primary recommendation** on `UseHttpsRedirection` placement + gating + port config with rationale.
- **Test-host contract** — what `WebApplicationFactory<Program>` configuration maximizes fidelity to the production cookie contract without forcing HTTPS on the test handler.
- **Dev-environment recipe** — one sentence each for "dev without HTTPS certs" and "dev with HTTPS certs" so `dotnet run` works in both.
- **Gotchas** — named pitfalls (redirect loops, silent cookie drops, Secure-over-HTTP oddities) with the ASP.NET Core version each applies to.
- **Alternatives considered and why rejected.**

---

## Current Repo State (for the research agent to inspect if useful)

- `backend/src/RunCoach.Api/Program.cs` — Slice 0 auth pipeline (commit `1b8eac8`).
- `backend/tests/RunCoach.Api.Tests/Infrastructure/RunCoachAppFactory.cs` — the `WebApplicationFactory<Program>` + Testcontainers fixture.
- `docs/research/artifacts/batch-15a-jwt-auth-patterns-browser-clients.md` — R-044 / DEC-044 cookie-session recommendation (if it landed there; otherwise check `batch-15*`).
- `docs/specs/12-spec-slice-0-foundation/12-spec-slice-0-foundation.md` — spec §Unit 2 functional requirements for the cookie.
