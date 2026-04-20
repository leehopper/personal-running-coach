# Research Prompt: Batch 15c — R-046

# EF Core 10 Migration Application Strategy + Testcontainers/xUnit v3 Integration-Test Architecture

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a new .NET 10 / EF Core 10 backend tested with xUnit v3 + Testcontainers + a real Postgres, what are the current (2026) best practices for (a) applying database migrations across local-dev / CI / production, and (b) structuring the integration-test fixture lifecycle (per-class + Respawn, per-test fresh schema, per-test transaction) for performance and reliability?

## Context

I'm building Slice 0 (Foundation) of an AI-powered running coach (RunCoach):

- Backend: ASP.NET Core 10 + EF Core 10 + Marten (event sourcing on Postgres JSONB; same Postgres instance, dedicated `marten` schema). Postgres is the only relational store.
- Tests: xUnit v3 with the new MTP runner, `WebApplicationFactory<Program>` for integration tests, Testcontainers for ephemeral Postgres per the project-wide "Testcontainers, not in-memory" rule in `backend/CLAUDE.md`.
- Slice 0 introduces the first EF Core migration (Identity tables + `ApplicationUser`). Slices 1–4 add `UserProfile`, `WorkoutLog` (with JSONB metrics), `ConversationTurn`. Marten document writes start in Slice 1.
- Trajectory: solo-dev now → public beta later. No production environment exists yet; CI runs on GitHub Actions.

The Slice 0 spec currently assumes:

- `Database.Migrate()` on startup when `ASPNETCORE_ENVIRONMENT == Development`.
- Production migrations via `dotnet ef database update` from a deploy pipeline (documented as a convention, not implemented).
- Testcontainers per-class fixture (`IClassFixture<PostgresFixture>`); choice between transactional rollback per test and fresh-schema per test deferred to implementation.

I need a current-best-practice answer to both halves before Slice 0 implementation locks in patterns the entire test suite will inherit.

## Research Question

**Primary:** What is the current 2026 recommended pattern for (a) applying EF Core 10 migrations across environments and (b) structuring xUnit v3 + Testcontainers integration-test fixtures for a .NET 10 backend running against a real Postgres instance?

**Sub-questions:**

### Part A — Migration Application Strategy

1. **`Database.Migrate()` at startup.** What are the current known failure modes (multi-instance race, slow startup, partial-failure leaving DB inconsistent)? Has anything in EF Core 9/10 changed the calculus (e.g., distributed locks, idempotent migrations)?

2. **Migration bundles (`dotnet ef migrations bundle`).** Microsoft's documented pattern for production. What are the operational ergonomics — versioning the bundle, running it idempotently, observing failure? Compare with running `dotnet ef database update` directly from CI/CD vs from a sidecar job.

3. **Sidecar / init-container patterns.** For Docker Compose / Kubernetes deployments, is the consensus "one-shot init container that runs migrations before the API starts"? Reference any maintained libraries (e.g., `EvolveDb`, `DbUp`, `FluentMigrator`) or whether the .NET ecosystem has converged on EF-native tooling.

4. **Idempotency / re-runnability.** EF Core can generate idempotent migration scripts. Is that the current recommendation for production? What's the operational story when a migration partially fails?

5. **Hybrid recommendation.** Is the current best-practice pattern: dev = `Database.Migrate()` on startup, CI test fixtures = `Database.Migrate()` per fixture, production = bundle-based one-shot from CD? Or has the field moved past that?

6. **EF Core + Marten coexistence.** Marten has its own auto-create behavior (`AutoCreate.CreateOrUpdate`). EF Core has migrations. Both run against the same Postgres in different schemas. Are there ordering or interaction concerns at startup or in tests?

### Part B — xUnit v3 + Testcontainers Fixture Architecture

7. **xUnit v3 lifecycle changes.** xUnit v3 introduced new lifetime semantics (`IAsyncLifetime`, parallelization defaults, the MTP runner). What's the current-best fixture pattern for sharing an expensive resource (Postgres container) across tests?

