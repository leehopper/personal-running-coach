# Research Prompt: Batch 23a — R-069

# Marten + EF Core 10 Dual-Write Atomicity Inside a Wolverine `[AggregateHandler]` Body (Marten 8.28 + `Marten.EntityFrameworkCore` + Wolverine 5.x, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: Inside a Wolverine 5.x `[AggregateHandler]` body that writes BOTH Marten events (via `session.Events.StartStream<Plan>(planId, planEvents)`) AND a direct EF Core `DbContext` row update (`db.UserProfiles.Single(p => p.UserId == userId).CurrentPlanId = planId`) on the same Postgres database — do these commit atomically as one Postgres transaction, or are they two separate connection-level transactions sequenced by Wolverine? If the latter, what is the canonical 2026 path to make them atomic — `Marten.EntityFrameworkCore` shared-connection mode, an indirect Marten-projection pattern, or an accepted consistency window with a startup reconciliation job?

## Context

I'm finalizing the Slice 1 (Onboarding → Plan) implementation for RunCoach, an AI running coach. The spec adopted the canonical "single-handler / single-session / single-transaction" pattern from R-066 / DEC-057: `OnboardingTurnHandler` is a Wolverine `[AggregateHandler]` over the onboarding event stream, and on the terminal branch it does ALL the following inside one handler body:

1. Append onboarding events (pre-terminal) to the onboarding stream via `IEventStream<OnboardingView>`.
2. Call `await planGen.GeneratePlanAsync(...)` (a plain DI service) to run the six-call structured-output chain. Returns `IReadOnlyList<object> planEvents`.
3. `session.Events.StartStream<Plan>(planId, planEvents)` — opens a NEW Marten event stream for the user's plan.
4. **`db.UserProfiles.Single(p => p.UserId == userId).CurrentPlanId = planId`** — direct EF Core write on the injected `RunCoachDbContext`.
5. Append `OnboardingCompleted(planId)` to the onboarding stream as the LAST event.
6. Wolverine's transactional middleware calls `SaveChangesAsync` at handler exit.

The R-066 artifact (`docs/research/artifacts/batch-22a-wolverine-aggregate-handler-transaction-scope.md`) §3 included a parenthetical caveat that my spec depends on but R-066 explicitly flagged as out-of-scope:

> *"(Note the EF Core + Marten dual-write caveat: with `Marten.EntityFrameworkCore` and `Wolverine.EntityFrameworkCore` both wired, Wolverine will weave both unit-of-work commits, but they are still two separate connection-level transactions on the same Postgres database unless you explicitly opt into shared connection mode. The `Wolverine.Marten` outbox tables and Marten's events both live in one transaction; the EF Core `UserProfile` write is a second transaction batched immediately after. For RunCoach's needs — `CurrentPlanId` ↔ Plan stream consistency — the practical guarantee is that `UserProfile.CurrentPlanId` is written *after* the Plan stream is durable, and any failure between them is recoverable by a startup reconciliation job. **This is out of scope but worth flagging.**)"*

This is load-bearing for the spec's failure-mode guarantees. The spec's `InvokeAsyncTransactionScopeTests` regression test asserts: on mid-chain LLM failure, NO Plan stream exists AND `UserProfile.CurrentPlanId` is null. If the dual-write claim is correct, there's an asymmetric failure window: step 3 commits, step 4 fails (transient Postgres issue, concurrency exception, `ct` cancellation between commits) → Plan stream is durable but `UserProfile.CurrentPlanId` is null. From the test's perspective the assertion still passes (CurrentPlanId is null), but the orphan Plan stream is leaked into the database and the user is wedged: the next request sees no `CurrentPlanId` so the route guard sends them to `/onboarding` which sees the completed view and sends them to `/`, infinite redirect loop, or worse if any UI code dereferences the orphan plan.

### What's already settled (per DEC-047 and R-048)

`UserProfileFromOnboardingProjection : EfCoreSingleStreamProjection<UserProfile, RunCoachDbContext>` from the `Marten.EntityFrameworkCore` package writes the six onboarding-answer slot columns into `UserProfile`. R-048 / DEC-047 explicitly state: *"The projection runs in the same Postgres transaction as the event append — no async-eventual-consistency surface."* So **EF writes that flow through a Marten event-driven projection are atomic with the events.** That's the easy case.

