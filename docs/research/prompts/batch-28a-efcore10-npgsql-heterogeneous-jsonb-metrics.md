# Research Prompt: Batch 28a — R-077

# EF Core 10 / Npgsql 10 mapping, indexing, and codegen round-trip for an open-ended heterogeneous JSONB metrics bag on a relational entity (.NET 10, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For an EF Core 10.0.8 + `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2 entity (`WorkoutLog`) on PostgreSQL, coexisting with a Marten 9.2.1 event store on the same database — what is the canonical 2026 pattern for persisting an **open-ended, heterogeneous JSONB "metrics bag"** (arbitrary keys such as `rpe`, `hrAvg`, `hrMax`, `calories`, `hrv`, `sleepScore`, `recoveryScore`, `weather`, `terrain`, plus a nested `splits` array-of-objects) alongside strongly-typed core columns, such that (a) the column truly accepts unknown keys without a DDL migration per new metric, (b) it round-trips cleanly through the project's OpenAPI→TypeScript/Zod codegen pipeline so the frontend never hand-maintains the metric-key list, (c) the door is left open for value-by-key querying/indexing in a later slice without re-architecting, and (d) the EF model does not destabilize the migration diff or collide with Marten's schema management?

Deliver a recommendation with: the chosen CLR-side representation, the EF Core fluent mapping, a worked `WorkoutLog` entity + `IEntityTypeConfiguration`, the OpenAPI schema shape it produces and the generated Zod it yields, an indexing decision (do-now vs design-for-later), and a regression/round-trip test pattern.

### Sub-questions the artifact must answer

1. **CLR representation of the bag.** Compare, for EF Core 10.0.8 + Npgsql 10.0.2, the realistic options for `WorkoutLog.Metrics`:
   - (a) a **raw `string`/`JsonDocument` column** typed `jsonb` via `.HasColumnType("jsonb")`;
   - (b) an **owned reference type mapped with `OwnsOne(... , b => b.ToJson())`** (the pattern already used in this repo for *closed* records — see Context) but with an open `Dictionary<string, JsonElement>` / `Dictionary<string, object?>` member;
   - (c) a **`Dictionary<string, JsonElement>` mapped directly** (Npgsql's JSONB POCO/`Dictionary` mapping, or EF's primitive-collection/`ToJson` support);
   - (d) a **typed owned record** with one nullable property per canonical metric (closed set, loses true open-endedness).
   Score each on: open-key support without DDL, query/index reachability, compile-time safety, `System.Text.Json` polymorphic-value pitfalls (`JsonElement` vs `object?`), null-vs-absent semantics, and migration-diff stability next to Marten 9 (Weasel). Cite exact EF Core 10 / Npgsql 10 APIs and namespaces.