8. **Per-class fixture with Respawn vs per-test fresh schema vs per-test transaction.** Compare on:
   - Setup cost (container startup, migration apply, schema teardown).
   - Test parallelization compatibility.
   - Isolation reliability (e.g., transaction-rollback breaks for tests that depend on `BEGIN`/`COMMIT` themselves, like outbox patterns).
   - Failure-mode debuggability.
   - Compatibility with Marten document writes (later slices).

9. **`Respawn` library status in 2026.** Is it still maintained, still recommended, and does it work with Postgres + Identity tables + Marten schemas cleanly?

10. **Container reuse across test runs.** Testcontainers has `WithReuse(true)`. Is that the current recommendation for local-dev test loops, and how does it interact with `dotnet test --watch` and CI?

11. **Snapshot / clone patterns.** Postgres supports `CREATE DATABASE … TEMPLATE …`. Is there an idiom for migrating a "template" DB once per fixture and cloning per test for cheap isolation? Any maintained .NET library that does this for Testcontainers?

12. **xUnit v3 + WebApplicationFactory in 2026.** Are there any known interactions between xUnit v3's new lifecycle and `WebApplicationFactory<Program>` that affect fixture design? `WebApplicationFactory` traditionally uses `IClassFixture` — does that still work or do v3-specific patterns exist?

13. **Parallelization budget.** Per-class fixture with Respawn enables parallelism across classes but not within a class. Per-test fresh schema enables full parallelism but pays setup cost per test. What's the actual measured trade-off in 2026 for a Postgres-on-Docker test suite of ~50–500 tests?

14. **Marten testing patterns.** Marten has its own test patterns (`DocumentStore.Advanced.Clean.CompletelyRemoveAll()`). Does it integrate cleanly with Respawn / per-test schema, or does it want its own teardown?

## Why It Matters

- **Test DX cascades** — the pattern Slice 0 picks is the pattern every later slice inherits. A bad choice at Slice 0 (e.g., per-test container startup) makes every later slice slower and more painful.
- **CI cost & flake rate** — Postgres-in-Docker tests can add 30s+ per fixture; multiplied across slices and re-runs, this is meaningful.
- **Multi-instance production realism** — `Database.Migrate()` on startup is fine for single-instance dev, but if the project later goes to multiple API replicas (or a hosted PaaS that auto-scales), the race condition can cause real outages. Picking the right pattern now beats retrofitting later.
- **Marten coexistence** — RunCoach's choice of EF + Marten on the same Postgres is unusual. The migration story has to handle both stores' schemas without stepping on each other.

## Deliverables

- **Two concrete recommendations**, one for migration application and one for test fixture architecture, each with a one-paragraph rationale.
- **A migration-strategy decision matrix** (startup-migrate / bundle / sidecar / init-container × dev / CI / prod), with the recommendation cell highlighted.
- **A fixture-architecture decision matrix** (per-class+Respawn / per-test schema / per-test transaction / template-clone × Identity tables / Marten docs / parallel suite × measured cost), with the recommendation cell highlighted.
- **Wiring sketches** — `Program.cs` snippet for the recommended migration pattern; `PostgresFixture.cs` + an example test class for the recommended fixture pattern.
- **Library/version pins** for any test helpers (Testcontainers.PostgreSql, Respawn, etc.) and confirmation of compatibility with xUnit v3 + .NET 10.
- **Marten coexistence notes** — explicit guidance on schema ordering, fixture teardown, and test-time auto-create config.
- **Failure-mode catalog** — what breaks the recommended pattern (e.g., parallelism limit, transactional outbox tests, distributed-test runs) and what the escape hatch is.
- **Citations** — current Microsoft Learn docs, Testcontainers .NET docs, Respawn maintainer activity, xUnit v3 release notes, 2025–2026 community sources.

## Out of Scope

- Choice of test framework — xUnit v3 is locked in per the project's existing test suite.
- In-memory database alternatives — `backend/CLAUDE.md` explicitly forbids them.
- Choice of database — Postgres is locked in.
- Performance benchmarking of Postgres itself; only test-suite cost matters.
- Production-grade observability for migration runs (metrics/logging during migration apply); flag if relevant but not the focus.
