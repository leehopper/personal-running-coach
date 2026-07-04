# Slice 4C Design: Onboarding Redesign + km/miles Units

> **Design + requirements — not an implementation plan.** Captures the "what" and the locked design decisions for the onboarding intake redesign and the km/miles unit affordance. The per-piece "how" (form field set, endpoint shapes, prompt prose, task breakdown) is written as a spec in a fresh session at build time. Parent: [`./cycle-plan.md`](./cycle-plan.md). Sibling slices: [`./slice-4a-voice-retune.md`](./slice-4a-voice-retune.md) (4A, COMPLETE), [`./slice-4b-conversation-core.md`](./slice-4b-conversation-core.md) (4B, COMPLETE). Closes the last three open Slice 4 items from the 2026-06-13 live-pass re-run plus two 4B review-cycle carryovers.

## Origin: the Slice 4C brainstorm (2026-07-01)

Slice 4 (Open Conversation) was decomposed 2026-06-17 into 4A (voice re-tune, COMPLETE 2026-06-23, DEC-084), 4B (conversation core, COMPLETE 2026-07-01, DEC-085), and **4C (onboarding redesign + km/miles units — this doc)**. With 4B shipped, a `/catchup` brainstorm grounded in a five-surface code map (backend + frontend onboarding, the unit system, the pre-scoped requirements, and the relevant decisions/research; load-bearing claims independently verified) locked the 4C decisions below.

The entire 4C "what" comes from three Captured-During-Cycle rows and two 4B carryovers, since no dedicated 4C requirements doc predates this one (`slice-4-conversation.md` is a 4B-era doc, silent on onboarding UX and units):

- **Onboarding is too prose/chat-driven** (cycle-plan 2026-06-13): the six-topic back-and-forth free-text flow feels heavy; the builder wants a structured starting form for the known fields (goal, event+date, weekly volume, days, duration, units, injuries) with a free-text box per area for nuance. Pre-flagged "likely a research-prompt item before building."
- **WeeklySchedule slot-merge loop** (cycle-plan 2026-06-11 live pass): days/duration given across separate free-text turns are never merged into the atomic `WeeklyScheduleAnswer`, so the model re-asks for clarification until one message states every field at once. Plus an input-kind/assistant-text mismatch.
- **Unit flexibility not communicated** (cycle-plan 2026-06-13): the coach asked km-vs-miles only at the very end while speaking km the whole flow; the runner should be told up front they can speak in either unit and see the app in their unit.
- **4B carryovers to triage**: the `SeedActivePlanAsync` `ITenanted` test-harness gap, and the orphaned `ConversationPanel`/`getConversationTurns` read chain.

## Purpose

Make onboarding feel light and make km/miles a real, editable, end-to-end display preference. Two concerns, one slice, decomposed so the unblocked units work ships without waiting on the research-gated onboarding redesign.

This is UX + a deterministic display/parse layer, not a storage rework and not an LLM-behaviour change. Storage stays canonical km/SI; the LLM never converts units (DEC-010/DEC-041/REVIEW.md); the prompt stays km-native.

## Decomposition (mirrors Slice 2 → 2a/2b)

- **4C-units** — a units foundation with no LLM/onboarding coupling. **Unblocked; ships first.**
- **4C-onboarding** — the hybrid form-first intake redesign. **Research-gated (R-085); ships after the artifact lands.**
- **Cleanup** — two small standalone tasks (delete the orphaned conversation read chain; fix the `SeedActivePlanAsync` tenancy seam). Ride with whichever sub-slice lands first.

The sub-slices are sequenced by a real dependency: the onboarding form collects units up front and renders inputs in the chosen unit, so it consumes the 4C-units foundation. Each sub-slice gets its own spec → PR-sequence at build time.

## Locked context — binding constraints (do not re-open)

These shipped decisions constrain 4C; the brainstorm honors them, it does not re-litigate them:

