# Research Prompt: Batch 29a — R-081

# Idempotency mechanism for a pure-EF-Core create endpoint (no Wolverine handler, no Marten side effect): EF-native unique-key vs. revisiting event-sourcing for WorkoutLog (EF Core 10 / Npgsql 10 / Marten 9 / Wolverine 6, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For an ASP.NET Core controller → repository → EF Core 10 write that has NO Marten event side effect and is explicitly forbidden from running inside a Wolverine handler body, what is the canonical 2026 idempotency mechanism for a client-supplied Guid idempotency-key on a POST create endpoint, such that (a) a replayed key returns the prior result without creating a duplicate row, (b) a failed attempt leaves the key safely reusable, and (c) the mechanism is atomic within the single EF transaction — without inventing the unproven Wolverine/Marten shared-connection co-commit that DEC-060/R-069 already ruled out?

Deliver a recommendation that closes the Slice 2b PR3 idempotency gap (linked decisions DEC-076, DEC-072, DEC-073, DEC-060/R-069). The Slice 2b spec inherited the phrase "co-transactional Marten IdempotencyMarker pattern (DEC-073/DEC-060)" for the WorkoutLog create endpoint, but that phrase was carried over by analogy to the plan-regenerate Wolverine handler. WorkoutLog is — by deliberate Final decision (DEC-076) and shipped code — a pure-EF immutable fact with no Marten stream and no handler; IWorkoutLogRepository.CreateAsync "MUST NOT be called from inside a Wolverine handler body." The Marten IdempotencyMarker only becomes co-transactional via Wolverine's handler transaction bracket, which does not exist on this path; combining a controller-side Marten session commit with a separate DbContext.SaveChangesAsync is the exact two-connection dual-write R-069 proved non-atomic.

### Sub-questions the artifact must answer
1. (load-bearing / critical) For a single EF Core 10 + Npgsql 10 SaveChanges, what is the canonical idempotency-key pattern? Compare: (a) an IdempotencyKey column + unique index (UserId, IdempotencyKey) directly on the WorkoutLog row with insert-then-catch DbUpdateException(unique-violation); (b) a dedicated EF IdempotencyRecord table storing the serialized response, written in the same SaveChanges as the domain row; (c) PostgreSQL INSERT … ON CONFLICT DO NOTHING / RETURNING. Address how each returns the *prior result* (the original workoutLogId) on replay, and how each behaves under a concurrent double-submit race.
2. How does each option satisfy "a failed attempt rolls back so the same key is reusable" when the write is one atomic EF transaction (i.e., no marker is persisted unless the row is) vs. the handler-path semantics DEC-073 described?
3. Does introducing an EF-native idempotency mechanism on this one endpoint, while the LLM/handler endpoints keep the Marten-document IIdempotencyStore, create an acceptable two-mechanism split — or is there a single abstraction (e.g., an IIdempotencyStore with both a Marten-session-backed and an EF-DbContext-backed implementation selected per call site) that keeps the call-site contract uniform without re-introducing the dual-write problem? R-069 warned "don't add a third store" — does an EF-native key on the domain row itself count as a store, or is it free?
4. Counterfactual: is the cost of revisiting DEC-076 to event-source WorkoutLog (a WorkoutLogged event + EfCoreSingleStreamProjection, the one proven co-transactional path) lower than maintaining a second idempotency mechanism? Enumerate concretely what event-sourcing WorkoutLog would cost given DEC-076's findings (no stable prescribed-workout id, whole-plan regeneration, snapshot-on-log) and whether it buys anything the EF-native key does not.

## Context

