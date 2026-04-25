# Research Prompt: Batch 22a — R-066

# Wolverine `[AggregateHandler]` Transaction Scope Across Synchronous `IMessageBus.InvokeAsync<TResponse>` (.NET 10 + Wolverine 5.x + Marten 8.28, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a Wolverine 5.x `[AggregateHandler]` over a Marten event stream that, on the terminal branch (e.g., `OnboardingCompleted`), calls `await IMessageBus.InvokeAsync<TResponse>(downstreamCommand, ct)` to invoke a *different* Wolverine handler synchronously — does the downstream handler execute inside the **same Postgres transaction** as the outer `[AggregateHandler]`'s event append + projection update, or does Wolverine commit the outer transaction first and run the downstream handler in a fresh transaction? What is the canonical Wolverine 2026 pattern for "I want this multi-step pipeline to be atomic across two handlers and one HTTP request"?

## Context

I'm finalizing the Slice 1 (Onboarding → Plan) spec for RunCoach, an AI running coach. The wiring is event-sourced with Marten 8.28 + Wolverine 5.28, conjoined-tenancy per user, EF Core 10 alongside via `Marten.EntityFrameworkCore`, with `Policies.AutoApplyTransactions()` enabled on the Wolverine bus per the Slice 0 foundation.

The flow that triggers this question:

1. SPA POSTs `/api/v1/onboarding/turns` → controller dispatches a `SubmitUserTurn` command.
2. Wolverine `[AggregateHandler]` over the onboarding stream loads the `OnboardingView` projection via `FetchForWriting`, calls Anthropic for the next-turn structured output, and on the terminal branch produces the event sequence including `OnboardingCompleted`.
3. **Same handler, same code block** invokes `await IMessageBus.InvokeAsync<PlanGenerationResponse>(new GeneratePlanCommand(userId, onboardingStreamId), ct)`. The downstream handler runs the six-call structured-output chain (1 macro + 4 meso + 1 micro), opens a fresh Marten Plan stream via `session.Events.StartStream<Plan>(planId, new PlanGenerated(...))`, and updates `UserProfile.CurrentPlanId` on the EF row.
4. The HTTP response carries the resulting `planId`. The frontend redirects to `/`.

This is the "sync-via-handler" UX choice — fine for personal-validation MVP-0 scale; spec captures the seam to flip to async via `PublishAsync` + outbox at Slice 4+.

The **load-bearing assumption** in the spec's failure-mode analysis is that the outer `[AggregateHandler]`'s event append AND the synchronous downstream `InvokeAsync` execution share a **single Postgres transaction**, so a failure in any of the six LLM calls rolls back `OnboardingCompleted` along with the partial Plan stream, leaving the user with a clean retry surface (same idempotency key resumes from where it left off).

If that assumption is wrong — if Wolverine commits the outer transaction before the inner `InvokeAsync` runs — then on plan-gen failure we have:
- `OnboardingCompleted` already in the onboarding stream
- No Plan stream
- `UserProfile.CurrentPlanId` is null
- The retry endpoint (which checks `OnboardingCompleted`) refuses to re-run onboarding because it's "already complete"
- The user is stuck without a plan and the only recovery is the Settings → Regenerate flow (which exists for Slice 1 but is the "inelegant" path).

Fallback design captured in the spec § Open Questions: append all events EXCEPT `OnboardingCompleted` first, run plan generation, append `OnboardingCompleted` last as the transaction-closing operation. This needs Wolverine docs to confirm it's expressible in the `[AggregateHandler]` return shape (`(Events, OutgoingMessages)`) and what the side-effect ordering guarantees are.

### What I've ruled out / what I know

