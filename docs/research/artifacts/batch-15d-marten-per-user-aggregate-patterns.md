# Marten 2026 patterns for RunCoach's per-user plan aggregate

**Bottom line**: Pin **Marten 8.28** + **Wolverine 5.28** on **Npgsql 9**, use a **stream-per-user (Guid)** `Plan` aggregate with an **Inline single-stream projection** on a **conjoined multi-tenant** store in a dedicated `runcoach_events` schema, let **Wolverine's `[AggregateHandler]` workflow** own the command path with a transactional outbox that composes Marten + EF Core on the same Postgres, and leave the async daemon **Solo in MVP-0** / **Wolverine-managed distribution in MVP-1+**. Snapshots are per-append (free) and no additional cadence is needed at your volume. Everything below is concrete enough to drop into Slice 0 wiring.

The research confirmed that the Critter Stack in early 2026 has stabilized — Marten 8 shipped in June 2025, V9 is on the horizon (sync API removal, Npgsql 10) but with **no near-term breaking changes planned** per Jeremy Miller's April 2026 blog. The API you commit to now is the API you'll still be on six months out, barring a deliberate v9 upgrade window. The one live risk is that Marten 8.28 ships a `net10.0` TFM but the team has flagged .NET 10 as "untested" — budget for minor CI friction, not architectural rework.

---

## 1. Stream identity — user Guid, stream-per-user

**Recommendation: stream-per-user, stream id = `UserId` (Guid), default `StreamIdentity.AsGuid`.**

The four events (`PlanGenerated`, `PlanAdaptedFromLog`, `PlanRestructuredFromConversation`, `PlanRegenerated`) are all mutations of "the current plan," and your invariant is **one active plan per user**. In that setting, the user *is* the aggregate boundary of coaching state — a plan is just the current projected shape. `FetchForWriting<Plan>(userId)` becomes the entire command path; there is no cross-stream "which plan is active?" lookup, and the LLM projection key is the user id directly. The alternative (new `planId` per regeneration, with a separate "active plan pointer" doc) buys nothing at MVP-0 and multiplies race conditions around `PlanRegenerated`.

At ≤ hundreds of events per user per year, stream length is **an order of magnitude below** any snapshotting threshold the Marten community discusses. Oskar Dudycz (Marten core contributor) puts the breakeven for snapshot cost-benefit around **1,000–10,000 events per stream**; Marten's own docs say live aggregation "is perfectly appropriate for short streams, but maybe a performance issue in longer event streams." You're nowhere near the pain zone.

If historical plan archives become a real feature later, you have a clean migration path: add a `PlanArchived` event and either (a) keep stream-per-user with `UseArchivedStreamPartitioning = true` for hot/cold separation, or (b) bump `ProjectionVersion` and switch to stream-per-plan behind a new projection table using the blue-green recipe from Miller's March 2025 zero-downtime-deployments post. **The choice now does not constrain that.**

**Guid vs string vs derived**: plain Guid. Strongly-typed stream id wrappers are still a friction point in Marten 8 (aggregate-document strong-typed ids work; stream-id wrappers remain Guid/string under the hood). Don't derive a Guid by hashing `userId` — just use the user id Guid directly. Strings make `mt_events.stream_id` a `varchar`, measurably slower on pk lookup and rebuild.

**Projection rebuild cost**: with `UseOptimizedProjectionRebuilds = true` (default-on in v8), rebuilds go **stream-by-stream in reverse-recency order** — active users get current data fast, stale users trickle. At 10k users × ~200 events ≈ 2M events, a full rebuild is IO-bound minutes on modest Postgres, not hours. The optimized-rebuild flag requires **exactly one single-stream projection per stream type** — RunCoach satisfies that.

---

## 2. Projection strategy — Inline wins because reads dominate

**Recommendation: `SnapshotLifecycle.Inline` (or `ProjectionLifecycle.Inline` on the separate `PlanProjection` class).**

Marten 7+ offers three lifecycles: **Inline** (projection computed and persisted in the same transaction as the event append), **Async** (daemon tails the event log, eventual consistency), and **Live** (no persistence, fold on read). Jeremy Miller's general guidance is to run multi-stream and enriched projections async; for a **simple single-stream projection with no enrichment**, Inline is the Marten-tutorial default and what the Wolverine aggregate-handler samples ship.

For RunCoach specifically, the access pattern tilts the decision hard toward Inline:

- **Reads dominate.** The LLM reads the current plan document on every coaching call — many more reads than event appends. Inline means every read is a single pk lookup on `mt_doc_plan`; no replay, no daemon-lag staleness. With `Marten.AspNetCore`'s `session.Events.WriteLatest<Plan>(userId, HttpContext)`, the JSONB streams straight from Postgres to the HTTP response body with **zero deserialize/reserialize** — ideal when the consumer is another service feeding an LLM context window.
- **The write tax is negligible.** With `UseIdentityMapForAggregates = true` plus `FetchForWriting → SaveChangesAsync → FetchLatest`, the aggregate load is reused, so the effective tax is one extra `UPDATE` in the already-open transaction. At hundreds of events per user per year, this is rounding error.
- **No daemon to reason about.** Inline means no async daemon for the `Plan` projection, which means no leader election, no HWM stalls, no Solo-on-multi-replica corruption risk. That simplifies the MVP-0 → MVP-1 hosting transition.

