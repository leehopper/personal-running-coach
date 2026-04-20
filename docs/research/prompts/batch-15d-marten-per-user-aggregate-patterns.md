# Research Prompt: Batch 15d — R-047

# Marten 2026 Event-Sourcing Patterns for a Per-User Plan Aggregate on Postgres

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a Marten 7+ event-sourced application backing a per-user "training plan" aggregate (Slice 1 of MVP-0), what are the current 2026 best practices for stream identity, projection strategy, async daemon configuration, snapshot frequency, and schema/multitenancy choices on Postgres?

## Context

I'm building MVP-0 of an AI-powered running coach (RunCoach):

- Backend: ASP.NET Core 10 with EF Core 10 (relational user state — Identity, UserProfile, WorkoutLog, ConversationTurn) + Marten (event-sourced state — the training Plan).
- Both stores share one Postgres instance: EF Core uses the `public` schema; Marten gets a dedicated schema (`marten` is the default placeholder; this prompt should validate or change that).
- Slice 0 wires up Marten registration via `AddMarten(...)`. **No documents are written in Slice 0** — the registration is foundation-only. Slice 1 introduces the `Plan` aggregate.
- Multi-tenant by user from day one. Every API call is JWT-authenticated and scoped to the calling user's id.
- `Plan` aggregate events identified so far:
  - `PlanGenerated` (Slice 1) — initial macro/meso/micro plan from onboarding.
  - `PlanAdaptedFromLog` (Slice 3) — plan modification triggered by a workout log; includes reason + modified workouts.
  - `PlanRestructuredFromConversation` (Slice 4 or later) — modification triggered by chat (goal change, injury report).
  - `PlanRegenerated` (Slice 1+) — user-triggered regeneration for iteration/correction.
- The **projection** is "current plan document" — a single JSON doc (macro phase schedule + this-week meso template + active-day micro prescriptions) consumed by the LLM context-injection layer.
- Volume estimate: thousands of users at MVP-1 cap, ≤ ~hundreds of events per user per year (training plan changes weekly-ish, plus adaptation events on every workout log = a few per week).
- Trajectory: solo-dev now → friends/testers (MVP-1) → public beta. Single-instance API for MVP-0; could go multi-replica at MVP-1+.

The cycle plan pre-flagged this as an explicit research trigger ("Marten event-sourcing patterns for a per-user plan aggregate — stream-per-user vs stream-per-plan, projection update strategy (inline vs async), snapshot frequency"). Resolving it before Slice 1 starts means Slice 0's Marten registration has the right shape and Slice 1 doesn't refactor.

## Research Question

**Primary:** For a Marten 7+ application backing a per-user training-plan aggregate as described above, what are the current best practices for stream identity, projection strategy, async daemon configuration, snapshot policy, and schema/multitenancy?

**Sub-questions:**

1. **Stream identity — stream-per-user vs stream-per-plan.**
   - If a user can have only one active plan at a time (current model), is the stream id the user id, the plan id, or a derived value?
   - If users could later have multiple plans (e.g., archived prior plans, alternative plans for comparison), does the stream-identity choice now constrain that?
   - What's the projection rebuild cost difference between the two patterns?
   - Real-world idiom in the Marten community in 2026?

2. **Projection update strategy — inline vs async daemon vs live aggregation.**
   - For a single-instance API serving HTTP requests synchronously, what's the recommended default in Marten 7+ (`ProjectionLifecycle.Inline` vs `Async` vs `Live`)?
   - When does the recommendation flip to async daemon — load profile, multi-instance, latency requirements?
   - Does inline projection update during an HTTP request impose a write-tax that matters for our volume?
   - The current plan document is read on every coaching LLM invocation (potentially many per user per day). Does that consumption pattern change the recommendation?

3. **Async daemon for multi-instance hosting.**
   - For MVP-1 / public beta when multiple API replicas are likely, what's Marten's current async daemon story? Single-active-replica election, hot/cold replicas, the daemon as a separate process?
   - What configuration knobs (high-water marks, sharding, batch size) are load-bearing? Sensible defaults or required tuning?

4. **Snapshot frequency.**
   - For ~hundreds of events per stream per year, are snapshots even worth taking? At what stream length does Marten's reconstitution cost cross the snapshot break-even?
   - Marten's `SnapshotMeta` / `Persisted` aggregate options — current best-practice config?
   - Trade-off: snapshot overhead (storage, write amplification) vs reconstitution cost.

