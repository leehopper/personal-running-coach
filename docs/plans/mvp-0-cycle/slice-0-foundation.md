# Slice 0 Requirements: Foundation

> **Requirements only — not a specification and not an implementation plan.** This doc captures the "what" for Slice 0 at a level that survives implementation discoveries. The "how" is written in a fresh session at build time (see *How this feeds the spec* below). The parent doc is `docs/plans/mvp-0-cycle/cycle-plan.md`; if anything here conflicts with it, the cycle plan wins.

## Purpose

Wire up multi-tenant authentication and persistence so a user can register, log in, and see an authenticated empty home page. No business features in this slice.

## Functional requirements

When this slice is complete, the user (or calling client) can:

- Register an account with email + password.
- Log in with valid credentials and receive an authenticated session.
- Reach an authenticated home surface with a minimal "you're logged in" acknowledgement.
- Log out and return to an unauthenticated state.
- Be redirected to login when visiting protected content without credentials.
- Have the session persist across browser refresh.
- Receive appropriate error responses for: duplicate-email registration, invalid login credentials, weak-password registration, malformed request bodies.

## Quality requirements

- The full stack (Postgres, API, web) comes up healthy via `docker compose up`.
- Database migrations apply automatically on development startup.
- Backend integration tests exercise auth endpoints against a real Postgres instance — not an in-memory stand-in — consistent with the project-wide "Testcontainers, not in-memory" testing rule in `backend/CLAUDE.md`.
- Frontend component tests cover form validation states.
- One end-to-end test covers the register → authenticated home → logout happy path.
- CI passes with all required checks on the slice PR.
- No build warnings (project-wide `TreatWarningsAsErrors` enforcement).

## Scope: In

- Multi-tenant identity (per-user records; every business-entity query in later slices isolates by user id).
- JWT-based authentication (stateless).
- Persistence foundation: EF Core on Postgres for relational entities (user records + Identity tables), plus Marten registered against the same Postgres for future event-sourced state (no documents written this slice).
- Frontend auth UX: login, register, protected home, client-side token persistence.
- Module-level scaffolding for `Modules/Identity/` (backend) and `modules/auth/` (frontend).
- A shared API client layer on the frontend that attaches the token to outbound requests.

## Scope: Out (deferred)

- Refresh tokens / token rotation / server-side revocation — personal-use for now, deferred to pre-public-release per the cycle plan.
- Password reset, email verification, email change.
- Account lockout / brute-force protection.
- Multi-factor authentication.
- Any business features (plan, workout logging, coaching — Slices 1-4).
- Pre-public-release safety scaffolding (PAR-Q+, ToS, beta agreement — cross-cycle deferred).

## Pragmatic defaults for deferred decisions

The spec-writing session may override these, but they're the baseline unless the author has a specific reason to diverge:

- JWT lifetime: long-lived (~30 days); no refresh-token endpoint.
- Password policy: minimum 12 characters with upper + lower + digit requirements; no symbol requirement; no history check.
- Session storage: client-side (localStorage or equivalent).
- Logout: client-side token clear; any server-side logout endpoint is a placeholder no-op.

## Research to consult before writing the spec

- `docs/research/artifacts/batch-10b-dotnet-backend-review-practices.md` — backend conventions.
- `docs/research/artifacts/batch-10c-ci-quality-gates-private-repo.md` — CI pipeline shape.
- `docs/research/artifacts/batch-10a-frontend-latest-practices.md` — React 19 + TypeScript + Vite conventions.
- `backend/CLAUDE.md` — module-first organization, Identity vs. EF Core vs. Marten ownership, testing patterns.
- `frontend/CLAUDE.md` — module-first organization, route/page naming, form patterns, state management boundaries.

## Open items for the spec-writing session to resolve

- Exact HTTP error-response envelope shape (field names, status-code mapping) — decide once in the spec.
- Whether an authenticated "who am I" endpoint belongs in Slice 0 or lands with the `UserProfile` entity in Slice 1.
- Migration application strategy beyond local-dev (production, CI integration-test setup).
- Whether Marten needs any initial configuration beyond registration (schema name, default serializer options).
- Any deviation from the pragmatic defaults above — document the reason.

## How this feeds the spec

When Slice 0 implementation begins in a fresh session:

1. Read this doc, the cycle plan, and the research artifacts above.
2. Clarify any ambiguous requirements with the user.
3. Write the specification under `docs/specs/` following existing project convention (e.g., `docs/specs/slice-0-foundation/spec.md`).
4. User reviews the spec before implementation starts.
5. Implement against the spec. Discoveries that change the spec are captured as spec amendments, not in-session notes.

This requirements doc is durable across implementation churn. The spec is allowed to churn. Implementation detail lives only in the spec and the resulting code.