If write-path P95 ever gets dominated by projection JSON serialization (unlikely at this plan-doc size), **the switch to Async is a one-line change** — `FetchLatest` wallpapers over the lifecycle difference, so call sites don't move.

**Do not use Live aggregation as the default.** It's fine for ad-hoc queries, but the LLM-feed pattern wants the Inline-persisted doc for zero-copy JSON streaming.

---

## 3. Async daemon for multi-instance hosting

**Recommendation: `DaemonMode.Solo` in MVP-0; switch to Wolverine-managed distribution at MVP-1+.** For RunCoach this is mostly about *other* projections you may add later (streaks, leaderboards) — the `Plan` projection is Inline and doesn't need the daemon at all.

Marten 8's daemon modes:

- `Disabled` — no daemon.
- `Solo` — all projection shards on one node; assumes exactly one node running. Fastest spin-up. Use in MVP-0, local dev, and tests.
- `HotCold` — Postgres advisory-lock leader election per projection shard. **Exactly one process owns a given shard.**

The core Marten warning on HotCold is blunt: *"The built in capability of Marten to distribute projections is somewhat limited, and it's still likely that all projections will end up running on the first process to start up."* For real multi-node balancing JasperFx points at either the commercial Critter Stack Pro add-on or **Wolverine-managed distribution**, which "does not depend on advisory locks and spreads work out more evenly through a cluster." Since Wolverine is already in your stack, **enable `UseWolverineManagedEventSubscriptionDistribution = true` on `IntegrateWithWolverine(...)`** when you go multi-replica.

**Load-bearing knobs** (all fine at defaults for your volume):
- `EventAppendMode.Quick` — recommended from day one; ~2× faster appends and reduces sequence gaps that cause HWM stalls. Gotcha: `IEvent.Version`/`IEvent.Sequence` aren't populated during Inline projection execution under Quick mode; if your `Apply` methods need Version, either stay on `Rich` or add `IRevisioned` so Marten back-fills on load. For the `Plan` projection, don't write apply logic that depends on `Version`.
- `UseIdentityMapForAggregates = true` — perf for single-stream aggregate handlers + `FetchLatest`.
- `EnableAdvancedAsyncTracking = true` — writes `mt_high_water_skips` for audit; prerequisite for CritterWatch.
- `Projections.Errors.SkipUnknownEvents = true` for continuous mode — essential for blue/green deploys where an old node sees a new event type.

**PgBouncer interaction**: advisory locks are session-scoped, and PgBouncer in transaction-pooling mode breaks them — duplicate daemons appear. Keep Marten's daemon connection on direct or session pooling only.

**OTel metrics to alert on**: `marten.{projection}.gap` histogram (growing = falling behind), `marten.{projection}.skipped` counter (> 0 means data loss), `marten.daemon.highwatermark` span status.

**In-process vs separate worker**: in-process is fine at your scale. Split to a separate `dotnet run -- projections` worker only when daemon CPU/memory visibly competes with API work — not a Slice-0 concern.

---

## 4. Snapshot policy — don't tune, let Inline snapshot per append

**Recommendation: rely on Marten's default per-append snapshotting via `SnapshotLifecycle.Inline`. No custom cadence.**

Marten 8's "snapshot" vocabulary aligned with the Decider paper: a snapshot is simply "a persisted version of the aggregate projection." There is **no built-in every-N-events config** — if you want that, you fall back to `AggregateStreamAsync` with manual state management, which Oskar Dudycz's cookbook covers but explicitly warns against as a first choice.

At your volume (hundreds of events per stream per year), two facts collapse the decision:
1. Live fold over 200 events is microseconds, not milliseconds. Folding cost is irrelevant.
2. You want Inline anyway (§2) because reads dominate and you want `WriteLatest` zero-copy JSON streaming.

Inline's per-append snapshot is therefore **already the optimal policy**. Revisit only if per-user event counts cross ~5,000/year, at which point `opts.Projections.CacheLimitPerTenant = 1000` (aggregate cache) starts earning its keep.

---

## 5. Schema and multitenancy — dedicated schema + Conjoined

**Recommendation:**
- **Schema name**: `opts.DatabaseSchemaName = "runcoach_events"` (and `opts.Events.DatabaseSchemaName = "runcoach_events"` for clarity). Keep EF Core on `public`.
- **Multitenancy**: `TenancyStyle.Conjoined` with `tenant_id = user_id` (string form of the user Guid).