- **DEC-047** — onboarding state is a per-user Marten stream + inline `SingleStreamProjection<OnboardingView>` + `EfCoreSingleStreamProjection<RunnerOnboardingProfile>` (table `UserProfile`), deterministic-led over a static six-topic list + a deterministic completion gate. A redesign **extends** this model; it does not replace it.
- **DEC-060** — the EF `UserProfile` row is written by a projection inside Marten's transaction (no dual-write). A deterministic form must still **originate events**; it does not write the EF row directly.
- **DEC-058** — onboarding output is Pattern-B (`OnboardingSchema.Frozen`, six nullable `Normalized*` slots + a `Topic` discriminator, runtime one-slot validator). Any new/changed onboarding field edits this frozen schema; Anthropic rejects `minimum`/`maximum`/`oneOf`/`pattern`/`format`, so bounds live in prompt text + validator, not JSON Schema.
- **DEC-084** — onboarding voice is gruff-direct with a hard `VoiceProseGuard` STYLE gate (no em/en dashes, no exclamation marks, no filler); `onboarding-v1` is an enforced prose surface. Any prompt rewrite regenerates the DEC-074 hash manifest and re-records affected eval fixtures. Its open item (the advisory restraint rubric mis-scoring short intake turns 0.0 on inapplicable criteria) is a test-only fix 4C-onboarding may fold in.
- **DEC-041** — canonical metric/SI storage; convert only at the boundary/display; the LLM never does unit math (`Distance` stores meters, `Pace` stores sec/km). The imperial DISPLAY formatter and `UnitsNet` were **deferred to MVP-1**; 4C is ahead of that phasing and records an amending decision (DEC-086).
- **DEC-010 / root `REVIEW.md` lines 50-53 (CRITICAL)** — the deterministic computation layer does ALL numeric work including distance/pace conversion; the LLM layer never converts units.
- **DEC-085 PR3b** — `WorkoutDraftUnitConverter` is the deterministic km/miles precedent (`MetersPerMile = 1609.344` exact); the classifier extracts the runner-stated unit verbatim and conversion happens deterministically at confirm time. 4C-units reuses this pattern (and its TS mirror already present in `draft-to-form.helpers.ts`); it does not add a parallel one.
- **DEC-075 / DEC-063** — redesigned onboarding form fields inherit the RHF + Zod-v4 conventions (empty-numeric coercion footgun, `shouldUnregister:false`); `motion/react` stays deferred with a defined adoption path (Tailwind + `tw-animate-css` is the installed animation capability).

## Design decisions (brainstorm 2026-07-01)

### D1 — Decomposition: units first (unblocked), onboarding second (research-gated) (**builder-locked**)

4C ships as **4C-units** then **4C-onboarding**, with two standalone cleanup tasks. The units foundation has no LLM/onboarding coupling and proceeds immediately; the onboarding redesign is research-gated (D2) and consumes the units foundation. Rejected: a single monolithic spec (stalls the unblocked units work behind research); two fully independent sub-slices with no shared seam (duplicates the units-consumption seam the form depends on). The builder's steer was "split as needed to optimize development."

### D2 — Onboarding intake = hybrid form-first (**builder-locked**)

A structured form is the **primary** intake and collects the known fields directly; it **originates `AnswerCaptured` events deterministically** (form submission maps to the same event the LLM extraction produces today), so the DEC-047/DEC-060 event-sourced model survives — only the *origin* of the event changes (form submit vs LLM extraction), and both projections (`OnboardingView` + `UserProfile`) materialize unchanged. The LLM's role shrinks to **optional per-area free-text nuance** ("anything else about your schedule?") that feeds coaching context.

