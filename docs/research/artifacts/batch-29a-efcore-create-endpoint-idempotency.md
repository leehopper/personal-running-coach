> **Research artifact — Batch 29a · R-081.** Commissioned via the RunCoach research protocol; prompt at `docs/research/prompts/batch-29a-efcore-create-endpoint-idempotency.md`. Deep-web-research output landed & integrated 2026-06-06 (queue → Integrated). Locks **DEC-077** (EF-native idempotency key for the pure-EF `WorkoutLog` create endpoint — rejects the spec's inherited "co-transactional Marten IdempotencyMarker" wording on this path, and rejects event-sourcing `WorkoutLog`). Verbatim research output follows.

---

# Idempotency Design Decision: WorkoutLog Pure-EF Create Endpoint (2026)

*Research artifact — suitable for `docs/research/artifacts/`. Severity convention: **[CRITICAL]** = load-bearing for correctness; **[MAJOR]** = design-shaping; **[MINOR]** = ergonomics.*

## TL;DR
- **Use a client-supplied `Guid` idempotency key as a column on the `WorkoutLog` row itself, protected by a composite unique index on `(UserId, IdempotencyKey)`, with an insert-then-catch-23505 pattern (Option a). On a duplicate, re-read by `(UserId, IdempotencyKey)` and return the prior `workoutLogId`.** This is the canonical 2026 mechanism for a single-transaction EF Core 10 + Npgsql 10 write with no Marten side effect and no Wolverine handler.
- **This is NOT a "third store."** The key lives on the domain row; there is no separate marker document or response table. It is effectively free, and atomicity is automatic because the key and the row are inserted in the same `SaveChanges`.
- **Do NOT event-source WorkoutLog and do NOT build a co-transactional Marten IdempotencyMarker on this path.** The Marten/Wolverine co-commit only exists inside Wolverine's handler transaction bracket and inside `EfCore*Projection` apply paths; on a controller→repository→EF path it degrades to a non-atomic two-connection dual-write. Event-sourcing buys nothing the EF-native key does not, at meaningful cost.

## Key Findings

### 1. [CRITICAL] The spec is wrong on this path
The current spec asserts a "co-transactional Marten IdempotencyMarker pattern" for the WorkoutLog create endpoint. That pattern is only co-transactional when Wolverine's handler transaction bracket (or an `EfCore*Projection` apply path) coordinates the single shared `NpgsqlConnection`. On a plain ASP.NET Core controller → repository → `DbContext.SaveChangesAsync` path there is no such bracket. Committing a Marten session and separately calling `SaveChangesAsync` is a two-connection dual-write with no two-phase commit — exactly the non-atomic pattern already ruled out. The spec must be corrected.

### 2. [MAJOR] Recommended mechanism (ranked)
1. **EF-native unique key on the domain row (Option a) — CHOSEN.** New `IdempotencyKey uuid` column on `workout_logs` + unique index `(user_id, idempotency_key)`. Insert the row and key together in one `SaveChanges`.
2. **Dedicated `IdempotencyRecord` table in the same `SaveChanges` (Option b) — only if you must replay a full serialized HTTP response body, not just the id.** Adds a second table and a chicken-and-egg with the generated id.
3. **`INSERT … ON CONFLICT DO NOTHING` (Option c) — strong alternative; use if you want to avoid the abort-and-retry dance entirely.** Best concurrency ergonomics but requires raw SQL in EF Core 10 (no first-class API).
4. **Per-call-site dual-implementation `IIdempotencyStore` — acceptable but unnecessary here.**
5. **Event-source WorkoutLog — REJECTED.**

### 3. [CRITICAL] EF Core 10 / Npgsql 10 exception handling is stable and well-defined
A unique violation surfaces as `DbUpdateException` whose `InnerException` is `Npgsql.PostgresException` with `SqlState == "23505"`. Per the Npgsql source (`PostgresErrorCodes.cs`) and API docs, the supplied constant is `public const string UniqueViolation = "23505"` — prefer it over a string literal. The Npgsql `PostgresException.SqlState` property is documented as "Always present. Constants are defined in PostgresErrorCodes." It is NOT a `DbUpdateConcurrencyException` — EF only throws that for update/delete mismatches, never for insert constraint violations (Microsoft EF Core "Handling Concurrency Conflicts" docs: "this exception is generally never thrown when adding entities").

### 4. [CRITICAL] The transaction-abort subtlety is load-bearing
At the raw PostgreSQL level, a 23505 error **aborts the entire transaction**. pganalyze documents the follow-on state as SQLSTATE `25P02` (Class 25, `in_failed_sql_transaction`): "ERROR: current transaction is aborted, commands ignored until end of transaction block … after an error, the transaction state needs to be cleaned up using ROLLBACK before another query can be performed." The PostgreSQL tutorial confirms "ROLLBACK TO is the only way to regain control of a transaction block that was put in aborted state by the system due to an error, short of rolling it back completely and starting again." This means the loser of a concurrent race **cannot simply re-read in the same poisoned transaction**. Two clean ways out: (1) rely on EF Core's implicit single-`SaveChanges` transaction (auto-rolled-back, so the connection is clean for the re-read); or (2) avoid raising the error at all with `ON CONFLICT DO NOTHING`.

## Details

### Architecture recap (givens, not re-litigated)
- WorkoutLog is a pure-EF immutable fact: no Marten stream, no Wolverine handler. `CreateAsync` MUST NOT run inside a Wolverine handler body.
- Wolverine runs Marten and EF as two independent envelope transactions on two separate `NpgsqlConnection`s; there is no 2PC. The shared-connection transaction-participant exists only inside `EfCore*Projection` apply paths.
- LLM/handler idempotency (onboarding/regenerate) is governed by prior decisions and out of scope.

### [MAJOR] Re-verification against current pins (Marten 9.x / Wolverine 6.x)
The prior verification was against Marten 8.28 / Wolverine 5.x. The Critter Stack 2026 wave (Marten 9.0, Wolverine 6.0, Polecat 4.0) shipped on NuGet on May 24, 2026 (Wolverine is already at 6.0.x patch releases, e.g. 6.0.1 pinning Marten 9.0.1 / Polecat 4.1.1 / JasperFx 2.0.1). Per Jeremy D. Miller's release blog (jeremydmiller.com, May 24, 2026): "Wolverine 6.0 was a smaller release in terms of what users will notice. The release mostly revolved around the changes in the underlying dependencies, the AOT compliance, and cold start improvements." — i.e., no change to the fundamental outbox/connection model.

Marten 9's own EF Core projection docs still state the co-transactional guarantee is achieved by Marten "register[ing] a transaction participant so the DbContext's SaveChangesAsync is called within Marten's transaction" and by "creat[ing] a per-slice DbContext using the same PostgreSQL connection as the Marten session" — i.e., the shared-connection co-commit exists ONLY inside the projection apply path, NOT for arbitrary handler-body or controller-body DbContext writes. Wolverine's outbox remains an eventual-consistency store-and-forward design that exists, in Miller's words, "to obviate the lack of true 2 phase commits." **The "two independent connections, no 2PC" claim still holds on the current pins.** Nothing in the 9.x/6.x changelog reverses it.

### Sub-question 1 — canonical single-transaction pattern, options compared

**Option (a): IdempotencyKey column + unique index on the domain row. CHOSEN.**

Migration shape (one new column, one new index — no new table):

```csharp
// WorkoutLog entity
public class WorkoutLog
{
    public Guid Id { get; set; }                 // PK (app-generated GUID — see below)
    public Guid UserId { get; set; }
    public Guid IdempotencyKey { get; set; }     // client-supplied
    // ... immutable fact fields, metrics jsonb, etc.
}

// In OnModelCreating / IEntityTypeConfiguration<WorkoutLog>
builder.HasIndex(w => new { w.UserId, w.IdempotencyKey })
       .IsUnique()
       .HasDatabaseName("ux_workout_logs_user_idempotency");
```

EF migration produces roughly:

```sql
ALTER TABLE workout_logs ADD COLUMN idempotency_key uuid NOT NULL;
CREATE UNIQUE INDEX ux_workout_logs_user_idempotency
    ON workout_logs (user_id, idempotency_key);
```

Use a **unique index** (`HasIndex(...).IsUnique()`), not an alternate key (`HasAlternateKey`). Per Microsoft's EF Core modeling guidance, `HasAlternateKey` exists primarily to provide the *target of a foreign key* and carries read-only semantics; "If you just want to enforce uniqueness on a column, define a unique index rather than an alternate key." A composite unique index is the correct EF Core construct for `(UserId, IdempotencyKey)`.

How it returns the prior result on replay: the original `workoutLogId` is read straight back from the winning row via the same `(UserId, IdempotencyKey)` predicate — there is no separate place to store the id because the id IS on the row that the key uniquely identifies.

**Option (b): dedicated IdempotencyRecord table written in the same SaveChanges.**

```csharp
public class IdempotencyRecord
{
    public Guid UserId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public Guid WorkoutLogId { get; set; }       // the response id
    public string? ResponseBody { get; set; }    // optional serialized payload
}
// unique index on (UserId, IdempotencyKey)
```

Because both the `WorkoutLog` and the `IdempotencyRecord` are added to the same `DbContext` and flushed in one `SaveChanges`, they commit atomically in one transaction — so this IS single-transaction-safe. Its only justification over (a) is when you must replay a full serialized HTTP response (status + body + headers), the Stripe model. **Chicken-and-egg:** if `WorkoutLogId` is DB-generated, you do not know it until after the insert, so storing it in the record "after the fact" would need a second round trip. The clean fix is a **client-supplied or app-generated `Guid` PK** (e.g., a sequential GUID) assigned before `SaveChanges`, so the id is known up front and both rows insert together. This is why id-generation strategy directly drives the recommendation: with an app-side GUID, Option (b) collapses to "Option (a) plus an extra table you probably don't need."

**Option (c): INSERT … ON CONFLICT DO NOTHING / RETURNING.**

```sql
INSERT INTO workout_logs (id, user_id, idempotency_key, ...)
VALUES (@id, @userId, @key, ...)
ON CONFLICT (user_id, idempotency_key) DO NOTHING
RETURNING id;
```

If `RETURNING` yields a row, you created it; if it yields nothing, it was a duplicate replay — then `SELECT id FROM workout_logs WHERE user_id=@userId AND idempotency_key=@key`. The decisive advantage, confirmed by the PostgreSQL INSERT docs: **`ON CONFLICT DO NOTHING` does not raise 23505 at all** ("specifies an alternative action to raising a unique violation… ON CONFLICT DO NOTHING simply avoids inserting a row"), so the transaction is never poisoned and the follow-up `SELECT` is safe in the same transaction. The cost in EF Core 10: there is still no first-class `OnConflict` API — `dotnet/efcore` issue #16949 ("Add support for INSERT IGNORE / ON CONFLICT DO NOTHING," opened by shravan2x on Aug 5, 2019) remains Open in the Backlog. So you write this via `ExecuteSqlRaw`/`FromSql`, or pull in `FlexLabs.EntityFrameworkCore.Upsert` 10.0.0 (on NuGet, depends on `Microsoft.EntityFrameworkCore.Relational >= 10.0.0 && < 11.0.0`; uses "INSERT … ON CONFLICT DO UPDATE in PostgreSQL/Sqlite").

### Detecting and translating SQLSTATE 23505 in EF Core 10 / Npgsql 10

```csharp
public async Task<Guid> CreateAsync(WorkoutLog log, CancellationToken ct)
{
    _db.WorkoutLogs.Add(log);
    try
    {
        await _db.SaveChangesAsync(ct);          // single implicit transaction
        return log.Id;                            // first-write-wins: we won
    }
    catch (DbUpdateException ex)
        when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
              && pg.ConstraintName == "ux_workout_logs_user_idempotency")
    {
        // Replay path: someone (maybe us, on a retry) already inserted this key.
        // The failed SaveChanges rolled back its own implicit transaction,
        // so the context's connection is clean for a fresh read.
        var existingId = await _db.WorkoutLogs
            .AsNoTracking()
            .Where(w => w.UserId == log.UserId && w.IdempotencyKey == log.IdempotencyKey)
            .Select(w => w.Id)
            .SingleAsync(ct);
        return existingId;
    }
}
```

`PostgresErrorCodes.UniqueViolation` equals `"23505"`. Inspect `SqlState` (the documented, locale-stable five-character SQLSTATE) rather than parsing the message text. The `ConstraintName` guard (Npgsql's `PostgresException` exposes `ConstraintName` and `TableName` directly) ensures you only treat YOUR idempotency index as a replay — any other unique violation (a genuine bug) still throws.

### Sub-question 2 — "a failed attempt leaves the key reusable" under one atomic transaction

This property is **automatic and free** with Option (a)/(c), and it is the crucial behavioral difference from the handler path:

- **EF default (Microsoft EF Core Transactions docs):** "By default, if the database provider supports transactions, all changes in a single call to SaveChanges are applied in a transaction. If any of the changes fail, then the transaction is rolled back and none of the changes are applied to the database. This means that SaveChanges is guaranteed to either completely succeed, or leave the database unmodified if an error occurs." Because the idempotency key is a *column on the WorkoutLog row*, the key is persisted **if and only if** the row is persisted. If the create fails (validation, DB error, crash before commit), nothing is written — the key was never durably stored, so the client may safely retry with the **same** key. There is no orphaned marker to clean up.
- **Contrast with the handler path:** in a Wolverine handler, a Marten-backed `IdempotencyMarker` becomes co-transactional via the handler transaction bracket, so an in-handler LLM failure rolls the marker back together with the event append. That mechanism *requires the bracket*. On the pure-EF path the bracket does not exist — but you don't need it, because the marker and the fact are literally the same row in the same `SaveChanges`. The EF-native key achieves the identical "failed attempt → key reusable" guarantee with strictly less machinery.

One caveat: idempotency keys protect against duplicate *creation*, not against a successful-write-but-lost-response. If the row commits and the HTTP response is then lost, the client's retry correctly replays the prior `workoutLogId` (good). That is exactly the Stripe semantic (Stripe API Reference): "Stripe's idempotency works by saving the resulting status code and body of the first request made for any given idempotency key, regardless of whether it succeeds or fails. Subsequent requests with the same key return the same result, including 500 errors." With Option (a) you return the same id; with Option (b) you can return the byte-identical body. (Stripe also "suggest[s] using V4 UUIDs" for keys, matching the client-supplied `Guid` here.)

### Sub-question 3 — concurrent double-submit race (precise sequence)

Two simultaneous POSTs with the same `(UserId, IdempotencyKey)`, default `READ COMMITTED` isolation:

1. Request A and Request B both `INSERT` the same key. Each is its own implicit `SaveChanges` transaction on its own connection.
2. The unique index serializes them at the database. Whichever commits first **wins**; PostgreSQL makes the second `INSERT` **block** until the first transaction resolves (writers block writers), then:
   - If A commits, B's insert fails with **23505**.
   - (If A had rolled back, B would succeed and become the winner — also correct.)
3. B's `DbUpdateException`/`PostgresException(23505)` is caught. **Critical detail:** the 23505 aborted B's transaction. With EF's *implicit* single-`SaveChanges` transaction, that transaction is already rolled back by the time the exception surfaces, so B's `DbContext` connection is clean and the follow-up re-read runs in a **fresh implicit transaction** — and under `READ COMMITTED` it now sees A's committed row.
4. B re-reads by `(UserId, IdempotencyKey)` and returns A's `workoutLogId`. **First-write-wins, single id, zero duplicate rows.**

**Read-after-write / isolation considerations you must respect:**
- The re-read must NOT happen inside the same *poisoned* transaction. If you wrapped the insert in an **explicit** `BeginTransaction`, a raw 23505 leaves it in state `25P02` and any further command (including your SELECT) is rejected until rollback. EF Core mitigates this (Microsoft EF Core Transactions docs): "When SaveChanges is invoked and a transaction is already in progress on the context, EF automatically creates a savepoint before saving any data… If SaveChanges encounters any error, it automatically rolls the transaction back to the savepoint, leaving the transaction in the same state as if it had never started. This allows you to possibly correct issues and retry saving." So inside an explicit transaction the savepoint rollback un-poisons it and the re-read can proceed. **Simplest correct design: don't open an explicit transaction at all — let the single `SaveChanges` own the implicit transaction, and re-read afterward.**
- `READ COMMITTED` is sufficient and correct here: by the time B catches the violation, A has committed (that's *why* B saw the conflict), so B's subsequent committed-read is guaranteed to see A's row. Do not use `SERIALIZABLE` — PostgreSQL docs warn it "is possible to see unique constraint violations caused by conflicts with overlapping Serializable transactions even after explicitly checking that the key isn't present," which only adds retry complexity.

### EF Core 10 behavioral notes: change-tracker state after a failed SaveChanges

After a failed `SaveChanges`, the added `WorkoutLog` entity **remains tracked in the `Added` state** in the change tracker. If you reuse the same `DbContext` and call `SaveChanges` again, EF will try to re-insert it and fail again. Two robust options:
- **Preferred (matches the code above):** the catch block performs only a *read* and returns; it never calls `SaveChanges` again on that context, so the lingering `Added` entity is harmless and the context is disposed at end of request scope. Use `AsNoTracking()` on the re-read so the read isn't confused by the tracked, unsaved instance (an `Added` entity is still returned by tracked queries from the local cache even before it is persisted).
- **If you must continue writing on the same context:** detach the failed entity (`_db.Entry(log).State = EntityState.Detached;`) before proceeding. Microsoft's connection-resiliency guidance is more conservative still: for unrecoverable cases, "Discard the current DbContext. Create a new DbContext and restore the state of your application from the database." For a per-request scoped context that just returns the replayed id, discarding at scope end is automatic.

The DbContext is **not globally unusable** after a caught `DbUpdateException` from a unique violation — a subsequent *query* works fine (the implicit insert transaction has rolled back). It only becomes problematic if you retry the *same* unsaved insert. This is the well-documented "recover from DbUpdateException" pattern.

### Sub-question 4 — contract uniformity: is the EF-native key a "third store"?

The prior decision warned "don't add a third store." Verdict: **an EF-native idempotency key on the domain row is NOT a store.** A "store" is a separately-managed persistence concern with its own lifecycle (the Marten document-backed `IIdempotencyStore` for LLM handlers is one; a dedicated EF `IdempotencyRecord` table would be a second). Putting a `uuid` column + unique index on a row you are already inserting adds **zero** new lifecycle: no separate read, no separate write, no separate cleanup, no separate transaction. It is effectively free.

So you have a **deliberate, defensible two-mechanism split**, not a three-store sprawl:
- **Handler/LLM paths:** Marten-document-backed `IIdempotencyStore`, co-transactional via the Wolverine bracket. (Unchanged.)
- **Pure-EF WorkoutLog path:** idempotency key as a column on the fact, enforced by a unique index.

**Should you unify them behind one `IIdempotencyStore` with a Marten-backed and an EF-DbContext-backed implementation selected per call site?** You *can* — Wolverine 6 even supports `WithDbContextAbstraction<TAbstraction,TDbContext>()` to let a handler depend on an interface that a DbContext implements, emitting "a runtime cast at the top of the chain so SaveChangesAsync + outbox enrolment fire against the concrete OrdersDbContext underneath." But for WorkoutLog it is over-abstraction: a generic `IIdempotencyStore.CheckAndStore(...)` interface implies a *separate marker write*, which on the EF path either (i) reintroduces the dual-write if backed by Marten, or (ii) becomes a redundant second table when the row's own unique key already does the job. **Keep the call-site contract uniform at the level of "POST endpoints accept an `Idempotency-Key` and are safe to retry," but let each path use its native enforcement.** Do not manufacture a shared interface whose EF implementation would be a no-op wrapper around a column constraint. The honest abstraction here is the HTTP contract, not a storage interface.

### Counterfactual — cost of event-sourcing WorkoutLog instead

The one proven co-transactional path is a Marten event (`WorkoutLogged`) projected via `EfCoreSingleStreamProjection` (or an inline Marten projection), where Marten registers the DbContext as a transaction participant on the shared connection. Reversing the Final pure-EF decision to get "free" co-transactional idempotency would cost:

- **A stream identity you don't have.** Event sourcing wants a stable stream id. Prior findings establish there is *no stable prescribed-workout id* (whole-plan regeneration replaces ids), so you'd be inventing a synthetic stream key — and the natural candidate for that key is… the idempotency key, which means you're back to needing a unique key anyway.
- **Snapshot-on-log semantics.** WorkoutLog is an immutable fact captured by snapshotting the prescription at log time. Event sourcing adds an event-replay/projection layer on top of what is conceptually a single INSERT. You gain an audit log you don't need (the fact is already immutable) and pay rebuild/projection-daemon operational overhead.
- **A second persistence model for one endpoint.** You'd run Marten's event store *and* EF for one create path, plus the projection wiring, Weasel-managed migration of the projected table, and the async-daemon health-check surface — for a fact that has no behavior to aggregate.
- **It buys nothing the EF-native key doesn't.** Idempotency: the unique index already gives first-write-wins. Atomicity: a single `SaveChanges` is already atomic. Auditability: an immutable row is already an audit record. Replay: re-reading by key already returns the prior id.

Net: event-sourcing WorkoutLog is **strictly more expensive** (new stream-id concept, projection infrastructure, second persistence model, operational surface) for **no idempotency benefit**. Keep the pure-EF decision. The cost of maintaining the "second mechanism" (one column + one index + one catch block) is far lower than the cost of reversing to event sourcing.

### Atomicity + replay proof sketch (consolidated)
1. **Atomicity:** one `SaveChanges` ⇒ one implicit PostgreSQL transaction wrapping the single `INSERT` of the `WorkoutLog` row (which carries `IdempotencyKey`). The key cannot exist without the row and vice versa. ∎
2. **Replay returns the prior id:** the winning row is uniquely located by `(UserId, IdempotencyKey)`; `Id` is a column on that row; re-read returns it verbatim. ∎
3. **Race resolves to first-write-wins:** the unique index is the single serialization point; PostgreSQL admits exactly one committed row for the key; the loser catches 23505, its failed `SaveChanges` auto-rolls-back its implicit transaction (clean connection), and its `READ COMMITTED` re-read sees the winner's committed row. ∎
4. **Failed attempt is reusable:** a failed create rolls back the implicit transaction, so no key is durably written; the same key is free to retry. ∎

## Recommendations

**Stage 1 — Ship the EF-native key (do this now).**
1. Add `IdempotencyKey uuid` to `WorkoutLog` and a unique index `(user_id, idempotency_key)` via an EF migration. One column, one index, no new table. Use an app-generated `Guid` PK so the id is known before insert.
2. Implement the insert-then-catch-23505 pattern in `CreateAsync` exactly as sketched: catch `DbUpdateException` where inner is `PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }` and `ConstraintName == "ux_workout_logs_user_idempotency"`; on match, `AsNoTracking()` re-read by key and return the prior id.
3. Do **not** open an explicit transaction; let the single `SaveChanges` own its implicit transaction so the failed-insert auto-rollback leaves the connection clean for the re-read.
4. Require the client to send `Idempotency-Key` (a v4 `Guid`) on the POST; reject missing keys with 400 (or generate server-side if you prefer Stripe's lenient default).

**Stage 2 — Correct the spec.** Replace the erroneous "co-transactional Marten IdempotencyMarker pattern" language for this endpoint with the EF-native-key decision. Document explicitly that the Marten marker pattern applies only on handler/projection paths that own the shared connection.

**Stage 3 — Only if product requires full response replay.** If WorkoutLog POST must replay a byte-identical response body (not just the id), upgrade to Option (b): a dedicated `IdempotencyRecord` written in the *same* `SaveChanges`, with an app-generated GUID PK so the id is known before insert. Otherwise skip it.

**Consider `ON CONFLICT DO NOTHING` (Option c) if** profiling shows meaningful contention/exception volume from concurrent double-submits, since it avoids the throw/abort path entirely. Implement via raw SQL or `FlexLabs.EntityFrameworkCore.Upsert` 10.0.0.

**Thresholds that would change this recommendation:**
- If WorkoutLog ever gains a Wolverine handler or a Marten event side effect → revisit; the handler-bracket marker becomes available and may be preferred for consistency with other handler paths.
- If you must replay full HTTP responses → move to Option (b).
- If exception-rate from races becomes a hot path in telemetry → move to Option (c).

## Caveats
- **No 2PC re-confirmed but not exhaustively regression-tested on 9.x/6.x.** The conclusion rests on (a) Marten 9 EF Core projection docs still describing the shared-connection participant as projection-only, (b) the Wolverine 6.0 release notes describing it as a dependencies/AOT/cold-start release with no outbox-model change, and (c) Wolverine's outbox being inherently an eventual-consistency design. If a future point release introduces a general-purpose shared-connection co-commit for arbitrary controller writes, this analysis should be revisited.
- **No single official doc states "after a 23505 in your own explicit transaction you must roll back before reading."** That conclusion is synthesized from PostgreSQL's documented 25P02 abort behavior (PostgreSQL tutorial + pganalyze) plus Microsoft's documented EF auto-savepoint behavior. The synthesis is sound; the recommended design sidesteps it entirely by not opening an explicit transaction.
- **EF Core has no first-class ON CONFLICT API as of EF Core 10** (dotnet/efcore #16949 open since 2019); Option (c) requires raw SQL or a third-party upsert library.
- **Change-tracker hygiene matters.** Reusing a DbContext to retry the *same* failed insert will re-throw; the recommended pattern only reads after the catch, and per-request scoped contexts are disposed automatically.
- The metrics jsonb bag, prescription-snapshot resolution, and frontend retry UX are settled and out of scope.