**Why a dedicated, project-scoped schema**: Marten's default is `public`, which collides with EF Core. A separate schema makes Weasel's diffing unambiguous, makes `pg_dump --schema=` trivial, and future-proofs against a second Marten store via `AddMartenStore<T>()`. `runcoach_events` beats the generic `marten` — the Marten tutorials themselves use project-scoped names (`incidents`, `orders`, `payment_service`) and there is **no JasperFx convention to call it `marten`**. The schema name is visible in error messages, CLI output, generated SQL logs, `pg_dump`, OTel tags — so it leaks to developers and DBAs, though never to end users, and nothing about "Marten" as a brand requires reserving that identifier. Marten 8.28 explicitly improved coexistence with EF Core's `OwnsOne().ToJson()` so Weasel no longer emits spurious diffs when sharing a DB — another reason to pin 8.28+.

**Why Conjoined for per-user multitenancy**: schema-per-tenant is **explicitly out of scope** in Marten (Miller: *"very unlikely to ever be supported…Marten compiles database schema names into generated code"*). Database-per-tenant at thousands of users means thousands of Postgres databases — untenable for connection pools, migrations, and `pg_hba.conf`. Conjoined adds a `tenant_id varchar` column to every Marten table, indexed automatically, and scopes sessions via `store.LightweightSession(userId)`. Marten bakes tenant_id into the composite primary key so the same document id can coexist across tenants. Scales comfortably to millions of rows.

**One watchpoint**: the conjoined primary-key ordering defaults to `PrimaryKeyTenancyOrdering.Id_Then_TenantId` for V7/V8 compatibility but **flips to `TenantId_Then_Id` in V9**. For thousands of small per-user tenants, skip explicit list partitioning in MVP-0 — add it later if one tenant grows hot.

---

## 6. Aggregate definition style — Decider + separate projection class

**Recommendation: `Plan` is an immutable `record`; `PlanProjection : SingleStreamProjection<Plan, Guid>` is a separate projection class; command logic lives in a pure static `PlanDecider` used by Wolverine `[AggregateHandler]` methods.**

Marten 8 supports four shapes — live-only, self-aggregating snapshot (apply methods on the record itself), separate `SingleStreamProjection<TDoc, TId>` class, and the newer "Evolve" explicit style. Jeremy Miller's 2025-2026 writing consistently pushes toward the **FP / Decider approach** over aggregate-root-with-methods: *"I strongly prefer and recommend the FP 'Decider' approach…folks using the older 'Aggregate Root' approach tend to have more runtime bugs."* This is also what makes `UseIdentityMapForAggregates = true` safe — that flag is documented as unsafe when code mutates the aggregate outside Marten's pipeline.

For a doc primarily consumed by an LLM, the separate-projection pattern has an extra win: the projection's JSON shape is **decoupled from domain types used in commands**. You can rename internal fields, add LLM-specific annotations, or reshape for prompt-engineering purposes without touching event schemas or command DTOs.

The self-aggregating record with `Apply` methods (option 2) is still idiomatic in quickstart samples and fine for demo code; for production with an LLM consumer, separate is cleaner.

**Sketch** (this is the Slice 1 skeleton):

```csharp
// Events — immutable records, kept in a Contracts project that can be referenced by both handlers and projection
public sealed record PlanGenerated(MacroPhaseSchedule Macro, MesoTemplate ThisWeek, DayPrescription? ActiveDay);
public sealed record PlanAdaptedFromLog(Guid WorkoutLogId, string Reason,
    MesoTemplate UpdatedThisWeek, DayPrescription? UpdatedActiveDay);
public sealed record PlanRestructuredFromConversation(Guid ConversationTurnId, string Reason,
    MacroPhaseSchedule? UpdatedMacro, MesoTemplate UpdatedThisWeek);
public sealed record PlanRegenerated(MacroPhaseSchedule Macro, MesoTemplate ThisWeek, DayPrescription? ActiveDay);

// Projection document — the LLM-consumable shape
public sealed record Plan(
    Guid Id,                 // = userId = stream id
    int Version,             // filled by Marten via IRevisioned convention
    MacroPhaseSchedule Macro,
    MesoTemplate ThisWeek,
    DayPrescription? ActiveDay,
    DateTimeOffset GeneratedAt);

// Projection class — evolve logic lives here, not on the record
public sealed class PlanProjection : SingleStreamProjection<Plan, Guid>
{
    public PlanProjection()
    {
        IncludeType<PlanGenerated>();
        IncludeType<PlanAdaptedFromLog>();
        IncludeType<PlanRestructuredFromConversation>();
        IncludeType<PlanRegenerated>();
    }

    public static Plan Create(IEvent<PlanGenerated> e) =>
        new(e.StreamId, (int)e.Version, e.Data.Macro, e.Data.ThisWeek, e.Data.ActiveDay, e.Timestamp);

    public Plan Apply(PlanAdaptedFromLog e, Plan current) =>
        current with { ThisWeek = e.UpdatedThisWeek, ActiveDay = e.UpdatedActiveDay };

    public Plan Apply(PlanRestructuredFromConversation e, Plan current) =>
        current with { Macro = e.UpdatedMacro ?? current.Macro, ThisWeek = e.UpdatedThisWeek };

    public Plan Apply(IEvent<PlanRegenerated> e, Plan _) =>
        new(e.StreamId, (int)e.Version, e.Data.Macro, e.Data.ThisWeek, e.Data.ActiveDay, e.Timestamp);
}

// Pure decider — no Marten types, fully unit-testable
public static class PlanDecider
{
    public static IEnumerable<object> AdaptFromLog(Plan current, WorkoutLoggedCommand cmd, AdaptationAssessment assessment)
    {
        if (!assessment.RequiresAdaptation) yield break;
        yield return new PlanAdaptedFromLog(cmd.WorkoutLogId, assessment.Reason,
            assessment.UpdatedThisWeek, assessment.UpdatedActiveDay);
    }
}
```