### What's NOT settled

The DIRECT EF write inside the `[AggregateHandler]` body — step 4 above — is NOT through a Marten projection. It's a normal `_db.UserProfiles.Single(...)` followed by a property assignment, relying on Wolverine's transactional middleware to call `SaveChangesAsync` at handler exit. R-066 §3 says this commits in a **different** Postgres transaction than Marten's events. Even if "batched immediately after," there is a non-zero window between the two commits.

Three candidate resolutions, none of which R-066 picks for me:

**Option 1 — Indirect via Marten projection.** Add a `PlanLinkedToUser(Guid UserId, Guid PlanId)` event to the **onboarding** stream (NOT the Plan stream — the onboarding stream is the per-user 1:1 stream that drives `UserProfileFromOnboardingProjection`). Extend `UserProfileFromOnboardingProjection.Apply(PlanLinkedToUser e, UserProfile p)` to set `p.CurrentPlanId = e.PlanId`. Append `PlanLinkedToUser` BEFORE `OnboardingCompleted` in the handler body. Atomic by construction because everything flows through Marten's events and Marten owns the transaction. Cost: one extra event type + one more projection apply method. Conceptual mismatch: `CurrentPlanId` is a forward pointer from `User → Plan`, not a backward semantic event ("the user's profile changed because…"). But it's defensible because the *act of linking* is a meaningful state transition.

**Option 2 — Shared-connection mode.** If `Marten.EntityFrameworkCore` 2026 supports a shared-connection mode (Marten and `RunCoachDbContext` opening the SAME `NpgsqlConnection` and enrolling in ONE Postgres transaction), use it. Direct EF writes inside the handler body would then commit inside Marten's transaction. R-066 §3 alludes to this ("unless you explicitly opt into shared connection mode") but does NOT cite a specific API or document that this exists in `Marten.EntityFrameworkCore` v8.x. Need to verify against actual JasperFx source / docs.

**Option 3 — Accept the window + reconciliation.** Document the consistency window in the spec, ensure the EF write is the LAST operation in the handler body (so the Plan stream is durable before the EF write attempts), and add a startup `IHostedService` reconciliation job that scans for orphan Plan streams (Plan stream exists but no `UserProfile.CurrentPlanId` pointer to it) and either (a) backfills `CurrentPlanId` if the Plan is the latest one for the user, or (b) marks the orphan Plan stream as superseded. Operational cost: one new service. Code cost: small. Failure-mode complexity: real (need to handle the case where multiple orphan plans exist after several failures).

### What the existing research covers — and doesn't

- **R-047 (`batch-15d-marten-per-user-aggregate-patterns.md`)** locked Marten 8.28 + Wolverine 5.28 + Npgsql 9 + `IntegrateWithWolverine()`. It establishes that Wolverine's `Policies.AutoApplyTransactions()` brackets ONE transaction around Marten + EF + outgoing-messages **when EF is registered via `AddDbContextWithWolverineIntegration<T>`**. Did NOT verify whether that "one transaction" is one logical Wolverine unit-of-work that commits two Postgres transactions sequentially, OR truly one Postgres transaction with both stores enrolled. R-066 §3's claim says the former.
- **R-048 / DEC-047** confirms `EfCoreSingleStreamProjection` (Marten projection writing into EF) shares Marten's transaction. Does NOT cover direct EF writes from outside a projection.
- **R-066 (`batch-22a-wolverine-aggregate-handler-transaction-scope.md`)** §3 made the parenthetical flag I'm now researching. The recommended canonical pattern in R-066 §"Canonical Pattern Recommendation" assumes the dual-write is atomic without verifying it — that's the gap.
- **DEC-048** locks `IntegrateWithWolverine()` as the sole envelope-storage wiring. Does not cover EF-side connection sharing.
- **DEC-049** locks runtime envelope provisioning (no manual `MapWolverineEnvelopeStorage`). Does not address dual-write.

## Research Question