- **The WeeklySchedule slot-merge loop dissolves**: structured slots are collected whole from form fields, so there is no multi-turn free-text assembly to fail. The resume-path `pickInputTypeForTopic` mismatch also goes away because the form carries structured state rather than guessing an input control from a topic.
- **Units collected up front** (first field) per the 2026-06-13 finding; subsequent numeric inputs are entered in the chosen unit and converted miles→km on write (D3). Any free-text nuance uses the DEC-085 verbatim-extract + deterministic-convert precedent — the LLM never converts.
- Rejected: full deterministic form with no LLM at all (loses the free-text nuance the builder asked for); keep conversational Pattern-B and fix the loop server-side via field-level partial-slot merge or transcript-feedback (a non-obvious event-model change with replay/idempotency implications, and transcript-feedback breaks the byte-stable Pattern-B prompt-prefix cache — both rejected as more risk than the form path).
- **Research gate**: per `CLAUDE.md` § Research Protocol and the pre-flag on the 2026-06-13 row, the form-first redesign lands a research artifact (R-085, `batch-31a`) before the 4C-onboarding spec. The units foundation is not gated. **R-085 landed + integrated 2026-07-04 — see § "R-085 findings integrated" below.** It resolves the "how" within this locked shape and refines one point: onboarding needs **no LLM call at all** (nuance is stored verbatim text rendered into later coaching prompts, not an onboarding-time extraction), so the "optional LLM free-text nuance" framing above collapses to "optional free-text stored on existing slot fields." The units-verbatim-extract/DEC-085 mention applies to the *log-write* path, not onboarding.

### D3 — Units = frontend display-only conversion; prompt stays km-native (**builder-locked**)

Conversion lives on the **frontend at display** (and miles→km on write for numeric inputs); the wire and the plan-gen prompt stay km/SI. A shared unit-aware formatter module reads the persisted preference and converts km↔miles, reusing the `1609.344` constant already mirrored in `draft-to-form.helpers.ts`.