---

## 7. Projection type for LLM consumption and schema evolution

**Recommendation: strongly-typed `record Plan(…)`, not `JsonDocument` / `JObject` / raw string.** Marten requires projection documents to be **public CLR types** for code-generation; records serialize cleanly through both Newtonsoft and System.Text.Json. You get compile-time safety on the LLM contract, LINQ access, and tractable upcasting. `JsonDocument` as a **property** of a typed record is fine for one "free-form" LLM-annotation field; `JsonDocument` as the entire projected doc defeats Marten's machinery.

**For the LLM feed endpoint, use zero-copy JSON streaming**:

```csharp
app.MapGet("/users/{userId:guid}/plan",
    (Guid userId, IDocumentSession session, HttpContext http) =>
        session.Events.WriteLatest<Plan>(userId, http));
```

`Marten.AspNetCore`'s `WriteLatest` copies JSONB from Postgres straight to the response body with no deserialize/reserialize. This is Inline-optimal and is the specific reason I'd keep Inline lifecycle even if writes became slightly more expensive.

**Schema evolution in two tracks**:

*Event evolution* (immutable past): use Marten's upcasting — `opts.Events.Upcast<OldEvent, NewEvent>(old => new NewEvent(...))` or the raw-JSON overload. Critical gotcha: **renaming a CLR event type without `opts.Events.MapEventType<NewName>("old_alias")` silently returns zero rows** — no exception, just mystery empty streams. Register aliases whenever you rename.

*Projection evolution* (the LLM doc shape):
- **Additive** (new field with default): update `PlanProjection.Apply`, deploy, rebuild via `await daemon.RebuildProjectionAsync("PlanProjection", ct)` or CLI `dotnet run -- projections --rebuild -p PlanProjection`. With `UseOptimizedProjectionRebuilds = true`, rebuild runs stream-by-stream reverse-recency — active users current within minutes.
- **Breaking** (rename/remove/semantic shift): bump `ProjectionVersion = 2` inside `PlanProjection`; Marten writes v2 to `mt_doc_plan_2`; run as Async so v1 keeps serving reads; cut over when caught up; decommission v1. This is the official **zero-downtime blue/green** recipe from Miller's March 2025 post.

**Other gotchas to encode now**: keep `Plan` immutable (supports `UseIdentityMapForAggregates`); initialize all state inside `Apply`/`Create` because Marten may bypass field initializers; test rebuild in CI because custom serializer converters can hang rebuilds at non-deterministic points.

---

## 8. Daemon vs Wolverine — use the aggregate handler workflow

**Recommendation: Controller (or Wolverine.HTTP endpoint) → Wolverine `[AggregateHandler]` method → events appended by Wolverine middleware → outbound side effects dispatched via Wolverine's durable outbox atomically with the Marten transaction.** Pattern (a) from the sub-question, concretely.

The Wolverine Aggregate Handler Workflow wraps Marten's `FetchForWriting<T>` in middleware so your handler is a **pure Decider function** that receives the command + current aggregate and yields events. Wolverine generates the surrounding code: open outboxed `IDocumentSession`, `FetchForWriting<Plan>(cmd.UserId, cmd.Version)`, invoke handler, `AppendMany`, `SaveChangesAsync`. Outgoing messages the handler returns are dispatched through the same transaction via Wolverine's outbox.

**Why not pattern (b)** (controller appends + subscription reacts): Marten's docs explicitly recommend choosing either **subscriptions** or **event forwarding** — not both in the same app — and subscriptions require the async daemon to be running plus Wolverine-managed distribution for multi-node. Your `Plan` projection is Inline and doesn't need the daemon at all; layering a subscription-based reaction flow adds daemon dependency you don't otherwise have. The `[AggregateHandler]` approach is the one JasperFx itself ships in CQRS tutorials and is where all the recent feature work landed (Wolverine 5.17's `AlwaysEnforceConsistency`, 5.x multi-stream aggregate handlers, strong-typed ids, `[ReadAggregate]`/`[WriteAggregate]`).

