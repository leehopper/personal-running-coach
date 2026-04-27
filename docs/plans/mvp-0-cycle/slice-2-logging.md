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

## How this feeds the spec

When Slice 2 implementation begins in a fresh session:

1. Read this doc + the cycle plan + research artifacts above + the `ContextAssembler` code shipped after Slice 1.
2. Brainstorm with the user to nail down the open items.
3. Write the spec under `docs/specs/slice-2-logging/`.
4. User reviews before implementation.
5. Implement against the spec.

## Relationship to the cycle plan

The cycle plan's "Slice 2 — Workout Logging" section carries acceptance criteria and a brief scope summary; this doc elaborates without crossing into implementation. If they conflict, the cycle plan wins — update this doc to match.
