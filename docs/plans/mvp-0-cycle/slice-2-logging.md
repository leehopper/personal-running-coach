# Slice 2 Requirements: Workout Logging

> **Requirements only — not a specification and not an implementation plan.** Captures the "what" at a level that survives implementation discoveries. The "how" is written as a spec in a fresh session at build time. Parent: `docs/plans/mvp-0-cycle/cycle-plan.md`.

## Purpose

Let the user log what they actually ran, with flexibility across data richness — from bare-minimum (distance + duration + did-I-complete-it) to rich (HR, splits, RPE, biodata, calories) — plus a freeform "what happened" narrative that becomes first-class context for the coaching LLM.

## Functional requirements

When this slice is complete:

- The user sees today's prescribed workout on the home surface.
- The user can log a completed or partial workout via a form.
- Minimum required logging: distance, duration, completion status (complete / partial / skipped), freeform notes.
- Optional richer logging: any subset of RPE, average HR, max HR, calories, splits, HRV, sleep score, recovery score, weather, terrain — without the form blocking on missing fields.
- The freeform "what happened?" notes are explicitly first-class: they're persisted, they flow into the coaching LLM context, and they're visible in history.
- Logged workouts appear in a history list with their notes and whatever structured metrics were provided.
- (Verified but not user-visible:) the freeform notes plus whatever structured metrics were captured become part of the context the coaching LLM sees for later slices' adaptation and open-conversation flows.

## Quality requirements

- Integration tests cover the log endpoint across minimum, rich, and notes-only shapes.
- Unit tests for the `ContextAssembler` extension cover: empty metrics, partial metrics, full metrics, long notes, notes with special characters.
- Component tests cover form validation (required fields present, optional fields truly optional).
- One E2E test: log a minimum-payload workout, log a rich-payload workout, see both in history with their notes.
- (Verification path for context injection:) an eval scenario confirms that a logged workout's notes + metrics show up in the prompt context the LLM receives, without necessarily testing adaptation (which is Slice 3).

## Scope: In

- `WorkoutLog` EF Core entity: required core columns (distance, duration, completion status, notes), plus a flexible structured-metrics store for everything optional.
- Persistence, read-by-user, read-by-planned-workout.
- Log form UI with a collapsed "more details" affordance for optional fields.
- Today's-workout card on the home surface with a "Log" action.
- History list UI that renders whatever is present — structured metrics when available, notes when not.
- `ContextAssembler` extension that includes recent logs (structured metrics + notes) in the training-history block for LLM context.

## Scope: Out (deferred)

- Adaptation in response to logs (Slice 3).
- Apple HealthKit auto-fill (post-MVP-0; the flexible-metrics store is designed so HealthKit can populate it without schema migration).
- Garmin / Strava / COROS ingestion (post-MVP-1 per DEC-033).
- Voice notes or speech-to-text logging.
- Mid-workout logging (opens a temporal-binding problem — deferred).
- LLM-parse-natural-language-into-form (the "log my run" → populated form flow — explicitly de-prioritized in session brainstorm in favor of structured form + freeform narrative).
- Editing or deleting a logged workout.

## Pragmatic defaults for deferred decisions

- **Metrics storage shape:** flexible/extensible so adding a new metric later requires no schema change. The spec picks the exact mechanism (e.g., JSONB column on `WorkoutLog`).
- **Canonical metric key naming:** the spec locks a canonical set (e.g., `rpe`, `hrAvg`, `hrMax`, `calories`, `splits`, `hrv`, `sleepScore`, `recoveryScore`, `weather`, `terrain`) so manual entry today and auto-fill later produce the same shape.
- **Absent-metric rendering in context:** the assembler should explicitly express "not provided" rather than silently omit — the LLM reads absence as signal.
- **Completion status:** three values (complete / partial / skipped). Spec can expand.
- **Notes length:** no hard server-side cap; frontend provides generous textarea.
- **Units:** inherit the existing unit-system design (DEC-041, `docs/planning/unit-system-design.md`). Whatever the user preference is, stored in canonical units internally.