**Slice 3 sketch** (workout log → plan adaptation → LLM coaching):

```csharp
public record LogWorkoutCommand(Guid UserId, int? Version, WorkoutLogDto Log);

public static class LogWorkoutHandler
{
    // Load EF-side dependencies via Wolverine compound handler convention
    public static async Task<AdaptationAssessment> LoadAsync(
        LogWorkoutCommand cmd, AppDbContext db, IPlanAdaptationService svc, CancellationToken ct)
    {
        var log = new WorkoutLog(cmd.UserId, cmd.Log);
        db.WorkoutLogs.Add(log);
        // SaveChangesAsync is not called here — Wolverine EF middleware handles it
        return await svc.AssessAsync(cmd.UserId, log, ct);
    }

    [AggregateHandler]
    public static (IEnumerable<object> Events, OutgoingMessages Outbound) Handle(
        LogWorkoutCommand cmd, Plan plan, AdaptationAssessment assessment)
    {
        var events = PlanDecider.AdaptFromLog(plan, cmd, assessment).ToList();
        var outbound = new OutgoingMessages();
        if (events.Count > 0)
            outbound.Add(new TriggerCoachingCall(cmd.UserId, assessment.Reason));
        return (events, outbound);
    }
}

// Separately — the LLM call handler; runs on a durable local queue
public static class CoachingCallHandler
{
    public static async Task Handle(TriggerCoachingCall msg, ILlmClient llm,
        IQuerySession session, CancellationToken ct)
    {
        var plan = await session.Events.FetchLatest<Plan>(msg.UserId, token: ct);
        await llm.CoachAsync(msg.UserId, plan!, msg.Reason, ct);
    }
}
```

The LLM call is **not** in the command transaction — it's a durable outbound message. If it fails, Wolverine retries via the inbox; if it succeeds, you've already persisted the event and the log. This avoids blocking the HTTP response on external AI latency and is resilient to LLM provider hiccups.

---

## 9. Testing with xUnit v3 + Testcontainers

**Recommendation**: **one shared `AlbaHost` per test assembly** via an xUnit v3 collection fixture, **`MartenDaemonModeIsSolo()` forced on the test host**, and **`Host.ResetAllMartenDataAsync()` between tests**. This is the pattern Miller explicitly blessed in the August 2025 post on faster integration tests.

Why not per-test teardown with `Advanced.Clean.CompletelyRemoveAll()` or Respawn:
- `CompletelyRemoveAll()` drops + recreates schema objects — very slow.
- Respawn works for relational schemas but doesn't know about Marten's event-progression bookkeeping.
- `ResetAllMartenDataAsync()` specifically disables projections, truncates documents + events, re-runs `InitialData`, and restarts projections — **order of magnitude faster** and keeps the daemon state consistent.

Why Solo daemon for tests: advisory-lock contention across parallel test classes is a known test flake source. `services.MartenDaemonModeIsSolo()` (Marten 8.8+) forces Solo mode regardless of production config, making projection timing deterministic. Pair with `store.WaitForNonStaleProjectionDataAsync(5.Seconds())` when testing Async projections, or (better, for RunCoach) keep the `Plan` projection Inline so tests don't need the wait helper at all.

**xUnit v3 fixture sharp edges** to know about: Issue #3249 (April 2025) reported fixture `InitializeAsync`/`DisposeAsync` don't always fire in v3 — if bitten, have the test class itself implement `IAsyncLifetime`. Issue #3437 (November 2025) noted `[assembly: CaptureConsole]` doesn't capture logs from fixture init/dispose. Both remain live. The workaround is to keep fixture init minimal and do per-test state work in the class's `IAsyncLifetime.InitializeAsync`.

**Fixture sketch**:

```csharp
public sealed class AppFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").Build();
    public IAlbaHost Host { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        await Postgres.StartAsync();
        Host = await AlbaHost.For<Program>(b =>
        {
            b.UseSetting("ConnectionStrings:Postgres", Postgres.GetConnectionString());
            b.ConfigureServices(services => services.MartenDaemonModeIsSolo());
        });
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        await Postgres.DisposeAsync();
    }
}

[CollectionDefinition(nameof(AppCollection))]
public sealed class AppCollection : ICollectionFixture<AppFixture> { }

[Collection(nameof(AppCollection))]
public abstract class IntegrationContext : IAsyncLifetime
{
    protected IntegrationContext(AppFixture fx) => Fixture = fx;
    protected AppFixture Fixture { get; }
    public ValueTask InitializeAsync() => new(Fixture.Host.ResetAllMartenDataAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class PlanProjectionTests(AppFixture fx) : IntegrationContext(fx)
{
    [Fact]
    public async Task PlanGenerated_creates_initial_plan()
    {
        var store = Fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString()); // Conjoined tenant
        var userId = Guid.NewGuid();
        session.Events.StartStream<Plan>(userId, new PlanGenerated(Macro, Meso, Day));
        await session.SaveChangesAsync();

        var plan = await session.Events.FetchLatest<Plan>(userId);
        plan.ShouldNotBeNull();
        plan.Macro.ShouldBe(Macro);
    }
}
```

