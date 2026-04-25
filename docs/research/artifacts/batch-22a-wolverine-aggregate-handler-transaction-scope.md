# Wolverine `[AggregateHandler]` + `IMessageBus.InvokeAsync` Transaction Scope

**Research ticket:** R-066 (RunCoach)
**Stack pin:** .NET 10 · Wolverine 5.28 · Marten 8.28 · EF Core 10 (`Marten.EntityFrameworkCore`) · `Policies.AutoApplyTransactions()` · conjoined tenancy per user · xUnit v3 + AssemblyFixture + Testcontainers Postgres
**Artifact path:** `research/artifacts/batch-22a-wolverine-aggregate-handler-invokeasync-transaction-scope.md`

---

## Executive Summary (TL;DR)

**The load‑bearing assumption in the Slice 1 spec is wrong.** When a Wolverine 5.x `[AggregateHandler]` calls `await IMessageBus.InvokeAsync<TResponse>(downstreamCommand, ct)` from inside its handler body, the downstream handler **does not run inside the outer handler's Postgres transaction**. Each invocation of `InvokeAsync` triggers a fresh execution of the handler pipeline through `IMessageInvoker`, which in the Wolverine.Marten code generator builds its own `MessageContext`, opens its own outbox‑enrolled `IDocumentSession` via `OutboxedSessionFactory.OpenSession(context)`, and calls `SaveChangesAsync()` on that session at the end of the inner handler. The outer handler's transaction is committed strictly **after** the outer body returns — including after `await InvokeAsync` has resolved — so the inner handler has *already* committed (or rolled back) its own work in a separate transaction by then.

Severity classification of the spec defect:

| Concern | Severity | Reason |
|---|---|---|
| `OnboardingCompleted` already in onboarding stream when plan‑gen fails | **CRITICAL** | User is permanently wedged: retry endpoint refuses re‑run because onboarding is "complete," no Plan exists, `UserProfile.CurrentPlanId` is null. |
| Partial Plan stream possible | **HIGH** | Inner handler could partially append before LLM failure depending on where `SaveChangesAsync` falls relative to LLM calls. |
| Outer transaction rollback assumption | **HIGH** | Wolverine never rolls back the inner transaction when the outer throws (it's already committed). |
| Slice‑4 async flip | **MEDIUM** | Now becomes simpler, not harder — see Slice 4 callout. |

**Canonical Wolverine 2026 pattern for "atomic multi-step pipeline within one HTTP request":** Do **all** the work inside a **single handler** using the same injected `IDocumentSession`, expressed as a *compound handler* (Before/Load/Handle methods) so Wolverine's transactional middleware brackets one logical transaction around the entire call chain. Use `MartenOps.StartStream<Plan>(...)` and event lists as **return values / cascading messages** rather than calling `InvokeAsync` from within. If the work genuinely needs to span two distinct, independently retriable handlers, use a **Wolverine Saga** (which is explicitly the Wolverine team's recommended composition primitive for this shape) and accept that each saga step is its own transaction with compensating actions for failure.

---

## Primary Question Answer with Source Citations

> **Q:** When a Wolverine 5.x `[AggregateHandler]` invokes `await IMessageBus.InvokeAsync<TResponse>(downstreamCommand, ct)` from inside its handler body, does the downstream handler execute inside the same Postgres transaction as the outer handler?

**A: No. The downstream handler runs in its own transaction, opened on its own `IDocumentSession`, and that transaction commits (or aborts) independently of the outer handler's transaction.**

### Trace through the source

