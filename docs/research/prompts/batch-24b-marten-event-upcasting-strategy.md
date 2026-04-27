# Research Prompt: Batch 24b — R-072

# Marten 8.32 Event Schema Evolution / Upcasting Strategy for a Per-User Aggregate (Marten + Wolverine + EF Core projection, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a Marten 8.32.x event store with three event-sourced aggregates (Slice 1 `OnboardingAggregate` and `Plan`; Slice 4 `Conversation`) projected via a mix of standard Marten document projections AND `Marten.EntityFrameworkCore.EfCoreSingleStreamProjection<TDoc, TKey, TDbContext>` per DEC-061 — what is the canonical 2026 upcasting strategy that lets us add, rename, or evolve event payload properties without breaking projection of pre-existing streams, and that generalizes across both projection styles?

Deliver a recommendation with: tooling choice (Marten built-ins vs custom middleware), registration shape on `StoreOptions`, a worked example for a representative migration (e.g., adding `TypicalSessionMinutes` to an existing `AnswerCaptured` event whose old payloads don't carry it), and a regression-test pattern that proves an old-shape stream still projects after the migration.

### Sub-questions the artifact must answer

1. **Upcasting surface in Marten 8.32.** What APIs are GA on `StoreOptions` for event upcasting / event-payload migration today? Compare `Events.AddEventType<T>` with versioning, `Events.Upcast<TOld, TNew>(...)` if it exists, JSON-level `JsonNetSerializer` / `STJSerializer` upcaster registration, and any `IEventUpcaster<T>` extension surface. Cite exact namespaces.
2. **Versioned event types vs same-name evolution.** Two camps: (a) introduce `AnswerCapturedV2 : IEvent` with an upcaster from V1, keep V1 in the codebase forever; (b) keep `AnswerCaptured` and use a JSON-level upcaster to fill in defaults for missing properties. Compare on read-side complexity, projection apply-method ergonomics, JsonSchema/Anthropic compatibility, and operational cost (how many events live in the codebase after 5 migrations?).
3. **EfCoreSingleStreamProjection compat.** DEC-061 locks the EF projection registration shape (`opts.Add(IProjection, ProjectionLifecycle)`). Does the chosen upcasting strategy intercept events BEFORE the projection's Apply method runs, regardless of projection type? Confirm the same upcaster works for both standard `SingleStreamProjection<T>` and `EfCoreSingleStreamProjection<T, TKey, TCtx>`.
4. **Integration with the conjoined-tenancy event store.** All projection targets implement `Marten.Metadata.ITenanted`; events flow through tenanted streams. Does the upcaster see the tenanted envelope or the raw payload? Any gotchas around tenant-aware streams?
5. **Anthropic Pattern-B schema overlap.** `OnboardingTurnOutput` (DEC-058) is a byte-stable schema with six nullable typed `Normalized*` slots. If a future migration adds a seventh slot, the *event* `AnswerCaptured(NormalizedPayload: JsonDocument)` carries whatever Anthropic returned. Should the upcaster only normalize the *event* shape, the *projection's* read of the payload, or both?
6. **Test pattern.** Show the canonical 2026 regression-test recipe: fabricate a stream with an "old-shape" event JSON (perhaps via raw `IDocumentSession.Connection.ExecuteScalarAsync<int>`), run the projection daemon, assert the read-side shape matches the current contract. Cite the Marten test fixture utilities that make this clean.
7. **Operational concerns.** Does upcasting affect cold-start time of the projection daemon (replay cost)? Affect disk size of the events table? Are there hot-restore implications when a new event type lands?
8. **Failure modes.** What happens when an upcaster throws, when an upcaster's "old shape" can't be inferred unambiguously, when two upcasters target the same event type? What's the canonical observability hook so silent upcaster failures don't masquerade as missing-data bugs in production?
9. **Marten 9 forward-compat.** The cycle plan's deferred items note Marten 9 is undated. Is the 2026 upcasting strategy stable across Marten 8 → 9, or is there a known shift? If a shift exists, name the migration cost.

## Context

Slice 1 of the MVP-0 cycle for RunCoach (AI running coach) ships three event types on the onboarding stream — `OnboardingStarted`, `TopicAsked`, `AnswerCaptured` — plus `PlanGenerated` / `PlanLinkedToUser` / `OnboardingCompleted` on the plan stream. Slice 2 adds `WorkoutLogged` events; Slice 3 adds `PlanAdaptedFromLog`; Slice 4 adds `ConversationTurnRecorded`. Each is a candidate for evolution as the surrounding feature design clarifies.

The Slice 1 onboarding bugs were entirely contract-drift between backend and frontend (R-071 covers the codegen fix). Slice 1B is the hardening pass that closes BOTH classes of contract risk: the wire-side (R-071) and the persistence-side (this prompt). Without a working upcasting strategy, the first `AnswerCaptured` shape change in Slice 2 means either rewriting all existing streams, accepting silent projection failures on old streams, or shipping a one-off migration script — none of which are sustainable past two or three migrations.

The architectural rule from DEC-060 — "handler bodies emit events; projections own EF state" — means every event-shape change traverses BOTH a Marten document apply path AND an `EfCoreSingleStreamProjection` apply path. The upcasting strategy must intercept early enough that both paths see the normalized shape.

## Why It Matters

Until the upcasting strategy lands, every event-payload edit is a one-way door: once it ships to production, old streams in the events table reflect the old schema and either need migration data jobs or cause projection daemon errors on replay. With single-builder usage today the cost is low, but two MVP-1 testers running for a month each accumulate hundreds of events; a single Slice 3 adaptation-event evolution could break their plan-projection silently.

## Deliverables

- **Recommended approach** with rationale. Versioned event types, JSON-level upcaster, or hybrid — pick one default plus one fallback.
- **Code shape sketch** for both projection styles (standard Marten projection and EfCoreSingleStreamProjection). Sub-200 LOC total; if the chosen strategy needs more, flag it.
- **Registration shape** on `StoreOptions` consistent with DEC-061's `opts.Add(...)` discipline for EF projections and `opts.Projections.Add(...)` for document projections.
- **Regression-test recipe** — exact xUnit pattern to fabricate an old-shape event in a synthetic stream and assert projection still works, suitable to drop into the existing `MartenStoreOptionsCompositionTests` companion file.
- **Failure-mode telemetry** — OTel span shape for upcaster invocations so silent upcasting failures land in the dashboard.
- **Marten 9 forward-compat assessment** — is the 2026 strategy a one-way bet or is it stable across the major-version upgrade?
- **Gotchas** specific to conjoined tenancy (DEC-061), the dual-write atomicity rule (DEC-060), and Anthropic Pattern-B's byte-stable schema constraint (DEC-058).

## Out of scope

- Choosing between Marten and another event-store (EventStoreDB, etc.) — out of scope; Marten is locked.
- Full event-sourcing / CQRS pedagogy — assume the reader knows projections, streams, and the apply-method pattern.
- Snapshot strategy. Marten supports it but Slice 1B doesn't enable it.
- Replay-from-zero performance tuning. Out of scope until the test suite or production daemon shows a problem.

The artifact lands at `docs/research/artifacts/batch-24b-marten-event-upcasting-strategy.md` and integrates into `slice-1b-hardening.md` plus a new DEC entry locking the strategy.