**Projection unit tests** (no Postgres): test the Decider pure functions directly. Integration tests exercise the full `StartStream → SaveChanges → FetchLatest` path to catch serializer issues and projection registration bugs.

---

## 10. Version migration risk — low, but .NET 10 is "untested"

The current stable is **Marten 8.28.0** (March 31, 2026) and **Wolverine 5.28.0** (April 7, 2026) in a coordinated release. Miller's April 2026 blog explicitly signals no major near-term features planned — **API is stabilized**, focus has shifted to CritterWatch observability and scaling research.

**Breaking changes 7→8** you may have to reckon with if you inherited older docs/samples:
- Almost all sync DB APIs removed — `LoadAsync` everywhere.
- `JasperFx` / `JasperFx.Events` reorg — `IEvent`, `StreamAction` moved; `Oakton` subsumed by `JasperFx`; `Marten.CommandLine` merged into core Marten (remove any explicit package reference).
- `SingleStreamProjection<TDoc, TId>` now takes two generic args.
- Custom `IProjection` name defaults to type name, not full name (dashboard lookup changes).
- Npgsql 9 required; Postgres 13+; .NET 8/9 officially supported.

**Marten 9 is on the horizon** but undated: it will drop remaining sync LINQ ops (tied to Npgsql 10 dropping sync) and will flip conjoined primary-key ordering to `TenantId_Then_Id`. Neither is a rewrite risk — sync removal is mechanical, and the PK reorder is a one-time index-rebuild migration. No other load-bearing changes signaled.

**The one live risk**: Marten 8.28 ships a `net10.0` TFM in its NuGet metadata but the team has flagged .NET 10 as "untested." Plan for minor CI friction (occasional transitive dependency ceremony, maybe one or two bug reports). If you hit a hard blocker, target the Marten-hosting project at `net9.0` while keeping the API project on `net10.0` — Marten doesn't require the host project to be on the latest TFM.

**Npgsql compatibility watchpoint**: Marten 8 pulls Npgsql 9. If `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x brings Npgsql 10 transitively, you'll get version conflicts. Verify the transitive graph after package restore; if conflicted, hold the EF provider at the last Npgsql-9-compatible version until Marten 9 ships.

---

## 11. Schema name — `runcoach_events`

**Recommendation: `runcoach_events`.** Project-scoped, lowercase-with-underscores (idiomatic Postgres), avoids the generic `marten` name that would collide if you later split to multiple `IDocumentStore`s. JasperFx has **no convention** mandating `marten`; their own tutorials use project names (`incidents`, `orders`, `payment_service`). Schema name is visible to developers, DBAs, and ops (error messages, CLI output, generated SQL logs, `pg_dump`, OTel tags), so project-scoping pays off in readability. Nothing in end-user contexts is affected. Marten's hardcoded `mt_` table prefix stays — so actual tables become `runcoach_events.mt_events`, `runcoach_events.mt_streams`, `runcoach_events.mt_doc_plan`, etc.

---

## 12. Cross-store consistency — Wolverine outbox composing Marten + EF Core

**Recommendation: one logical unit of work per command, run as a Wolverine handler with `IntegrateWithWolverine()` (Marten) + `AddDbContextWithWolverineIntegration<AppDbContext>()` (EF Core) + `AutoApplyTransactions()`. The LLM call is scheduled through Wolverine's durable outbox, not executed in-band.**

The Critter Stack integration story in 2026 is now coherent enough that this is **a first-class pattern**, not a workaround:

- `AddMarten(...).IntegrateWithWolverine()` registers Wolverine's envelope storage in Marten's schema (or, for modular-monolith setups, a separate `MessageStorageSchemaName = "wolverine"`), gives Marten as saga storage, and wires Wolverine's transactional middleware around `IDocumentSession`.
- `AddDbContextWithWolverineIntegration<AppDbContext>()` (from `WolverineFx.EntityFrameworkCore`) maps Wolverine envelope storage into your EF `DbContext` via `modelBuilder.MapWolverineEnvelopeStorage()`, enabling command batching when Wolverine flushes outbound messages alongside `SaveChangesAsync`.
- With both in place and `opts.Policies.AutoApplyTransactions()`, Wolverine's middleware opens one transaction per handler, flows it through both the Marten session and the EF `DbContext` (they share the Postgres connection on the same DB), commits atomically, then releases outbound messages from the outbox.

This is **single-Postgres transactional** for EF + Marten. It is **not** two-phase commit (you're on one database, so 2PC isn't needed), and it is **not** eventual consistency between EF and Marten (both commit together). Eventual consistency is introduced deliberately for the **external** side effect — the LLM call goes through the outbox and runs post-commit on a durable queue, so AI provider latency doesn't block the HTTP response and provider failures don't roll back your plan-adaptation write.

**Wiring sketch** (this is the Slice 0 Program.cs reference):

```csharp
var builder = WebApplication.CreateBuilder(args);
var pg = builder.Configuration.GetConnectionString("Postgres")!;