Verified repo state (2026-06-05):
- WorkoutLog: pure EF entity, `[Table("WorkoutLog")]`, EF migration `AddWorkoutLog` shipped; implements `Marten.Metadata.ITenanted` for tenancy parity only (DEC-072). WorkoutLogConfiguration has NO idempotency column — only `HasIndex(UserId, OccurredOn, WorkoutLogId)` for keyset paging.
- WorkoutLogRepository.CreateAsync = `_db.WorkoutLogs.Add(log); await _db.SaveChangesAsync(ct);`. Interface XML-doc forbids calling it from a Wolverine handler body.
- WorkoutLogsController.CreateLog is a RED 501 stub awaiting PR3 GREEN.
- The ONLY IIdempotencyStore impl is MartenIdempotencyStore: `Record` calls `_session.Insert(marker)` on a tenant-scoped IDocumentSession; used by OnboardingTurnHandler + RegeneratePlanHandler (both Wolverine handlers). Program.cs MoveToErrorQueue-handles DocumentAlreadyExistsException from concurrent marker inserts — a handler-pipeline construct.
- DEC-060 / R-069 (batch-23a, Done): Wolverine 5.x runs Marten and EF as two independent envelope transactions on two NpgsqlConnections (no 2PC); the shared-connection transaction-participant exists ONLY inside EfCore*Projection apply paths, not for arbitrary handler-body DbContext writes.
- DEC-076 (Final): WorkoutLog↔prescribed-workout is snapshot-on-log, not a FK; no synthetic workout id; WorkoutLog is an EF immutable fact, no Marten stream. "Open (impl-time)" leaves "how the create endpoint receives the coordinate" open but never the idempotency wiring.
- DEC-073 (Accepted, first-live-Slice-3): the "co-transactional marker rolls back on failure" clause is explicitly an in-handler-LLM-failure resilience contract; "no live LLM call sits on a 2b request path — the create is pure persistence."
- 16-questions Q3 offered only {reuse the Marten marker "as the regenerate flow does", or no idempotency}; the pure-EF-vs-Marten tension was never surfaced.

Stack: .NET 10 / C# 14, EF Core 10.0.8, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.2, Marten 9.x / Wolverine 6.x (R-069 verified against Marten 8.28 / Wolverine 5.x; re-verify the shared-connection claim has not changed on the current pins), PostgreSQL.

## Why It Matters

PR3 cannot ship a correct, atomic, idempotent create endpoint while the spec asserts a mechanism ("co-transactional Marten IdempotencyMarker") that is impossible on a pure-EF non-handler path. The feature file requires "exactly one WorkoutLog record exists for that idempotencyKey" and "a failed create attempt rolls back its idempotency marker." The wrong choice either re-introduces the dual-write split-brain R-069 exists to kill, fragments the idempotency contract across the codebase, or forces a contradictory reversal of DEC-076. This is a one-way persistence-shape door at the foundation of all logging.

## Deliverables

- **Recommended mechanism** — a single ranked choice among EF-native unique-key (and its sub-variants), a per-call-site dual-implementation IIdempotencyStore, or event-sourcing WorkoutLog — with the concurrency/replay/rollback semantics spelled out and the migration shape (new column + unique index vs. new table) stated.
- **Atomicity + replay proof sketch** — exactly how the prior workoutLogId is returned on replay and how the unique-violation race resolves to "first-write-wins" within one EF SaveChanges.
- **Contract-uniformity verdict** — whether a second mechanism is acceptable or an abstraction should unify the call sites; explicit answer to R-069's "don't add a third store."
- **DEC-076 counterfactual cost** — concrete enumeration of what event-sourcing WorkoutLog would add/break, to justify keeping or reversing the pure-EF decision.

## Out of scope

- The LLM-handler idempotency path (onboarding/regenerate) — DEC-073/DEC-060 already govern it; do not re-open.
- The prescription-snapshot resolution logic (DEC-076) and the metrics jsonb bag (DEC-072) — settled.
- Frontend retry/"try again" UX (DEC-075) — settled.

Artifact destination: `docs/research/artifacts/batch-29a-efcore-create-endpoint-idempotency.md`. Integration path: a new decision (DEC-077) ratifying the mechanism, plus a correction to the 16-spec Slice 2b Unit 3 wording (replace "co-transactional Marten IdempotencyMarker pattern" with the chosen pure-EF mechanism) and the IWorkoutLogRepository.CreateAsync doc.