- `Policies.AutoApplyTransactions()` is enabled on the bus per Slice 0 — this should mean handlers run inside one transaction by default per the Marten + EF + outgoing-messages composition.
- `[AggregateHandler]` returns `(events, outgoingMessages)` — outgoing messages flow through the outbox. But `IMessageBus.InvokeAsync<T>` is a synchronous request-response, NOT an outbox publish.
- The Wolverine docs (last I checked, 2025) describe the outbox as "send messages reliably alongside data changes" — this is for fire-and-forget. Sync request-response semantics for `InvokeAsync` aren't clearly documented for the inside-handler case.
- JasperFx samples (`WebApiWithMarten`, `OrderSagaSample`) use `MartenOps.PublishMessage` for cross-handler effects — that's fire-and-forget through the outbox, NOT in-transaction sync invocation.

### What the existing research covers — and doesn't

- `batch-15d-marten-per-user-aggregate-patterns.md` (R-047) covers the registration shape and tenancy. It does NOT cover handler-to-handler transaction propagation.
- `batch-16a-onboarding-conversation-state.md` (R-048) prescribes the Wolverine `[AggregateHandler]` per-turn pattern. It does NOT prescribe how the downstream `GeneratePlanCommand` invocation interacts with the outer transaction — it shows them as separate handlers wired via a Wolverine event subscription, but my spec choice (sync-via-handler) bypasses that subscription.
- `batch-18a-dotnet10-marten-wolverine-aspire-otel-startup-composition.md` (R-054) corrected several startup-time composition errors but did NOT cover runtime transaction scope through `InvokeAsync`.
- Wolverine's official docs (`wolverine-fx.com/guide/messaging.html`) describe the outbox model but the in-handler `InvokeAsync` semantics need direct verification.

## Research Question

**Primary:** When a Wolverine 5.x `[AggregateHandler]` invokes `await IMessageBus.InvokeAsync<TResponse>(downstreamCommand, ct)` from inside its handler body (NOT via the returned `OutgoingMessages`), does the downstream handler execute inside the same Postgres transaction as the outer handler? Trace this against Wolverine's actual source (`Wolverine.Runtime.Messaging` + `Wolverine.Persistence.Postgresql`) on the 5.x release line, not just docs.

**Sub-questions** (each must be actionable):

1. **Transaction propagation semantics.** Does `IMessageBus.InvokeAsync<T>` reuse the calling handler's `IDocumentSession` / `DbContext` (and therefore the Postgres connection + transaction), or does it open a fresh session/connection? If the answer depends on the registration shape (`AutoApplyTransactions()` vs explicit `Transactional` filter vs default), enumerate the matrix.

2. **`[AggregateHandler]` body vs `OutgoingMessages` return.** Is there a documented Wolverine-canonical separation: "do these in the handler body inside the transaction" vs "return these as `OutgoingMessages` for outbox dispatch"? What's the canonical pattern for "I want this work to be atomic with the event append" — emit a `MartenOps.PublishMessage` (outbox, async) vs call `InvokeAsync` (in-process, possibly fresh transaction)?

3. **Cross-stream atomicity.** The downstream handler opens a NEW Marten event stream (`session.Events.StartStream<Plan>(planId, ...)`) on a *different* tenant-scoped store identity than the outer onboarding stream. Marten supports multiple streams per session; do all writes within `SaveChangesAsync` commit atomically when they target the same `IDocumentStore`? Specifically: if the outer handler's events + the inner handler's `StartStream` calls all flow through the SAME `IDocumentSession`, do they hit one Postgres transaction?