## Research to consult before writing the spec

- `docs/research/artifacts/batch-3c-wearable-integrations.md` — what metric shapes to anticipate for future HealthKit / Garmin ingestion, so canonical metric keys chosen now match what integrations will produce later.
- `docs/research/artifacts/batch-9b-unit-system-design.md` — distance/pace unit handling + internal canonical storage.
- `docs/research/artifacts/batch-10b-dotnet-backend-review-practices.md` — EF Core patterns, JSONB usage, entity design conventions.
- `docs/planning/memory-and-architecture.md` — five-layer summarization hierarchy; Slice 2 produces layer-1 (per-workout) summaries implicitly via the log + notes.
- `docs/planning/coaching-persona.md` — voice for the post-log follow-up messaging, if any.

## Open items for the spec-writing session to resolve

> **All resolved as of 2026-05-31.** The first three (storage mechanism, canonical keys, indexing) are settled by Batch 28 research — see § Research resolutions. The last three (prescribed-workout relationship, post-log flow, history pagination/filtering/sorting) are settled by the brainstorm — see § Brainstorm resolutions. The original questions are retained below for traceability.

- Exact storage mechanism for flexible metrics (JSONB column, side table, key-value entity, etc.).
- Canonical metric key list and naming conventions (from brainstorm: `rpe`, `hrAvg`, `hrMax`, `calories`, `splits`, `hrv`, `sleepScore`, `recoveryScore`, `weather`, `terrain`).
- Indexing strategy — if any later slice queries by metric value (e.g., "HR > 150 runs"), decide GIN vs. expression indexes vs. defer.
- Relationship between a `WorkoutLog` and a prescribed workout in the plan projection: nullable FK? matched by date + expected structure? off-plan logs also accepted?
- How the post-log flow looks — does the user just save and go back to home, or is there a lightweight confirmation / next-workout-preview surface?
- History list pagination, filtering, and sorting defaults.

## Carry-forward from earlier slices

These items surfaced during Slice 1 but were intentionally deferred to land here in Slice 2 because they fit the slice's scope better than Slice 1B's contract-codegen pass. The Slice 2 spec session must address each before implementation starts.

- **Wolverine LLM-failure error policy.** When `IPlanGenerationService` (or any in-handler LLM call) throws — transient 429, network blip, timeout under longer prompts — the current behavior is an abort that propagates to a 500 with no actionable user-facing message. Slice 2's longer prompts (workout-log narrative + pace-zone interpretation) make this measurably more likely. Spec must define: retry policy via Wolverine error handlers (or accept Anthropic-SDK backoff as sufficient), structured `OnboardingTurnResponseDto`/`WorkoutLogResponseDto` `kind=Error` shape carrying a user-facing message, frontend chat/log-form fallback affordance ("try again"), and a dead-letter signal that surfaces in OTel without paging the builder.
- **Canonical `WorkoutLog.Metrics` JSONB key constants in a shared file.** Cycle-plan flagged this in the original Slice 2 scope, but Slice 1 ships nothing that pre-locks the shape. Spec must place the canonical key set in one C# file (e.g., `Modules/Training/Constants/WorkoutMetricKeys.cs`) AND ensure the OpenAPI codegen pipeline (Slice 1B) round-trips the shape so the frontend never hand-maintains the metric-key list. Without this, the same hand-mirrored-shape drift that bit `OnboardingProgressDto` will recur on every metric add.
- **Eval-cache invalidation on prompt-template edit.** `Prompts/*.yaml` edits silently invalidate the committed eval cache; today the only signal is a CI Replay-mode failure on the next push, after the prompt change has already merged. Slice 2 will iterate prompts more frequently than Slice 1 did. Spec must define a pre-test hash check of `Prompts/*` against an eval-cache sentinel so a stale cache fails LOCALLY, with a clear "run rerecord-eval-cache.sh" message, before push. Alternately: version prompts in the cache key.
- **(reference)** Slice 1 contract-drift audit (in `docs/plans/mvp-0-cycle/slice-1b-hardening.md`) — Slice 2's new endpoints must consume the codegen pipeline, not hand-maintain Zod. Verify Slice 1B has merged before any Slice 2 endpoint lands.
- Whether to surface layer-1 summaries or the raw log in the history list.