5. **Schema and multitenancy.**
   - Is the dedicated `marten` schema (separate from EF Core's `public`) the right boundary, or does Marten 7+ have a different recommendation (database-per-tenant, schema-per-tenant for the multitenancy story)?
   - For per-user multitenancy where the tenant id is the user id, does Marten's `MultiTenancyPerSchema` / `MultiTenantedDatabases` / single-tenant-with-tenant-column pattern apply, and which is current best practice for our scale?
   - At what scale does any of this start to matter?

6. **Aggregate definition style.**
   - Marten supports event-sourced aggregates via several styles (live aggregation, self-aggregating snapshot, projection class with `Apply` methods, the `Single Stream Projection` API). What's the 2026 recommendation for an aggregate that is consumed primarily as a projected document (not via reconstitution per HTTP request)?
   - Should the `Plan` aggregate class itself encode behavior (DDD-style), or should events be plain records with a separate `PlanProjection` materializing them?

7. **Document projection + LLM context-injection consumption.**
   - The projection is a structured JSON doc the LLM reads via `ContextAssembler`. Marten can serialize the projection any way. Is the recommendation to define a strongly-typed projection class with `[JsonIgnore]`-ish discipline, or a `JsonDocument` field, or something else?
   - Any gotchas when the projection schema evolves (new fields added, fields renamed) and existing streams need to be re-projected?

8. **Async daemon vs background processing via Wolverine.**
   - The project uses Wolverine for background processing (per `CLAUDE.md`). Does Wolverine + Marten's async daemon coexist cleanly? Is one preferred over the other for projection rebuilds and event-driven side-effects?
   - For the Slice 3 adaptation flow (log → adaptation evaluation → potentially append `PlanAdaptedFromLog`), is the recommended pattern "Wolverine handler appends event inline" or "controller appends, async daemon updates projection, Wolverine reacts to projection change"?

9. **Testing patterns.**
   - For integration tests against a real Postgres (Testcontainers), how is Marten's daemon handled? Started per test? Per fixture? Run inline so tests are deterministic?
   - Document teardown — Marten's `Advanced.Clean.CompletelyRemoveAll()` vs Respawn vs per-fixture schema reset.

10. **Marten 7 → current version migration risk.**
    - Marten has had significant API evolution (CritterStack, async daemon rewrites). What's the current stable version, and what's the upgrade churn cost from "wired in Slice 0" to "stable on whatever 2026 Marten ships next"?

11. **Schema name choice.**
    - Is `marten` a sensible schema name, or is there a project-naming convention? (e.g., does the Marten community prefer `events`, `documents`, `runcoach_events`, etc.?)
    - Does the schema name appear in any user-visible context where the trademark / project-naming rules apply? (Probably no — internal only.)

12. **Cross-store consistency.**
    - When a workout log (EF Core) is created and triggers a `PlanAdaptedFromLog` event (Marten), what's the recommended consistency pattern? Two-phase commit? Outbox pattern? Eventual consistency?
    - Marten has an outbox integration. Wolverine has its own. What's the recommended composition for "log save (EF) + plan event (Marten) + LLM coaching call" in a single logical operation?

## Why It Matters

- **Slice 0 wires Marten** — registration shape is set then. A wrong choice means refactoring the registration, schema, and projection between Slice 0 and Slice 1.
- **Plan adaptation is the differentiator** — the adaptation loop is *the* product feature. The event log IS the audit trail per DEC-031 and `memory-and-architecture.md`. Getting the event-sourcing model wrong undermines the core value proposition.
- **LLM context injection reads the projection on every coaching call** — projection latency and shape directly affect coaching UX.
- **Multi-tenant from day one** — the schema/multitenancy choice now determines whether scaling to friends/testers/beta is a config change or a rebuild.
- **EF + Marten coexistence is unusual** — fewer reference patterns than EF-only or Marten-only systems. Need explicit guidance, not assumed-compatible defaults.

## Deliverables

- **A concrete recommendation** for each of: stream identity, projection strategy, async daemon usage, snapshot frequency, schema name, multitenancy pattern, aggregate-definition style.
- **A wiring sketch** — `Program.cs` Marten registration excerpt incorporating all recommendations, ready to be the Slice 0 implementation reference.
- **A `Plan` aggregate sketch** — class structure for the aggregate + events + projection at the level Slice 1 will implement, using whichever idiom the recommendation lands on.
- **Cross-store consistency recommendation** — for "EF Core write + Marten event + side-effect," document the recommended pattern (outbox via Wolverine, two-phase commit, eventual via daemon, etc.) with rationale.
- **Migration / upgrade notes** — if Marten 8 ships during MVP-0, what's the upgrade story? Any choices in Slice 0 that would force a rewrite later?
- **Failure-mode catalog** — what breaks the recommended setup (multi-instance race, daemon stuck, schema drift, projection corruption) and the operational response.
- **Library version pins** — current Marten / Wolverine / supporting libraries.
- **Citations** — current Marten docs, JasperFx Software / Jeremy Miller blog or release notes, real-world Marten + ASP.NET Core 10 case studies if available.

## Out of Scope

- Choice of event-sourcing library — Marten is locked in per the cycle plan and prior decisions.
- Choice of database — Postgres is locked in.
- The training-science domain modeling itself (what events to emit at the domain level) — covered by `batch-2b-planning-architecture.md`. This prompt is about the Marten implementation, not the domain shape.
- Multi-database deployments (Marten on a separate Postgres from EF Core) — out of scope; one Postgres for MVP-0.
- Read replicas / CQRS query separation — the projection is itself the read model; no separate query store planned for MVP-0.