2. **The `splits` array-of-objects inside the bag.** Is storing `splits: [{ index, distanceMeters, durationSeconds, paceSecondsPerKm }, ...]` inside the same JSONB document sound, or should splits be a separate child table / separate JSONB column? Cover query reachability and the codegen consequence (an array of objects nested in an otherwise-open map).
3. **Indexing strategy — now vs deferred.** The MVP-0 slice does not query by metric value, but a later slice ("HR > 150 runs", "RPE ≥ 8") will. Compare a single **GIN index** on the whole `jsonb` column vs **expression/B-tree indexes** on extracted keys (`((Metrics ->> 'rpe')::numeric)`). Can EF Core 10 model either via `HasIndex`/`HasMethod("gin")`, or must they be hand-authored in a migration? What is the lowest-regret choice that avoids re-architecting the column later — index now, or ship un-indexed and add an index migration when the query lands? State the recommendation explicitly.
4. **LINQ query surface.** What JSONB access does the Npgsql 10 EF provider translate to SQL today (`EF.Functions` JSONB operators, `->`/`->>`/`@>`/`?` containment, indexer access on a mapped `Dictionary`)? Where does the developer fall back to raw SQL? This bounds (3).
5. **OpenAPI / Swashbuckle → Zod round-trip (the load-bearing constraint).** The repo's contract pipeline is Swashbuckle 10 → committed `backend/openapi/swagger.json` → `@rtk-query/codegen-openapi` 2.2.0 (TS) + Orval 8.12.1 `client: 'zod'` (runtime Zod v4), gated by `git diff --exit-code` (DEC-066). How should an open-ended metrics map be expressed in the DTO so Swashbuckle emits a usable OpenAPI schema (`additionalProperties: { ... }` vs a fixed property set vs `type: object` free-form) and Orval produces a Zod schema the log form can actually consume? Does `additionalProperties` survive Orval's `override.zod.strict = true` (which maps `additionalProperties:false` → `z.strictObject`)? Reconcile "open bag" with "strict-object drift gate." Recommend the DTO shape that keeps the gate meaningful on the *core* fields while allowing the *metrics* map to be open.
6. **Canonical metric-key constants (carry-forward).** The cycle requires the canonical key set to live in one C# file (e.g., `Modules/Training/Constants/WorkoutMetricKeys.cs`) and to flow to the frontend via codegen rather than a hand-mirrored TS list. What mechanism makes the canonical keys *generatable* (e.g., a `const`/enum surfaced in the OpenAPI doc, an x-extension, or a documented free map + a generated key enum)? How do manual entry today and Garmin/HealthKit auto-fill later write the *same* keys?
7. **Value objects inside vs beside the bag.** `WorkoutLog.Distance` and `Duration` are core columns, not metrics. The repo has `Distance` (`readonly record struct`, canonical meters) and `Pace` (sec/km) value objects but **zero `ValueConverter` precedent**. For EF Core 10: should `Distance`/`Duration` map via a `ValueConverter` (e.g., `double` meters / `long` ticks ↔ value object), via EF 8+ **complex types**, or as primitives with conversion at the boundary? Give the .NET 10 idiom and whether `ValueConverter` on a `readonly record struct` is fully supported (incl. comparer/snapshot concerns). If any unit-bearing metric lands inside the JSONB bag (e.g., elevation in meters), how are units preserved unambiguously?
8. **Coexistence with Marten 9 + DEC-060/DEC-062.** `WorkoutLog` is expected to be an **EF relational entity** (history), while a `WorkoutLogged` event lands on the Marten plan stream (DEC-060: handlers emit events, projections own EF state; DEC-062: EF projections register via `opts.Add(...)`). Does the new `jsonb` column introduce any Weasel/Marten 9 schema-diff noise on `dotnet ef migrations add`? Should `WorkoutLog` implement `Marten.Metadata.ITenanted` for conjoined-tenancy consistency (as `UserProfile` does), or is tenant scoping implicit? Any gotcha when an `EfCoreSingleStreamProjection` writes a row carrying a large JSONB bag inside Marten's transaction?
9. **Migration safety.** Confirm the "add a new metric key requires no schema change" claim holds for the recommended representation, and that adding/removing canonical keys does not produce a spurious EF migration. Note any `dotnet ef` snapshot behavior for `jsonb`/owned-JSON columns under EF Core 10.

## Context