## Research resolutions (Batch 28 — integrated 2026-05-31)

A `/cw-research` pre-spec pass (R-077–R-080, artifacts `docs/research/artifacts/batch-28{a,b,c,d}-*.md`) answered the technical open items and all three carry-forwards before the brainstorm. Load-bearing claims were independently re-verified before the decisions were locked. The decisions below are **locked** as DEC-072–075; the brainstorm need only confirm them and settle the remaining *product/UX* questions.

**Open items — resolved:**

- **Flexible-metrics storage mechanism** → **DEC-072**: a single nullable `string` column mapped to PostgreSQL `jsonb` (API owns the JSON), open map on the wire (`additionalProperties` → `z.record`), with the core fields staying a strict/closed object so the codegen drift gate is meaningful. (EF Core 10 cannot map an open dictionary via `ToJson()`.)
- **Canonical metric-key list + naming** → **DEC-072** (+ R-080 Part 2): one C# file `WorkoutMetricKeys` as the single source, surfaced to the frontend via a codegen enum. Keys/units: `rpe`, `hrAvg`/`hrMax` (bpm), `calories` (kcal), `hrv` (ms SDNN), `sleepScore`/`recoveryScore` (0–100), `cadence` (full steps/min), `elevationGain` (m), `power` (W), `weather`, `terrain`, `splits` (array); reserve-now `verticalOscillation`/`groundContactTime`/`strideLength` (+optional `cadenceMax`); pace always derived as `paceSecPerKm`. Chosen to match HealthKit/Strava/Garmin source names so future auto-fill needs no migration.
- **Indexing strategy** → **DEC-072**: none at MVP-0; add a hand-authored expression index when the first value-query slice lands (the `jsonb` column doesn't change shape, so it's a pure additive migration).

**Carry-forwards — resolved:**

- **Wolverine LLM-failure error policy** → **DEC-073**: single retry layer = the Anthropic SDK (`MaxRetries=2`, ~30s per-attempt timeout, bounded by the request `CancellationToken`); `ICoachingLlm` throws `Transient`/`Permanent` exceptions (translated in `ClaudeCoachingLlm`); wire envelope = a `Kind=Error` flat-slot branch at HTTP 200 carrying `errorMessage`/`retryable`/`retryAfterSeconds`; non-paging OTel `coaching.llm.failures` counter; idempotency marker co-transactional so the same key is reusable on retry. **Scope note:** there is **no live LLM call on a Slice 2b request path** — the log write is pure persistence; this contract is decided now and first exercised live in Slice 3. No resilience/envelope code is in 2b's implementation scope beyond defining the shared shapes.
- **Canonical `WorkoutLog.Metrics` JSONB key constants** → **DEC-072** (single-sourced `WorkoutMetricKeys` flowing through codegen — no hand-mirrored TS list).
- **Eval-cache invalidation on prompt-template edit** → **DEC-074**: a committed SHA-256 sentinel manifest of `Prompts/*.yaml` (`.prompt-hashes.sha256`), checked by a glob-scoped lefthook pre-commit hook + an `EvalTestBase` backstop, regenerated by `rerecord-eval-cache.sh`. Fails locally at commit with a "run rerecord-eval-cache.sh" message instead of as a post-merge CI Replay failure.
- **Codegen-not-hand-maintained-Zod** (Slice 1B reference) → confirmed: Slice 1B (DEC-066) merged; new 2b endpoints consume the codegen pipeline. Frontend log-form + history conventions locked as **DEC-075** (RHF + Zod-v4 empty-numeric→`undefined` coercion, derive schema from generated Zod, `shouldUnregister:false`, `<dl>` sparse-metric history, splits display-only, pessimistic create).

**Resolved by the brainstorm (2026-05-31):** all four product/UX questions are settled — see § Brainstorm resolutions (product/UX) below:

- `WorkoutLog` ↔ prescribed-workout relationship → **snapshot-on-log** (server-authoritative nullable prescription snapshot; off-plan = null; no minted id).
- Post-log flow → dedicated **`/log` route** + success toast (no preview surface).
- History pagination / filtering / sorting → **DB-driven `POST .../query`** keyset endpoint (all operations at the DB layer), newest-first, no filters at MVP-0; **ISO-week-grouped** display.
- Raw logs vs layer-1 summaries → **raw** (UI sparse `<dl>` per DEC-075; assembler injects compact layer-1 one-liners with selective RPE/HR absence markers).

**Follow-up surfaced:** add `role="alert"` to the shared shadcn `FormMessage` for assertive error announcement (it currently lacks it) — coordinate with task #560.

## Brainstorm resolutions (product/UX — 2026-05-31)

A `/catchup`-initiated brainstorm settled the four remaining product/UX questions, grounded in a codebase verification pass over plan-event/projection mechanics, the regeneration lifecycle, and Slice 3/4 needs. The load-bearing finding that reshaped question A: prescribed workouts have **no stable identifier and no calendar date** — a `WorkoutOutput` is addressed only by `(PlanId, weekNumber 1-based, dayOfWeek 0–6)`; the `MicroWorkout.WorkoutId` record is dead code; plans regenerate into a **new stream with a new `PlanId`** (the prior stream retained indefinitely — no deletion/archival code exists); and there is **no plan start-date anchor** (the frontend's `resolveCurrentWeek` returns the lowest populated week = week 1 at MVP-0). These are the product/UX "what"; the spec writes the "how". DEC-entry candidates are flagged.

**A. `WorkoutLog` ↔ prescribed-workout relationship → snapshot-on-log.**
The `WorkoutLog` (EF Core, tenanted, treated as an immutable historical fact) carries a **nullable, server-authoritative prescription snapshot** captured at log time — not a live foreign key. The snapshot holds the originating `SourcePlanId`, the `(WeekNumber, DayOfWeek)` coordinate that located the prescription, and a frozen copy of the prescribed `WorkoutType` / `TargetDistance` / `TargetDuration` / `TargetPace`. Off-plan runs are first-class: the whole snapshot is null.
- *Rationale:* durability comes from the frozen copy, not a key. A live FK cannot span a regeneration (P2's workouts are freshly generated, with no identity continuity to P1's), and a minted workout id buys neither durability (same reason), nor uniqueness (the coordinate already uniquely addresses a slot — one `WorkoutOutput` per day), nor reorder-stability (matching is by `dayOfWeek` value, not array position) — while costing the injection of server data into the verbatim-LLM-output projection and coupling EF identity to the Marten event layer (`backend/CLAUDE.md` warns against shared cross-store identity). Slice 3's deterministic prescribed-vs-actual deviation engine needs the prescription *as it stood when the run happened*; because adaptation regenerates the plan, only a snapshot captured at log time is point-in-time correct. The snapshot is written server-side from the live plan (the "Log" action originates on the today card, so the coordinate is in hand) — client-supplied prescribed values are never trusted.
- *Supersedes* the cycle-plan "Data Model" `WorkoutLog.PlannedWorkoutId`, a 2026-04-19 assumption that predated the no-stable-id finding. `read-by-planned-workout` = query on `(SourcePlanId, WeekNumber, DayOfWeek)`.
- *Refinement:* split the cycle-plan's single `LoggedAt` into `OccurredOn` (the run's calendar date — the matching anchor) and a `CreatedOn` audit field, since a run can be logged on a later day.
- *Recorded as **DEC-076**.* The snapshot-on-log model is the load-bearing architectural decision of the slice.

**B. Post-log flow → dedicated `/log` route + success toast.**
Logging is a dedicated mobile-first `/log` route (full-screen single-column form; optional metrics behind the locked "More details" `Collapsible`), not a dialog. On a pessimistic-create success the user is navigated home and a transient success toast confirms the save; the home surfaces already reflect the result (today card → completed, run in history, tomorrow in the upcoming list). No post-log confirmation/preview surface and no coach acknowledgment — the coaching voice and forward framing belong to Slice 3 (the persona design carries no post-log voice at MVP-0).
- *Adds* a reusable shadcn `sonner` toast primitive (none exists today).