- **Pace is net-new work**: pace is an *inverse* conversion (`sec/km × 1.609344 = sec/mi`), not the same forward constant, and `WorkoutDraftUnitConverter` covers distance + duration only. A pace formatter is authored with a small formula-derived display spec: render `mm:ss/mi` rounded to whole seconds; **race distances stay literal proper nouns** (a `5K`/`10K`/half/marathon never renders "3.1 mi"); **track intervals stay in meters** regardless of preference.
- **Coverage is end-to-end across every numeric surface**: plan cards / today / upcoming, logged-workout **history**, the adaptation before/after diff, and all pace labels — not just the plan view. (Spec-time correction: the **log-confirmation card is excluded** — it echoes the runner-**stated** unit, confirming what was logged; see the Scope note.)
- Rejected: backend API-boundary conversion with explicit `{value, unit}` DTO fields (DEC-041's MVP-1 shape — a versioned-API + orval/zod codegen change across every distance/pace field, more blast radius than a self/family MVP-0 display toggle needs); SI-unify-first migration of the km-native plan/prompt layer (cleanest long-term but touches the plan structured-output contract; deferred to MVP-1).

### D4 — Canonical preference = a dedicated `UserSettings` store, decoupled from onboarding (**builder-locked**)

The unit preference moves to a **dedicated user-settings store** — a mutable EF row keyed by `userId` (last-write-wins, no event history; matches the `RunnerOnboardingProfile` EF-entity pattern minus the projection), a natural home for future settings — with `GET`/`PUT` endpoints. This decouples "settings" from "onboarding answers": the preference is editable post-onboarding without mutating the onboarding event stream or re-sending a whole `PreferencesAnswer` record.

- The enum stays the shipped `PreferredUnits {Kilometers, Miles}` (already codegen'd to the frontend); 4C does **not** resurrect DEC-041's never-built `UnitPreference {Metric, Imperial}` (YAGNI). Recorded via DEC-086 amending DEC-041's phasing.
- 4C-onboarding writes the runner's chosen unit into `UserSettings` as part of the form; until it lands, `UserSettings` defaults to `Kilometers` and is editable via the Settings toggle. The onboarding `PreferencesAnswer.PreferredUnits` slot becomes vestigial for units (superseded by `UserSettings`); its migration/removal is a 4C-onboarding concern.
- A **Settings-page Units toggle** (new Units section; the page currently exposes only Plan + Appearance) writes the preference via `PUT`.
- Rejected: keep the preference on the onboarding aggregate + a purpose-built settings-edit event on the onboarding stream (keeps units entangled with onboarding answers); reuse the existing whole-record `POST /answers/revise` path (the client must resend all four `PreferencesAnswer` fields and it mutates the onboarding aggregate for a settings edit); defer editability entirely (no way to change units without re-onboarding).

### D5 — Cleanup: delete the orphaned conversation read chain; fix the `SeedActivePlanAsync` tenancy seam (**builder-locked**)

- **Delete** the orphaned Slice 3 read chain — `getConversationTurns` + the two generated hooks (`useGetConversationTurnsQuery`/`useLazyGetConversationTurnsQuery`) + `useConversationTurns` + `ConversationPanel` + their specs (and the turns dispatches in `conversation.api.spec.ts`/`workout-log.api.spec.ts`). No Slice-3-style read-only "Explain-the-change" surface will be re-mounted; `CoachChat` (4B PR6) is the conversation surface now.
- **Fix** the `SeedActivePlanAsync` `ITenanted` test-harness gap — drop the explicit `TenantId` to match `ConversationTimelineControllerIntegrationTests`' seeding, so the authenticated SSE request's EF tenant filter no longer hides the seeded `RunnerOnboardingProfile`; restore the end-to-end on-plan card-prescription assertion. Test-harness only, non-blocking, unrelated to onboarding/units — lands as a standalone chore.

### D6 — Known MVP-0 limitation: coach prose stays km-native (recommended default, ratified)

Frontend-display-only means **structured numbers convert, but LLM free-text prose stays km-native**: the coach's sentences say "8 km easy" even for a miles-preferring user while the plan card shows "5.0 mi". Making the coach *speak* miles requires pre-converting the prompt data — DEC-041's **MVP-1** item, which breaks the byte-stable Pattern-B prompt cache and conflicts with keeping the LLM unit-agnostic. Accepted for MVP-0 (the builder is the primary user and defaults to km). The 2026-06-13 "speak either unit, I'll convert" affordance is honored on the **input** side (deterministic parse of a runner-stated unit, per DEC-085) — output prose is the deferred half.

## R-085 findings integrated (2026-07-04)

The form-first research artifact landed at [`docs/research/artifacts/batch-31a-form-first-onboarding-redesign.md`](../../research/artifacts/batch-31a-form-first-onboarding-redesign.md) and was integrated here. It resolves the "how" inside the D2 builder-locked shape; the code sketches (the `SubmitStructuredAnswers` handler, the unit-aware `Controller`, the RTK Query wiring) stay in the artifact and are the reference for the 4C-onboarding spec written fresh at build time — this doc records the design-level resolutions only. Every load-bearing claim was verified against `backend/src` / `frontend/src` at integration time (audit table in the artifact's Integration addendum); all verify, with **one refinement to the "zero manifest bust" headline** (finding R3 below).

- **R1 — Event origination = one `SubmitStructuredAnswers` command, whole-record-per-topic, reusing the existing `AnswerCaptured` event.** A deterministic Wolverine `[AggregateHandler]` over `FetchForWriting<OnboardingView>` appends one `AnswerCaptured` per completed topic (payload serialized the same way the LLM path serializes it), inside the same Marten transaction that already drives `OnboardingView` + the EF `UserProfile` projection — so DEC-047/060 and the completion gate are byte-for-byte unchanged and the form never writes the EF row (literal DEC-060 satisfaction). **No new event type** (`StructuredAnswersSubmitted` rejected — needless upcast/projection churn; identical replay only if one event shape exists). Whole-record-per-topic beats per-field progressive save decisively for MVP-0 (matches the atomic shape, ~6 events/onboarding, zero projection change; RHF holds in-session draft state). `FetchForWriting`/`[AggregateHandler]` is already the codebase idiom, including in this module (`SubmitUserTurn`, `OnboardingTurnHandler`).
- **R2 — No onboarding-time LLM call.** Per-area nuance is **stored verbatim text**, not LLM-extracted, and is interpolated into plan-gen/coaching prompts *later* (after the cache breakpoint, in the volatile suffix, to preserve prefix-cache byte-stability). This refines D2: the onboarding flow has zero LLM calls. DEC-085 (LLM never converts units) is satisfied trivially — there is no onboarding LLM call to convert anything.
- **R3 — Nuance rides on existing slot free-text fields — but the `Description`-field audit is NOT uniform (load-bearing).** Verified ground truth: `PrimaryGoal`, `CurrentFitness`, `WeeklySchedule`, `Preferences` each already carry a `Description` field; `InjuryHistory` carries `ActiveInjuryDescription` + `PastInjurySummary` (reuse `PastInjurySummary`). **`TargetEventAnswer` has no free-text field at all** (`EventName`/`DistanceKm`/`EventDateIso`/`TargetFinishTimeIso` only). So "reuse existing fields → no frozen-schema change → no DEC-074 manifest bust" holds for **5 of 6 topics**; TargetEvent is the sole exception. **Spec-time decision:** default to **omitting a nuance box on the TargetEvent section** (its four structured fields are complete), or route event nuance into an adjacent field — either keeps `OnboardingSchema.Frozen` and the manifest untouched. Adding a `Description` to `TargetEventAnswer` is the only path that busts the manifest (edits the frozen schema → `.prompt-hashes.sha256` regen + onboarding-eval re-record under DEC-084); take it only if the builder wants event nuance and won't reuse a field.
- **R4 — Both `slice-4-conversation.md` carry-forwards are OBSOLETED** (resolves the design doc's prior open question). A form carries its own static field affordances, so the server-driven `SuggestedInputType` redesign is unnecessary (delete the client `pickInputTypeForTopic` mirror outright, no server contract), and the `(topic, hasOutstandingClarification, isResume)` state-machine contract has nothing to coordinate (no turns; resume = a single `GET /state` hydrate; validation is client-side + the server completion gate). Mark both obsolete in the 4C-onboarding spec.
- **R5 — Retire the conversational path via feature-flag cutover; deprecate-in-place server-side.** New streams start on the form path behind `onboarding.formFirst`; because both paths emit only `AnswerCaptured`, an in-flight conversational stream **completes via the form with zero data migration** (hydrate from `OnboardingView`, submit the rest). Keep `POST /turns` + `OnboardingTurnHandler`/`OnboardingTurnOutput`/`onboarding-v1.yaml` deprecated for one release, then hard-remove; keep `GET /state` (resume). Retiring the prompt regenerates the DEC-074 manifest and archives the onboarding voice eval; `VoiceProseGuard` then gates only the remaining prose surfaces (`CoachChat`, plan-gen, adaptation).
- **R6 — Eval scope: retire the multi-slot free-text-merge scenarios** (resolves the open item below). A validated form group co-submits fields, so the merge failure mode structurally cannot occur — there is nothing to eval. The deterministic form path is covered by unit/integration tests (event origination, projection output, completion gate) + Vitest/RTL (section advance, conditional reveal, unit-aware numeric, day-toggle, error a11y) + a Playwright form→plan-gen E2E (which replaces the chat E2E's control-swap-per-turn assertion with fill→single-submit→state-complete).
- **R7 — Schedule UI + dependency verdict: no new dependency.** Day-of-week = radix `ToggleGroup` (`type="multiple"`, already installed via unified `radix-ui`) inside a `<fieldset><legend>`, wired through an RHF `Controller` (it does not bubble a native value). `TargetEvent`'s race date uses the existing native `<input type="date">`. **`react-day-picker`/shadcn Calendar is NOT warranted** (day-of-week ≠ calendar) — DEC-041/DEC-063 deferrals hold.
- **R8 — RTK Query + a11y.** One `submitStructuredAnswers` mutation + a `getOnboardingState` query (resume-hydrate `defaultValues`), tag-invalidation-linked, over the existing `__Host-` cookie + `X-XSRF-TOKEN` base query; dispatch `resetApiState()` on auth change (ties to the known per-user cache-reset gap tracked in ROADMAP § Deferred Items). A11y baseline: `<fieldset>`/`<legend>` per topic, error summary + `aria-describedby`/`aria-invalid`, focus management on the conditional TargetEvent reveal, WCAG 2.2 target-size (SC 2.5.8) on the day toggles, correct mobile input types.

## Architecture & data flow

### 4C-units

```
Preference source of truth = UserSettings (EF row, PK userId)
  GET /api/v1/settings/units   → { preferredUnits }
  PUT /api/v1/settings/units   → persists { preferredUnits }; default Kilometers

Frontend (display-only):
  RTK Query settings slice reads preferredUnits (cached; invalidated on PUT)
  shared unit-format module (reads preferredUnits):
    distance: km value → km | miles (× 1/1.609344), miles→km on write
    pace:     sec/km    → mm:ss/km | mm:ss/mi (× 1.609344), rounded whole seconds
    race distances: literal proper nouns (5K/10K/half/marathon)
    track intervals: meters, unit-invariant
  consumers: plan cards / today / upcoming, workout-log history, log confirmation card, pace labels
Wire + plan-gen prompt: unchanged, km-native. LLM never converts (DEC-010/041).
```

### 4C-onboarding (research-gated)

```
Structured form (primary) → form submit maps to AnswerCaptured events (deterministic origin)
  → OnboardingProjection (OnboardingView) + UserProfileFromOnboardingProjection (UserProfile)  [DEC-047/060 unchanged]
  → deterministic OnboardingCompletionGate → plan generation (unchanged terminal branch)
Units field first → written to UserSettings; numeric inputs entered in chosen unit, miles→km on write
Optional per-area free-text nuance → LLM extracts verbatim (DEC-085 pattern) → coaching context; LLM never converts
Schema/prompt edits (if any) → re-freeze OnboardingSchema.Frozen + DEC-074 manifest regen + onboarding eval re-record under DEC-084
```

## Scope

### In scope

- **4C-units**: the `UserSettings` store + `GET`/`PUT` endpoints; the shared frontend unit-format module (distance + net-new pace formatter with the display spec); wiring the module across plan/today/upcoming, workout-log history, the adaptation before/after diff, and pace labels; miles→km conversion at the one numeric write site; the Settings-page Units toggle; DEC-086 (amend DEC-041 phasing). **Note (spec-time correction, 2026-07-01):** the log-confirmation card is **excluded** from wiring — it keeps echoing the runner-**stated** unit (confirm-what-you-logged semantics). **The dead-code deletion is dropped from 4C-units** — ground truth (verified codebase map) shows the `FormatPreferences`/`UserPreferences` path is entangled with the legacy `AssembleAsync` context-assembler island (zero production callers; its section-build/overflow subtree is not shared with the live `Compose*` paths; pinned only by ~90 `ContextAssemblerTests` + 3 `EvalTestBase` helpers whose coaching-system / safety-boundary / logged-workout evals run through it). Excising it is an eval-architecture untangle, not a units line-item — tracked as a separate cleanup (see Open / deferred items). Spec: `docs/specs/20-spec-slice-4c-units/`.
- **4C-onboarding** (post-research): the form-first hybrid intake collecting the known fields; deterministic `AnswerCaptured` origination from form submission; optional per-area free-text nuance feeding coaching context; units collected up front and written to `UserSettings`; any onboarding prompt/schema change with the DEC-074 manifest regen + eval re-record under DEC-084; retirement of the now-vestigial `PreferencesAnswer.PreferredUnits` for units; the DEC-084 restraint-rubric register-only scoping fold-in (optional).
- **Cleanup**: delete the orphaned `ConversationPanel`/`getConversationTurns` chain; fix the `SeedActivePlanAsync` `ITenanted` seam.

### Out of scope (deferred)

- **LLM speaking miles** — pre-converting prompt data to the user's unit + a post-process wrong-unit regex validator (DEC-041 MVP-1). Prose stays km-native (D6).
- **SI-migration of the km-native plan structured-output layer** and **API-boundary `{value, unit}` DTO fields** (DEC-041 MVP-1; cross-cutting versioned-API/orval-zod churn).
- **`UnitsNet` / an i18n framework / locale auto-detect** — DEC-041 prescribes home-grown value objects and defers `UnitsNet`; MVP-0 is English-only. Introducing any of these would contradict DEC-041.
- **`react-day-picker` / shadcn Calendar** unless the onboarding research explicitly recommends a month calendar beyond the existing `multi-select-turn-input` + `date-turn-input`.

## Sub-slice acceptance criteria

### 4C-units — "I can…"

- [ ] …set my units to miles in Settings and see plan cards, today's workout, upcoming weeks, and workout-log history render distances in miles and paces as `mm:ss/mi`, while race names (`5K`/`10K`/half/marathon) and track intervals (e.g. `400m`) stay literal.
- [ ] …change the toggle back to km and see every surface update without a reload artifact (cache invalidated on `PUT`).
- [ ] …enter a distance in miles on a numeric input and have it persisted correctly as km/SI (deterministic, unit-tested conversion; the LLM never converts).
- [ ] …confirm the plan-gen prompt and the wire are unchanged (still km-native), with zero LLM unit conversion — verified by the existing eval suite staying green in Replay.

### 4C-onboarding — "I can…" (gated on R-085)

- [ ] …complete onboarding via a structured form for the known fields (goal, event+date, weekly volume, days, session duration, units, injuries) with a free-text box per area for nuance, and never hit the WeeklySchedule slot-merge loop.
- [ ] …choose my units on the first screen and see subsequent inputs and copy speak my unit from the start.
- [ ] …refresh mid-onboarding and resume without an input control mismatched to the question (the form carries structured state).
- [ ] …complete the form and get a generated plan, with the event-sourced `OnboardingView` + `UserProfile` projections materialized unchanged (DEC-047/060 intact) and the DEC-074 manifest + onboarding eval green under the DEC-084 voice guard.

### Cleanup — "I can…"

- [ ] …grep the frontend and find no live reference to `getConversationTurns` / `useConversationTurns` / `ConversationPanel` (deleted, specs removed, `CoachChat` unaffected).
- [ ] …run the SSE integration tests and see the on-plan card-prescription assertion pass end-to-end (the `SeedActivePlanAsync` tenancy seam fixed).

## Research gate

- **R-085 (`batch-31a`) — form-first hybrid onboarding intake over an event-sourced Pattern-B flow. ✅ LANDED + INTEGRATED 2026-07-04.** Artifact at [`docs/research/artifacts/batch-31a-form-first-onboarding-redesign.md`](../../research/artifacts/batch-31a-form-first-onboarding-redesign.md); prompt at `docs/research/prompts/batch-31a-form-first-onboarding-redesign.md`; marked `Integrated` in `docs/research/research-queue.md`. Findings integrated in § "R-085 findings integrated" above. **The 4C-onboarding gate is now clear — its spec can be written.** (4C-units already shipped in parallel, all 6 PRs merged.)

## Open / deferred items

- **`AssembleAsync` legacy-island cleanup (new, 2026-07-01 — its own scoped item).** `ContextAssembler.AssembleAsync` (the "legacy plan-generation path") has zero production callers; its section-build / overflow-cascade / `FormatPreferences` subtree is self-contained and **not** shared with the live `ComposeForOnboardingAsync` / `ComposeForPlanGenerationAsync` / `ComposeForAdaptationAsync` methods. It is pinned only by ~90 `ContextAssemblerTests` + 3 `EvalTestBase` helpers, so the coaching-system-voice / safety-boundary / logged-workout-context evals validate a prompt-assembly path production never runs. Cleaning it up = re-point those evals onto a real production prompt path (or consciously retire the coverage) and delete the legacy island + its ~90 tests + the `UserPreferences` / Training `UserProfile` records — likely with a DEC-074 manifest regen + funded-key fixture re-records. Needs its own DEC + spec; **out of 4C-units.** Surfaced by the 4C-units codebase map when the "delete the dead path" design item proved to contradict ground truth.
- The DEC-084 open item (scope the onboarding restraint rubric to register-only criteria so short clarifier turns stop scoring 0.0) is **moot under the R-085 design** — the retired conversational onboarding surface has no LLM-scored intake turn to score, and `VoiceProseGuard`/the restraint judge stop gating onboarding once `onboarding-v1` is retired (R5). Nothing to fold in unless the spec keeps a residual LLM turn, which R2/R5 recommend against.
- ~~Whether the form-first redesign eliminates the multi-slot free-text-merge eval scenarios~~ **RESOLVED by R-085 (R6): retire them** — a validated form group co-submits slots, so the merge failure mode structurally cannot occur. The 2026-06-11 row's "add eval scenarios for multi-slot free-text merges" ask is superseded; the form path is covered by deterministic unit/integration + Vitest/RTL + a Playwright E2E instead.