**Primary:** Inside a Wolverine 5.x `[AggregateHandler]` body that writes both Marten events (via the injected `IDocumentSession`) AND a direct EF Core row update (via the injected `RunCoachDbContext` — registered through `AddDbContextWithWolverineIntegration<T>` per Slice 0's `Program.cs`), are these committed in **one** Postgres transaction or **two** sequential Postgres transactions on the same database? Trace against actual Wolverine 5.x + `Marten.EntityFrameworkCore` 8.x source on the release branches current as of 2026-04-25.

**Sub-questions** (each must be actionable):

1. **The literal commit ordering.** Walk through Wolverine's transactional-middleware codegen for a handler that injects BOTH `IDocumentSession` AND `RunCoachDbContext` (where `RunCoachDbContext` is registered via `AddDbContextWithWolverineIntegration`). What does the generated handler look like? Does Wolverine call `IDocumentSession.SaveChangesAsync` first, then `RunCoachDbContext.SaveChangesAsync`? Or does it use a single `IUnitOfWork`-style commit primitive that enrolls both?

2. **Connection sharing reality check.** Does `Marten.EntityFrameworkCore` 8.x (or 9.x if it lands) support a shared-connection mode where Marten and EF Core open the SAME `NpgsqlConnection` and enrol in ONE `NpgsqlTransaction`? If yes: how is it configured (likely on the `AddMarten(...)` builder)? If no: what's the workaround — explicit `TransactionScope` with `Enlist=true`, or a custom `IDbContextFactory` that reuses Marten's connection?

3. **`Wolverine.EntityFrameworkCore` integration semantics.** The `AddDbContextWithWolverineIntegration<T>` extension installs `WolverineModelCustomizer` (per DEC-049). Does that customizer also enrol the EF DbContext in Marten's transaction, or just the Wolverine outbox storage? Cite the source.

4. **`AutoApplyTransactions()` policy contract.** When `Policies.AutoApplyTransactions()` is on AND the handler injects both stores, does the policy guarantee one transaction or two? Quote the docs and the source.

5. **Failure-mode behavior under "two transactions."** If commit-1 (Marten events) succeeds but commit-2 (EF UserProfile) fails (network blip, concurrency, ct firing between the awaits), what's left in the database? Is there any auto-retry or compensation? Or does the calling HTTP request just see a 500 with the Plan stream + onboarding events committed?

6. **Three architectural options (Option 1 / 2 / 3 from §Context).** For each option, evaluate against: (a) atomicity guarantee, (b) code complexity, (c) operational complexity, (d) compatibility with the existing DEC-057 single-handler pattern, (e) impact on the spec's `InvokeAsyncTransactionScopeTests` regression test, (f) impact on Slice 4's eventual async-flip migration. Recommend one.

7. **Is `CurrentPlanId` really the only direct-EF-write?** Audit Slice 1's spec for any other direct EF writes that bypass projections. If Option 1 (indirect via Marten projection) is recommended, every direct EF write needs a corresponding projection event. List the audit findings.

8. **`IdempotencyRecord` writes.** The spec's `IIdempotencyStore` writes records via EF. Is that another direct-EF-write that participates in the same transaction question? Or is `IIdempotencyStore` cleanly outside the dual-write boundary because it's call-site-scoped?

9. **EF DbContext re-injection in plain DI services called from handlers.** `IPlanGenerationService` (the plain DI service from Unit 2) does NOT touch `IDocumentSession` or `RunCoachDbContext`. Sanity check: Wolverine doesn't somehow leak a fresh DbContext / session into plain DI services called from the handler — they should not be participating in the transaction question because they don't write anywhere.

10. **Verification path.** Provide a concrete xUnit v3 + AssemblyFixture + Testcontainers Postgres integration test (matching this repo's existing fixture) that empirically demonstrates whether the dual-write commits as one or two Postgres transactions. Hint: inject a Postgres extension that's incompatible with cross-transaction visibility, or use `pg_stat_xact_user_tables` snapshots between the awaits, or use a connection-side hook to count `BEGIN`/`COMMIT` calls.

## Why It Matters

- **Spec correctness.** The Slice 1 spec's failure-mode guarantees (§ Failure-mode behavior in `13-spec-slice-1-onboarding.md`) explicitly assume single-transaction atomicity for "Plan stream + UserProfile.CurrentPlanId + OnboardingCompleted." If R-066 §3's flag is correct, this assumption is wrong and the Slice 1 PR ships with a known leak.
- **Test correctness.** `InvokeAsyncTransactionScopeTests` (Unit 1) and `RegenerateTransactionScopeTests` (Unit 5) are designed to prove the all-or-nothing guarantee. They currently assert: NO Plan stream + NO `OnboardingCompleted` + null `CurrentPlanId` after mid-chain failure. If the dual-write isn't atomic, the test passes for "no Plan stream" + "no OnboardingCompleted" but should also exercise the inverse (Plan stream commits, EF write fails) — and that test would fail under the current architecture.
- **Same shape recurs.** Slice 3's adaptation handler will write `PlanAdaptedFromLog` to Marten + update `UserProfile.LastAdaptationAt` (or similar) on EF — same dual-write question. Slice 4's open-conversation will write `ConversationTurn` events + update `UserProfile.LastChatAt` — same question. Lock the pattern now.
- **Reconciliation cost.** If Option 3 (accept window + reconciliation) is the right answer, the reconciliation `IHostedService` is a small new piece of infrastructure that should be designed once and reused across slices.
- **R-066 follow-through.** R-066 explicitly flagged this as out-of-scope. Closing the loop is the responsible follow-up before the architecture goes load-bearing.

## Deliverables

- **Definitive answer with primary-source citations:** is the dual-write one transaction or two? Cite Wolverine 5.x source (`release/5.x` or current), `Marten.EntityFrameworkCore` 8.x source, `Wolverine.EntityFrameworkCore` integration source. Microsoft Learn / JasperFx repo issues / blog posts all valid; tertiary sources need verification.
- **Recommended option (1, 2, or 3) with rationale.** Include the cost matrix from sub-question 6.
- **Concrete redesign recommendation for the Slice 1 spec** if Option 1 (indirect projection) is the answer:
  - New event type to declare (`PlanLinkedToUser` or similar) — exact shape.
  - Update to `UserProfileFromOnboardingProjection.Apply(...)` signature.
  - Update to T01.6's handler-body sequence (where to append `PlanLinkedToUser` relative to `StartStream<Plan>` and `OnboardingCompleted`).
  - Update to the regression tests' assertion shape.
- **Concrete configuration recommendation** if Option 2 (shared-connection mode) is the answer:
  - The exact `AddMarten(...)` / `AddDbContextWithWolverineIntegration<T>(...)` registration shape.
  - Whether existing Slice 0 startup tests still pass.
  - Whether this is GA-stable in `Marten.EntityFrameworkCore` 8.28 or requires Marten 9.
- **Concrete reconciliation-job recommendation** if Option 3 (accept window) is the answer:
  - The detection query (orphan Plan streams).
  - The reconciliation policy (backfill latest, mark superseded, alert).
  - The `IHostedService` shape.
  - The metric / OTel attribute to track orphan-rate.
- **Verification snippet.** A short xUnit v3 integration test that empirically proves the dual-write semantics on the existing Testcontainers fixture.
- **Audit of all direct-EF-writes in Slice 1's spec** (sub-question 7). For each, name the file/handler and confirm whether it falls inside or outside the dual-write boundary.
- **Slice 3 / Slice 4 implications callout.** Brief: how does the answer affect the handlers in those slices?

## Out of scope

- Marten 9 upgrade strategy — Marten 8.28 is the pin per Slice 0 and the cycle plan. If Option 2 (shared-connection mode) only exists in Marten 9, document that as a finding but don't recommend pinning Marten 9.
- Wolverine codegen modes (`TypeLoadMode`) — orthogonal concern, locked in Slice 0.
- Cross-`IDocumentStore` atomicity (writes to a *different* Marten store) — RunCoach uses one store per process.
- Distributed transactions / `TransactionScope` across multiple Postgres databases — RunCoach is one Postgres database.

## Date stamp

All claims about Wolverine / Marten / `Marten.EntityFrameworkCore` behaviour must be verified against source on or after 2026-04-25.