4. **Failure-mode behavior.** What happens to the outer handler's `(events, outgoingMessages)` return value if the inner `InvokeAsync` throws? Specifically: are the outer's events still committed (because `[AggregateHandler]` saved them before the body's continuation ran), or are they rolled back as a unit? If rolled back, what's the rollback boundary — the entire HTTP request, the outer handler's body, or something narrower?

5. **The fallback design.** If the answer to #1 is "no, fresh transaction," is the spec's fallback design (append all onboarding events EXCEPT `OnboardingCompleted` first, run plan-gen, append `OnboardingCompleted` last) expressible in the `[AggregateHandler]` return contract? Does Wolverine's `(events, outgoingMessages)` tuple support multi-step event emission with a synchronous downstream call sandwiched between the steps? If not, what's the canonical workaround — a saga, a custom `IMessageHandler<>` outside `[AggregateHandler]`, or a different shape?

6. **`MartenOps` vs `IMessageBus` invocation.** What's the canonical 2026 difference between `MartenOps.PublishMessage(cmd)` (returned in `OutgoingMessages`) and `await IMessageBus.InvokeAsync<TResponse>(cmd, ct)` for cross-handler dispatch? Cover transaction scope, response semantics (one-way vs request-response), failure-mode behavior, and which one production Wolverine apps use for the "sync request → atomic side effects" shape.

7. **Verification path.** Provide a concrete Wolverine integration test pattern (xUnit v3 + AssemblyFixture + Testcontainers Postgres — matching this repo's existing fixture from Slice 0) that proves whether transaction propagation does or does not extend across `InvokeAsync` for `[AggregateHandler]` callers.

## Why It Matters

- **Spec correctness.** The Slice 1 spec assumes one transaction. If wrong, the failure-mode behavior section is wrong, the integration tests will pass on the happy path but a real LLM 5xx on call 4-of-6 will leak `OnboardingCompleted` into the stream with no plan, and we ship a recovery hole that only Settings → Regenerate covers.
- **Foundation cost.** This pattern (one HTTP request → one [AggregateHandler] → one synchronous downstream Wolverine command) appears throughout the Slice 1-4 wiring. Slice 4's open-conversation handler will likely have the same shape (incoming user message → conversation aggregate → downstream `EmbedConversationContextCommand` for vector search). Locking the transaction-scope idiom now prevents repeated wrong choices.
- **Slice 4 async-flip seam.** The plan to flip `InvokeAsync` → `PublishAsync` post-Slice-4 was sized as "one-line change at the call site." If the transaction semantics differ between the two, the flip is more than one line and the cost-of-flip estimate in the spec needs revisiting.
- **Operational debugging.** When the first real-world plan-gen failure happens (it will), being able to say "we know the recovery semantics" beats "we'll figure it out under fire."

## Deliverables

- **Definitive answer with primary-source citations** to whether `IMessageBus.InvokeAsync<TResponse>` from inside a `[AggregateHandler]` body shares the outer transaction. Cite Wolverine source (`release/5.x` or whatever's current), not blog posts. Microsoft Learn / JasperFx repo issues / Wolverine docs all valid; tertiary sources (Reddit, third-party tutorials) need verification.
- **Canonical pattern for "atomic multi-step pipeline within one HTTP request."** If `InvokeAsync` shares the transaction, document it. If not, name the canonical alternative (saga, multi-step handler, return-events-for-second-handler, etc.) with sample code.
- **Concrete redesign recommendation for the Slice 1 spec** if the assumption is wrong. Include: which sub-tasks need re-scoping (#94 T01.6, #97 T02.3, #98 T02.4), what the corrected event-emission ordering looks like in code, and how integration-test coverage proves the correction.
- **Verification snippet.** A short xUnit v3 integration test that, against the existing Testcontainers Postgres fixture, proves the answer empirically — not just by reading docs.
- **Gotchas.** Marten `EventAppendMode.Quick` interactions; outbox + `Policies.AutoApplyTransactions()` interactions; conjoined tenancy interactions; any ordering / sequencing pitfalls specific to this composition.
- **Slice 4 implication callout.** Brief: how does the answer affect the eventual `InvokeAsync` → `PublishAsync` async-flip at Slice 4+? Is the call-site change still one line, or does the transaction-scope difference require more?

## Out of scope

- Wolverine codegen (`dotnet run -- codegen write`) — separate concern, locked in Slice 0.
- Outbox semantics for fire-and-forget messages — well-documented; this prompt is specifically about request-response.
- Marten 9 upgrade considerations — Marten 8.28 is the pin per Slice 0.
- Cross-DocumentStore atomicity (`InvokeAsync` to a handler that touches a *different* `IDocumentStore`) — RunCoach uses one store per process.