**C. History query → DB-driven `POST .../query` + week-grouped display.**
History is served by a query-body endpoint (`POST /api/v1/workouts/logs/{query|search}`, name TBD), distinct from the `POST .../logs` create endpoint. The query DTO (paging, sort, optional filters) translates to a **single EF Core keyset query** — sort, filtering, and pagination all execute at the database layer, never in app logic. MVP-0 defaults: newest-first by `OccurredOn` (tiebreak by id), keyset "Load older" paging, no filters exposed yet (DTO shaped to grow). Modelled as an RTK Query `query` endpoint over POST (`providesTags`), with `infiniteQuery` merge for accumulation; create's `invalidatesTags` busts it. It is a read, so it carries no antiforgery token (spec confirms no global filter forces one on POSTs).
- *Display:* runs are **grouped by ISO calendar week** (`Week of …` headers) as a client-side presentation over the flat, DB-sorted/paged result — grouping is display structuring, not a query operation, so it stays out of the DB query and out of pagination (paging is by run, not by week).
- *Rationale (pattern):* the query-body pattern keeps rich querying off REST verbs (not built for it) and pushes efficiency to the database; this endpoint shape is the reusable template Slice 3/4 inherit.

**D. History rendering (two surfaces).**
- *UI (settled by DEC-075):* raw individual logs, each rendered as a sparse-metric `<dl>` (only present metrics), splits display-only.
- *LLM context (this slice's `ContextAssembler` extension):* recent logs are injected into the training-history block as **compact layer-1-style one-liners** (present metrics + notes inline, in the shape the memory-architecture layer-1 spec already defines), not multi-line blocks — the assembler is a compression engine, and verbose per-workout blocks would fight the Layer-1 tier and risk the budget-overflow truncation that collapses Layer 1 into weekly summaries (dropping the very detail being added). Metric labels/units come from the single shared `WorkoutMetricKeys` source (the same source behind the frontend metric-meta map), so UI and prompt cannot drift. **Absence handling:** present metrics inline; an explicit absence marker (e.g. `(no HR/RPE)`) **only for the effort/intensity signals (RPE, HR)** that change coaching reasoning; peripheral metrics silently omitted (their absence is not coaching signal); notes always rendered when present. The determinism that satisfies "absence is signal" (this doc's Pragmatic-defaults + DEC-072) lives in the formatter and an eval asserting the bare-vs-rich distinction — not in LLM goodwill. **No** prescribed-vs-actual framing in 2b — the snapshot is on the log for Slice 3's deviation engine; 2b's assembler renders what the user did.

**Spec-time carry-ins (surfaced during the brainstorm):**
- The assembler input shape (`Modules/Training/Models/WorkoutSummary` or a new recent-log type) must grow to carry the metrics bag + notes (today it has six fixed fields and no metrics bag).
- The assembler has no production call site yet (plan generation uses a different compose path); 2b extends and **evals** the capability — the live wiring lands with its Slice 3/4 consumer.
- The shared shadcn `FormMessage` lacks `role="alert"` (task #560); the `/log` form needs assertive error announcement, so coordinate that fix here.
- Confirm no global antiforgery filter forces a token on the read-POST query endpoint.

## How this feeds the spec

When Slice 2 implementation begins in a fresh session:

1. Read this doc + the cycle plan + research artifacts above + the `ContextAssembler` code shipped after Slice 1.
2. Brainstorm with the user to nail down the open items.
3. Write the spec under `docs/specs/slice-2-logging/`.
4. User reviews before implementation.
5. Implement against the spec.

## Relationship to the cycle plan

The cycle plan's "Slice 2 — Workout Logging" section carries acceptance criteria and a brief scope summary; this doc elaborates without crossing into implementation. If they conflict, the cycle plan wins — update this doc to match.