Slice 2b (Workout Logging) of the MVP-0 cycle for **RunCoach** (an AI running coach; .NET 10 / C# 14 backend, React 19 frontend) adds a `WorkoutLog` EF Core entity. Required columns: `Id`, `UserId`, `PlannedWorkoutId` (nullable), `LoggedAt`, `Distance`, `Duration`, `CompletionStatus` (enum: complete/partial/skipped), `Notes`. One nullable JSONB column `Metrics` holds an arbitrary key bag (`rpe`, `hrAvg`, `hrMax`, `calories`, `splits`, `hrv`, `sleepScore`, `recoveryScore`, `weather`, `terrain`, …) with **no schema enforcement** — explicitly designed so future Apple HealthKit / Garmin ingestion populates the same keys without a migration.

**Existing repo precedent (verified):**
- `RunnerOnboardingProfile` (EF entity, `[Table("UserProfile")]`) maps five JSONB columns via `builder.OwnsOne(p => p.TargetEvent, b => b.ToJson())` — i.e., the established pattern is `OwnsOne(...).ToJson()` over **closed, structured owned records**, not an open map. There is **no precedent for a heterogeneous/open JSONB bag** and **no `ValueConverter` anywhere** in the codebase.
- `RunCoachDbContext` uses `ApplyConfigurationsFromAssembly`, pins all entities to the `public` schema (Marten owns `runcoach_events`), and inherits `IdentityDbContext<…, Guid>`.
- Value objects: `Distance` (`readonly record struct`, stores meters; `.Kilometers`/`.Miles`), `Pace` (sec/km), `PaceRange`, `TrainingPaces` — used in compute only, never persisted yet.
- Stack: EF Core 10.0.8, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2, Marten 9.2.1 + `Marten.EntityFrameworkCore` 9.2.1, Wolverine 6.1.0, on PostgreSQL. Migrations follow a strict "never edit existing migrations; only `dotnet ef migrations add`" rule.
- Contract pipeline (DEC-066): Swashbuckle 10 (`SupportNonNullableReferenceTypes()` + a `RequireNonNullablePropertiesSchemaFilter`) → committed `backend/openapi/swagger.json` → `@rtk-query/codegen-openapi` 2.2.0 + Orval 8.12.1 (`client: 'zod'`, `override.zod.strict = true`) → `git diff --exit-code` drift gate.

The `Metrics` bag is the first open-ended JSONB structure in the project. Every prior JSONB use is a fixed shape. The wearable-integration research (`batch-3c`) and unit-system research (`batch-9b`) already establish the *semantic* metric set and canonical-meters storage rule; this prompt is about the **EF Core 10 / Npgsql 10 persistence-and-codegen mechanics**, not which metrics to capture.

## Why It Matters

The metrics column is a one-way architectural door. Pick a representation that can't be queried/indexed and a later "show me my high-RPE weeks" slice forces a data migration; pick one that doesn't round-trip through codegen and the frontend hand-maintains a metric-key list that drifts (exactly the contract-drift class Slice 1B was built to kill); pick one that destabilizes the Marten/Weasel migration diff and every future `dotnet ef migrations add` emits noise. The choice also has to serve a future where HealthKit/Garmin write the same keys an athlete types by hand today. Getting the representation, the codegen shape, and the canonical-key mechanism right once is far cheaper than retrofitting after real workout history exists.

## Deliverables

- **Recommended representation** for `Metrics` (one default + one fallback), with the exact EF Core 10 fluent mapping and a worked `WorkoutLog` entity + `IEntityTypeConfiguration<WorkoutLog>` (sub-150 LOC).
- **OpenAPI + Zod shape** the recommended DTO produces, showing the generated Zod and confirming the drift gate stays meaningful on core fields while the metrics map stays open.
- **Canonical-key mechanism** — how `WorkoutMetricKeys` (C#) becomes the single source of truth and reaches the frontend via codegen, not a hand-mirrored list.
- **Indexing decision** stated as a recommendation (index now vs design-for-later), with the migration SQL if "now," and the trigger for adding it if "later."
- **Value-object mapping guidance** for `Distance`/`Duration` (ValueConverter vs complex type vs primitive), establishing the project's first such pattern.
- **Round-trip/regression test pattern** — write a `WorkoutLog` with a sparse bag + a splits array, read it back, assert key fidelity and null-vs-absent; plus a `dotnet ef migrations add` no-op check proving a new key needs no DDL.
- **Marten 9 coexistence note** — confirm no Weasel diff noise and resolve the `ITenanted` question.

## Out of scope

- Which metrics to capture / coaching semantics of each (settled by `batch-3c`).
- Display unit conversion / user unit preference (settled by `batch-9b` / DEC-041).
- How the metrics flow into the LLM prompt (covered separately by the ContextAssembler design and any Slice 2b context-injection work).
- Choosing a different database or ORM — PostgreSQL + EF Core + Marten are locked.
- The `WorkoutLog`↔prescribed-workout relationship (FK vs date-match) — a spec-time domain decision, not a persistence-mechanics research item.

The artifact lands at `docs/research/artifacts/batch-28a-efcore10-npgsql-heterogeneous-jsonb-metrics.md` and integrates into the Slice 2b spec plus a new DEC entry locking the metrics-column representation and the canonical-key mechanism.
