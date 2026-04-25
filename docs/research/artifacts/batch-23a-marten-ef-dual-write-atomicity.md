# Marten + EF Core 10 Dual-Write Atomicity Inside a Wolverine 5.x [AggregateHandler]

**Artifact:** `docs/research/artifacts/batch-23a-marten-efcore-dual-write-atomicity.md`
**Pinned versions:** Marten 8.28 · Marten.EntityFrameworkCore 8.28 · Wolverine 5.x (≥5.28) · WolverineFx.EntityFrameworkCore 5.x · Npgsql 9 · Postgres 17 · .NET 10 · EF Core 10
**Research cutoff:** 2026‑04‑25
**Closes:** R‑066 (batch‑22a‑wolverine‑aggregate‑handler‑transaction‑scope.md) parenthetical.

---

## TL;DR (one paragraph)

When a Wolverine 5.x `[AggregateHandler]` body writes BOTH (a) Marten events through the injected `IDocumentSession` AND (b) a direct EF Core row update through a `RunCoachDbContext` registered via `AddDbContextWithWolverineIntegration<RunCoachDbContext>()`, **Wolverine commits these as TWO sequential connection‑level transactions on TWO separate `NpgsqlConnection` objects**, not one Postgres transaction. Wolverine's transactional middleware is a *unit‑of‑work composition* of two independent persistence units of work (Marten's `IDocumentSession.SaveChangesAsync` and EF Core's `DbContext.SaveChangesAsync`), each of which opens its own Npgsql connection and its own `BEGIN/COMMIT`. There is no shared `NpgsqlConnection`/`NpgsqlTransaction` across the two stores, no `IUnitOfWork`‑style 2PC, and no `TransactionScope` (Wolverine explicitly disclaims distributed transactions). Therefore the dual write is **not atomic**: a crash, cancellation, or `PostgresException` between the two `SaveChangesAsync` awaits leaves Marten committed and the `UserProfile.CurrentPlanId` update lost. The recommended fix for RunCoach Slice 1 is **Option 1 — append a `PlanLinkedToUser` event to the onboarding stream and apply it in `UserProfileFromOnboardingProjection` (an `EfCoreSingleStreamProjection<UserProfile, Guid, RunCoachDbContext>`)**. Marten.EntityFrameworkCore's EF Core projections explicitly "create a per‑slice DbContext using the same PostgreSQL connection as the Marten session" and "register a transaction participant so the DbContext's SaveChangesAsync is called within Marten's transaction, ensuring atomicity" ([Marten EF Core Projections docs, "How It Works", martendb.io, fetched 2026‑04‑25](https://martendb.io/events/projections/efcore.html)). Option 1 makes the write atomic by construction, fits DEC‑057's single‑handler/single‑session pattern, and is directly portable to Slice 3/Slice 4 dual‑writes (`PlanAdaptedFromLog → UserProfile.LastAdaptationAt`, `ConversationTurn → UserProfile.LastChatAt`). Option 2 (shared‑connection mode in `Marten.EntityFrameworkCore` 8.x) **only exists for projection apply methods**, not for arbitrary handler‑body EF Core writes against a separately registered `RunCoachDbContext`, so it does not solve the dual‑write‑in‑a‑handler case directly. Option 3 (accept window + reconciliation IHostedService) is a fallback only.

---

## 1. Literal Commit Ordering — what does Wolverine generate?

### 1.1 Marten‑only `[AggregateHandler]` (baseline)

Wolverine docs publish the generated code for the canonical `MarkItemReady` aggregate handler. The relevant excerpt:

```csharp
public class MarkItemReadyHandler1442193977 : MessageHandler
{
    private readonly OutboxedSessionFactory _outboxedSessionFactory;
    public override async Task HandleAsync(MessageContext context, CancellationToken cancellation)
    {
        var markItemReady = (MarkItemReady)context.Envelope.Message;
        await using var documentSession = _outboxedSessionFactory.OpenSession(context);
        var eventStore = documentSession.Events;
        var eventStream = await eventStore
            .FetchForWriting<Order>(markItemReady.OrderId, markItemReady.Version, cancellation)
            .ConfigureAwait(false);
        var outgoing1 = MarkItemReadyHandler.Handle(markItemReady, eventStream.Aggregate);
        if (outgoing1 != null) eventStream.AppendMany(outgoing1);
        await documentSession.SaveChangesAsync(cancellation).ConfigureAwait(false);
    }
}
```

([Aggregate Handlers and Event Sourcing, wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/marten/event-sourcing.html)). The doc page is explicit: "Wolverine is wrapping middleware around our basic command handler to … Save any outstanding changes and commits the Marten unit of work." The Wolverine.Marten path is built around a **single** `IDocumentSession.SaveChangesAsync` as the transaction boundary, and the docs warn: "When using the transactional middleware with Marten, Wolverine is assuming that there will be a single, atomic transaction for the entire message handler. … it is very strongly recommended that you do not call `IDocumentSession.SaveChangesAsync()` yourself" ([Transactional Middleware (Marten), wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/marten/transactional-middleware)).

### 1.2 EF Core‑only handler (baseline)

For an `AddDbContextWithWolverineIntegration<ItemsDbContext>`‑registered context, the generated code (per Jeremy Miller's *Wolverine meets EF Core and Sql Server*, jeremydmiller.com, 2023‑01‑10) is:

```csharp
public class CreateItemCommandHandler1452615242 : MessageHandler
{
    private readonly DbContextOptions<ItemsDbContext> _dbContextOptions;
    public override async Task HandleAsync(MessageContext context, CancellationToken cancellation)
    {
        await using var itemsDbContext = new ItemsDbContext(_dbContextOptions);
        var createItemCommand = (CreateItemCommand)context.Envelope.Message;
        var outgoing1 = CreateItemCommandHandler.Handle(createItemCommand, itemsDbContext);
        // …envelope persistence…
        await itemsDbContext.SaveChangesAsync(cancellation).ConfigureAwait(false);
    }
}
```

In `TransactionMiddlewareMode.Eager` (default), the middleware additionally calls `DbContext.Database.BeginTransactionAsync()` before the handler body runs and commits at the end ([Transactional Middleware (EF Core), wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/efcore/transactional-middleware.html)).

### 1.3 The dual‑injection case (the question we are answering)

When a single `[AggregateHandler]` handler injects **both** `IDocumentSession` (Marten path active because `IntegrateWithWolverine()` was called) **and** `RunCoachDbContext` (EF path active because `UseEntityFrameworkCoreTransactions()` and `AddDbContextWithWolverineIntegration<RunCoachDbContext>()` were called), Wolverine 5.x composes both middleware policies. The generated code looks like the structural union of the two cases above:

```csharp
public class OnboardingTurnHandler_xxxx : MessageHandler
{
    private readonly OutboxedSessionFactory _outboxedSessionFactory;
    private readonly DbContextOptions<RunCoachDbContext> _dbContextOptions;
    private readonly IPlanGenerationService _planGen;

    public override async Task HandleAsync(MessageContext context, CancellationToken cancellation)
    {
        var cmd = (OnboardingTurn)context.Envelope.Message;

        // EF Core "Eager" middleware: opens an Npgsql connection and BEGIN
        await using var dbContext = new RunCoachDbContext(_dbContextOptions);
        await using var efTx = await dbContext.Database.BeginTransactionAsync(cancellation);

        // Marten path: opens a SECOND Npgsql connection
        await using var documentSession = _outboxedSessionFactory.OpenSession(context);
        var eventStream = await documentSession.Events.FetchForWriting<Onboarding>(cmd.UserId, cancellation);

        // Your handler body executes
        await OnboardingTurnHandler.Handle(cmd, eventStream, dbContext, _planGen, cancellation);

        // FIRST commit: Marten transaction on connection #1
        await documentSession.SaveChangesAsync(cancellation).ConfigureAwait(false);

        // SECOND commit: EF Core transaction on connection #2
        await dbContext.SaveChangesAsync(cancellation).ConfigureAwait(false);
        await efTx.CommitAsync(cancellation).ConfigureAwait(false);
    }
}
```

The actual ordering and the exact frame chain is determined by Wolverine's codegen pipeline that composes `OpenMartenSessionFrame` (from `Wolverine.Marten`, see referenced path `Wolverine.Marten.Codegen.OpenMartenSessionFrame` in [Issue #649, JasperFx/wolverine, github.com](https://github.com/JasperFx/wolverine/issues/649)) and the EF Core transaction frames from `Wolverine.EntityFrameworkCore`. The two persistence "envelope transactions" used internally are:

- `src/Persistence/Wolverine.Marten/MartenEnvelopeTransaction.cs` — confirmed in [Issue #1876, JasperFx/wolverine, github.com, fetched 2026‑04‑25](https://github.com/JasperFx/wolverine/issues/1876) which links the V5.4.0 source path.
- `src/Persistence/Wolverine.EntityFrameworkCore/Internals/EfCoreEnvelopeTransaction.cs` — same issue links `V5.4.0/src/Persistence/Wolverine.EntityFrameworkCore/Internals/EfCoreEnvelopeTransaction.cs#L174`.

These are two independent `IEnvelopeTransaction` implementations. Wolverine's outbox model picks **one** envelope transaction per handler chain to persist outgoing envelopes; it does not enroll the *other* store in the chosen one. There is no `IUnitOfWork`‑style cross‑store commit in Wolverine 5.x. The Wolverine docs state the limitation directly: "Wolverine can only use the transactional inbox/outbox with a single database registration. This limitation will be lifted later as folks are going to eventually hit this limitation with modular monolith approaches." ([EF Core Integration, wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/efcore/)).

The EF Core integration docs describe the second‑connection reality with disarming candor — "Otherwise Wolverine has to use the exposed database `DbConnection` off of the active `DbContext` and make completely separate calls to the database (but at least in the same transaction!) to persist new messages" ([Transactional Inbox and Outbox with EF Core, wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/efcore/outbox-and-inbox.html)). That "in the same transaction" is **within the EF transaction**, not across to Marten's transaction.

**Definitive answer to the primary question: TWO Postgres transactions on TWO `NpgsqlConnection` objects, sequenced by Wolverine's middleware composition.**

---

## 2. Connection‑Sharing Reality Check

### 2.1 Does Marten.EntityFrameworkCore 8.x support shared‑connection mode for ad‑hoc handler writes?

**No, not for arbitrary handler‑body writes against a separately registered `DbContext`.** The shared‑connection capability exists in `Marten.EntityFrameworkCore` 8.x **only inside projection apply paths**.

The Marten docs are unambiguous on this. The "How It Works" section of *EF Core Projections* states:

> "Under the hood, EF Core projections:
> 1. **Create a per‑slice DbContext** using the same PostgreSQL connection as the Marten session
> 2. **Register a transaction participant** so the DbContext's `SaveChangesAsync` is called within Marten's transaction, ensuring atomicity
> 3. **Migrate entity tables** through Weasel alongside Marten's own schema objects, so `dotnet ef` migrations are not needed
> 4. **Use EF Core change tracking** for insert vs. update detection (detached entities are added; unchanged entities are marked as modified)"

([EF Core Projections, martendb.io, fetched 2026‑04‑25](https://martendb.io/events/projections/efcore.html))

Three projection base classes ship in `Marten.EntityFrameworkCore` 8.x:

| Base class | Atomic with `IDocumentSession.SaveChangesAsync`? |
|---|---|
| `EfCoreSingleStreamProjection<TDoc, TId, TDbContext>` | **Yes** (Inline lifecycle) |
| `EfCoreMultiStreamProjection<TDoc, TId, TDbContext>` | **Yes** (Inline lifecycle) |
| `EfCoreEventProjection<TDbContext>` | **Yes** (Inline lifecycle), can write to BOTH Marten docs *and* EF Core entities in one transaction |

The shipping milestone is **Marten 8.23** (issue #4145 "First class EF Core projections", milestone `8.23`, [github.com, fetched 2026‑04‑25](https://github.com/JasperFx/marten/issues/4145)), so the feature is GA in Marten 8.28 (latest stable, `Marten 8.28.0` per [NuGet Gallery, fetched 2026‑04‑25](https://www.nuget.org/packages/Marten/) listing the package as "Last updated 3/31/2026"). EF Core 10 / .NET 10 compatibility is delivered via `Weasel 8.12.0` which "fixes `MissingMethodException` when using `Weasel.EntityFrameworkCore` with EF Core 10 on .NET 10" ([*Marten – The Shade Tree Developer*, jeremydmiller.com, fetched 2026‑04‑25](https://jeremydmiller.com/tag/marten/)).

### 2.2 What is **not** offered

`Marten.EntityFrameworkCore` 8.x does **not** ship a fluent‑config option on `AddMarten(...)` (e.g. an `UseEntityFrameworkCoreCompatibilityMode()` style API) that would force a regular DI‑resolved `RunCoachDbContext` injected into a handler body to share `IDocumentSession`'s `NpgsqlConnection`/`NpgsqlTransaction`. The shared‑connection participant is set up *internally* by Marten's projection runtime when applying a slice; it is not exposed to user code. There is no Marten 9 GA either as of 2026‑04‑25 — Marten 8.x is the current major; Polecat 2.0.x is also current ([Marten Releases, github.com, fetched 2026‑04‑25](https://github.com/JasperFx/marten/releases)).

### 2.3 The workaround if you need ad‑hoc shared connection

You can do it manually with EF Core's standard pattern:

```csharp
var conn = (NpgsqlConnection)session.Connection!; // Marten exposes this
await dbContext.Database.UseTransactionAsync(session.Connection.BeginTransaction());
```

…but this is fragile, undocumented for Marten 8.x sessions (Marten 7+ uses an "auto‑close" connection lifecycle by default rather than the V6 "sticky" lifecycle, see [Marten Migration Guide, martendb.io, fetched 2026‑04‑25](https://martendb.io/migration-guide)), and reaches into Marten internals. Not recommended.

**Verdict:** for arbitrary `[AggregateHandler]` handler‑body writes that need to commit atomically with Marten events, **Option 2 is not available in Marten 8.28**; the supported atomic path is to route the EF write through an `EfCoreSingleStreamProjection` (Inline) — which is exactly Option 1 below.

---

## 3. Wolverine.EntityFrameworkCore Integration Semantics

The `AddDbContextWithWolverineIntegration<T>` extension does two distinct things — it does **not** enroll the EF DbContext in Marten's transaction:

1. **Performance optimization:** registers `DbContextOptions<T>` with `ServiceLifetime.Singleton` for faster Wolverine codegen access ("This is actually a significant performance gain for Wolverine's sake") and adds the EF Core middleware activation flag.
2. **Envelope storage mapping:** equivalent to calling `modelBuilder.MapWolverineEnvelopeStorage()` inside `OnModelCreating`. This adds the `wolverine_outgoing_envelopes` and `wolverine_incoming_envelopes` mappings to the EF model so that Wolverine can write its outbox/inbox rows through the same `DbContext.SaveChangesAsync()` and benefit from EF Core command batching.

Quoting the Wolverine docs verbatim:

> "You can optimize this by adding mappings for Wolverine's envelope storage to your DbContext types such that Wolverine can just use EF Core to persist new messages and depend on EF Core database command batching. Otherwise Wolverine has to use the exposed database DbConnection off of the active DbContext and make completely separate calls to the database (but at least in the same transaction!) to persist new messages at the same time it's calling DbContext.SaveChangesAsync() with any pending entity changes." ([Transactional Inbox and Outbox with EF Core, wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/efcore/outbox-and-inbox.html))

The "model customizer" the question asks about is the EF Core mechanism that wires `MapWolverineEnvelopeStorage()` into the registered `DbContext`. Its purpose is **purely** to let EF Core batch envelope rows alongside the user's domain rows in **the EF transaction**. It does not call into Marten, register a `Marten.IDocumentSession`, or share any `NpgsqlConnection` with Marten. (Source: bootstrapping samples and prose in [EF Core Integration, wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/efcore/) and [EF Core is Better with Wolverine, jeremydmiller.com, 2026‑04‑21](https://jeremydmiller.com/2026/04/21/ef-core-is-better-with-wolverine/).)

---

## 4. `AutoApplyTransactions()` Policy Contract

`Policies.AutoApplyTransactions()` is a *handler‑attachment* policy. Its contract is: "When using the opt in `Handlers.AutoApplyTransactions()` option, Wolverine (really Lamar) can detect that your handler method uses a DbContext if it's a method argument, a dependency of any service injected as a method argument, or a dependency of any service injected as a constructor argument of the handler class. That will enroll EF Core as both a strategy for stateful saga support and for transactional middleware." ([EF Core Integration, wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/efcore/)). The Marten‑side equivalent: "With this enabled, Wolverine will automatically use the Marten transactional middleware for handlers that have a dependency on `IDocumentSession`" ([Transactional Middleware (Marten), wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/marten/transactional-middleware)).

Crucially, when **both** dependencies are present in a handler, Wolverine attaches **both** middleware policies. The policy contract does **not** promise a single transaction across stores. The closest the docs come to this is the per‑store guarantee:

> "When using the transactional middleware with Marten, Wolverine is assuming that there will be a single, atomic transaction for the entire message handler." ([Transactional Middleware (Marten), wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/marten/transactional-middleware))

That sentence is scoped to *"the Marten unit of work"*. The EF docs use parallel language scoped to *"`DbContext.SaveChangesAsync`"*. Neither doc claims cross‑store atomicity, and the durability page is explicit that Wolverine "does not support any kind of 2 phase commits between the database and message brokers" ([Durable Messaging, wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/)) — and by extension, no 2PC between persistence stores.

**Answer:** `AutoApplyTransactions()` guarantees a single transaction **per persistence engine**, not one transaction across the two engines. With both engines active, you get **two** transactions.

---

## 5. Failure‑Mode Behavior Under Two Transactions

Given the generated ordering above (Marten commit first, EF commit second), here are the failure modes with `OnboardingTurnHandler` as the test case:

| Step | Failure | DB state after handler exits | What the HTTP caller sees |
|---|---|---|---|
| Marten `SaveChangesAsync` throws (concurrency, connection drop) | Plan stream NOT created; no events appended; UserProfile unchanged | Clean — atomic from the user's POV | 500 (or Wolverine retry) |
| Marten commits, then EF `SaveChangesAsync` throws (FK violation, concurrency, network blip, `OperationCanceledException` between awaits) | **Plan stream EXISTS, OnboardingCompleted EXISTS, BUT `UserProfile.CurrentPlanId` is still null** | 500 — but the events are now durable | **Orphan Plan / split brain** |
| Both commit, broker‑outbox flush fails | Plan + UserProfile both correct; outgoing message sits in `wolverine_outgoing_envelopes` table | Wolverine background agent retries and eventually delivers | OK — outbox saves the day for *messages* |

Wolverine's automatic retry policies (e.g. `Policies.OnAnyException().RetryWithCooldown(...)`) operate at the **handler envelope** level — i.e. they re‑invoke the entire handler. After commit‑1 has succeeded, retrying the handler will:

1. Open a **new** Marten session, `FetchForWriting<Onboarding>` again.
2. Hit Marten's optimistic concurrency on the onboarding stream because the previously appended events bumped the version → throws `ConcurrencyException`. Or, if `[AggregateHandler]` is wired to do `FetchLatest`, it will re‑apply onboarding terminal logic and try to call `StartStream<Plan>` again with a **new** Plan id, then fail again on EF if the underlying error is deterministic.

So Wolverine retry **does not heal** the half‑committed state in the dual‑write case; it can only paper over transient failures that prevent commit‑1 from succeeding in the first place. There is no built‑in compensation for the "commit‑1 succeeded, commit‑2 failed" window. The HTTP caller sees a 500, and your database has a Plan stream + onboarding events with no `UserProfile.CurrentPlanId` pointing at it. This is exactly the spec's all‑or‑nothing regression‑test failure mode.

For completeness on cancellation: `CancellationToken` firing between the two awaits will propagate `OperationCanceledException` from the second `SaveChangesAsync`. Marten's commit has already completed at that point. EF's `efTx.CommitAsync(cancellationToken)` will not roll back the *Marten* transaction.

---

## 6. Three Architectural Options — Cost Matrix and Recommendation

### Option 1 — Indirect via Marten EF Core projection (`PlanLinkedToUser` event + `EfCoreSingleStreamProjection`)

Add a `PlanLinkedToUser(Guid UserId, Guid PlanId)` event to the onboarding stream. Replace the direct `db.UserProfiles.Single(...).CurrentPlanId = planId;` line in `OnboardingTurnHandler` with `events += new PlanLinkedToUser(userId, planId);`. Extend `UserProfileFromOnboardingProjection : EfCoreSingleStreamProjection<UserProfile, Guid, RunCoachDbContext>` with an apply branch that mutates `UserProfile.CurrentPlanId`. With the projection registered as `ProjectionLifecycle.Inline`, the EF write happens inside Marten's transaction on Marten's `NpgsqlConnection`, exactly as the Marten docs specify.

### Option 2 — Shared‑connection mode for the *handler‑body* DbContext

Not available in Marten 8.28 for arbitrary DI‑injected `DbContext` services. The shared‑connection participant is internal to `EfCore*Projection` types only. Implementing it for an injected handler `DbContext` would require either (a) reaching into Marten internals to grab `session.Connection`, then `dbContext.Database.UseTransactionAsync(session.Connection.BeginTransaction())` — undocumented and fragile against Marten's "auto‑close connection" lifecycle ([Marten Migration Guide, martendb.io, fetched 2026‑04‑25](https://martendb.io/migration-guide)) — or (b) wait for Marten 9 (no GA date, and a Marten 9 upgrade is explicitly out of scope per the prompt).

### Option 3 — Accept the consistency window + reconciliation IHostedService

Run the handler as today, accept that there is a small window where a Plan stream exists but `UserProfile.CurrentPlanId` is null, and run a periodic background job that reconciles.

### Cost matrix

| Criterion | Option 1 (projection) | Option 2 (shared conn) | Option 3 (reconcile) |
|---|---|---|---|
| (a) Atomicity guarantee | **Strong** — single Marten transaction, EF participant enrolled, all‑or‑nothing by Marten construction. | Strong **only if** you implement the manual `UseTransactionAsync` workaround correctly; otherwise N/A in 8.28. | **None** at write time; eventual via background job. |
| (b) Code complexity | Low — one new event record, one apply method, one line change in handler. | High — bypass Marten's documented public API; reach into `session.Connection`; refactor `RunCoachDbContext` lifetime; brittle. | Medium — handler stays as is; new `IHostedService`, detection query, OTel. |
| (c) Operational complexity | Low — projection runs Inline so no async daemon dependency for this slice. | Medium‑High — new failure modes (connection sharing exceptions; concurrent connection use). | High — orphan‑rate alert, on‑call playbook, race conditions between the job and live writes. |
| (d) Compat with DEC‑057 single‑handler / single‑session | **Excellent** — preserves the rule. The handler still uses one `IDocumentSession`; the EF write is a *projection apply* not a handler‑body write. | Conflicts — handler still has both `IDocumentSession` and `RunCoachDbContext` dependencies. DEC‑057 explicitly aims to have only one persistence vector per handler. | Preserves DEC‑057 textually (handler unchanged) but normalizes a known split‑brain class of bug. |
| (e) Spec regression tests (all‑or‑nothing) | Tests pass deterministically — failures roll back both. | Tests pass *if* workaround is correct; otherwise unchanged from today. | Tests must be relaxed to allow eventual consistency, which is a step backward for Slice 1. |
| (f) Future async‑flip migration | **Excellent** — flipping `UserProfileFromOnboardingProjection` from `Inline` to `Async` is a one‑line config change. The handler does not change. Eventual‑consistency semantics propagate cleanly. | Poor — locks you to a synchronous shared‑connection path that does not survive an async flip (the projection runs in the daemon, separate from the handler's session). | N/A — already eventual. |

### Recommendation: **Option 1**

Option 1 wins on every axis except a small ergonomic cost of having to express the link as an event. It is the only option that achieves true atomicity with Marten 8.28 + Wolverine 5.x + EF Core 10 today, and it is the most future‑proof against an async‑flip. It is also the option that the Critter Stack team explicitly designed `Marten.EntityFrameworkCore` 8.x to support — the doc's own example (the `OrderSummaryProjection`) is structurally identical to the proposed `UserProfileFromOnboardingProjection`.

---

## 7. Option 1 — Concrete Spec Changes for Slice 1

### 7.1 New event type

```csharp
// RunCoach.Domain.Onboarding.Events
public sealed record PlanLinkedToUser(Guid UserId, Guid PlanId);
```

The event lives on the **onboarding** stream (not the `Plan` stream). The `Plan` stream is started by `StartStream<Plan>(planId, planEvents)` and is independent.

### 7.2 Projection apply method update

```csharp
public sealed class UserProfileFromOnboardingProjection
    : EfCoreSingleStreamProjection<UserProfile, Guid, RunCoachDbContext>
{
    public override UserProfile? ApplyEvent(
        UserProfile? snapshot,
        Guid userId,
        IEvent @event,
        RunCoachDbContext dbContext,
        IQuerySession session)
    {
        snapshot ??= new UserProfile { UserId = userId };

        switch (@event.Data)
        {
            case OnboardingStarted started:
                snapshot.OnboardingStartedAt = @event.Timestamp.UtcDateTime;
                return snapshot;

            // … other onboarding apply branches …

            case PlanLinkedToUser linked:
                snapshot.CurrentPlanId = linked.PlanId;
                return snapshot;

            case OnboardingCompleted completed:
                snapshot.OnboardingCompletedAt = @event.Timestamp.UtcDateTime;
                return snapshot;
        }

        return snapshot;
    }
}
```

Register Inline in `Program.cs`:

```csharp
builder.Services.AddMarten(opts =>
    {
        opts.Connection(connStr);
        opts.DatabaseSchemaName = "runcoach";
        opts.Add(new UserProfileFromOnboardingProjection(), ProjectionLifecycle.Inline);
        opts.AddEntityTablesFromDbContext<RunCoachDbContext>(); // not strictly required for SingleStream, but harmless
    })
    .UseLightweightSessions()
    .IntegrateWithWolverine();
```

### 7.3 Handler body (final shape)

```csharp
[AggregateHandler]
public static async Task<(IEnumerable<object>, OutgoingMessages)> Handle(
    OnboardingTurn cmd,
    IEventStream<Onboarding> stream,                 // <-- only Marten dependency
    IPlanGenerationService planGen,                  // <-- plain DI; no DB
    CancellationToken ct)
{
    // 1. pre-terminal events from existing turn logic
    var events = new List<object>(stream.Aggregate.NextEventsFor(cmd));

    if (!stream.Aggregate.IsTerminalAfter(cmd, events))
        return (events, default);

    // 2. plan generation (no DB writes; pure LLM)
    var (planId, planEvents) = await planGen.GeneratePlanAsync(stream.Key, stream.Aggregate, ct);

    // 3. start the new Plan stream via the IDocumentSession (Marten side-effect)
    //    — done via session in a Before/Load method or via MartenOps.StartStream
    yield return MartenOps.StartStream<Plan>(planId, planEvents);

    // 4. link event on the onboarding stream — this is the dual-write replacement
    events.Add(new PlanLinkedToUser(stream.Key, planId));

    // 5. terminal event
    events.Add(new OnboardingCompleted(planId));

    return (events, default);
}
```

Notes:

- `RunCoachDbContext` is **removed** from the handler signature. This re‑establishes DEC‑057.
- The two stream writes (the new `Plan` stream via `MartenOps.StartStream<Plan>` and the appends to the onboarding stream) are both in the same `IDocumentSession.SaveChangesAsync` and therefore in one Marten transaction. The Marten docs are explicit that `StartStream` and stream appends in the same session commit atomically — this is a basic Marten guarantee, see the OrderSagaSample patterns in [Aggregate Handlers, wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/marten/event-sourcing.html).
- The `PlanLinkedToUser` apply method runs Inline as a Marten transaction participant on the same `NpgsqlConnection` per [martendb.io EF Core Projections, fetched 2026‑04‑25](https://martendb.io/events/projections/efcore.html) — **one Postgres transaction**, all‑or‑nothing.

### 7.4 Regression test assertion shape

```csharp
[Fact]
public async Task OnboardingTurn_terminal_branch_is_atomic_under_planGen_failure()
{
    // Arrange: planGen will throw on the second LLM call
    _stubPlanGen.FailOnCallNumber = 2;

    // Act
    var ex = await Should.ThrowAsync<PlanGenerationException>(
        () => _host.InvokeMessageAndWaitAsync(new OnboardingTurn(_userId, "ready")));

    // Assert: NEITHER side of the dual-write may be present.
    using var session = _host.DocumentStore().LightweightSession();
    var events = await session.Events.FetchStreamAsync(_userId);
    events.ShouldNotContain(e => e.Data is OnboardingCompleted);
    events.ShouldNotContain(e => e.Data is PlanLinkedToUser);

    var planStreams = await session.Events.QueryAllRawEvents()
        .Where(e => e.EventTypeName == "plan_started")
        .ToListAsync();
    planStreams.ShouldBeEmpty(); // no orphan Plan streams

    using var db = _host.Services.GetRequiredService<RunCoachDbContext>();
    var profile = await db.UserProfiles.SingleAsync(p => p.UserId == _userId);
    profile.CurrentPlanId.ShouldBeNull();
}
```

Pair this with a positive "happy path" test that asserts all five outputs present together.

---

## 8. Audit Recommendation (assuming Option 1)

If Option 1 is adopted, the architectural rule for RunCoach Slice 1+ becomes:

> **Inside any Wolverine `[AggregateHandler]` body, the only persistence side effect permitted is appending events to Marten streams (via `IEventStream<T>.AppendOne/AppendMany`, `MartenOps.StartStream`, or returning `Events` / `IEnumerable<object>`). Direct mutations of EF entities via `RunCoachDbContext` are forbidden in handler bodies. EF state is updated only inside `EfCore*Projection` apply methods, which run as Marten transaction participants.**

Audit checklist for the handler:

1. `grep` the Slice 1 handler assembly for `RunCoachDbContext` injected as a method argument or constructor parameter on any class with `[AggregateHandler]` or `[Transactional]`. Each occurrence is a violation.
2. `grep` for `db.SaveChangesAsync` and `dbContext.SaveChangesAsync` inside `[AggregateHandler]` body files. Should be zero.
3. Confirm that `OnboardingTurnHandler` no longer takes `RunCoachDbContext` as a parameter (DEC‑057 enforcement test).
4. Add a unit test that walks the `Wolverine.Configuration.HandlerChain` for the onboarding command and asserts the chain contains `MartenEnvelopeTransaction`/`OpenMartenSessionFrame` but NOT the EF Core transaction frames. Wolverine exposes the chain model for this kind of architectural test (see `dotnet run -- codegen preview`, [Working with Code Generation, wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/codegen)).
5. Slice 3/Slice 4: same rule — `PlanAdaptedFromLog` and `ConversationTurnRecorded` events, with apply branches in the corresponding `EfCoreSingleStreamProjection` for `UserProfile.LastAdaptationAt` and `UserProfile.LastChatAt`.

---

## 9. `IIdempotencyStore` Writes — Are They Part of the Question?

It depends entirely on **where** the `IIdempotencyStore` writes happen and **how** it is implemented.

- **If the implementation is "EF Core writes inside the handler body"** (e.g. `IIdempotencyStore.MarkProcessedAsync(envelopeId)` inserts a row via `RunCoachDbContext`), then yes — that row is in the EF transaction and is exactly the same dual‑write problem, with the same answer. Move it to either (a) a Marten event or (b) Wolverine's built‑in idempotency tracking.
- **If you use Wolverine's built‑in `Policies.AutoApplyTransactions(IdempotencyStyle.Eager)` or `AutoApplyIdempotencyOnNonTransactionalHandlers()`**, Wolverine writes idempotency records into `wolverine_incoming_envelopes` itself, *as part of whichever envelope transaction is active for the chain* (Marten or EF — see [Idempotency in Messaging, wolverinefx.io, fetched 2026‑04‑25](https://wolverinefx.io/tutorials/idempotency)). In that case the idempotency check piggy‑backs on the chosen envelope transaction and is not a *third* commit.
- The Wolverine inbox itself participates in **one** of the two store transactions (the one Wolverine chose as the `IEnvelopeTransaction` for the chain). With both Marten and EF active, this is determined by `opts.Durability.MessageStorageSchemaName` and the registration order; the Wolverine docs note "this is important for modular monolith usage" ([Ancillary Stores, wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/marten/ancillary-stores)).

**Recommendation for RunCoach:** if you have an `IIdempotencyStore` of your own that writes via EF, replace it with Wolverine's built‑in idempotency tracking. Don't add a third store to the dual‑write problem.

---

## 10. Plain DI Services in the Handler

`IPlanGenerationService.GeneratePlanAsync` does not inject `IDocumentSession` or `RunCoachDbContext`. Wolverine's codegen analyzes the IoC dependency graph at bootstrapping time (Lamar or `ServiceProvider`) and only enrolls a store's middleware if the handler — or *any service it depends on* — depends on that store ([EF Core Integration, wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/efcore/) "...method argument, a dependency of any service injected as a method argument, or a dependency of any service injected as a constructor argument"). For a service that only calls an LLM HTTP client, this means:

- No additional `DbContext` or `IDocumentSession` is created for `IPlanGenerationService`.
- No transaction is opened on its behalf.
- It cannot leak a "fresh" persistence handle, because Wolverine's codegen never wires one up.

There is one historical wrinkle: in older versions, the transitive‑dependency detection was incomplete (see [Issue #173, JasperFx/wolverine, github.com](https://github.com/JasperFx/wolverine/issues/173) which observed `IBuildingRepository → IDocumentSession` not always triggering middleware attachment). That was fixed for direct injection patterns, and in Wolverine 5.x the detection is reliable for `AddDbContextWithWolverineIntegration`‑registered contexts and for `IDocumentSession`. However, this also means that **if a service Wolverine doesn't see depends on `IDocumentSession`, it will still create its own session via `OutboxedSessionFactory.OpenSession(context)` for the handler**; the `IPlanGenerationService` not touching either store is therefore safe — no second session, no leaked DbContext.

For the RunCoach handler's six‑call LLM chain inside `IPlanGenerationService`, the practical caution is unrelated to transaction atomicity: long LLM calls hold the handler open, which means the Marten/EF transactions are **also held open** for the duration. That is a connection‑pool / lock‑duration concern, not an atomicity concern, but it informs Slice 3/Slice 4 design (consider `[NonTransactional]` plus explicit idempotency, with the LLM chain happening *before* a follow‑up command that does the persistence).

---

## 11. Verification Path — Empirical xUnit v3 Integration Test

The cleanest empirical proof uses Postgres `pg_stat_activity.backend_xid` snapshots from a **third** observer connection. Marten's `IDocumentSession` has its own backend pid/xid; EF Core's `DbContext` opens a different pid; if they were in one transaction they would share `xact_start` and `backend_xid`.

```csharp
using JasperFx.Resources;
using Marten;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RunCoach.AppHost;
using Shouldly;
using Testcontainers.PostgreSql;
using Wolverine;
using Wolverine.Tracking;
using Xunit;

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine").Build();
    public Task InitializeAsync() => Container.StartAsync();
    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[Collection(nameof(PostgresCollection))]
public sealed class DualWriteAtomicityTests(PostgresFixture pg)
{
    [Fact]
    public async Task Handler_with_both_stores_uses_TWO_postgres_transactions()
    {
        // Arrange: stand up the real RunCoach host against the Testcontainer
        await using var host = await TestHostBuilder.CreateAsync(pg.Container.GetConnectionString());

        // Observer connection — separate, used to query pg_stat_activity
        await using var observer = new NpgsqlConnection(pg.Container.GetConnectionString());
        await observer.OpenAsync();

        // Snapshot: capture every backend pid/xid that becomes "active" while the handler runs.
        var snapshots = new List<(int pid, long? xid, string app, DateTime xactStart)>();
        var cts = new CancellationTokenSource();
        var poller = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                await using var cmd = new NpgsqlCommand(
                    @"select pid, backend_xid::text::bigint, application_name, xact_start
                      from pg_stat_activity
                      where state = 'active' and backend_xid is not null", observer);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    snapshots.Add(((int)reader.GetInt32(0),
                                   reader.IsDBNull(1) ? null : reader.GetInt64(1),
                                   reader.GetString(2),
                                   reader.GetDateTime(3)));
                await Task.Delay(5, cts.Token);
            }
        });

        // Act
        var tracked = await host.InvokeMessageAndWaitAsync(
            new OnboardingTurn(Guid.NewGuid(), "ready"));

        await Task.Delay(50); cts.Cancel();
        try { await poller; } catch (OperationCanceledException) { }

        // Assert: at least two distinct backend_xid values were observed
        // for backend pids belonging to our app => two transactions.
        var distinctXids = snapshots
            .Where(s => s.app.Contains("runcoach", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.xid)
            .Where(x => x.HasValue)
            .Distinct()
            .ToArray();

        distinctXids.Length.ShouldBeGreaterThanOrEqualTo(2,
            "Marten and EF Core each opened their own Postgres transaction (different backend_xid).");
    }
}
```

Two robustness notes:

1. `backend_xid` is only assigned once a backend executes a write. SELECT‑only transactions don't get an xid. For RunCoach the handler always writes, so the column is reliable.
2. An even simpler proof: register an `Action<NpgsqlNoticeEventArgs>` on a debug Npgsql data source via `NpgsqlDataSourceBuilder.MapNotice()` and `LOG_STATEMENT='all'` on the test container; count `BEGIN`/`COMMIT` log lines. With Option 1 in place, the same test should observe **one** `BEGIN`/`COMMIT` for the handler. ([Postgres `log_statement` docs are well known; not cited here as primary‑source.])

---

## 12. Slice 3 / Slice 4 Implications

Both upcoming slices have the same dual‑write shape as Slice 1:

| Slice | Handler | Marten side | EF side (dual-write) |
|---|---|---|---|
| Slice 3 | `LogAdaptationHandler` (or similar) | `PlanAdaptedFromLog` events on the `Plan` stream | `UserProfile.LastAdaptationAt` |
| Slice 4 | `ConversationTurnHandler` | `ConversationTurn`/`UserSpoke`/`AssistantSpoke` events on a `Conversation` stream | `UserProfile.LastChatAt` |

The Option 1 pattern generalizes cleanly:

- **Slice 3:** add a `PlanAdaptationRecorded(Guid UserId, DateTime At)` event on the *onboarding* stream (or a new `UserActivity` stream owned by `UserProfile`), then apply it in `UserProfileFromOnboardingProjection` — or split the projection: `UserProfileFromActivityProjection : EfCoreMultiStreamProjection<UserProfile, Guid, RunCoachDbContext>` that listens to `Plan.PlanAdaptedFromLog` events and updates the user profile by `UserId` (use `Identity<PlanAdaptedFromLog>(e => e.UserId)`).
- **Slice 4:** identical, with `Identity<ConversationTurnRecorded>(e => e.UserId)` on a multi‑stream projection.

Operationally, Slice 4 likely wants `ProjectionLifecycle.Async` (since chat is high‑frequency and the user does not need synchronous read‑your‑writes on `UserProfile.LastChatAt`). With the projection running async, the handler is still atomic for Marten events; the EF read‑model just has its usual eventual‑consistency semantics. The Marten docs explicitly support both lifecycles for `EfCore*Projection` ([EF Core Projections, martendb.io, fetched 2026‑04‑25](https://martendb.io/events/projections/efcore.html) "All three types support Inline, Async, and Live projection lifecycles."). For Slice 1 (terminal onboarding → plan creation) keep `Inline` so the very next read of `/me` shows `CurrentPlanId` populated.

The architectural payoff is large: a single rule — *handler bodies emit events; projections own EF state* — covers all three slices, satisfies DEC‑057, and never has to reason about cross‑store atomicity again until the project genuinely needs ancillary stores (in which case Wolverine's modular‑monolith story for ancillary Marten stores ([Ancillary Stores, wolverinefx.net, fetched 2026‑04‑25](https://wolverinefx.net/guide/durability/marten/ancillary-stores)) becomes relevant).

---

## 13. Source Citation Index (date‑stamped)

All sources fetched **2026‑04‑25** unless noted.

**Primary (official docs and source):**

1. [EF Core Projections — martendb.io](https://martendb.io/events/projections/efcore.html) — "create a per‑slice DbContext using the same PostgreSQL connection as the Marten session" / "transaction participant" / "ensuring atomicity" — definitive answer for Option 1.
2. [Aggregate Handlers and Event Sourcing — wolverinefx.net](https://wolverinefx.net/guide/durability/marten/event-sourcing.html) — generated `MarkItemReadyHandler` code with `OutboxedSessionFactory.OpenSession(context)` and single `documentSession.SaveChangesAsync(...)`.
3. [Transactional Middleware (Marten) — wolverinefx.net](https://wolverinefx.net/guide/durability/marten/transactional-middleware) — "single, atomic transaction for the entire message handler" scoped to Marten.
4. [Transactional Middleware (EF Core) — wolverinefx.net](https://wolverinefx.net/guide/durability/efcore/transactional-middleware.html) — `TransactionMiddlewareMode.Eager` / `Lightweight`; explicit note that Lightweight "is not supported or necessary for Marten or RavenDb, which have their own unit of work implementations."
5. [Entity Framework Core Integration — wolverinefx.net](https://wolverinefx.net/guide/durability/efcore/) — "Wolverine can only use the transactional inbox/outbox with a single database registration. This limitation will be lifted later…"
6. [Transactional Inbox and Outbox with EF Core — wolverinefx.net](https://wolverinefx.net/guide/durability/efcore/outbox-and-inbox.html) — "completely separate calls to the database (but at least in the same transaction!)" — the second connection is **EF's** transaction, not Marten's.
7. [Durable Messaging — wolverinefx.net](https://wolverinefx.net/guide/durability/) — "Wolverine does not support any kind of 2 phase commits between the database and message brokers."
8. [Marten Migration Guide — martendb.io](https://martendb.io/migration-guide) — Marten 7+ "auto‑close" connection lifetime relevant to any manual shared‑connection workaround.
9. [Working with Code Generation — wolverinefx.net](https://wolverinefx.net/guide/codegen) — `dotnet run -- codegen preview` to inspect generated handlers; rationale for service detection and chain composition.

**Primary (GitHub source paths and issues):**

10. [Issue #1876 — JasperFx/wolverine, github.com](https://github.com/JasperFx/wolverine/issues/1876) — links to source paths confirming separate envelope transactions: `src/Persistence/Wolverine.Marten/MartenEnvelopeTransaction.cs`, `src/Persistence/Wolverine.EntityFrameworkCore/Internals/EfCoreEnvelopeTransaction.cs`, `src/Persistence/Wolverine.RDBMS/DatabaseEnvelopeTransaction.cs` — V5.4.0 branch.
11. [Issue #4145 — JasperFx/marten, github.com](https://github.com/JasperFx/marten/issues/4145) — "First class EF Core projections", milestone `8.23`, shipped Feb 2026.
12. [Marten Releases (8.28.0 etc.) — github.com](https://github.com/JasperFx/marten/releases) — release notes including the EF Core 10 / .NET 10 Weasel 8.12 fix.
13. [Wolverine repo CLAUDE.md — github.com](https://github.com/JasperFx/wolverine/blob/main/CLAUDE.md) — confirms `src/Persistence/Wolverine.Marten/` and `src/Persistence/Wolverine.EntityFrameworkCore/` directory layout.
14. [NuGet Marten 8.28.0 — nuget.org](https://www.nuget.org/packages/Marten/) — "Last updated 3/31/2026", confirming 8.28 as the current pinned baseline.
15. [Issue #649 — JasperFx/wolverine, github.com](https://github.com/JasperFx/wolverine/issues/649) — references `Wolverine.Marten.Codegen.OpenMartenSessionFrame` and `Wolverine.Http.CodeGen.CreateMessageContextWithMaybeTenantFrame` — the codegen frames that compose into the chain.
16. [Issue #173 — JasperFx/wolverine, github.com](https://github.com/JasperFx/wolverine/issues/173) — original observation that `AutoApplyTransactions()` enrollment depends on visible service dependencies.

**Secondary (Jeremy Miller blog — author/maintainer; treat as authoritative tertiary, but cross‑checked against official docs above):**

17. [Wolverine meets EF Core and Sql Server — jeremydmiller.com, 2023‑01‑10](https://jeremydmiller.com/2023/01/10/wolverine-meets-ef-core-and-sql-server/) — full generated EF handler example.
18. [EF Core is Better with Wolverine — jeremydmiller.com, 2026‑04‑21](https://jeremydmiller.com/2026/04/21/ef-core-is-better-with-wolverine/) — current EF Core integration narrative including envelope mapping into DbContext.
19. [Building a Critter Stack Application: Wolverine's Aggregate Handler Workflow — jeremydmiller.com, 2023‑12‑06](https://jeremydmiller.com/2023/12/06/building-a-critter-stack-application-wolverines-aggregate-handler-workflow-ftw/) — generated `CategoriseIncidentHandler` showing the canonical aggregate‑handler chain.
20. [Build Resilient Systems with Wolverine's Transactional Outbox — jeremydmiller.com, 2024‑12‑08](https://jeremydmiller.com/2024/12/08/build-resilient-systems-with-wolverines-transactional-outbox/) — outbox semantics and the explicit "no 2PC" position.
21. [Wolverine 5 and Modular Monoliths — jeremydmiller.com, 2025‑10‑27](https://jeremydmiller.com/2025/10/27/wolverine-5-and-modular-monoliths/) — Wolverine 5.x story for combining `AddMarten().IntegrateWithWolverine()` with `UseEntityFrameworkCoreTransactions()` + `AddDbContextWithWolverineIntegration<T>` in the same process.
22. [Customizing the Wolverine Code Generation Model — jeremydmiller.com, 2026‑04‑20](https://jeremydmiller.com/2026/04/20/customizing-the-wolverine-code-generation-model/) — confirms current codegen model.

**Note on tertiary‑source claims:** the precise *call ordering* (Marten commit‑first vs EF commit‑first) within the dual‑injection generated handler is inferred from the structure of the two envelope‑transaction implementations and the codegen frame composition (point 10 above) rather than from an officially documented snippet showing both stores in one handler. This research recommends running `dotnet run -- codegen preview` against the actual `OnboardingTurnHandler` registration to capture the exact emitted code in the project tree before relying on any specific ordering for failure‑mode reasoning. The empirical xUnit v3 test in §11 verifies the cardinality (TWO transactions) regardless of ordering.

---

*End of artifact.*