// EF Core — relational state, Identity, UserProfile, WorkoutLog, ConversationTurn
builder.Services.AddDbContextWithWolverineIntegration<AppDbContext>(
    opts => opts.UseNpgsql(pg, npg => npg.MigrationsHistoryTable("__ef_migrations", "public")),
    optionsLifetime: ServiceLifetime.Singleton);

// Marten — event-sourced Plan aggregate
builder.Services
    .AddMarten(opts =>
    {
        opts.Connection(pg);
        opts.DatabaseSchemaName = "runcoach_events";
        opts.Events.DatabaseSchemaName = "runcoach_events";

        // Identity + multitenancy
        opts.Events.StreamIdentity = StreamIdentity.AsGuid;
        opts.Events.TenancyStyle = TenancyStyle.Conjoined;
        opts.Policies.AllDocumentsAreMultiTenanted();

        // Performance / correctness defaults
        opts.Events.AppendMode = EventAppendMode.Quick;
        opts.Events.UseIdentityMapForAggregates = true;
        opts.Events.UseOptimizedProjectionRebuilds = true;      // default in v8, be explicit
        opts.Events.UseArchivedStreamPartitioning = true;       // future-proof
        opts.Events.EnableAdvancedAsyncTracking = true;         // OTel / CritterWatch readiness

        // Error handling — essential for blue/green rollouts
        opts.Projections.Errors.SkipUnknownEvents = true;

        // Projections — add in Slice 1
        // opts.Projections.Add<PlanProjection>(ProjectionLifecycle.Inline);

        // Production hardening
        opts.DisableNpgsqlLogging = true;
    })
    .UseLightweightSessions()
    .UseNpgsqlDataSource()
    .IntegrateWithWolverine()
    .AddAsyncDaemon(
        builder.Environment.IsDevelopment()
            ? DaemonMode.Solo
            : DaemonMode.HotCold); // MVP-1+: add UseWolverineManagedEventSubscriptionDistribution on IntegrateWithWolverine

// Dev/MVP-0 only — in prod, run `dotnet run -- marten-apply` from CI and replace with AssertDatabaseMatchesConfigurationOnStartup
if (builder.Environment.IsDevelopment())
    builder.Services.AddMartenStore<IDocumentStore>().ApplyAllDatabaseChangesOnStartup();