1. `IMessageBus.InvokeAsync<T>(message, ct, timeout)` on `Wolverine.Runtime.MessageBus` resolves to:

   ```csharp
   public Task<T> InvokeAsync<T>(object message, CancellationToken cancellation = default, TimeSpan? timeout = default)
   {
       Runtime.AssertHasStarted();
       return Runtime.FindInvoker(message.GetType()).InvokeAsync<T>(message, this, cancellation, timeout);
   }
   ```
   Verified directly in [`MessageBus.cs` on `main`](https://github.com/JasperFx/wolverine/blob/main/src/Wolverine/Runtime/MessageBus.cs). No transaction state, no `IDocumentSession`, no outbox enrollment is propagated to the invoker beyond the `MessageBus` instance itself.

2. `Runtime.FindInvoker(...)` returns an `IMessageInvoker` for that message type. For local-handled messages, this dispatches into the per-message `Executor`. The DeepWiki source map shows the entry path `MessageBus.InvokeAsync → IMessageInvoker → Executor.ExecuteAsync(MessageContext, ct)` — see [`src/Wolverine/Runtime/MessageBus.cs:182-211`](https://github.com/JasperFx/wolverine/blob/8af80f29/src/Wolverine/Runtime/MessageBus.cs#L182-L211), [`src/Wolverine/Runtime/HandlerPipeline.cs:56-108`](https://github.com/JasperFx/wolverine/blob/8af80f29/src/Wolverine/Runtime/HandlerPipeline.cs#L56-L108), [`src/Wolverine/Runtime/Handlers/Executor.cs:164-209`](https://github.com/JasperFx/wolverine/blob/8af80f29/src/Wolverine/Runtime/Handlers/Executor.cs#L164-L209) ([Source](https://deepwiki.com/JasperFx/wolverine/2.1-message-handling-system)).

3. The generated `MessageHandler` for any handler that depends on Marten persistence (which `[AggregateHandler]` always does) opens a **brand-new** `IDocumentSession` inside its `HandleAsync(MessageContext context, CancellationToken cancellation)` body. Verbatim from the documented codegen for `MarkItemReadyHandler` and the IncidentService sample:

   ```csharp
   public override async Task HandleAsync(MessageContext context, CancellationToken cancellation)
   {
       var markItemReady = (MarkItemReady)context.Envelope.Message;
       await using var documentSession = _outboxedSessionFactory.OpenSession(context);
       var eventStream = await documentSession.Events
           .FetchForWriting<Order>(markItemReady.OrderId, cancellation);
       // ... user code runs ...
       eventStream.AppendMany(outgoing1);
       await documentSession.SaveChangesAsync(cancellation);
   }
   ```
   ([Source: Aggregate Handlers and Event Sourcing | Wolverine](https://wolverinefx.net/guide/durability/marten/event-sourcing.html); [Source: CategoriseIncidentHandler codegen](https://jeremydmiller.com/2023/12/06/building-a-critter-stack-application-wolverines-aggregate-handler-workflow-ftw/); [Source: Event Sourcing and CQRS with Marten | Wolverine](https://wolverinefx.net/tutorials/cqrs-with-marten.html))

   The key call is `_outboxedSessionFactory.OpenSession(context)` — `OutboxedSessionFactory` lives in `Wolverine.Marten.Publishing` and produces a **new** `IDocumentSession` enlisted in the Wolverine outbox using the supplied `MessageContext` ([Source](https://wolverinefx.net/guide/http/multi-tenancy)). One handler invocation = one session = one Postgres connection = one transaction at `SaveChangesAsync` time.

4. When you call `InvokeAsync` from inside the outer `[AggregateHandler]`, Wolverine runs the *whole* generated inner-handler body — including its own `OpenSession` and its own `SaveChangesAsync`. There is **no plumbing** that takes the outer handler's `IDocumentSession`, ambient `Transaction` (an `IEnvelopeTransaction` on `MessageBus`), or open Npgsql connection and hands it down to the inner handler. The inner handler creates its own. This is reinforced by the warning in the official Sagas docs:

   > "**Do not call `IMessageBus.InvokeAsync()` within a Saga related handler to execute a command on that same Saga. You will be acting on old or missing data.** Utilize cascading messages for subsequent work."
   > ([Source: Sagas | Wolverine](https://wolverinefx.net/guide/durability/sagas.html))

   The reason is precisely the question being asked: the inner `InvokeAsync` opens its own session, and the outer saga state has not yet been persisted because `SaveChangesAsync` for the outer hasn't run. Milan Jovanović independently summarizes the same rule and the same reason: *"You'll be acting on stale or missing data. Use cascading messages (return values) for subsequent work."* ([Source](https://www.milanjovanovic.tech/blog/implementing-the-saga-pattern-with-wolverine)).

5. Wolverine's transactional middleware contract is one transaction *per handler invocation*: *"When using the transactional middleware with Marten, Wolverine is assuming that there will be a single, atomic transaction for the entire message handler."* ([Source: Transactional Middleware | Wolverine](https://wolverinefx.net/guide/durability/marten/transactional-middleware)) — emphasis on *the* handler (singular). There is no "join the parent's transaction" mode.

### What this means concretely for the Slice 1 flow

Time-ordered actuals (with `Policies.AutoApplyTransactions()` enabled):

| Step | Transaction | Persistence event |
|---|---|---|
| 1. Outer handler starts; outer `IDocumentSession` opened, outer outbox enrolled | **TX_outer (open)** | none committed |
| 2. Outer LLM call returns; outer code calls `await bus.InvokeAsync<PlanGenerationResponse>(new GeneratePlanCommand(...), ct)` | TX_outer **still open**, control yields to inner pipeline | none committed |
| 3. Inner handler runs: `_outboxedSessionFactory.OpenSession(innerContext)` opens **TX_inner**, six LLM calls, `session.Events.StartStream<Plan>(planId, new PlanGenerated(...))`, `dbContext.UserProfile.CurrentPlanId = planId` | **TX_inner (open)** in parallel with TX_outer | none committed |
| 4. Inner `SaveChangesAsync` executes | **TX_inner COMMITS** | Plan stream + UserProfile row written, **independently of outer** |
| 5. Outer code resumes after `await`; produces final event tuple including `OnboardingCompleted` | TX_outer still open | none committed yet |
| 6. Outer transactional middleware calls outer `SaveChangesAsync` | **TX_outer COMMITS** | onboarding events including `OnboardingCompleted` written |

**Failure modes the spec assumes don't exist but do:**

- LLM fails inside step 3 → TX_inner aborts. The outer code now has a thrown exception bubbling out of `await InvokeAsync`. Outer `SaveChangesAsync` is never called → TX_outer rolls back → onboarding events are **not** committed. *(This is actually the happy-ish failure case for atomicity, but only because the outer threw — see below.)*
- Inner handler **succeeds**, then a transient PG error or `ConcurrencyException` happens at outer `SaveChangesAsync` → TX_outer rolls back, but **TX_inner has already committed**. Plan stream and `UserProfile.CurrentPlanId` exist with a `userId` whose onboarding stream has no `OnboardingCompleted`. The next request looks at onboarding status and re-runs onboarding turns; plan-gen runs again; you get a **second** Plan stream for the same user. Cross-table inconsistency.
- Inner LLM partial failure: depends on whether you call `SaveChangesAsync` between LLM calls in the inner handler. If you do — and `AutoApplyTransactions` will call it once at the end automatically, but if you call it manually mid-handler you'll get partial commits. The Wolverine team explicitly warns: *"it's best to not directly call `IDocumentSession.SaveChangesAsync()` yourself because that negates the transactional middleware's ability to mark the transaction boundary and can cause unexpected problems with the outbox."* ([Source](https://wolverinefx.net/guide/durability/marten/transactional-middleware)).

The "outer rolls back the inner" assumption is therefore **only true in the trivial case where the inner throws**. The dangerous failure modes are the other direction: inner succeeds, outer rolls back, and you've leaked an inconsistent half‑state into the database with no automatic compensation.

---

## Sub-Question Answers

### 1. Transaction propagation semantics

`IMessageBus.InvokeAsync<T>` does **not** reuse the calling handler's `IDocumentSession` / `DbContext`, and does not enlist itself in the outer Postgres connection. It dispatches via `Runtime.FindInvoker(...).InvokeAsync<T>(message, this, ct, timeout)` ([Source: `MessageBus.cs`](https://github.com/JasperFx/wolverine/blob/main/src/Wolverine/Runtime/MessageBus.cs)) and the resulting handler invocation builds its own session through `OutboxedSessionFactory.OpenSession(context)` from a fresh `MessageContext` ([Source: codegen example](https://wolverinefx.net/guide/http/multi-tenancy); [Source: Aggregate Handlers codegen](https://wolverinefx.net/guide/durability/marten/event-sourcing.html)).

Registration matrix:

| Configuration | Inner handler runs in… |
|---|---|
| `Policies.AutoApplyTransactions()` enabled (your case) | Its own new `IDocumentSession` + own TX, opened via `OutboxedSessionFactory` |
| `[Transactional]` attribute on inner handler explicitly | Same — own session, own TX |
| No transactional middleware on inner handler | Inner handler doesn't auto-commit, but if user code on the inner manually calls `SaveChangesAsync` it's still on the inner's session, still its own TX |
| Inner handler is `[AggregateHandler]` (your case for `GeneratePlanCommand`) | Inner handler always opens its own session via `FetchForWriting` / `StartStream` codegen — own TX |

There is no setting on `IMessageBus` or the Marten integration that "joins the caller's transaction." The Marten integration's `IntegrateWithWolverine()` registers the Wolverine outbox & middleware on the host but doesn't change the per-handler session lifecycle ([Source](https://wolverinefx.net/guide/durability/marten/)).

### 2. `[AggregateHandler]` body vs `OutgoingMessages` return — the canonical separation

**Yes, the documented separation is exactly this:**

- **Inside the handler body**, with the injected `IDocumentSession`: do everything you need to be atomic with the event append. Wolverine's transactional middleware brackets ONE transaction around the *entire body* and one `SaveChangesAsync` call at the end. ([Source: Transactional Middleware](https://wolverinefx.net/guide/durability/marten/transactional-middleware))
- **Returned in `OutgoingMessages` / cascading messages**: things you want delivered *after* the outer transaction commits, via Wolverine's transactional outbox. *"Cascading messages returned from handler methods will not be sent out until after the original message succeeds and is part of the underlying transport transaction."* ([Source: Cascading Messages](https://wolverinefx.net/guide/handlers/cascading.html))

`[AggregateHandler]` formalizes this beautifully: *"Events and messages returned from the handler are saved and dispatched atomically. Wolverine uses Marten's outbox to store messages until the transaction commits."* ([Source: martendb.io tutorial](https://martendb.io/tutorials/wolverine-integration))

The Critter Stack canonical idiom for "I want this work to be atomic with the event append" is therefore:

- For **Marten document/stream writes**: return `IMartenOp` side effects (`MartenOps.StartStream`, `MartenOps.Store`, `MartenOps.Insert`, `MartenOps.Update`, `MartenOps.Delete`) or yield events from the handler. ([Source: Marten Operation Side Effects](https://wolverinefx.net/guide/durability/marten/operations.html))
- For **outgoing messages**: return them as cascading messages or via `MartenOps.PublishMessage(cmd)` returned in `OutgoingMessages`. They go into the Wolverine outbox in the same Postgres transaction as the events. ([Source: Marten as Transactional Outbox](https://wolverinefx.net/guide/durability/marten/outbox))
- For **HTTP responses**: use `(ResponseDto, IMartenOp, OutgoingMessages)` tuple returns or the `[EmptyResponse]` attribute. ([Source: Integration with Marten | Wolverine](https://wolverinefx.net/guide/http/marten))

`InvokeAsync` is the **wrong tool** for "atomic with the event append" — it's fundamentally a fresh-pipeline-execution primitive. The Wolverine docs themselves position it as a mediator-style request/reply for *external* callers (HTTP endpoints, Hot Chocolate mutations, console drivers), not for in-handler composition ([Source: Sending Messages with IMessageBus](https://wolverinefx.net/guide/messaging/message-bus); [Source: Wolverine as Mediator](https://wolverinefx.net/tutorials/mediator)).

### 3. Cross-stream atomicity within one `IDocumentSession`

**Yes, multiple streams written through the same `IDocumentSession` commit atomically when `SaveChangesAsync` runs**, *provided they target the same `IDocumentStore`*. This is a Marten guarantee, not a Wolverine one:

> "Marten has long had the ability to support both reading and appending to multiple event streams at one time with guarantees about data consistency and even the ability to achieve strongly consistent transactional writes across multiple streams at one time. Wolverine just added some syntactic sugar to make cross-stream command handlers be more declarative with its 'aggregate handler workflow' integration with Marten." ([Source: jeremydmiller.com](https://jeremydmiller.com/tag/marten/page/2/))

The transfer-money sample with two `IEventStream<Account>` instances on one session, committed atomically by the Wolverine middleware's single `SaveChangesAsync`, is the canonical demonstration ([Source: Aggregate Handlers and Event Sourcing](https://wolverinefx.net/guide/durability/marten/event-sourcing.html)).

So if you collapse the work into a single handler that injects `IDocumentSession`, calls `FetchForWriting<OnboardingView>(...)` for the onboarding stream **and** `session.Events.StartStream<Plan>(planId, new PlanGenerated(...))` on the same session, plus stages the EF Core `UserProfile` update, all inside the body — *and* the EF Core integration is wired correctly through Wolverine — you get one Postgres transaction, one batched commit, and cross-stream atomicity for free. The crucial constraint: **everything must flow through the single handler's session**, not through `InvokeAsync` to a different handler.

(Note the EF Core + Marten dual-write caveat: with `Marten.EntityFrameworkCore` and Wolverine.EntityFrameworkCore both wired, Wolverine will weave both unit-of-work commits, but they are still two separate connection-level transactions on the same Postgres database unless you explicitly opt into shared connection mode. The `Wolverine.Marten` outbox tables and Marten's events both live in one transaction; the EF Core `UserProfile` write is a second transaction batched immediately after. For RunCoach's needs — `CurrentPlanId` ↔ Plan stream consistency — the practical guarantee is that `UserProfile.CurrentPlanId` is written *after* the Plan stream is durable, and any failure between them is recoverable by a startup reconciliation job. This is out of scope but worth flagging.)

### 4. Failure-mode behavior of outer `(events, outgoingMessages)` tuple when inner `InvokeAsync` throws

If `await bus.InvokeAsync<PlanGenerationResponse>(...)` throws inside the outer handler's body:

- The exception **propagates up** through the outer handler method.
- The outer transactional middleware's `try/finally` does NOT call `SaveChangesAsync` on the outer session when the user code throws — instead it propagates the exception to the Executor, which applies the configured retry/error policy.
- The outer `IDocumentSession` is disposed without committing. **Outer events are not persisted.** This is the safe direction.
- However: **inner work may already have committed** at the moment the inner threw or before the inner threw. There is no Wolverine mechanism that issues a `ROLLBACK` against the inner's already-committed connection. (Postgres can't rollback a committed transaction; that's the whole point of an outbox pattern.)

If `await InvokeAsync` returns **successfully**, then later in the outer handler something else throws (e.g. concurrency exception when the outer middleware tries to commit, transient Npgsql, or even a `cancellation` token firing between the await and the end of method):

- TX_inner has **already committed** ([Source: Build Resilient Systems](https://jeremydmiller.com/2024/12/08/build-resilient-systems-with-wolverines-transactional-outbox/)).
- TX_outer rolls back.
- You now have a Plan stream + populated `UserProfile.CurrentPlanId` for a user whose onboarding stream is still missing `OnboardingCompleted`. **This is the dangerous state described in the Slice 1 problem statement, and Wolverine will not prevent it.**

The rollback boundary is the **outer handler invocation**, period. It does not extend across `await InvokeAsync` calls. The HTTP request boundary is irrelevant to Wolverine — there is no per-request ambient transaction unless you build one yourself.

### 5. Can the `(events, outgoingMessages)` return contract express the "fallback" event sequencing?

**No, not cleanly.** Wolverine's `[AggregateHandler]` return contract supports:

- A single value type (response) +
- `IEnumerable<object>` of events (or a strongly-typed `Events` accumulator) +
- `OutgoingMessages` for cascading messages +
- `UpdatedAggregate` marker for HTTP response shaping +
- Side-effect markers (`IMartenOp`, `IStartStream`, etc.).

All of those are evaluated **after** the handler body returns and applied in a single `SaveChangesAsync` call. The handler body itself is executed once, top-to-bottom; you cannot say *"emit events A, B, then synchronously execute another handler that may fail, then emit event C, all in one transaction"* using only the return contract. The events are returned as a collection at the end of the body, not appended progressively to a transaction-scoped buffer that other handlers can interleave with.

You **could** approximate the "emit A, B, run plan-gen, emit C" sequence by having two handlers chained via cascading messages, **but** cascaded messages run *after* the outer transaction commits ([Source: Cascading Messages](https://wolverinefx.net/guide/handlers/cascading.html); [Source: Event Forwarding](https://wolverinefx.net/guide/durability/marten/event-forwarding.html)) — so the chain isn't atomic anyway. That's actually what the Wolverine team wants you to do, but it means accepting eventual consistency, not transactional atomicity.

The canonical workarounds, in order of preference:

1. **Single handler, all work in body** (recommended for this slice). Inject `IDocumentSession`, do the LLM calls + `FetchForWriting<OnboardingView>` + `session.Events.StartStream<Plan>(...)` + EF update all in one method. Returns `(PlanGenerationResponse, OutgoingMessages)` or just `IMartenOp[]`. One transaction, full atomicity.
2. **Compound handler** with `Before/Load/Handle` methods sharing the same injected session ([Source: Compound Handlers](https://jeremydmiller.com/2023/03/07/compound-handlers-in-wolverine/); [Source: Message Handlers](https://wolverinefx.net/guide/handlers/)). All methods execute inside the same transaction bracket. Useful when you want to keep the LLM-orchestration code in a separate testable function.
3. **Wolverine Saga** if you genuinely need two retriable steps with state between them, accepting that each step is its own transaction. This is the documented pattern for multi-step business workflows ([Source: Sagas | Wolverine](https://wolverinefx.net/guide/durability/sagas.html)).
4. **Explicit `IDocumentSession`/`IMartenOutbox` with manual `SaveChangesAsync`** if you really want imperative control — but the docs explicitly discourage this when transactional middleware is on ([Source](https://wolverinefx.net/guide/durability/marten/transactional-middleware)).

For RunCoach Slice 1, **option 1 is the right answer** because the entire pipeline (LLM turn → terminal-branch detection → 6-call plan-gen chain → Plan stream creation → UserProfile update → response with planId) is already serial and request-scoped. Splitting it into two handlers buys nothing and breaks atomicity.

### 6. `MartenOps.PublishMessage` vs `IMessageBus.InvokeAsync` — definitive 2026 comparison

| Dimension | `MartenOps.PublishMessage(cmd)` (or returned cascading message) | `await IMessageBus.InvokeAsync<TResponse>(cmd, ct)` |
|---|---|---|
| **Transaction scope** | Enrolled in the **caller's** outer transaction via the Wolverine outbox; persisted in `wolverine_outgoing_envelopes` in the **same Postgres TX** as the outer handler's events ([Source](https://wolverinefx.net/guide/durability/marten/outbox)) | Opens a **new** `IDocumentSession` and a **new** Postgres transaction in the inner handler's pipeline; commits independently |
| **When does it run?** | After outer TX commits — fire-and-forget over the in-memory or durable local queue / external broker | Synchronously, *before* outer TX commits, on the calling thread |
| **Response semantics** | One-way; no return value to the caller | Request-response; returns `TResponse` to the caller |
| **Failure of downstream** | Outer TX still commits; downstream message goes to retry/DLQ/inbox like any other Wolverine message | Outer TX is *not* committed (because outer body throws), but downstream's own TX **may have already committed**. Wolverine cannot un-commit it. |
| **Atomicity with outer event append** | YES — same transaction, same batched command ([Source](https://wolverinefx.net/guide/durability/marten/outbox)) | NO — separate transaction by construction |
| **Production usage for "sync request → atomic side effects"** | This **is** the canonical pattern. The Critter Stack's whole `[AggregateHandler]` design is built around it. | Used when the caller is *outside* Wolverine handlers (HTTP endpoints not using `WolverinePost`, Hot Chocolate mutations, console drivers, integration tests) ([Source: Wolverine as Mediator](https://wolverinefx.net/tutorials/mediator); [Source: Error Handling notes about Hot Chocolate use case](https://wolverinefx.net/guide/handlers/error-handling.html)) |

For RunCoach's "SPA POST → atomic event append + plan generation + response with planId" shape, `MartenOps.PublishMessage` doesn't actually help directly because it's a *one-way* fire-and-forget (you'd lose the synchronous `planId` response). The response requirement is what makes the spec author reach for `InvokeAsync`. The right answer is to **not split the work across two handlers at all**: do plan generation in the same handler body and return the `planId` directly as part of the response tuple. If a future slice needs plan generation triggered from many places, *then* extract a service class (not a handler) that the `[AggregateHandler]` calls as a plain async method — share the injected `IDocumentSession` via constructor or method parameter so the writes still flow through the outer transaction.

### 7. Verification path

See the **Verification Test Pattern** section below for a complete xUnit v3 + AssemblyFixture + Testcontainers Postgres test that empirically demonstrates the transaction split.

---

## Canonical Pattern Recommendation

For "I want this multi-step pipeline to be atomic across two logical concerns and one HTTP request" with `[AggregateHandler]` over Marten:

```csharp
// THE CANONICAL 2026 PATTERN — single handler, single session, single transaction

public sealed record SubmitUserTurn(Guid UserId, Guid OnboardingStreamId, string TurnContent);

public sealed record SubmitUserTurnResponse(
    Guid OnboardingStreamId,
    Guid? PlanId,
    bool OnboardingComplete);

public static class SubmitUserTurnHandler
{
    [AggregateHandler]
    public static async Task<(SubmitUserTurnResponse, OutgoingMessages)> Handle(
        SubmitUserTurn command,
        IEventStream<OnboardingView> stream,         // outer aggregate, FetchForWriting under the hood
        IDocumentSession session,                    // SAME session used for everything
        UserProfileDbContext ef,                     // EF row update on the SAME logical TX
        IClaudeClient anthropic,
        CancellationToken ct)
    {
        var view = stream.Aggregate;
        var nextTurn = await anthropic.GetNextTurnAsync(view, command.TurnContent, ct);

        var outgoing = new OutgoingMessages();

        if (!nextTurn.IsTerminal)
        {
            stream.AppendOne(new TurnSubmitted(command.TurnContent, nextTurn.AssistantMessage));
            return (new SubmitUserTurnResponse(command.OnboardingStreamId, null, false), outgoing);
        }

        // Terminal branch: emit pre-terminal events first
        stream.AppendMany(nextTurn.PreTerminalEvents);   // e.g. ProfileFinalized

        // Run plan generation INLINE on the SAME session — no InvokeAsync
        var planId = CombGuidIdGeneration.NewGuid();
        var planEvents = await PlanGenerationService.GeneratePlanAsync(
            anthropic, view, command.UserId, planId, ct);

        session.Events.StartStream<Plan>(planId, planEvents);

        // EF row update on the same logical Wolverine transaction
        var profile = await ef.UserProfiles.SingleAsync(p => p.UserId == command.UserId, ct);
        profile.CurrentPlanId = planId;

        // ONLY NOW append the terminal event — last line in the transaction
        stream.AppendOne(new OnboardingCompleted(planId));

        // Cascading messages go via the outbox, after this TX commits
        outgoing.Add(new SendWelcomeEmail(command.UserId, planId));

        return (
            new SubmitUserTurnResponse(command.OnboardingStreamId, planId, true),
            outgoing);
    }
}

// PlanGenerationService is a plain DI service, NOT a handler.
// It does NOT call SaveChangesAsync. It builds and returns events.
public static class PlanGenerationService
{
    public static async Task<IReadOnlyList<object>> GeneratePlanAsync(
        IClaudeClient anthropic,
        OnboardingView view,
        Guid userId,
        Guid planId,
        CancellationToken ct)
    {
        var macro = await anthropic.GenerateMacroAsync(view, ct);
        var meso1 = await anthropic.GenerateMesoAsync(macro, 1, ct);
        var meso2 = await anthropic.GenerateMesoAsync(macro, 2, ct);
        var meso3 = await anthropic.GenerateMesoAsync(macro, 3, ct);
        var meso4 = await anthropic.GenerateMesoAsync(macro, 4, ct);
        var micro = await anthropic.GenerateMicroAsync(meso1, ct);

        return new object[]
        {
            new PlanGenerated(planId, userId, macro),
            new MesoCycleCreated(1, meso1),
            new MesoCycleCreated(2, meso2),
            new MesoCycleCreated(3, meso3),
            new MesoCycleCreated(4, meso4),
            new FirstMicroCycleCreated(micro),
        };
    }
}
```

**Why this is correct:**

- `[AggregateHandler]` middleware gives the handler `IEventStream<OnboardingView>` via `FetchForWriting` ([Source](https://wolverinefx.net/guide/durability/marten/event-sourcing.html)).
- The same `IDocumentSession` is injected as a method parameter, and used to `StartStream<Plan>(...)`. Marten supports multiple-stream writes per session as one atomic commit ([Source](https://martendb.io/scenarios/aggregates-events-repositories); [Source](https://jeremydmiller.com/tag/marten/page/2/)).
- `PlanGenerationService` is a plain function over Anthropic — no Wolverine, no `SaveChangesAsync`. It returns events; the handler body owns the persistence boundary.
- LLM-call failures throw out of `GeneratePlanAsync`, which propagates through the handler, which causes the transactional middleware to skip `SaveChangesAsync`. **Nothing** is persisted — not the pre-terminal `ProfileFinalized` events, not the `Plan` stream, not the `CurrentPlanId` update, not `OnboardingCompleted`. The user's onboarding stream is unchanged; the retry endpoint sees onboarding as still-in-progress.
- `OnboardingCompleted` is appended **last**, so even partial-progress reads from a failed-and-retrying transaction (which Postgres won't expose anyway under default isolation, but defense in depth) can never see a "complete" onboarding without a corresponding plan.
- `SendWelcomeEmail` goes via cascading message → Wolverine outbox → only fires after the full transaction commits ([Source](https://wolverinefx.net/guide/handlers/cascading.html)).
- This is also dramatically simpler to integration-test: one Wolverine `InvokeAsync` from the test → assert all final state, or assert that *no* state changed if you injected a failing Anthropic stub.

---

## Slice 1 Spec Redesign

### Severity-classified action items

| # | Action | Severity | Rationale |
|---|---|---|---|
| 1 | **Remove `GeneratePlanCommand` as a separate Wolverine command.** Convert `PlanGenerationHandler` into a plain `PlanGenerationService` (plain DI service, returns events). | **CRITICAL** | Eliminates the cross-handler transaction split. |
| 2 | **Inline plan generation into `SubmitUserTurnHandler`** on the terminal branch. Use the same injected `IDocumentSession` for both onboarding `FetchForWriting` and `session.Events.StartStream<Plan>(planId, ...)`. | **CRITICAL** | One handler = one transaction. |
| 3 | **Append `OnboardingCompleted` *last* in the event sequence.** Append `ProfileFinalized` (or whatever pre-terminal events you have) before the plan-gen call; append `OnboardingCompleted` after plan-gen returns events. | **HIGH** | Defense-in-depth ordering; matches the spec's "fallback design" but expressible cleanly because everything is one TX. |
| 4 | **Move the `UserProfile.CurrentPlanId` update inside the same handler**, after the Plan events are staged on the Marten session but before the handler returns. | **HIGH** | EF row write rides the same Wolverine transactional middleware bracket. |
| 5 | **Drop `[AggregateHandler]`-level usage of `IMessageBus.InvokeAsync` from the spec entirely.** Replace any "downstream command" pattern with: cascading messages (post-commit, async) for fire-and-forget, plain service classes for sync work. | **CRITICAL** | Aligns with Wolverine team's documented best practice ([Source: Best Practices](https://wolverinefx.net/introduction/best-practices)). |
| 6 | **Convert `SendWelcomeEmail`-style downstream notifications to cascading messages.** Return them in `OutgoingMessages` from the handler. | MEDIUM | They legitimately *should* be eventual / post-commit. Wolverine outbox handles delivery durability. |
| 7 | **Add an integration test that proves: LLM failure → no onboarding events committed AND no Plan stream exists AND `UserProfile.CurrentPlanId` is null.** See the next section for the test pattern. | **HIGH** | This is the regression test for the original concern. |
| 8 | **Add an integration test for the happy path** that asserts onboarding stream contains `OnboardingCompleted`, Plan stream contains `PlanGenerated` + 4 meso + 1 micro events, and `UserProfile.CurrentPlanId == planId`, all readable in a single read transaction after the handler returns. | **HIGH** | Proves cross-stream + cross-store atomicity. |
| 9 | Verify `Marten.EntityFrameworkCore` is configured to share the connection / use Wolverine's outbox-aware EF integration so the EF write enrolls in the Wolverine transaction. | MEDIUM | If EF runs on a *separate* connection there's still a small dual-write window; document the residual risk. |
| 10 | Set `opts.Events.AppendMode = EventAppendMode.Quick` only after verifying that no inline projection over the onboarding or Plan streams depends on `IEvent.Version`/`IEvent.Sequence` at append time. | LOW | Marten 8 default is `Rich`; switching to `Quick` gives a 40-50% speedup but loses metadata in inline projections ([Source](https://martendb.io/events/appending)). RunCoach's projections are async by default, so `Quick` is likely fine — but it's a checklist item, not free. |

### Corrected event-emission ordering in code

```csharp
[AggregateHandler]
public static async Task<(SubmitUserTurnResponse, OutgoingMessages)> Handle(
    SubmitUserTurn command,
    IEventStream<OnboardingView> stream,
    IDocumentSession session,
    UserProfileDbContext ef,
    IClaudeClient anthropic,
    PlanGenerationService planGen,
    CancellationToken ct)
{
    var view = stream.Aggregate;
    var turn = await anthropic.GetNextTurnAsync(view, command.TurnContent, ct);

    if (!turn.IsTerminal)
    {
        stream.AppendOne(new TurnSubmitted(command.TurnContent, turn.AssistantMessage));
        return (new SubmitUserTurnResponse(stream.Id, null, false), new OutgoingMessages());
    }

    // 1. Pre-terminal onboarding events
    stream.AppendMany(turn.PreTerminalEvents);

    // 2. Plan generation runs INLINE — same session, same TX, throws on LLM failure
    var planId = CombGuidIdGeneration.NewGuid();
    var planEvents = await planGen.GeneratePlanAsync(view, command.UserId, planId, ct);
    session.Events.StartStream<Plan>(planId, planEvents);

    // 3. EF profile update
    var profile = await ef.UserProfiles.SingleAsync(p => p.UserId == command.UserId, ct);
    profile.CurrentPlanId = planId;

    // 4. ONLY NOW commit the terminal event — last
    stream.AppendOne(new OnboardingCompleted(planId));

    var outgoing = new OutgoingMessages
    {
        new SendWelcomeEmail(command.UserId, planId)  // cascades after TX commits
    };

    return (new SubmitUserTurnResponse(stream.Id, planId, true), outgoing);
}
```

---

## Verification Test Pattern

Concrete xUnit v3 + AssemblyFixture + Testcontainers Postgres + Alba pattern that **empirically proves** transaction propagation does NOT extend across `InvokeAsync`. The pattern follows the JasperFx `IncidentService.Tests` IntegrationContext shape ([Source](https://github.com/JasperFx/wolverine/blob/main/src/Samples/IncidentService/IncidentService.Tests/IntegrationContext.cs)) and Alba+Wolverine tracking ([Source](https://wolverinefx.net/guide/testing.html)).

```csharp
// File: tests/RunCoach.IntegrationTests/AppFixture.cs
using Alba;
using JasperFx;
using JasperFx.CommandLine;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wolverine;

namespace RunCoach.IntegrationTests;

public sealed class AppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    public IAlbaHost Host { get; private set; } = default!;
    public string ConnectionString => _pg.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();

        JasperFxEnvironment.AutoStartHost = true;

        Host = await AlbaHost.For<RunCoach.Api.Program>(b =>
        {
            b.UseSetting("ConnectionStrings:Marten", _pg.GetConnectionString());
            b.UseSetting("ConnectionStrings:UserProfileEf", _pg.GetConnectionString());
            b.ConfigureServices(services =>
            {
                services.DisableAllExternalWolverineTransports();
                services.RunWolverineInSoloMode();
                services.MartenDaemonModeIsSolo();

                // Replace the Anthropic client with a stub the tests can control
                services.RemoveAll<IClaudeClient>();
                services.AddSingleton<StubClaudeClient>();
                services.AddSingleton<IClaudeClient>(s => s.GetRequiredService<StubClaudeClient>());
            });
        });
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        await _pg.DisposeAsync();
    }
}

[assembly: AssemblyFixture(typeof(RunCoach.IntegrationTests.AppFixture))]
```

```csharp
// File: tests/RunCoach.IntegrationTests/InvokeAsyncTransactionScopeTests.cs
using JasperFx.Resources;
using Marten;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Wolverine;
using Wolverine.Tracking;

namespace RunCoach.IntegrationTests;

// xUnit v3 — AssemblyFixture-injected via constructor parameter
public sealed class InvokeAsyncTransactionScopeTests(AppFixture fixture)
{
    private readonly AppFixture _fixture = fixture;
    private IDocumentStore Store => _fixture.Host.Services.GetRequiredService<IDocumentStore>();
    private StubClaudeClient Claude => _fixture.Host.Services.GetRequiredService<StubClaudeClient>();

    [Fact]
    public async Task InvokeAsync_FromAggregateHandler_DoesNotShareOuterTransaction()
    {
        // ARRANGE — clean DB
        await _fixture.Host.ResetAllMartenDataAsync();

        var userId = Guid.NewGuid();
        var streamId = Guid.NewGuid();

        await using (var seedSession = Store.LightweightSession())
        {
            seedSession.Events.StartStream<OnboardingView>(streamId,
                new OnboardingStarted(userId));
            await seedSession.SaveChangesAsync();
        }

        // ARRANGE — Claude succeeds for the outer "next turn" decision (terminal),
        // succeeds for macro + meso 1..3, but FAILS on meso 4
        Claude.NextTurnReturns(new Turn { IsTerminal = true });
        Claude.MacroReturns(new MacroPlan(...));
        Claude.MesoReturns(1, new MesoCycle(...));
        Claude.MesoReturns(2, new MesoCycle(...));
        Claude.MesoReturns(3, new MesoCycle(...));
        Claude.MesoThrowsOn(4, new HttpRequestException("LLM 503"));

        var bus = _fixture.Host.Services.GetRequiredService<IMessageBus>();

        // ACT — call the handler. With the BUGGY (current spec) shape it would
        // be GeneratePlanCommand-via-InvokeAsync. We're proving here what happens
        // BEFORE the fix.
        var act = async () => await bus.InvokeAsync<SubmitUserTurnResponse>(
            new SubmitUserTurn(userId, streamId, "I'm aiming for a marathon"));

        // ASSERT — outer must throw because plan-gen failed
        await act.ShouldThrowAsync<HttpRequestException>();

        // ASSERT — KEY: with the BUGGY (InvokeAsync-based) shape, the inner handler's
        // PARTIAL Plan stream IS persisted because the inner TX committed
        // *before* the meso-4 LLM call (or partially) — proving the lack of
        // outer/inner transaction propagation.
        await using var assertSession = Store.LightweightSession();

        // Look for any Plan streams
        var planStreams = await assertSession
            .Query<Plan>()
            .Where(p => p.UserId == userId)
            .ToListAsync();

        // With BUGGY shape: planStreams.Count >= 0 BUT a Plan stream with
        // PartialPlanGenerated events exists. With FIXED shape (single handler,
        // single TX): planStreams.Count == 0.
        // The presence/absence of any Plan event row IS the empirical test:
        var planEventRows = await assertSession.Events.QueryAllRawEvents()
            .Where(e => e.StreamId == /* deterministic planId derivation */)
            .ToListAsync();

        // FIXED shape assertion:
        planEventRows.ShouldBeEmpty(
            "After fix: outer rolls back, inner work was on the same session, " +
            "so meso-4 failure rolls back ALL plan events including macro+meso1-3");

        // BUGGY shape assertion (what you'd see BEFORE the fix):
        // planEventRows.ShouldNotBeEmpty(
        //     "BUGGY: inner handler committed macro+meso1-3 in TX_inner before " +
        //     "meso-4 threw, proving InvokeAsync runs in its own transaction");

        // Also: onboarding stream must NOT contain OnboardingCompleted
        var onboardingEvents = await assertSession.Events.FetchStreamAsync(streamId);
        onboardingEvents.ShouldNotContain(e => e.Data is OnboardingCompleted);

        // And UserProfile.CurrentPlanId must be null
        await using var ef = _fixture.Host.Services
            .GetRequiredService<IDbContextFactory<UserProfileDbContext>>()
            .CreateDbContext();
        var profile = await ef.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.CurrentPlanId.ShouldBeNull();
    }

    [Fact]
    public async Task HappyPath_AllStreamsAndProfileUpdateCommitAtomically()
    {
        await _fixture.Host.ResetAllMartenDataAsync();

        var userId = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        await using (var seed = Store.LightweightSession())
        {
            seed.Events.StartStream<OnboardingView>(streamId, new OnboardingStarted(userId));
            await seed.SaveChangesAsync();
        }
        Claude.SucceedAll();

        var bus = _fixture.Host.Services.GetRequiredService<IMessageBus>();

        // Use Wolverine's tracked-session helper to ensure all outbox messages
        // (e.g. cascading SendWelcomeEmail) flush before assertions
        var tracked = await _fixture.Host.ExecuteAndWaitAsync(() =>
            bus.InvokeAsync<SubmitUserTurnResponse>(
                new SubmitUserTurn(userId, streamId, "marathon")));

        var response = tracked.Executed.SingleMessage<SubmitUserTurnResponse>();
        response.PlanId.ShouldNotBeNull();
        response.OnboardingComplete.ShouldBeTrue();

        // Read back state in ONE session — proves the writes are all visible
        await using var read = Store.LightweightSession();
        var onboarding = await read.Events.AggregateStreamAsync<OnboardingView>(streamId);
        onboarding!.IsComplete.ShouldBeTrue();

        var planEvents = await read.Events.FetchStreamAsync(response.PlanId.Value);
        planEvents.Select(e => e.Data.GetType().Name)
            .ShouldBe(new[] { "PlanGenerated", "MesoCycleCreated", "MesoCycleCreated",
                              "MesoCycleCreated", "MesoCycleCreated", "FirstMicroCycleCreated" });

        await using var ef = _fixture.Host.Services
            .GetRequiredService<IDbContextFactory<UserProfileDbContext>>()
            .CreateDbContext();
        var profile = await ef.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.CurrentPlanId.ShouldBe(response.PlanId);
    }
}
```

The first test is the **empirical proof** of the answer to the primary question: with the original (buggy) `InvokeAsync`-based shape, you would see Plan-stream rows persisted despite the outer handler throwing. With the fixed shape, you don't. Run the test against both code paths during the spec-fix PR; expect it to fail before the fix and pass after.

The Alba + `Host.ExecuteAndWaitAsync` pattern is the canonical Wolverine integration-test idiom ([Source](https://jeremydmiller.com/2023/07/09/integration-testing-an-http-service-that-publishes-a-wolverine-message/); [Source: Test Automation Support | Wolverine](https://wolverinefx.net/guide/testing.html)). `ResetAllMartenDataAsync` is the post-Marten-8.8 reset helper that also handles async daemon ([Source](https://jeremydmiller.com/2025/08/19/faster-more-reliable-integration-testing-against-marten-projections-or-subscriptions/)). `RunWolverineInSoloMode` and `MartenDaemonModeIsSolo` give you faster, deterministic test startup. `JasperFxEnvironment.AutoStartHost = true` is required for `WebApplicationFactory`/`AlbaHost` to bypass the JasperFx CLI bootstrap.

---

## Gotchas

### Marten `EventAppendMode.Quick` interactions

- `Quick` mode appends events without round-tripping for global sequence/version metadata first. If your inline projection over the onboarding stream needs `IEvent.Version` at append time, switching to `Quick` will silently break it ([Source](https://martendb.io/events/appending.html)). For RunCoach: the onboarding-stream projection is rebuilt inline by `FetchForWriting<OnboardingView>` from raw events, so version-at-append usage is unlikely — verify by code review and integration test.
- `Quick` mode works correctly inside the single-handler atomic pattern recommended above; the `SaveChangesAsync` boundary is unchanged.
- If you want to override event timestamps for testing/replay, you must use `EventAppendMode.QuickWithServerTimestamps` (Marten 8.4+) ([Source](https://jeremydmiller.com/2025/07/27/metadata-tracking-improvements-in-marten/)). Otherwise `Quick` takes timestamps from the database server.

### Outbox + `Policies.AutoApplyTransactions()` interactions

- With `IntegrateWithWolverine()` + `AutoApplyTransactions()`, the middleware automatically detects `IDocumentSession` (or any service that depends on it) in the handler's parameters and brackets the transaction. Plain methods inside the handler that take `IDocumentSession` will share that session via Wolverine's codegen ([Source](https://wolverinefx.net/guide/durability/marten/transactional-middleware)).
- **Do NOT call `session.SaveChangesAsync` yourself** inside an aggregate handler. That commits the transaction early, drops the outbox enrollment, and any subsequent staged outgoing messages won't be persisted in the same TX ([Source](https://wolverinefx.net/guide/durability/marten/transactional-middleware)).
- If you genuinely need to inspect Marten changes mid-handler without committing, take `IDocumentOperations` instead of `IDocumentSession` — it's the read/write API minus `SaveChangesAsync` ([Source](https://wolverinefx.net/guide/durability/marten/transactional-middleware)).
- The outbox flushes outgoing messages **after** `SaveChangesAsync` returns successfully. If TX commit succeeds but the broker is unreachable, the durability agent retries from the `wolverine_outgoing_envelopes` table ([Source](https://wolverinefx.net/guide/durability/)).

### Conjoined-tenancy interactions

- `Wolverine.Marten` respects Marten's conjoined tenancy and propagates `TenantId` from `MessageContext` into the `IDocumentSession` it opens via `OutboxedSessionFactory` ([Source](https://wolverinefx.net/guide/durability/marten/multi-tenancy)).
- For `InvokeAsync` from inside a handler, the tenant id flows automatically via the calling `MessageContext` (the `MessageBus` is `this`, which carries the tenant). For `InvokeForTenantAsync` you can override.
- For the recommended single-handler pattern: the outer `[AggregateHandler]` already has the correct tenant from the original HTTP request (`MessageContext.TenantId`), and the same session inherits it. **No special handling required** — but if you ever do split into a saga, ensure the saga's `Start()` method either preserves the tenant on the saga state or uses `InvokeForTenantAsync` for downstream cross-tenant work.
- Conjoined-tenancy + multi-stream writes in the same session: Marten enforces tenant isolation per-document/per-stream automatically; cross-tenant writes in one session will throw ([Source](https://wolverinefx.net/guide/durability/marten/multi-tenancy)). For RunCoach, all writes in the recommended pattern are scoped to the same `userId` → same tenant → safe.

### Ordering / sequencing pitfalls

- **`OnboardingCompleted` MUST be appended last** in the handler body, *after* the `StartStream<Plan>` call has staged its events on the session. Otherwise an inline single-stream projection on `OnboardingView` could be tempted to read `Plan` data that doesn't exist yet at projection time. With `Quick` mode this is even more important because the projection runs without full sequence metadata.
- Cascading messages emitted via `OutgoingMessages` are dispatched *after* the outer TX commits; **do not** rely on them as part of the atomic guarantee. They're for "we successfully completed onboarding, now eventually email the user" semantics.
- Wolverine's `EventForwardingToWolverine()` will *also* publish events through the outbox if you opt in, but the docs explicitly recommend choosing **either** event forwarding **or** explicit cascading messages, not both ([Source](https://wolverinefx.net/guide/durability/marten/event-forwarding.html)). For Slice 1 you don't need event forwarding.
- If a `ConcurrencyException` fires from `FetchForWriting` (someone else wrote to the onboarding stream between the read and the commit), Wolverine's default policy is to retry — but with `AutoApplyTransactions` and a handler this expensive (6 LLM calls), retrying the **whole** handler will make 6 more LLM calls. Configure: `opts.Policies.OnException<ConcurrencyException>().RetryTimes(3)` is the documented pattern ([Source](https://wolverinefx.net/tutorials/concurrency.html)) — but for RunCoach with expensive LLM work, prefer optimistic-version checking on the input command (`FetchForWriting<OnboardingView>(streamId, command.ExpectedVersion)`) and `MoveToErrorQueue()` on `ConcurrencyException` so concurrent submissions surface as 409s instead of silently re-running plan generation.

### `MediatorOnly` durability mode pitfall

If anyone is tempted to make Wolverine "lighter" by setting `opts.Durability.Mode = DurabilityMode.MediatorOnly` ([Source](https://wolverinefx.net/tutorials/mediator)), the outbox is disabled, durable inbox is disabled, and `SendAsync`/`PublishAsync` will throw `InvalidOperationException`. The transactional middleware still works for `IDocumentSession` brackets, but you lose the outbox guarantee for cascading messages. RunCoach should NOT use `MediatorOnly` because the durable outbox is exactly what makes "post-commit email send" reliable.

### Codegen scope vs Wolverine scope (do NOT resolve `IMessageBus` from a child scope)

Wolverine docs warn that resolving `IMessageBus` from a fresh `IServiceProvider.CreateScope()` gives you a **different** `MessageContext` than the one currently handling the message, which means a different (default) tenant id and a disconnected outbox ([Source: Best Practices](https://wolverinefx.net/introduction/best-practices)). If your `PlanGenerationService` ever takes `IServiceProvider` directly and creates its own scope to resolve services, it will silently break tenant propagation. Take `IDocumentSession` and `IClaudeClient` as direct constructor parameters; don't service-locate from inside the service.

---

## Slice 4 Implication Callout

The original Slice 1 spec assumed Slice 4 would flip `await IMessageBus.InvokeAsync<PlanGenerationResponse>(...)` to `await IMessageBus.PublishAsync(new GeneratePlanCommand(...))` — a one-line change to make plan generation async-after-commit. That change shape is **predicated on the buggy assumption** that the outer transaction extends across `InvokeAsync`. Once we adopt the canonical single-handler pattern, the Slice 4 flip becomes:

- **Slice 1 (recommended) shape:** Plan generation is inline in `SubmitUserTurnHandler`. HTTP response carries `planId`. Onboarding+Plan+UserProfile committed atomically.
- **Slice 4 desired shape:** HTTP response returns immediately on terminal turn with `OnboardingCompleted` only. Plan generation moves to a separate handler triggered by a cascading `GeneratePlanCommand` returned in `OutgoingMessages`. The `Plan` stream and `UserProfile.CurrentPlanId` are populated **after** the HTTP response, eventually-consistent. Frontend has to poll `/api/v1/plans/current` or accept a SignalR notification.

The transition between them is **NOT one line** — but it's not transaction-scope-related either. The differences are:

| Aspect | Slice 1 (sync, atomic) | Slice 4 (async, eventually-consistent) |
|---|---|---|
| Where plan generation runs | Inside `SubmitUserTurnHandler` body | In a separate `GeneratePlanHandler` triggered by cascading message |
| `OnboardingCompleted` event | Appended after Plan stream is staged (still atomic with everything) | Appended immediately; Plan stream created later |
| HTTP response | Returns `planId` synchronously | Returns "onboarding complete, plan generating" with no `planId` |
| Failure recovery | Single TX rolls back everything | Saga-like: `GeneratePlanHandler` retries via Wolverine's error policies; if it permanently fails, dead-letter and a manual retry endpoint |
| Frontend impact | Redirect to `/` with planId in response | Redirect + show "Generating your plan…" with polling/SignalR |
| Test coverage | Integration test in one tracked session | Integration test using `Host.ExecuteAndWaitAsync` to wait for cascading message processing ([Source](https://jeremydmiller.com/2023/07/09/integration-testing-an-http-service-that-publishes-a-wolverine-message/)) |

The handler-side change for Slice 4 is roughly:

```csharp
// Slice 4 shape — return cascading message instead of doing work inline
[AggregateHandler]
public static (SubmitUserTurnResponse, OutgoingMessages) Handle(
    SubmitUserTurn command,
    IEventStream<OnboardingView> stream)
{
    // ... terminal-branch detection ...
    stream.AppendMany(turn.PreTerminalEvents);
    stream.AppendOne(new OnboardingCompleted(planId: null));  // planId yet unknown

    return (
        new SubmitUserTurnResponse(stream.Id, planId: null, true),
        new OutgoingMessages
        {
            new GeneratePlanCommand(command.UserId, stream.Id)
        });
}

[AggregateHandler]
public static async Task<OutgoingMessages> Handle(
    GeneratePlanCommand command,
    IDocumentSession session,
    UserProfileDbContext ef,
    IClaudeClient anthropic,
    PlanGenerationService planGen,
    CancellationToken ct)
{
    // ... 6-call LLM chain ...
    var planId = CombGuidIdGeneration.NewGuid();
    var planEvents = await planGen.GeneratePlanAsync(...);
    session.Events.StartStream<Plan>(planId, planEvents);

    var profile = await ef.UserProfiles.SingleAsync(p => p.UserId == command.UserId, ct);
    profile.CurrentPlanId = planId;

    return new OutgoingMessages { new PlanReadyForUser(command.UserId, planId) };
}
```

So the **call-site change is small** (cascading message instead of inline call) but the **shape change is real**: a new handler, new failure semantics, frontend changes, new integration tests. Plan ~½ day for Slice 4 itself (handler split, cascading wiring, error policies, integration tests for the saga-like flow), plus frontend work for the polling / SignalR notification of plan-ready. The transaction-scope research here is what makes Slice 4 **safe**: each handler has its own clean transaction; nothing is hidden by a misunderstanding of `InvokeAsync`'s semantics.

---

## Sources

### Primary — JasperFx/wolverine source

- [`MessageBus.cs` on `main`](https://github.com/JasperFx/wolverine/blob/main/src/Wolverine/Runtime/MessageBus.cs) — full source; `InvokeAsync<T>` line is `return Runtime.FindInvoker(message.GetType()).InvokeAsync<T>(message, this, cancellation, timeout);`
- [Message Handling System (DeepWiki, indexed commit `8af80f29`)](https://deepwiki.com/JasperFx/wolverine/2.1-message-handling-system) — verifies the call path `MessageBus.InvokeAsync → IMessageInvoker → Executor.ExecuteAsync(MessageContext, ct)`. Cites `src/Wolverine/Runtime/Handlers/Executor.cs:164-209`, `src/Wolverine/Runtime/HandlerPipeline.cs:56-108`, `src/Wolverine/Runtime/MessageBus.cs:182-211`.
- [JasperFx/wolverine Issue #95 — codegen with IDocumentSession](https://github.com/JasperFx/wolverine/issues/95) — example generated handler code showing per-handler `_serviceScopeFactory.CreateScope()` and per-handler session opening.
- [JasperFx/wolverine Issue #1693 — middleware codegen](https://github.com/JasperFx/wolverine/issues/1693) — additional generated-code samples confirming per-handler scope.
- [JasperFx/wolverine Issue #1610 — HttpContext scope](https://github.com/JasperFx/wolverine/issues/1610) — confirms each Wolverine handler creates its own scope, not the parent's.
- [JasperFx/wolverine Issue #1151 — InvokeAsync options](https://github.com/JasperFx/wolverine/issues/1151) — confirms `InvokeAsync` semantics circa Wolverine 4.x/5.x.
- [JasperFx/wolverine Issue #310 — Cascaded saga not persisted](https://github.com/JasperFx/wolverine/issues/310) — shows generated saga handler using `OutboxedSessionFactory.OpenSession(context)`.
- [`IncidentService.Tests/IntegrationContext.cs`](https://github.com/JasperFx/wolverine/blob/main/src/Samples/IncidentService/IncidentService.Tests/IntegrationContext.cs) — canonical xUnit + Alba + Marten integration test scaffold.
- [`Wolverine.Http.Tests/IntegrationContext.cs`](https://github.com/JasperFx/wolverine/blob/main/src/Http/Wolverine.Http.Tests/IntegrationContext.cs) — `JasperFxEnvironment.AutoStartHost = true` and `RunWolverineInSoloMode` patterns.

### Primary — Wolverine official docs

- [Sending Messages with IMessageBus](https://wolverinefx.net/guide/messaging/message-bus) — `InvokeAsync` semantics, retry rules, Wolverine 3.0 breaking change about response not being cascaded.
- [Aggregate Handlers and Event Sourcing](https://wolverinefx.net/guide/durability/marten/event-sourcing.html) — `[AggregateHandler]` codegen, `OutboxedSessionFactory.OpenSession(context)`, multi-stream `IEventStream<T>` atomicity.
- [Transactional Middleware](https://wolverinefx.net/guide/durability/marten/transactional-middleware) — *"Wolverine is assuming that there will be a single, atomic transaction for the entire message handler"*; `AutoApplyTransactions()` rules; `[NonTransactional]`; do-not-call-`SaveChangesAsync`-yourself.
- [Marten as Transactional Outbox](https://wolverinefx.net/guide/durability/marten/outbox) — outbox commits in same TX as Marten events; cascading messages flush after commit.
- [Marten Operation Side Effects](https://wolverinefx.net/guide/durability/marten/operations.html) — `IMartenOp`, `MartenOps.StartStream`, `MartenOps.PublishMessage`, `MartenOps.Store`, etc.
- [Sagas | Wolverine](https://wolverinefx.net/guide/durability/sagas.html) — explicit warning: *"Do not call IMessageBus.InvokeAsync() within a Saga related handler to execute a command on that same Saga. You will be acting on old or missing data."*
- [Cascading Messages](https://wolverinefx.net/guide/handlers/cascading.html) — *"will not be sent out until after the original message succeeds"*.
- [Event Forwarding](https://wolverinefx.net/guide/durability/marten/event-forwarding.html) — event forwarding semantics; *"resulting event messages go out as cascading messages only after the original transaction succeeds"*.
- [Event Subscriptions](https://wolverinefx.net/guide/durability/marten/subscriptions.html) — for downstream async work over Marten events in strict order.
- [Side Effects from Handlers](https://wolverinefx.io/guide/handlers/side-effects) — *"side effects are processed inline with the originating message and within the same logical transaction."*
- [Persistence Helpers — `[Entity]` attribute](https://wolverinefx.net/guide/handlers/persistence.html).
- [Multi-Tenancy with Marten](https://wolverinefx.net/guide/durability/marten/multi-tenancy) — conjoined tenancy + Wolverine session scoping.
- [Multi-Tenancy and ASP.Net Core](https://wolverinefx.net/guide/http/multi-tenancy) — `OutboxedSessionFactory.OpenSession(messageContext)` codegen.
- [Marten Integration](https://wolverinefx.net/guide/durability/marten/) — `IntegrateWithWolverine()` setup.
- [Best Practices](https://wolverinefx.net/introduction/best-practices) — do not resolve `IMessageBus` from a child scope; use method injection; cascading messages over abstractions.
- [Error Handling](https://wolverinefx.net/guide/handlers/error-handling.html) — retry policies on `InvokeAsync`; `InvokeResult.Stop`/`TryAgain`.
- [Test Automation Support](https://wolverinefx.net/guide/testing.html) — tracked sessions, `RunWolverineInSoloMode`, `Host.ExecuteAndWaitAsync`.
- [Event Sourcing and CQRS with Marten tutorial](https://wolverinefx.net/tutorials/cqrs-with-marten.html) — full Critter Stack tutorial, generated handler code for `CategoriseIncidentHandler`, `ResetAllMartenDataAsync` test pattern.
- [Wolverine as Mediator](https://wolverinefx.net/tutorials/mediator) — `MediatorOnly` mode caveats.
- [Dealing with Concurrency](https://wolverinefx.net/tutorials/concurrency.html) — `ConcurrencyException` retry policies for `[AggregateHandler]`.
- [Sending Messages from HTTP Endpoints](https://wolverinefx.net/guide/http/messaging) — `OutgoingMessages` tuple-return shape.
- [Integration with Marten (HTTP)](https://wolverinefx.net/guide/http/marten) — `[AggregateHandler]` + `WolverinePost` + `MartenOps.StartStream`.
- [Runtime Architecture](https://wolverinefx.net/guide/runtime) — inline-invocation sequence diagram and listed responsibilities.
- [Working with Code Generation](https://wolverinefx.net/guide/codegen) — Static/Auto/Dynamic codegen modes.

### Primary — Marten official docs

- [Aggregate Handlers and Event Sourcing — multi-stream atomicity](https://wolverinefx.net/guide/durability/marten/event-sourcing.html) (cross-listed; transfer-money sample).
- [CQRS Command Handler Workflow / `FetchForWriting`](https://martendb.io/scenarios/command_handler_workflow.html).
- [Appending Events / `EventAppendMode.Quick`](https://martendb.io/events/appending.html) — Quick mode tradeoffs, 40-50% perf gain, version/sequence metadata loss.
- [Event Metadata](https://martendb.io/events/metadata) — `QuickWithServerTimestamps` mode in Marten 8.4+.
- [Optimizing for Performance and Scalability](https://martendb.io/events/optimizing).
- [Aggregates, Events, Repositories](https://martendb.io/scenarios/aggregates-events-repositories) — single-session multi-stream guarantees.
- [Marten Integration Testing](https://martendb.io/testing/integration.html) — Alba + xUnit pattern.
- [Marten + Wolverine tutorial](https://martendb.io/tutorials/wolverine-integration) — *"Events and messages returned from the handler are saved and dispatched atomically. Wolverine uses Marten's outbox to store messages until the transaction commits."*

### Secondary (verified) — JasperFx blog posts by Jeremy D. Miller (project maintainer)

- [Build Resilient Systems with Wolverine's Transactional Outbox](https://jeremydmiller.com/2024/12/08/build-resilient-systems-with-wolverines-transactional-outbox/) (2024-12-08).
- [Building a Critter Stack Application: Wolverine's Aggregate Handler Workflow FTW!](https://jeremydmiller.com/2023/12/06/building-a-critter-stack-application-wolverines-aggregate-handler-workflow-ftw/) — full codegen walkthrough.
- [Compound Handlers in Wolverine](https://jeremydmiller.com/2023/03/07/compound-handlers-in-wolverine/) — pure-function handler composition.
- [Transactional Outbox/Inbox with Wolverine and why you care](https://jeremydmiller.com/2022/12/15/transactional-outbox-inbox-with-wolverine-and-why-you-care/).
- [Integration Testing an HTTP Service that Publishes a Wolverine Message](https://jeremydmiller.com/2023/07/09/integration-testing-an-http-service-that-publishes-a-wolverine-message/) — Alba + tracked sessions pattern.
- [Faster & More Reliable Integration Testing Against Marten Projections or Subscriptions](https://jeremydmiller.com/2025/08/19/faster-more-reliable-integration-testing-against-marten-projections-or-subscriptions/) — `MartenDaemonModeIsSolo`, `RunWolverineInSoloMode`, `ResetAllMartenDataAsync`.
- [Marten just got better for CQRS architectures](https://jeremydmiller.com/2022/05/31/marten-just-got-better-for-cqrs-architectures/) — `FetchForWriting` deep dive.
- [Metadata Tracking Improvements in Marten](https://jeremydmiller.com/2025/07/27/metadata-tracking-improvements-in-marten/) — Marten 8.4 metadata tracking.
- [Customizing the Wolverine Code Generation Model](https://jeremydmiller.com/2026/04/20/customizing-the-wolverine-code-generation-model/) — recent (2026-04) codegen customization patterns; cited for the `OutboxedSessionFactory.OpenSession(messageContext)` codegen example.

### Secondary — Testing infrastructure

- [Testcontainers for .NET — xUnit.net integration](https://dotnet.testcontainers.org/test_frameworks/xunit_net/) — xUnit v3 `[assembly: AssemblyFixture]` pattern.
- [Testcontainers PostgreSQL module](https://dotnet.testcontainers.org/modules/postgres/).

### Tertiary (corroborating, non-authoritative — used only for cross-checking)

- Milan Jovanović, [Implementing the Saga Pattern With Wolverine](https://www.milanjovanovic.tech/blog/implementing-the-saga-pattern-with-wolverine) — independent restatement: *"Warning: Do not call IMessageBus.InvokeAsync() within a saga handler to execute a command on that same saga. You'll be acting on stale or missing data. Use cascading messages (return values) for subsequent work."*

— *End of R-066.*