// Wolverine
builder.Host.UseWolverine(opts =>
{
    opts.Services.AddScoped<IDocumentSession>(sp =>
    {
        var http = sp.GetRequiredService<IHttpContextAccessor>();
        var tenant = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("No tenant on request");
        return sp.GetRequiredService<IDocumentStore>().LightweightSession(tenant);
    });

    opts.Policies.AutoApplyTransactions();
    opts.Policies.UseDurableLocalQueues();
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();
app.MapWolverineEndpoints();
app.Run();
```

---

## Failure-mode catalog

The recommended setup has five main failure modes to monitor and rehearse:

**Multi-instance daemon race**. `DaemonMode.Solo` on more than one replica = silent projection corruption through concurrent upsert. Always use `HotCold` (advisory-lock leader election) or Wolverine-managed distribution for multi-replica. PgBouncer in transaction-pooling mode breaks advisory locks — use session pooling or direct connections for the daemon. Detection: duplicate daemon log entries, unexplained projection progression resets. Response: verify `DaemonMode` config, check PgBouncer mode.

**Daemon stuck (HWM frozen)**. Symptom: `"High Water agent is stale after threshold"` log entry, `marten.daemon.highwatermark` span stuck, growing `marten.{projection}.gap` histogram. Causes: long-running transactions leaving sequence gaps, Postgres failover, lost advisory lock. Mitigation: `EventAppendMode.Quick` from day one reduces gap frequency; `EnableAdvancedAsyncTracking` logs skipped seqs to `mt_high_water_skips`. Recovery: restart the daemon node (HotCold migrates the lock); if permanently stuck, inspect `mt_event_progression`, rebuild the affected projection with `dotnet run -- projections --rebuild -p PlanProjection`.

**Schema drift**. Code-vs-DB mismatch. Detection: Marten's Weasel subsystem detects automatically if `AssertDatabaseMatchesConfigurationOnStartup()` is on. Prod workflow: `AutoCreateSchemaObjects = None` + `dotnet run -- marten-apply` as a CI/CD migration step + assert-on-startup in app. Dev/MVP-0: `ApplyAllDatabaseChangesOnStartup()` is fine. Never `AutoCreateSchemaObjects = All` in prod — it drops tables.

**Projection corruption**. Data in `mt_doc_plan` diverges from event log (rare but possible on buggy `Apply` methods or serializer regressions). Rebuild: `await daemon.RebuildProjectionAsync<PlanProjection>(5.Minutes(), ct)` or CLI `dotnet run -- projections --rebuild -p PlanProjection`. During rebuild, error-skip flags flip to non-skipping so the first bad event halts — that's deliberate. For multi-tenant, `--tenant <id>` narrows the rebuild.

**Event schema drift and silent zero rows**. Renaming a CLR event without `MapEventType<NewName>("old_alias")` returns zero events with no exception. Renaming projection documents requires either `ProjectionVersion` bump or explicit alias migration. Unit tests covering rename-paths catch this before prod.

**Poison events**. Marten parks serialization-failed and apply-failed events in `mt_doc_deadletterevent` with `SkipSerializationErrors` / `SkipApplyErrors` true for continuous mode. Set up an alert on `COUNT(*) FROM runcoach_events.mt_doc_deadletterevent > 0`. Draining procedure: fix the projection or add an upcaster, then `RebuildProjectionAsync`.

**EF + Marten Npgsql conflict**. EF Core 10 provider may pull Npgsql 10 transitively while Marten 8 requires Npgsql 9. Detection: runtime binding errors or startup failures. Response: pin the EF provider to its last Npgsql-9-compatible build; revisit when Marten 9 ships.

---

## Library version pins

Target **Marten 8.28** and **Wolverine 5.28** as a coordinated pair — JasperFx explicitly releases them together.

```xml
<ItemGroup>
  <!-- Marten -->
  <PackageVersion Include="Marten" Version="8.28.0" />
  <PackageVersion Include="Marten.AspNetCore" Version="8.28.0" />
  <PackageVersion Include="Marten.NodaTime" Version="8.28.0" /> <!-- if using NodaTime -->

  <!-- Wolverine -->
  <PackageVersion Include="WolverineFx" Version="5.28.0" />
  <PackageVersion Include="WolverineFx.Marten" Version="5.28.0" />
  <PackageVersion Include="WolverineFx.EntityFrameworkCore" Version="5.28.0" />
  <PackageVersion Include="WolverineFx.Http" Version="5.28.0" />
  <PackageVersion Include="WolverineFx.Http.Marten" Version="5.28.0" />

  <!-- EF Core 10 + Postgres -->
  <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
  <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />

  <!-- Npgsql — Marten 8 pins 9.x; verify transitive resolution -->
  <PackageVersion Include="Npgsql" Version="9.0.4" />

  <!-- Testing -->
  <PackageVersion Include="xunit.v3" Version="3.0.0" /> <!-- or latest v3 -->
  <PackageVersion Include="Testcontainers.PostgreSql" Version="4.0.0" /> <!-- or latest -->
  <PackageVersion Include="Alba" Version="8.0.0" /> <!-- coordinated with Wolverine/Marten -->
</ItemGroup>
```

**Do not** add `Marten.CommandLine` as a separate package — it merged into `Marten` in v8. **Do not** mix Marten 7 and Marten 8 transitive references — the `JasperFx` / `JasperFx.Events` reorg will break.

---

## Conclusion — the shape of Slice 0

Slice 0's Marten wiring lands on a narrow, defensible default: stream-per-user Guid, Inline single-stream projection, Conjoined multitenancy, `runcoach_events` schema, Wolverine-integrated command path with durable outbox. Every element of this configuration is **explicitly blessed by recent JasperFx writing** (Miller's 2025-2026 posts on FP-decider projections, optimized rebuilds, zero-downtime projection versioning, Wolverine-managed distribution) and is the same shape the Critter Stack tutorials ship.

The interesting forward-looking move is to commit to the **Wolverine `[AggregateHandler]` workflow** from day one rather than writing hand-rolled controller code that appends events. It costs almost nothing to adopt, gives you automatic optimistic concurrency, automatic outbox composition with EF Core, and a painless multi-replica path via Wolverine-managed distribution — all features that would otherwise be retrofitted under pressure at MVP-1+. The LLM-call-via-outbox pattern is the second non-obvious win: it turns a tricky "save + append + external AI" transaction into two cleanly isolated concerns (durable local state, durable outbound dispatch) without introducing eventual consistency inside your own data.

Two real risks to watch: **.NET 10 "untested" status on Marten 8.28** (budget CI friction, not rework) and the **Npgsql 9/10 transitive pin** between Marten 8 and the EF Core 10 provider (verify at package restore). Neither is an architectural hazard — both are fixable at the package-pin layer. Marten 9 is coming but not imminent, and nothing in its announced scope forces decisions different from the ones above.

Ship Slice 0 with this wiring, add `PlanProjection` and the first `[AggregateHandler]` in Slice 1, and the subsequent slices slot in without reconsideration of the foundation.