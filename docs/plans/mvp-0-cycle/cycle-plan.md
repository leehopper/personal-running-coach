# MVP-0 + Adaptation Loop — Build Cycle Plan

> **Status:** Approved (2026-04-19). Active cycle.

## Status

- **Current Cycle:** MVP-0 + Adaptation Loop
- **Active Slice:** **Slice 3B (Live-Pass Fixes) — created 2026-06-11 from the live end-to-end validation pass; requirements at [`./slice-3b-live-pass-fixes.md`](./slice-3b-live-pass-fixes.md); F1 shipped 2026-06-11 (#185), F2/F3/F4 remaining.** Four immediate fixes, all in shipped Slice 3 / Slice 1 surfaces: **F1** the Amber safety referral is appended only on the L2 restructure path — Amber + L0/L1-absorb (incl. the cooldown dead-zone) drops the referral silently (live repro: an SG-I01 injury note during cooldown produced no safety turn) — **fixed and shipped #185 (2026-06-11): the referral is hoisted to a pre-escalation step (deduped per log), with DEC-081 recording the referral-commits-on-terminal-L2-failure exception to DEC-080**; **F2** the live macro generation persisted the trademarked term in `Macro.Rationale`, outside every existing guard; **F3** the macro ignored the target event date (16-week plan for a 10K nine weeks out, taper after the race); **F4** the live restructure proposed Week-1 target 24 km while its own micro week sums to 30 km — no weekly-target ↔ micro-week-sum validator rule. Done-gate: a fresh full live pass (new account, funded key, real browser) with all four fixes observable at the surface.
- **Slice 3 (Adaptation Loop) — code-complete and live-verified; all 7 PRs merged (PR1/PR3/PR2 2026-06-08, PR4 2026-06-09, PR5 + PR6 2026-06-10, PR7 2026-06-11). The gated live end-to-end validation pass ran 2026-06-11: the full onboard → plan → log → adapt loop worked against live Sonnet — registration, six-step onboarding, plan generation, three under-performing logs walking the rolling score 1.0 → 2.0 → 3.0 into a live L2 restructure rendered in the Coach panel with the before/after diff, and a fourth log confirming dead-zone suppression of a would-be second restructure. The pass surfaced six findings (Replay-mode CI structurally cannot catch them), triaged into Slice 3B (four immediate fixes), Slice 4 (onboarding slot-merge), and Captured During Cycle deferrals; the live-pass re-run after 3B is the slice's remaining close-out.** The brainstorm, adversarial verification, the 7-unit spec, and the 7-PR strategy are done at `docs/specs/17-spec-slice-3-adaptation/` (gitignored, working-tree-only). **PR1 `safety-gate`** — the deterministic `SafetyGate` keyword classifier (DEC-079 high-risk subset: crisis / emergency-referral / injury / RED-S word-boundary regexes, ReDoS-guarded, fail-closed on timeout) plus the `RecentLogSanitizer` sanitizer seam — merged 2026-06-08 (#161). **PR3 `events/projection`** — the `PlanAdaptedFromLog` / `SafetySignalRaised` adaptation events, the additive `PlanProjection` apply methods (current micro-week workout swaps + meso weekly-target edits, fail-loud on non-positive weeks), the net-new Marten-projected `ConversationLogView` read-model (DEC-060 co-transactional with `PlanProjection`, event-id-keyed idempotency, `EventVersion`-tiebroken newest-first), and the read-only `GET /api/v1/conversation/turns` endpoint — merged 2026-06-08 (#163). **PR2 `deviation-engine`** — the shared divide-by-zero-guarded `PaceDerivation` utility (with `RecentLogFormatter` refactored onto it, output unchanged), the `DeviationEngine` comparing a logged workout's actuals against its frozen `WorkoutPrescriptionSnapshot` as pace band-membership (vs the `PrescribedPace` fast/slow band) plus signed distance/duration percentages, and the `EscalationClassifier` resolving L0 absorb / L1 micro-adjust / L2 restructure from the deviation + `SafetyTier` + a threaded `AdaptationSignalState` with asymmetric enter/exit hysteresis (a restructure cannot flip-flop), over the named first-pass `AdaptationThresholds` — pure C#, no LLM, no Marten append — merged 2026-06-08 (#165). **PR4 `adaptation-output`** — the Pattern-B `PlanAdaptationOutput` structured-output contract (byte-stable frozen `AdaptationSchema`, `AdaptationKind` discriminator + nullable `NudgePatch`/`RestructurePlan` slots) with `PlanAdaptationOutputValidator` enforcing what constrained decoding can't express (exactly-one-slot-matches-kind, GATE-BEFORE-INCREASE, forward-path-on-load-cut), the versioned `adaptation.v1.yaml` Level-2 restructure prompt (R-068 nonce-spotlighted recent logs, trademark-clean), and DEC-073 going live: the `TransientCoachingLlmException`/`PermanentCoachingLlmException` hierarchy as `ICoachingLlm`'s total failure surface (SDK errors classified by status incl. non-leaf 408/409, per-attempt timeout, `max_tokens` truncation, malformed structured output), raw `Retry-After` capture through the owned HTTP pipeline, and the flat `AdaptationResponseDto` `Kind=Error` HTTP-200 envelope — merged 2026-06-09 (#167; deep-review + CodeRabbit follow-ups landed in-PR, `010cf98`). **PR5 `adaptation-orchestration`** — the `EvaluateAdaptationCommand` + Wolverine `EvaluateAdaptationHandler` (events-only — no `DbContext`/`SaveChanges`) wired as the post-create **synchronous** `InvokeForTenantAsync` trigger off the workout-log-create flow, orchestrating SafetyGate → deviation/escalation → [LLM restructure at L2] → validators → append (`PlanAdaptedFromLog` / `SafetySignalRaised`), with a `WorkoutLogId`-keyed co-transactional Marten idempotency marker (via `IIdempotencyStore`) rolled back on failure, the per-plan (`PlanId`-keyed) `AdaptationSignalStateDocument` hysteresis state rehydrated through the `AdaptationSignalState` validating factory (the PR2 carryover), the `RestructureDiffCalculator` before/after diff, `ContextAssembler.ComposeForAdaptationAsync` adaptation-prompt assembly, scripted `AmberReferralContent`/`EmergencyResponseContent`, and terminal failure ⇒ `Kind=Error` with the stream unchanged — merged 2026-06-10 (#169; **DEC-080** records two deliberate MVP-0 scope reductions surfaced by the deep review — validator-rejected proposals are terminal with zero re-prompts, and the adaptation prompt omits the runner-profile block). **PR6 `eval-suite`** — the deterministic adaptation calibration eval (`Eval/Adaptation/`, no LLM, always runs): 20 classification scenarios across all five `TestProfiles` (absorb 8 / nudge 6 / restructure 6) driving the real `DeviationEngine`→`EscalationClassifier` chain with DEC-079 asymmetric scoring (under-reaction = hard fail; over-reaction = low-not-penalized) — zero hard fails, no threshold tuning needed — plus 9 deterministic `SafetyGate` short-circuit scenarios (safety pass-rate 1.0 ≥ 0.95 gate), validator-reject coverage, the eval-side mileage-ramp constraint (≤ +10% week-over-week for novel load, recovery-ramp exemption up to the recently-held baseline) enforced against the cached restructure outputs, and the trademark guard; the **DEC-074 prompt-hash sentinel** (committed `Prompts/.prompt-hashes.sha256` manifest + `check-prompt-hashes.sh` + glob-scoped lefthook hook + the `EvalTestBase` static-ctor backstop with a testable `VerifyManifest` core, manifest regen ordered before `rerecord-eval-cache.sh`'s Record run); and the Replay-mode **LLM Level-2 restructure + Haiku communication-judge eval** (real `adaptation.v1.yaml` → cached Sonnet structured call → validator / Restructure-kind / Green-echo asserts → rationale judge, recorded for `lee` + `priya` against a funded key). Recording surfaced — and the PR fixed — a latent production bug: `AdaptationSchema.Frozen` would 400 on its first live call (`JsonSchemaExporter` emits `$ref`s into `#/properties/...` for the twice-used `WorkoutOutput[]`; Anthropic constrained decoding rejects non-`$defs` `$ref`s); `AnthropicSchemaSanitizer` now inlines every local `$ref`, a no-op for ref-free schemas so recorded cache keys stay byte-stable — merged 2026-06-10 (#172; 11 deep-review findings + 2 CodeRabbit comments addressed in-PR, `8e2269a`–`d8fc74f`). **PR7 `chat-panel`** — the read-only "Explain-the-change" conversation panel over the PR3 `GET /api/v1/conversation/turns` endpoint: the integer-enum wire-contract model (`conversation.model.ts` — `role`-discriminated `ConversationTurnDto` over adaptation vs. safety variants) guarded by a generated-Zod contract tripwire, the `conversation.api.ts` RTK Query endpoint + `Conversation` cache tag (invalidated by `createWorkoutLog` only on a fulfilled mutation so the panel refetches in the same interaction as the plan view), the `useConversationTurns` hook, and the `ConversationPanel` → `AdaptationTurn` (absorb renders nothing / nudge inline / restructure expandable with `BeforeAfterDiff`) + `SafetyTurn` (tier-based left-edge accent) components wired into `home.page.tsx` between `TodayCard` and `UpcomingList`, plus the slice's Playwright E2E for the log→adapt→panel flow and a lazy antiforgery-seed hardening for a fast first submit — merged 2026-06-11 (#174; the CodeRabbit invalidate-on-success fix + deep-review contract-spec / shared-test-helper follow-ups landed in-PR, `0cd39d9`). With PR7 the adaptation loop is code-complete; the gated live end-to-end validation pass remains. Slice 2b (Workout Logging proper) is complete and merged 2026-06-07. Slice 2 (Workout Logging) was decomposed 2026-05-18 into two sub-projects, each with its own brainstorm → spec → implementation cycle: **(2a) Frontend Visual Foundation** and **(2b) Workout Logging proper** — both now complete and merged (2a 2026-05-30; 2b 2026-06-07). Slices 0, 1, and 1B are also complete and merged.
- **Sub-project 2b (Workout Logging proper) — complete, merged 2026-06-07.** All 8 stacked PRs landed in dependency order: **PR1 `plan-anchor`** — the `PlanStartDate` calendar anchor on `PlanGenerated`/`PlanProjectionDto` + the pure `PlanCalendar` date→slot mapper (DEC-076 Unit 1) — #134, 2026-06-01; **PR6a `fe-infra`** — the date-derived `resolveCurrentWeek` (replacing the lowest-populated-week heuristic that pinned home to week 1), the app-wide `<Toaster>` mount, and `FormMessage` `role="alert"` — #136, 2026-06-01; **PR2 `workoutlog-entity`** — the `WorkoutLog` EF entity + EF-10 optional complex-type prescription snapshot + `WorkoutMetricKeys` + the repo's first value converters + migration + repository (DEC-072/DEC-076 Unit 2) — #145, 2026-06-05; **PR3 `create`** — the `POST /api/v1/workouts/logs` create endpoint (server-authoritative snapshot resolution over the active plan + EF-native idempotency, DEC-077 Unit 3) — #147, 2026-06-06; **PR4 `query`** — the DB-driven `POST /api/v1/workouts/logs/query` keyset history endpoint (newest-first, opaque keyset cursor, server-clamped page size, sort/page/limit in a single SQL query; DEC-076 § C Unit 4) — #151, 2026-06-06; **PR5 `context-assembler`** — the `ContextAssembler` extension injecting recent logged workouts' notes + metrics as compact Layer-1 one-liners (shared `WorkoutMetricKeys.Metadata` + `RecentLogFormatter`, never collapsed in the overflow cascade, eval-proofed; DEC-076 Unit 5) — #154, 2026-06-06; **PR6b `log-form`** — the `/log` route logging form + today's-card "Log" action (RHF/Zod with the schema derived from the generated create-request Zod, pessimistic create + success toast; DEC-075/DEC-076 Unit 6) — #156, 2026-06-07; **PR7 `history+e2e`** — the frontend ISO-week-grouped sparse-`<dl>` workout-history surface over `POST .../query` + the slice's Playwright E2E (Unit 7) — #158, 2026-06-07. Spec `docs/specs/16-spec-slice-2b-workout-logging/` (gitignored, working-tree-only); requirements `slice-2-logging.md`; decisions DEC-072–077. Post-merge verification is recorded in the "Slice 2b — post-merge verification" section below.
- **Sub-project 2a (Frontend Visual Foundation) — complete, merged 2026-05-30.** A `/catchup` brainstorm found the frontend has no deliberate visual design: `index.css` is bare (`@import 'tailwindcss';` only), colors are hardcoded ad hoc per component (`bg-slate-900`, `border-red-200`, …), there is no dark mode, and shadcn/ui was never actually installed despite root `CLAUDE.md` listing it in the stack (no `components.json`, no Radix deps, no `cn()` util — every component is hand-rolled Tailwind). 2a scope: palette + semantic design tokens, light/dark, Tailwind v4 `@theme` wiring, the shadcn/ui-vs-pure-Tailwind component-library decision (resolves the Captured-item dated 2026-05-06), typography/spacing scale, and migrating the existing surfaces (login, register, onboarding, settings, home) onto the system. Explicitly **out of 2a scope**: calendar component, chat-UI library, animation library — picked when the slices that need them arrive. The component-library + Tailwind-v4-theming choices route through the research-prompt cycle before the 2a spec is written. Brainstorm artifacts live in `.superpowers/brainstorm/` (gitignored). The 2a design doc — locked decisions, scope, open items — is `slice-2a-frontend-foundation.md` in this directory. The brainstorm is complete, and **R-075** (token + theming wiring) landed 2026-05-18 and is integrated as **DEC-070** — the token architecture (two-tier Catppuccin hybrid tokens on shadcn/ui + Tailwind v4) and class-based dark mode are locked. The spec is `docs/specs/15-spec-slice-2a-frontend-foundation/` (gitignored, working-tree-only). **Implemented 2026-05-19** — all 16 tasks across four branches (`slice-2a-foundation`, `slice-2a-contrast-gate`, `slice-2a-migration`, `slice-2a-home-settings`) via a cw-dispatch-team run — then **merged 2026-05-30** across PRs #111–#114 in dependency order (foundation → contrast-gate → auth/onboarding migration → home/settings + dark-mode toggle). Post-merge verification is recorded in the "Slice 2a — post-merge verification" section below. Locked: palette = the "hybrid" (Catppuccin Latte/Mocha neutral ramps + a text-contrast rule + one formula-derived teal accent); component library = shadcn/ui.
- **Slice 1B (Pre-Slice-2 Hardening) — complete, merged 2026-05-15** across PRs #91 (R-071..R-074 artifacts + DEC-066..069), #92 (`slice-1b-backend`), #93 (`slice-1b-infra`), #94 (`slice-1b-frontend`). All five acceptance criteria met (see the Slice 1B section below). Slice 1's four contract-drift bug classes are now structurally guarded: OpenAPI→TS/Zod codegen with a `git diff --exit-code` drift gate, Marten event upcasting with a synthetic-old-row regression test, a top-level React error boundary with client OTel correlation, and three DI-resolution regression tests (`IIdempotencyStore` / `IPlanGenerationService` / `RegeneratePlanHandler`).
- **Slice 1 (Onboarding → Plan) — complete, shipped 2026-04-26.** End-to-end debugging surfaced four contract-drift bugs (PascalCase wire leak, RTK tag-invalidation race, multi-select clarification dead-end, `Completed`/`Total` field rename) patched at the call site; Slice 1B closed the structural gaps so the same class cannot recur in Slice 2.
- **Active Slice Spec:** **`docs/specs/17-spec-slice-3-adaptation/`** (gitignored / working-tree-only) — written 2026-06-07: the spec (`17-spec-slice-3-adaptation.md` — 7 demoable units + FRs + proof artifacts), 7 Gherkin `.feature` files, `pr-strategy.md` (7 dependency-ordered PRs), plus `00-brainstorm-findings.md` (verified ground truth) + `01-design.md` (approved design). PR1 (deterministic SafetyGate, #161), PR3 (events/projection, #163), and PR2 (deviation engine, #165) merged 2026-06-08; PR4 (adaptation output/LLM/DEC-073, #167) merged 2026-06-09; PR5 (orchestration, #169) and PR6 (eval suite + DEC-074 sentinel, #172) merged 2026-06-10; PR7 (read-only chat panel, #174) merged 2026-06-11 — the slice is code-complete. The spec was produced via a research fan-out (11 sources) → synthesis brief → adversarial verification (4 agents against the codebase + decision log) → user brainstorm (3 decisions locked) → house-format spec + PR strategy. Sub-project 2b's spec (`docs/specs/16-spec-slice-2b-workout-logging/`) and 2a's (`docs/specs/15-spec-slice-2a-frontend-foundation/`) are complete and merged; 2b's requirements remain at `docs/plans/mvp-0-cycle/slice-2-logging.md`.
- **Verification status (Slice 1B):** landed via the standard PR pipeline against the `main-protection` ruleset — PR #92 carried CodeRabbit + SonarCloud + deep-review rounds. `main` is clean.
- **Decisions locked during Slice 1:** DEC-057 (single-handler/single-session/single-transaction per R-066), DEC-058 (Pattern B byte-stable schema per R-067), DEC-059 (layered containment-first sanitizer per R-068), DEC-060 (handler bodies emit events; projections own EF state per R-069), DEC-062 (`opts.Add(...)` registration shape for EF projections per R-070), DEC-063 (Tailwind-only animation baseline; defer `motion/react`), DEC-064 (xunit collection-parallelism disabled — non-parallel is canonical until per-collection DB isolation lands; supersedes DEC-061).
- **Decisions locked during Slice 1B:** DEC-066 (OpenAPI codegen: `@rtk-query/codegen-openapi` + Orval v8 Zod, committed `backend/openapi/swagger.json`, `git diff --exit-code` drift gate, per R-071), DEC-067 (Marten upcasting: versioned CLR event types + `Events.Upcast<TOld, TNew>` + `MapEventTypeWithSchemaVersion<T>(N)`, per R-072), DEC-068 (`react-error-boundary@6.1.1` + declarative `<BrowserRouter>` + POST `/api/v1/client-errors` + dev-only `<ThrowOnQuery />` Playwright pattern, per R-073), DEC-069 (`@opentelemetry/sdk-trace-web` 2.x + `instrumentation-fetch` 0.20x + OTLP/HTTP exporter + `cors:` block on collector receiver + `useSyncExternalStore` trace-id seam, per R-074).
- **Decisions locked pre-Slice-2b (Batch 28 research, integrated 2026-05-31):** DEC-072 (`WorkoutLog.Metrics` = open `jsonb` string bag + single-sourced `WorkoutMetricKeys` canonical keys/units + first EF `ValueConverter`; no index now, per R-077 + R-080-P2), DEC-073 (synchronous in-handler LLM-failure policy: Anthropic-SDK-only retry, `Transient`/`Permanent` `ICoachingLlm` exceptions, `Kind=Error` HTTP-200 envelope, co-transactional idempotency — decided now, first live in Slice 3, per R-078), DEC-074 (eval-cache↔prompt SHA-256 sentinel manifest + lefthook/EvalTestBase detection, per R-080-P1), DEC-075 (logging-form conventions: RHF + Zod-v4 empty-numeric→`undefined` coercion, derive schema from generated Zod, `<dl>` sparse-metric history, splits display-only, pessimistic create, per R-079). Load-bearing claims independently re-verified before lock (workflow `verify-slice2b-research-claims`: 33 claims / 1 refuted / 5 partial; corrections folded into the DECs). **All three Slice 1 carry-forwards are now resolved** (DEC-073 / DEC-072 / DEC-074). **Mid-implementation, PR3 (#147) ratified DEC-077** (EF-native idempotency for the `WorkoutLog` create path — an `IdempotencyKey` column + unique `(UserId, IdempotencyKey)` index committed in the same `SaveChanges`, with a 23505-guarded re-read of the prior id; the Marten-document `IIdempotencyStore` stays for the LLM/handler endpoints): the spec's carried-over "co-transactional Marten marker" phrasing was impossible for a pure-EF immutable fact with no Marten stream or handler (R-081 / `batch-29a`).
- **Decisions locked for Slice 3 (2026-06-07 brainstorm + verification):** **DEC-078** (adaptation triggering = deterministic DEC-012 gate + simplified MVP-0 signal; L0 absorb / L1 nudge handled in code with no LLM, L2 restructure = first LLM call; per-log band-deviation + hysteresis; full EWMA/ACWR/CTL/ATL/TSB load model deferred — **resolves the pragmatic-default-vs-DEC-012 conflict in favor of DEC-012**), **DEC-079** (high-risk safety subset — crisis 988/741741 short-circuit + emergency-referral + Amber injury/RED-S, full DEC-030 taxonomy deferred to pre-public-release; net-new Marten-projected `ConversationTurn` read-model for atomicity; Pattern-B `PlanAdaptationOutput`; `WorkoutLogId`-keyed adaptation idempotency; read-only Explain-the-change chat panel; blocking, no streaming). Inherits DEC-012 / DEC-058 / DEC-060 / DEC-073 / DEC-019 / DEC-030 / DEC-027 verbatim. **Verification (4 agents vs codebase + decision log) corrected three load-bearing brief claims now baked into the spec:** DEC-073's exception types + `Kind=Error` envelope are net-new (own PR4), the prescription snapshot is a double-precision pace *band* (not int scalar), and `MicroWorkoutsByWeek` holds only week 1 (adaptation edits the current micro week + meso weekly targets, not future micro detail). User audience steer: MVP-0 is self + family for the foreseeable future — prioritize a working loop + clean UX over safety breadth. **DEC-080** (2026-06-10, ratified mid-slice in PR5 #169 from the deep review) records two deliberate MVP-0 scope reductions of the shipped `EvaluateAdaptationHandler` / `ComposeForAdaptationAsync` from the local spec: a validator-rejected (or non-restructure / tier-mismatch) LLM proposal is terminal with zero re-prompts — DEC-073 governs only transport-level SDK retries, not a validator-reject re-prompt — and the adaptation prompt omits the runner-profile block (the `adaptation.v1.yaml` template defines no profile token); both deferred to Unit 6 eval calibration. **DEC-081** (2026-06-11, Slice 3B F1 #185) records the one deliberate exception to DEC-080's nothing-staged failure contract: the scripted Amber referral stages before the escalation branch and commits with the `Kind=Error` envelope on a terminal L2 failure (a safety turn is never dropped because the restructure failed), with a per-log committed-stream dedupe (`ReferralAlreadyRaisedAsync`) keeping a marker-released retry — or a concurrent racer's bounded retry — from double-appending.
- **Next Step:** **Continue Slice 3B (Live-Pass Fixes), then re-run the live end-to-end validation pass; Slice 4 (Open Conversation) follows.** **F1 (Amber referral on every escalation path) is shipped — #185, 2026-06-11 (DEC-081).** Remaining work order: **F2 (trademark scrub on persisted LLM prose) next** as the remaining hard-rule item, then F3 (date-aware plan horizon + validator) and F4 (restructure sum-consistency validator). Each lands through the standard PR pipeline; the slice closes only on the live-pass re-run per `slice-3b-live-pass-fixes.md` § Verification protocol. Slice 4 spec-writing picks up the onboarding Schedule slot-merge finding alongside its existing scope.
- **Blockers:** None.

This status block is the single source of truth for "where are we?" — mirrored into `ROADMAP.md` so `/catchup` finds it. Update both whenever a slice completes or the active slice changes.

### Slice 2a — post-merge verification

Slice 2a merged 2026-05-30 across PRs #111–#114 (foundation → contrast-gate → auth/onboarding migration → home/settings + dark-mode toggle), in dependency order. Close-out status — **all verified**:

- [x] Four `slice-2a-*` branches pushed; four stacked PRs opened, reviewed, and merged in dependency order. The R-075 integrate commit `fbe80c4` rode in via the foundation PR (#111).
- [x] **`check-contrast` passes on the fully-integrated `main`** — all 28 semantic foreground/background pairs clear their WCAG thresholds (`npm run check-contrast`, 2026-05-30). The gate runs in pre-commit + CI.
- [x] Frontend type-check + unit suite green on integrated `main` — `tsc -b` clean, **367/367 Vitest tests** pass across 35 files.
- [x] Worktrees torn down and the four merged local `slice-2a-*` branches deleted; no stray Vite dev server (port 5234 clear) or leftover `git` processes.
- [x] Slice 2a marked complete in this Status block and `ROADMAP.md`, with a Cycle History row added.
- [x] **Playwright E2E (`npm run e2e`) — 12/12 specs pass** against the host-run stack (Postgres + backend API on `https://localhost:5001` + Vite, 2026-05-30): error-boundary, theme, auth, onboarding, plan-render, regenerate-plan. Onboarding/plan responses are stubbed via Playwright `route` interception (real backend only for `register`), so the run makes no Anthropic calls. The colour-only migration preserved every `data-testid` selector and flow.

### Slice 2b — post-merge verification

Slice 2b merged 2026-06-07 across 8 stacked PRs (#134, #136, #145, #147, #151, #154, #156, #158) in dependency order. Close-out status — **all verified**:

- [x] All 8 stacked PRs opened, reviewed (CodeRabbit + SonarCloud + local deep-review), and squash-merged against the `main-protection` ruleset in dependency order.
- [x] Backend build + test green on integrated `main` — the full xUnit / MTP suite, including the new `WorkoutLog` entity + repository, the create + query endpoint integration tests, and the `ContextAssembler` recent-log eval.
- [x] Frontend type-check + unit suite green on integrated `main` — `npm run build` (tsc + vite) clean, **458 Vitest tests** pass; `npm run lint` 0 errors; `npm run check-contrast` 28/28 WCAG pairs.
- [x] **Playwright E2E** — the workout log→history journey (register → log a minimum + a rich workout → both appear in the week-grouped history with the rich note + Avg HR) passes against the host-run stack; create + history-query hit the real backend (onboarding/plan stubbed at the wire, per the 2a pattern).
- [x] Slice 2b marked complete in this Status block and `ROADMAP.md`, with a Cycle History row added.
- [x] PR7 (#158) deep-review follow-ups — week-sort comparator branch coverage, the infiniteQuery pagination-contract test, the zero-metric render test, and the HR-cell nullish-coalescing cleanup — addressed in-PR; no carry-forward.

---

## Captured During Cycle

Running log of "we should also do this" items found during the cycle but intentionally deferred — preserves the affordance the old `ROADMAP.md` Deferred Items section had, scoped to the active cycle so the list doesn't grow unboundedly.

**How to use**

- Any agent (or human) may append an entry when finding work that shouldn't block the current slice but shouldn't be lost.
- Each slice's PR description includes a `### Follow-ups found` section (empty is fine); items move into this table at slice completion.
- At cycle completion, every entry gets one of four dispositions:
  - (a) promoted to `docs/features/backlog.md`,
  - (b) becomes its own `docs/decisions/decision-log.md` entry,
  - (c) becomes a research prompt (see [When Agents Encounter Unknowns](#when-agents-encounter-unknowns)),
  - (d) scheduled into the next cycle.
- The table does not survive cycle completion un-triaged.

| Found | In slice | Item | Triage disposition |
|---|---|---|---|
| 2026-04-19 | (cycle-plan) | Frontend/backend breakdown pass on cycle-plan organization — sections currently mix layers, may read cleaner with explicit F/E vs B/E separation | Deferred; re-evaluate after Slice 1 when the shape of per-slice plans clarifies whether a layer-split would help. |
| 2026-04-19 | Slice 0 (Batch 15 audit) | Plan-projection → LLM prompt shape undefined — R-047 covered Marten's HTTP-streaming path, not the in-process `ContextAssembler` path. | Partially resolved by R-048 / DEC-047; finalize the Plan-side projection→prompt at Slice 1 spec-writing time. Open. |
| 2026-04-19 | Slice 0 (Batch 15 audit) | Production deployment topology undecided (VPS / PaaS / K8s / managed Postgres / CDN). | Partially resolved by R-049 / DEC-046 (secrets bootstrap is per-target); target choice itself still open — pre-MVP-1 research prompt. |
| 2026-04-19 | Slice 0 (Batch 15 audit) | Production observability (OTel collector + dashboard) — no infra decided. | Resolved for Slice 0 by R-050 / DEC-045 (`docker-compose.otel.yml` overlay). Production observability remains a pre-MVP-1 prompt. |
| 2026-04-19 | Slice 0 (Batch 15 audit) | Database backup / restore / data lifecycle — Plan adaptation history is irreplaceable once real users exist. | Pre-MVP-1 research prompt; coordinate with the production-deployment-topology decision. |
| 2026-04-19 | Slice 0 (Batch 16 integration) | FTC HBNR pre-public-release escalation — applies once any Apple Health / Strava / Garmin ingest exists; triggers the Azure Key Vault + Managed Identity migration. | Captured in DEC-046 cross-reference. Promote to a pre-public-release task list at MVP-1 cycle start. |
| 2026-04-19 | Slice 0 (Batch 17 audit) | Batch 17 research queued (R-051 LLM observability, R-052 Anthropic SDK choice, R-053 multi-turn eval pattern) — targets Slice 1 LLM call sites. | **Resolved 2026-04-24** — all three artifacts landed as `batch-17{a,b,c}` under `docs/research/artifacts/`. Verdicts: Phoenix self-hosted (R-051), first-party `Anthropic` NuGet 12.17.0 via `AsIChatClient` retiring DEC-037 bridge (R-052), thin M.E.AI.Evaluation multi-turn extension with ~30-scenario MVP-0 budget (R-053). DEC entries land with the Slice 1 spec session. |
| 2026-04-20 | Slice 0 (T01.5 → R-054) | `WebApplicationFactory<Program>` SUT boot hang — canonical Marten/Wolverine composition recipe needed. | Resolved as DEC-048 (Marten's `IntegrateWithWolverine` subsumes Wolverine's `PersistMessagesWithPostgresql`; never call both). |
| 2026-04-20 | Slice 0 (DEC-048 verification → R-055) | After DEC-048, `WebApplication.CreateBuilder` itself still hung — deeper than R-054 diagnosed. | Resolved as DEC-049 — synchronous `FileSystemWatcher` init on macOS arm64 (three default-reloading JSON config sources). Fix: `DOTNET_hostBuilder__reloadConfigOnChange=false` at process start + remove manual `MapWolverineEnvelopeStorage` from `OnModelCreating`. |
| 2026-04-21 | Slice 0 (PR #49 review) | Combined PR #49 review response: (a) removed plaintext dev Postgres password from `appsettings.Development.json` → `dotnet user-secrets`; (b) save/restore-bracketed env-var override in `RunCoachAppFactory` (process state restored on dispose); (c) Jaeger healthcheck + `service_healthy` for OTel collector; (d) `DevelopmentMigrationService` regression test + AAA markers + `StartupSmokeIntegrationTests` rename + dropped opt-in `SmokeTests.cs`; spec lines 47/61/170 rewritten to describe runtime envelope provisioning per DEC-049. | Applied. Residual: Prod-env fixture that proves `DevelopmentMigrationService` is NOT registered (deferred to post-Unit-2). |
| 2026-04-21 | Slice 0 (PR #49 review) | Duplicate TRX-upload-on-failure step between `ci.yml` and `sonarqube.yml` (~10 lines). | Deferred — low-drift-risk inline; revisit if a third workflow uploads the same artifact. |
| 2026-04-21 | Slice 0 (R-056 / R-057 integration) | `dotnet dev-certs https --trust` as a hard contributor prerequisite — `__Host-` + `Secure` is broken on `http://localhost` in Chrome / Safari. | Landed in CONTRIBUTING.md as part of Unit 3; `mkcert` documented as escape hatch. |
| 2026-04-21 | Slice 0 (R-056 integration) | Forwarded Headers middleware (`UseForwardedHeaders` + `KnownProxies`) canonical fix for `ERR_TOO_MANY_REDIRECTS` behind reverse proxies. | Deferred to MVP-1 deployment-target decision — pipeline-ordering change, not drop-in. |
| 2026-04-21 | Slice 0 (R-057 integration) | Test-host `UseEnvironment("Testing")` migration for tighter `ValidateOnStart` gating. | Deferred — bundle with the Prod-env fixture when it lands. |
| 2026-04-21 | Slice 0 (R-057 integration) | `PostConfigure<JwtBearerOptions>` deterministic test-key pattern for iOS-path tests. | Deferred to iOS-shim workstream (post-MVP-0 per DEC-033); pattern captured in R-057 artifact. |
| 2026-04-21 | Slice 0 (R-058 / DEC-052 follow-ups) | Frontend (T03.x) Zod schemas must mirror `RegisterRequest` DataAnnotations; contract-test in `shared-contracts`. | Deferred as a T03 follow-up — Unit 3 shipped the schemas; contract-test still open. **Resolved 2026-05-15 by Slice 1B (DEC-066, PR #94)** — `RegisterRequest` was migrated to a generated Zod schema; the OpenAPI codegen `git diff --exit-code` drift gate now serves as the contract-test. |
| 2026-04-21 | Slice 0 (R-058 / DEC-052 follow-ups) | 409 duplicate-email posture is a deliberate enumeration-resistance gap. Pre-public-release should migrate to "202 Accepted + email-to-existing-account." | Deferred to pre-public-release. |
| 2026-04-21 | Slice 0 (R-059 / DEC-053 follow-ups) | MVP-1 lockout re-opens the timing leak on a secondary axis; preferred mitigation is uniform-delay envelope (OWASP-aligned). | Deferred to MVP-1 lockout workstream; options captured in DEC-053 "Known limitations." |
| 2026-04-21 | Slice 0 (R-059 / DEC-053 follow-ups) | Password-reset endpoint must match login's timing-safety posture (identical 200 regardless of email existence; fire-and-forget email). | Deferred to password-reset workstream (post-MVP-0). |
| 2026-04-22 | Slice 0 (T02.6 → R-063 / R-064) | Containerized `dotnet restore` SIGILLs on Apple Silicon M3/M4/M5 under VZ (CoreCLR SVE2 JIT codegen, `dotnet/runtime#122608`, milestone .NET 11). | Resolved as DEC-056 under two research passes. Path B (host-run) is the sole Apple-Silicon dev loop until .NET 11; Path A CI-only on x86_64. 13 re-check triggers captured. |
| 2026-04-22 | Slice 0 (DEC-056 follow-ups) | `packages.lock.json` + `--locked-mode` defense-in-depth; `verify-container-restore.sh` smoke + Apple-Silicon CI matrix; Dependabot ignore on SDK tag. | Deferred. (a) is a workflow change; (b) needs M3+ GitHub runners (not yet GA); (c) blocks security updates — Dockerfile comment + digest pins act as the manual-review gate. |
| 2026-04-24 | Slice 1 (pre-spec gap audit) | Frontend chat-UI pattern gap surfaced by re-reviewing Slice 1 against Batch 17 — Batch 17 covered backend/LLM only; `batch-10a-frontend-latest-practices.md` never prescribed a guided multi-turn chat pattern or library choice. | **Resolved 2026-04-25** — R-065 landed as `batch-21a-onboarding-chat-ux-react19.md`. Verdict: build on shadcn/ui primitives + `motion/react` + RTK Query mutation; reject `assistant-ui` and CopilotKit for Slice 1 (defer `assistant-ui` to Slice 4). Discriminated-union Zod schema + component-map keyed on `suggestedInputType`; segmented checklist progress; `role="log"` + polite live region; pessimistic UI with idempotency key; share message-bubble + transcript-scroller primitives with Slice 4, dispose the turn-engine. DEC entry lands with the Slice 1 spec session. |
| 2026-04-25 | Slice 1 (spec-writing) | Wolverine `[AggregateHandler]` transaction scope across synchronous `IMessageBus.InvokeAsync<T>` — original spec assumption (one transaction wraps the outer event append + the synchronous downstream pipeline call) needed verification. | **Resolved 2026-04-25 as DEC-057** — R-066 landed as `batch-22a-wolverine-aggregate-handler-transaction-scope.md`. Spec assumption was WRONG: `InvokeAsync` from `[AggregateHandler]` opens its own session via `OutboxedSessionFactory.OpenSession(messageContext)` and commits independently. Canonical 2026 pattern: single-handler / single-session / single-transaction. Plan generation in Slice 1 ships as plain `IPlanGenerationService` (NOT a Wolverine command/handler) returning events; caller stages them on its own session. `OnboardingCompleted` appended LAST. R-066 regression test (`InvokeAsyncTransactionScopeTests`) is the empirical guard. Slice 4 async-flip is a real shape change (handler split + cascading message + frontend polling), NOT a one-line call-site flip. Concurrency policy: `MoveToErrorQueue` on `ConcurrencyException` so concurrent submissions surface as 409 rather than re-running 6 LLM calls. |
| 2026-04-25 | Slice 1 (spec-writing) | Anthropic structured-output schema design for topic-discriminated multi-turn responses — `OnboardingTurnOutput` shape needed locking; `oneOf`/`anyOf` support unverified for 2026; cache-stability of `output_config.format.schema` mutation unknown. | **Resolved 2026-04-25 as DEC-058** — R-067 landed as `batch-22b-anthropic-discriminated-structured-output.md`. Anthropic constrained decoding (GA early 2026) rejects `oneOf`, `allOf`, `if/then/else`, `not`, `prefixItems`, `min*/max*`, `pattern`, `format`, `uniqueItems`, `minItems/maxItems`, `minProperties/maxProperties` with HTTP 400. `anyOf` works but counts against a 16-union budget. Anthropic explicit: *"Changing the `output_config.format` parameter will invalidate any prompt cache for that conversation thread."* → Pattern D disqualified. **Pattern B locked**: single byte-stable schema with six nullable typed `Normalized*` slots + `Topic` discriminator. Both grammar cache (24h) + prompt-prefix cache (1h) hit from turn 2. Backend `OnboardingTurnOutputValidator` enforces "exactly one non-null slot matches Topic" because grammar can't. `AnthropicSchemaSanitizer` strips forbidden keywords defensively. Pattern recurs across Slices 3, 4, and future workout-log auto-extraction — same encoding inherits verbatim. |
| 2026-04-25 | Slice 1 (spec-writing) | Prompt-injection mitigation patterns for LLM-coaching free-text inputs — `IPromptSanitizer` was specified as "best-effort defense in depth" without technique, library, or MVP-0 scope; .NET ecosystem sparse on injection-mitigation libraries. | **Resolved 2026-04-25 as DEC-059** — R-068 landed as `batch-22c-prompt-injection-mitigation-dotnet.md`. `Microsoft.Extensions.AI` does NOT ship `IPromptSanitizer`; no mature .NET-native NuGet exists. Hand-roll ~300 LOC. **Layered, containment-first sanitizer**: Unicode normalize (always neutralize U+E0000–U+E007F + zero-width) + 12-pattern regex catalog (PI-01–PI-12, log-only at MVP-0 except DAN-family on `CurrentUserMessage`) + Spotlighting `<SECTION_NAME id="{nonce}">…</SECTION_NAME>` containment delimiters paired with a `data_handling` directive at end of system prompt blocks. Wired inside `ContextAssembler` per-section (NOT uniform middleware). Thin outer `SanitizationAuditChatClient` for OTel rollup with `openinference.span.kind = "GUARDRAIL"`. 25-case xUnit corpus from Lakera Gandalf + ProtectAI Rebuff + OWASP + Cisco Unicode-tag research. MVP-1 hardening triggers (R-068-T1 telemetry, T2 single-incident, T3 second-human, T4 FTC HBNR) documented; ProtectAI deberta-v3 ONNX classifier is the upgrade tier. New Unit 6 + Task #114/#115 added to the slice plan. |
| 2026-04-25 | Slice 1 (DEC-057 follow-up) | Slice 4 async-plan-gen UX migration — original spec sized as "one-line `InvokeAsync` → `PublishAsync` flip" turns out to be a real shape change per R-066. | **Deferred to during/after Slice 4 (revised scope).** Handler split: `OnboardingTurnHandler` returns `OnboardingCompleted(planId: null)` immediately + a cascading `GeneratePlanCommand` in `OutgoingMessages`; new `GeneratePlanHandler` runs the inline single-handler pattern with retry-via-Wolverine error policies. Frontend: optimistic-redirect skeleton + RTK Query cache-tag invalidation + "plan being built" home-page state with polling/SignalR notification. Estimate ~½ day backend + frontend skeleton. Trigger sooner if p99 plan-gen latency exceeds 60s on Sonnet 4.6 or if Slice 4's interactive chat starts competing for the request thread. |
| 2026-04-25 | Slice 1 (DEC-059 follow-up) | Prompt-injection sanitizer hardening triggers — MVP-0 ships log-only regex tier + Unicode normalize + delimiters; harder mitigations deferred. | **Deferred** with four explicit triggers (R-068-T1 through T4). T1 telemetry (>1% findings/turns over 7-day window) → ProtectAI deberta-v3 ONNX classifier. T2 single-incident (system-prompt leak / cross-user leak / `VDOT`-in-reply) → same-day ONNX integration. T3 second-human → promote PI-04/05/06 to neutralize-mode. T4 FTC HBNR escalation → Microsoft Presidio for PII redaction. All triggers logged via `runcoach.sanitization.policy_version` for audit replay. |
| 2026-04-26 | Slice 1 (T05.3 / T122 review carry-forward) | No integration test asserts the `POST /api/v1/plan/regenerate` HTTP 400 path when `intent.freeText` exceeds `RegenerationIntent.RawMaxFreeTextLength = 500`. Existing unit tests in `ContextAssemblerPlanGenerationTests` exercise only the constructor cap (`MaxFreeTextLength` boundary). Worker-2 in #122 caught the gap during cap-split verification but flagged as non-blocking. | **Deferred to Slice 2 or Slice 3** — small integration test addition (~30 LOC against the `WebApplicationFactory<Program>` fixture, posts a 501-char freeText, asserts 400 ProblemDetails with the cap reference). Picked up incidentally next slice or via a dedicated task before MVP-0 close. |
| 2026-04-26 | Slice 1 (T117 / DEC-064 carry-forward) | xunit collection-parallelism is currently disabled assembly-wide (DEC-064); re-enabling requires per-collection database isolation in `RunCoachAppFactory`. | **Deferred** with explicit triggers (per DEC-064): suite duration > 30s OR additional integration test classes pushing wall-time. Implementation path: split the assembly fixture into per-collection fixtures with isolated Postgres schemas, document the trade-off vs SUT boot cost. |
| 2026-04-26 | Slice 1 (T01.4 carry-forward, integration-test boot regression) | `MartenConfiguration.cs` registered `UserProfileFromOnboardingProjection` via `opts.Projections.Add(...)` instead of the `Marten.EntityFrameworkCore` extension `opts.Add(...)`. Symptom: every `WebApplicationFactory<Program>`-booted integration test failed at SUT startup with `Marten.Exceptions.InvalidDocumentException: Could not determine an 'id/Id' field or property for requested document type RunCoach.Api.Modules.Identity.Entities.UserProfile`. ~45 pre-existing integration tests + the deferred T01.6 / new T02.4 suites all blocked. | **Resolved 2026-04-26 as DEC-062.** R-070 landed as `batch-23b-marten-ef-projection-registration-regression.md`. Verified: `Marten.EntityFrameworkCore` 8.32.1 ships `opts.Add(IProjection, ProjectionLifecycle)` as the documented and only correct registration site for `EfCoreSingleStreamProjection<,,>`; the 3-type-param API `<TDoc, TId, TDbContext>` is stable across 8.23 → 8.32.1 (no breaking changes in this window). Companion requirement: conjoined-tenancy event store mandates EF projection target implement `Marten.Metadata.ITenanted`; Marten throws `InvalidProjectionException` at startup if missing. **Architectural rule (DEC-062):** EF projections register via `opts.Add(...)`; Marten document projections continue via `opts.Projections.Add(...)`. Reusable across Slice 3 (`UserProfileFromActivityProjection`) and Slice 4 (`ConversationTurnRecorded`). #116 holds the implementation plan: 3-type-param signature stays, registration call switches, `UserProfile` gains `ITenanted`, new EF migration `AddUserProfileTenantId`, new regression-guard `MartenStoreOptionsCompositionTests` (asserts `Storage.AllDocumentMappings.ShouldNotContain(typeof(UserProfile))` against a bare `DocumentStore.For(opts => ...)` — no SUT boot needed). |
| 2026-04-25 | Slice 1 (R-066 follow-through, spec-writing) | Marten + EF Core dual-write atomicity inside a Wolverine `[AggregateHandler]` body — R-066 §3 explicitly flagged the EF-side direct write (`UserProfile.CurrentPlanId = planId`) as committing in a separate Postgres transaction from the Marten events. Spec assumption of single-transaction atomicity needed verification. | **Resolved 2026-04-25 as DEC-060.** R-069 landed as `batch-23a-marten-ef-dual-write-atomicity.md`. Verdict: **TWO Postgres transactions on TWO `NpgsqlConnection` objects** — Wolverine 5.x composes `MartenEnvelopeTransaction` + `EfCoreEnvelopeTransaction` independently; no shared connection, no 2PC. Wolverine docs explicit: *"does not support any kind of 2 phase commits."* **Option 1 locked:** add `PlanLinkedToUser(UserId, PlanId)` event to the onboarding stream; extend `UserProfileFromOnboardingProjection : EfCoreSingleStreamProjection<UserProfile, Guid, RunCoachDbContext>` (Marten 8.23+ 3-type-param API) with apply branch that sets `CurrentPlanId`; remove `RunCoachDbContext` from `OnboardingTurnHandler` + `RegeneratePlanHandler` signatures. Marten.EntityFrameworkCore runs the projection as a transaction participant on the same `NpgsqlConnection` as Marten's session — atomic by construction. **Architectural rule (DEC-060):** handler bodies emit events; projections own EF state. `IIdempotencyStore` repositioned as Marten document (`IdempotencyMarker`), NOT EF table. Pattern generalizes to Slice 3 (`PlanAdaptationRecorded`) and Slice 4 (`ConversationTurnRecorded`). New regression test (`DualWriteAtomicityTests` per R-069 §11) uses `pg_stat_activity.backend_xid` snapshots to assert exactly ONE Postgres transaction during the handler run. Tasks #89/#90/#91/#92/#94/#110/#111 reshaped accordingly. |
| 2026-05-01 | Slice 1 Unit 5 (PR #77 deep-review type-5) | `ContextAssembler` exposes two public ctors producing semantically different objects — the 3-arg legacy ctor leaves `_sanitizer` / `_onboardingSystemPromptCache` null and `ComposeForOnboardingAsync` throws on those instances. Commit `27e241b` added an explicit DI factory because the default container's "most-resolvable parameters" heuristic silently picked the 3-arg form in production, breaking onboarding turns. The DI factory is a workaround, not a fix — the regression returns the moment a second registration path is added. | **Deferred to Slice 1 Unit 6 (onboarding turn handler).** When the production turn handler lands, mark the 3-arg ctor `internal` (test project has `InternalsVisibleTo`) so production cannot construct the half-functional form. Tests that intentionally exercise the throw-on-onboarding contract continue to work. Alternative: split into two distinct types (`PlanContextAssembler` / `OnboardingContextAssembler`) — cleaner but a larger refactor. |
| 2026-05-01 | Slice 1 Unit 5 (PR #77 deep-review type-3) | `OnboardingPromptComposition` has independent `Neutralized:bool` and `Findings:ImmutableArray<SanitizationFinding>` fields — they can desync at construction. Today exactly one producer (`ContextAssembler.ComposeForOnboardingAsync`) sources both from the same sanitizer result, so the risk is purely hypothetical. | **Deferred to Slice 1 Unit 6.** When the onboarding turn handler (or a plan-regeneration handler) becomes a second producer, decide: (a) if both producers genuinely need to set `Neutralized` for distinct semantic reasons (e.g. a "catastrophic fallback" path that doesn't emit per-finding entries), keep as-is and document the distinct semantics; (b) otherwise convert `Neutralized` to a computed property `=> Findings.Any(f => f.WasNeutralized)` and remove the ctor parameter. |
| 2026-05-01 | Slice 1 Unit 5 (PR #77 deep-review type-4) | `OnboardingTurnOutputValidationResult` is a positional readonly record struct — its primary ctor lets callers construct contradictory triples (e.g. `IsValid:true, Violation:NoNormalizedSlot, NonNullSlotCount:0`). Sole producer today is the validator itself, which always emits consistent triples. | **Deferred to Slice 1 Unit 6.** When the validator is wired into the turn handler, add internal static factory methods `Valid()` and `Invalid(violation, count)` that lock the construction shape; consider making the primary ctor `internal` so callers go through factories. Caveat: `default(T)` for a struct still produces `(false, None, 0)` which is also "invalid" — structural invariants on a struct are partly unenforceable. Worth doing anyway because the factory pattern signals intent. |
| 2026-05-01 | Slice 1 Unit 5 (PR #77 deep-review type-6) | `OnboardingTurnOutput` cannot express its Pattern-B-Invariant ("exactly one `Normalized*` slot is non-null AND it matches `Topic`") in the type system; it relies on a separate post-deserialization `OnboardingTurnOutputValidator`. | **Permanent design — no fix scheduled.** Documented in DEC-058 and the type/validator XML doc comments. Anthropic constrained decoding rejects `oneOf`/discriminated-union schemas with HTTP 400, so a sealed-hierarchy `ExtractedAnswer` wire shape can't be sent upstream. The current pattern is the right answer given the constraint. Apply the same Pattern-B + post-deserialization validator pattern to future structured-output types (Slice 2 logging, Slice 3 adaptation, Slice 4 conversation). Revisit only if Anthropic ever supports `oneOf` in constrained decoding. |
| 2026-05-06 | Slice 1 Unit 3 (PR #68 CodeRabbit review) | shadcn/ui primitive migration for the onboarding chat input components — CodeRabbit flagged `date-turn-input` (representative of all Slice 1 input components) for using native `<input>`/`<button>` instead of shadcn primitives, against `frontend/CLAUDE.md` § Styling. R-065 prescribed shadcn for Slice 1, but the project has zero shadcn footprint today: no `frontend/components.json`, no `src/components/ui/`, no `@radix-ui/*` deps, and login/register forms still ship plain Tailwind + native controls. | **Deferred.** Bootstrap shadcn (`components.json`, `cn` util, `Button`, `Input`, `Form`, `Label`, `Calendar`/`Popover` for date) as its own slice/unit so the rollout converts every existing form (login, register, all five `*-turn-input` components) in one pass with consistent dependency upgrades and accessibility audits, rather than per-PR drift. Trigger: next slice that introduces a new form OR Slice 1 close-out cleanup. **Folded into sub-project 2a (2026-05-18)** — the shadcn/ui-vs-pure-Tailwind component-library decision is now an explicit 2a (Frontend Visual Foundation) brainstorm question. |
| 2026-05-11 | Slice 1B (pre-spec research) | R-071 (OpenAPI → TS + Zod codegen) and R-072 (Marten event upcasting) artifacts landed at `docs/research/artifacts/batch-24a-openapi-typescript-zod-codegen.md` / `batch-24b-marten-event-upcasting-strategy.md`. | **Resolved 2026-05-11 as DEC-066 + DEC-067.** R-071 verdict: two-tool pipeline (`@rtk-query/codegen-openapi` + Orval v8 in `client: 'zod'` mode) reading a committed `backend/openapi/swagger.json` (Swashbuckle 10, OpenAPI 3.0 + `nullable: true`); `git diff --exit-code` is the drift gate, advisory `oasdiff` for breaking-change reporting. `OnboardingTurnResponseDto` codegen migration gated behind Slice 4 `JsonDocument` antipattern fix. R-072 verdict: versioned CLR event types + `Events.Upcast<TOld, TNew>(Func<TOld, TNew>)` on `StoreOptions.Events`, with `MapEventTypeWithSchemaVersion<T>(N)` from N=1; single registration intercepts both `SingleStreamProjection.Evolve` and `EfCoreSingleStreamProjection.ApplyEvent` (orthogonal to DEC-062). Both DEC entries record reconsider triggers and forward-compat through Marten 9. Slice 1B unblocked; spec-writing is the next step. |
| 2026-05-11 | Slice 1B (R-071 follow-up) | Backend Swashbuckle config gap surfaced by R-071: without `options.SupportNonNullableReferenceTypes()` + a `RequireNonNullablePropertiesSchemaFilter`, C# non-nullable reference types emit as `nullable: true` and generated Zod becomes `.nullish()` everywhere — drift gate goes silent. | **Promoted into Slice 1B spec scope.** Spec session must add both to Swashbuckle config alongside the codegen wiring; without this the gate that catches Slice 1 bug #4 does not work. **Resolved 2026-05-15 (PR #92)** — `SupportNonNullableReferenceTypes()` + `RequireNonNullablePropertiesSchemaFilter` landed in the Swashbuckle config. |
| 2026-05-11 | Slice 1B (R-072 follow-up) | Marten `mt_doc_dead_letter_event` table name has surfaced in two forms in the official docs (`mt_doc_deadletterevent` and `mt_doc_dead_letter_event`); upcaster failure dashboard query needs the actual schema-name verified before merge. | **Deferred to Slice 1B spec session.** Verify against the locally-running schema (`\dt` in psql) before authoring the dashboard SQL or any test fixture that references the dead-letter table. Three-minute check. **Resolved 2026-05-15 (PR #92)** — Slice 1B's Marten upcasting infrastructure shipped; the dead-letter table name is referenced in `Program.cs` and was confirmed against the live schema during implementation. |
| 2026-05-11 | Slice 1B (R-072 follow-up) | `Marten.Services.Json.Transformations.SystemTextJson.JsonTransformations.Upcast(...)` overload list was inferred from R-072's source citations but not fully enumerated from the Marten 8.32 source. | **Deferred to Slice 1B spec session.** Before authoring the first production upcaster class, open `src/Marten/Services/Json/Transformations/SystemTextJson/JsonTransformations.cs` directly in the Marten 8.32 source to confirm the exact signature surface. R-072 §13 logs this as an open follow-up. **Resolved 2026-05-15 (PR #92)** — Slice 1B shipped `Events.Upcast<TOld, TNew>` wiring in `Program.cs` + `MartenConfiguration.cs`; the overload surface was confirmed during implementation. |
| 2026-05-11 | Slice 1B (pre-spec gap audit) | Slice 1B's third acceptance criterion (top-level React error boundary with OTel correlation ID + Playwright forcing-throw test) is greenfield: frontend has zero `@opentelemetry/*` packages, zero `react-error-boundary`, zero render-throw fallback, zero `console.error` / `window.onerror` / shared error-display component, and uses declarative `<BrowserRouter>` + `<Routes>` (no data-router → no `errorElement` available). Backend OTel collector (`otel-collector-contrib` + Jaeger via `docker-compose.otel.yml`, OTLP HTTP on `:4318`) has **no CORS configured on the receiver**. Initial assumption that the error boundary was a 10-min spec-time lookup was wrong. | **Surfaced 2026-05-11 → R-073 + R-074 queued.** R-073 (`batch-25a-react19-error-boundary-recovery-ux.md`) covers library choice (hand-rolled vs `react-error-boundary` 5.x vs React 19 root options vs RR7 `errorElement`), router-migration cost, recovery UX, Playwright forcing-throw pattern, MVP-0 logging-shape lock (POST `/api/v1/client-errors` with cookie auth — R-074 upgrades to OTel later). R-074 (`batch-25b-react19-client-otel-correlation-id.md`) covers SDK choice + bundle delta, browser→collector vs proxy-via-backend topology, RTK Query `prepareHeaders` propagator seam, backend ASP.NET Core chaining via default W3C TraceContext propagators, PII scrubbing on the client side, MVP-0 sampling default. Repo-context blurb pre-compiled before the prompts were authored. Earlier "Phoenix collector" shorthand corrected to `otel-collector-contrib` + Jaeger (Phoenix was R-051's LLM-observability candidate, never shipped). |
| 2026-05-11 | Slice 1B (R-073 / R-074 sequencing) | R-073's MVP-0 logging shape (POST `/api/v1/client-errors`) intentionally pre-dates R-074's OTel-instrumented version. The seam is designed so R-074's recommendation upgrades R-073's payload in place without breaking the boundary contract. | **Architecturally locked.** R-073 ships the error boundary in Slice 1B with the simpler logging shape; R-074's OTel layer wires on top (boundary reads `traceparent` from a last-seen stash, payload format unchanged, transport optionally rerouted). If R-074 returns "skip client OTel for MVP-0" the boundary still works as-is. |
| 2026-05-12 | Slice 1B (R-073 / R-074 resolution) | R-073 (error boundary + recovery UX) and R-074 (client OTel + traceparent propagation) artifacts landed at `docs/research/artifacts/batch-25{a,b}-*.md`. | **Resolved 2026-05-12 as DEC-068 + DEC-069.** R-073 verdict: `react-error-boundary@6.1.1` as default (1 kB gz, zero deps, React 19 compatible); hand-rolled class as contingency fallback. Keep declarative `<BrowserRouter>`; defer data-router migration. Three-layer defence (boundary + React 19 root reporters + window listeners). MVP-0 logging: POST `/api/v1/client-errors` with `crypto.randomUUID()` correlation + cookie auth + `keepalive` + sendBeacon fallback. Recovery UX: full-page `role="alert"` card, "Try again" (soft reset) + "Reload page" (escalation). Playwright pattern: dev-only `<ThrowOnQuery />` gated by `import.meta.env.DEV` (zero production bytes). R-074 verdict: full `@opentelemetry/sdk-trace-web` 2.x + `instrumentation-fetch` 0.20x + OTLP/HTTP (~30–45 KB gz with `manualChunks` tree-shaking); hand-rolled ~40-LOC fallback if bundle exceeds 170 KB gz first-payload AND LCP regresses >100 ms. Browser → collector direct with `cors:` block on `otlphttp` receiver (never `["*"]`). Zero changes to `base-query.ts` — `FetchInstrumentation` patches `globalThis.fetch` transparently; first-import-in-`main.tsx` constraint is load-bearing. `useSyncExternalStore` singleton seam (`last-trace-id.ts`) feeds R-073's boundary; display trace-id only (32 hex, 8-8-8-8). PII scrubbing via `applyCustomAttributesOnSpan` (Option A) + collector `attributesprocessor` (belt-and-braces). Sampling MVP-0: AlwaysOn; MVP-1: opt-in via cookie consent with lazy-loaded SDK. **DEC-045 "Phoenix" shorthand correction:** the shipped collector is `otel/opentelemetry-collector-contrib:0.150.1` + Jaeger 1.76; "Phoenix" was R-051's LLM-observability *candidate* that never shipped. DEC-069 documents the actual implementation. |
| 2026-05-12 | Slice 1B (R-073 follow-up) | `POST /api/v1/client-errors` backend endpoint shape needs to be authored: cookie auth, accepts the R-073 wire shape, returns 204 No Content, persists somewhere or `/dev/null` initially. R-073 explicitly out-of-scopes the backend persistence model. | **Promoted into Slice 1B spec scope.** Spec session authors the endpoint inline; persistence shape can stay simple (Marten document or EF row) since the endpoint is single-caller and gets deprecated when DEC-069's OTel transport takes over. **Resolved 2026-05-15 (PR #92)** — `ClientErrorsController` (`POST /api/v1/client-errors`) landed. |
| 2026-05-12 | Slice 1B (R-074 follow-up) | `manualChunks` config for `vite.config.ts` keeps OTel in its own chunk; `chunkSizeWarningLimit: 100` is the recommendation. Also verify `Module "path" has been externalized for browser compatibility` warning is absent on first `vite build`; if present, add `optimizeDeps.exclude` + `rollupOptions.external` overrides. | **Promoted into Slice 1B spec scope.** Both are 2-line config changes; verification check moves into the spec's verification list. **Resolved 2026-05-15 (PR #94)** — `manualChunks` + `chunkSizeWarningLimit` landed in `vite.config.ts`. |
| 2026-05-12 | Slice 1B (R-074 follow-up) | `document.addEventListener('visibilitychange', ...)` → `provider.forceFlush()` is required for span-loss-on-tab-hide coverage; not in the default web SDK. | **Promoted into Slice 1B spec scope.** ~5 LOC addition to `otel.ts` at the bottom. **Resolved 2026-05-15 (PR #94)** — the `visibilitychange` → `forceFlush()` handler landed in `otel.ts`. |
| 2026-05-12 | Slice 1B (R-074 follow-up) | OpenTelemetry-JS browser instrumentation is labeled "experimental" by upstream; semantic-convention attribute names (`url.full`, `http.url`, `http.target`) may shift before stable. Stable enough for MVP-0; re-check at MVP-1. | **Deferred to MVP-1 prep.** Re-read DEC-069 references when public-tester rollout planning kicks off; bundle the attribute-name audit with the cookie-consent integration work. |
| 2026-05-12 | Slice 1B (DEC-045 cross-cycle correction) | DEC-045 referred to the OTel stack as "Phoenix" — a holdover from R-051's LLM-observability research. The actual shipped collector is `otel/opentelemetry-collector-contrib:0.150.1` + Jaeger; Phoenix was a *candidate* that never shipped. R-074 verified this against `docker-compose.otel.yml` + `infra/otel/otel-collector-config.yaml`. | **Documented in DEC-069; DEC-045 left as-is to preserve historical context.** Future references to "the OTel collector" or "the LLM observability backend" should use the actual names; if a Phoenix-style LLM-specific trace UI lands later (R-051's original direction), it would be a separate DEC. |
| 2026-05-18 | Slice 2 (pre-spec `/catchup` brainstorm) | Slice 2 decomposed into sub-project 2a (Frontend Visual Foundation) + 2b (Workout Logging proper), with 2a sequenced first. The frontend has no design foundation — bare `index.css`, hardcoded per-component colors, no dark mode, and shadcn/ui never installed despite the stack listing. | **Complete — 2a merged 2026-05-30; 2b next.** 2a covered palette + semantic tokens, light/dark, Tailwind v4 `@theme`, the component-library decision (subsumes the 2026-05-06 deferred shadcn-bootstrap item), typography/spacing scale, and migrating the existing surfaces onto the system. Calendar / chat-UI / animation libraries are explicitly excluded. The component-library + theming choices route through the research-prompt cycle. See the Status section's sub-project 2a bullet. |
| 2026-05-29 | Slice 2a (PR #113 deep-review) | Onboarding turn-inputs (numeric/date/single-select) were migrated to a bare `Controller` + raw `Input`/`RadioGroup` + manual `aria-*` + a hand-rolled `<p role="alert">`, **not** the shadcn `FormField`/`FormControl`/`FormMessage` stack the auth surfaces use via `AuthFormShell`/`AuthTextField`. R-075 (`batch-26a`) calls for a turn-input `<FormField>` row helper. | **Deferred — not in PR #113.** Migrating now breaks `numeric-turn-input.component.spec.tsx`'s `getByRole('alert')` and drops the live-region error announcement (shadcn `FormMessage` carries no `role="alert"`), and exceeds the colour-only migration scope. Trigger: a follow-up PR adding the `<FormField>` row helper with a `role="alert"` `FormMessage` variant, or documenting the carve-out. Task board #560. |
| 2026-05-29 | Slice 2a (PR #113 deep-review) | `DateTurnInput` and `NumericTurnInput` share a near-identical `Controller`+`Input` scaffold (form frame, label, Send `Button`, aria-described error `<p>`) introduced by the 2a migration; a shared single-field shell would remove the duplication. | **Deferred — judgment call.** Deep-review blind challenge rated extraction 38/100 (over-coupling risk: distinct Zod schemas, distinct defaults, numeric-only NaN/`valueAsNumber` coercion). Revisit only if more single-field turn-input variants land. Task board #561. |
| 2026-05-31 | Slice 2b (pre-spec research) | Batch 28 deep-research (R-077–R-080) commissioned and integrated: WorkoutLog metrics persistence + canonical keys, synchronous LLM-failure policy, eval-cache↔prompt sentinel, logging-form conventions. Load-bearing version-specific claims adversarially re-verified before lock (1 refuted, 5 partial — corrections folded in). | **Resolved/integrated** as DEC-072–075; all three Slice 1 carry-forwards closed. See the Status block "Decisions locked pre-Slice-2b" and `slice-2-logging.md` § Research resolutions. Artifacts at `docs/research/artifacts/batch-28{a,b,c,d}-*.md`. |
| 2026-05-31 | Slice 2b (R-079 / DEC-075) | Shared shadcn `FormMessage` lacks `role="alert"` — validation errors are associated via `aria-describedby`/`aria-invalid` but not assertively announced; the hand-rolled onboarding inputs do carry `role="alert"`. | **Deferred — coordinate with #560.** Add `role="alert"` (or an `aria-live` region) to the shared `FormMessage` as part of the #560 FormField-migration pass or a standalone a11y fix; it's a reviewable change to a shared component. |
| 2026-05-31 | Slice 2b (DEC-072) | Running-dynamics keys (`verticalOscillation` cm, `groundContactTime` ms, `strideLength` m, optional `cadenceMax`) are reserved-by-name in `WorkoutMetricKeys` but unpopulated at MVP-0. | **Deferred — zero-migration switch-on.** Names defined now; populate when HealthKit (iOS 16+) / Garmin running-dynamics ingestion lands (post-MVP-0 per DEC-033). |
| 2026-06-05 | Slice 2b (PR2 #145 review) | (a) `WorkoutPrescriptionSnapshot` has no fast/slow pace-ordering construction guard — the computed `PrescribedPace` `PaceRange` view throws on inverted bounds, and only at read time. (b) `WorkoutLog.Splits` exposes a mutable `List<>` on a type documented as an immutable historical fact. | **(a) Resolved in PR3 (#147); (b) still deferred.** (a) PR3 added the validating `WorkoutPrescriptionSnapshot.Create` static factory (fast-no-slower-than-slow guard) + unit tests, so an inverted-bounds snapshot fails at construction, not at read time; the create path sources already-ordered plan paces (`TargetPaceFastSecPerKm ≤ TargetPaceEasySecPerKm`). (b) The whole EF entity is a conventionally-mutable POCO; only convert `Splits` to a read-only `IReadOnlyList` surface (private backing field + `PropertyAccessMode.Field`) if a consumer needs structural immutability. |
| 2026-06-06 | Slice 2b (PR3 #147 deep-review) | (a) `CreateWorkoutLogRequestDto` has no server-side size caps on `Notes` / `Metrics` / `Splits` (bounded only by auth + Kestrel's ~28.6MB body limit). (b) `WorkoutPrescriptionSnapshot.Create`'s ordering guard is bypassable via the public `init` setters EF needs to materialize the complex type. | **(a) Deferred — pre-public-release; (b) declined.** (a) YAGNI for single-user MVP-0 (the "attacker" is the solo dev); the structural fix is rate limiting + spec'd input bounds (no rate-limiting infra exists yet), tracked under the pre-public-release gate — concrete caps would also shift the OpenAPI `maxLength`/`maxItems` contract (codegen-drift gate). (b) EF maps the snapshot as an optional complex type and needs the `init` setters; no production path uses the bypass (only `Create`, with ordered plan paces), the design predates the slice (#145), and after #147 the `Create` throw is caught in `ResolvePrescriptionAsync` and downgraded to an off-plan log — a private-ctor rewrite would break EF materialization for a non-reachable defect. |
| 2026-06-06 | Slice 2b (PR4 #151 deep-review) | Four non-blocking improvement suggestions on the history-query endpoint: (a, medium) the read projection (`MapToDto` / `DeserializeMetrics` / `MapSplitsToDto`) was untested with populated notes/metrics/splits — every seed used the empty `NewLog` helper; (b, low) the exact-page-multiple cursor-termination branch (full final page still emits a cursor; the trailing fetch is empty) was untested; (c, low) a `WorkoutLogDto` XML-doc comment carried a `Slice-3` forward reference (REVIEW.md code-comments rule); (d, low/contested) the request DTO omits the spec's "shaped-but-unused" filter block. | **All addressed (commit `cac20a2`, in PR #151).** (a) Added a rich-log read-projection integration test (seeds notes + a `jsonb` metrics bag + two splits, asserts the returned `WorkoutLogDto` echoes all three). (b) Added an exact-page-multiple termination test (4 logs at limit 2 → page2 non-null cursor, page3 empty + null cursor, no skips/dupes). (c) Dropped the forward slice reference, kept the load-bearing why. (d) Reconciled by **amending the Unit 4 spec** to record the filter-block deferral rather than adding the block — the request DTO grows additively when filter UI lands (a later optional property is not a wire break), so deferring avoids dead public-API surface and matches the "query DTO is shaped to grow; no filter UI at MVP-0" non-goal. No new DEC. |
| 2026-06-08 | Slice 3 (PR1 #161) | Deterministic `SafetyGate` deferred precision: (a) **negation handling** — "no chest pain" still escalates to Red today (a naive preceding-negator guard risks *suppressing* a real signal, e.g. "not going to lie, I want to kill myself"); (b) **fine-grained false-positive precision** — comma-clause-crossing proximity and obstetric vocabulary ("water broke"). | **Deferred to Unit 6 eval calibration.** Both need calibrated design against the 5 `TestProfiles` with asymmetric scoring (under-reaction = hard fail; over-reaction = low score), not a rushed regex; (a) is documented in the `SafetyKeywordCatalog` `<remarks>`. PR1's own review findings (test-coverage gaps for SG-E13 / C08-C09 / R09 + the reverse-order proximity rules, fail-open→fail-closed on regex timeout, tier-category construction invariant) were all addressed in-PR (commit `28b5d13`); no carry-forward. |
| 2026-06-08 | Slice 3 (PR3 #163) | (a) The Swashbuckle `RequireNonNullablePropertiesSchemaFilter` stamps every `$ref` property `required` even when the C# member is nullable, so the generated swagger/zod marks the conversation turn's nullable slots (`escalationLevel` / `adaptationKind` / `diff`) as required — a repo-wide, pre-existing limitation (40+ properties, incl. the already-shipping nullable `MesoDaySlotOutput.workoutType`). (b) `ConversationTurnView` models two role-shapes as one flat record with independently-nullable slots. (c) `WorkoutChange` permits the meaningless `(Before=null, After=null)`. | **(a) Deferred — cross-cutting follow-up; (b) & (c) declined.** (a) Type-accuracy, not a runtime bug: the generated zod is not applied to responses and consuming modules hand-write accurate discriminated domain models (`plan.model.ts`); PR3 follows the convention and the barrel comment flags the caveat. The structural fix is a nullability-aware filter applied repo-wide (its own PR), tracked here. (b) Non-public `init` would break Marten rehydration and a full discriminated-union split is constrained by the JSONB document identity + OpenAPI codegen; the two factories are the sole, correct producers, so a guard would be unreachable. (c) The dual-nullability is the intentional add/remove/edit forward design (null `After` = removal in a later slice); a throwing constructor on an event-sourced record would harden a benign projection no-op into a historical-event rehydration failure. PR3's own review round — test-coverage gaps (the `Upsert` replace-in-place idempotency branch, the cross-tenant Marten load, the DEC-060 atomic two-projection rollback, the `PlanProjection` second-loop validation + absent-week skip + add-new-workout branches, the non-Guid-`sub` 401), plus a real fix: a deterministic same-`CreatedAt` turn-ordering tiebreak via the per-stream event version — was all addressed in-PR (commit `e129c87`); no carry-forward. |
| 2026-06-08 | Slice 3 (PR2 #165) | Two latent design notes from deep-review, neither reachable in PR2 (`EscalationClassifier.Classify` has no production caller until PR5 wires orchestration): (a) **`NeedsAdjustment` dead-zone re-escalation** — while in `NeedsAdjustment` the classifier absorbs every trigger until the rolling score decays below the exit threshold (recovery), so a still-deteriorating runner is never re-escalated and L1 key-miss swaps are also suppressed; (b) **`AdaptationSignalState` enforces no invariants** — the positional record accepts a negative score/streak, a score above the cap, and `(NeedsAdjustment, …, null LastAdaptationOn)`, which silently disables the cooldown half of the hysteresis. | **Both deferred to PR5; tracked in `pr-strategy.md` § PR5 carryover.** (a) The current code is the deliberate PR2 spec behavior (the anti-flip-flop guarantee is L2-only and recovery is the sole dead-zone exit), so the suggested hard-trigger override is a spec change; whether L1 swaps survive the dead-zone and whether the consecutive-missed hard trigger re-escalates post-cooldown belong with PR5 orchestration + Unit 6 eval calibration against the 5 `TestProfiles` (a suppressed-trigger observability signal can't land in PR2 without breaking the class's pure/no-I/O contract). (b) The classifier always emits valid states, so a validating factory now would be unused code guarding a boundary that doesn't exist yet; the `WorkoutPrescriptionSnapshot.Create()`-style guard (clamp score to `[0, Max]`, reject a negative streak, reject `NeedsAdjustment` + null `LastAdaptationOn`) ships at PR5's DEC-078 rehydrate-from-persistence seam where an invalid state could actually enter. PR2's two test-coverage gaps (`CompletionStatus.Partial` untested across the decision layer; the −5% distance-shortfall under-performance clause never the sole driver) were addressed in-PR (commit `c6edfcf`); no carry-forward. |
| 2026-06-09 | Slice 3 (PR4 #167 CI) | `OnboardingTurnConcurrencyIntegrationTests` (the DEC-057 `EventAppendMode.Rich` stream-collision regression guard) flaked once under the coverage-instrumented `Backend analysis` CI job with `successCount == 2` — two concurrent `StartStream<OnboardingView>` commits winning instead of the asserted one. This is a **violation of the exactly-one-winner correctness property**, not a failure to reproduce the race, so it is either **(a)** a genuine rare gap in the Rich-mode stream-collision guarantee (intermittent duplicate-onboarding-stream corruption — the exact DEC-057 scenario) or **(b)** a test-harness contention artifact widened by coverage-instrumentation timing. Passed locally (1510/1510), the normal `Backend (build + test)` job, and the re-run. Not introduced by PR4 (no onboarding/concurrency files touched). | **Resolved 2026-06-11 — root-caused to (b), a test-harness timing artifact; no Marten Rich-mode gap, no corruption observed or possible (PR #182).** Evidence: CI attempt-1 log confirms only `successCount == 2` fired; 30× as-is loop under coverlet locally = 0 recurrence; a deterministic straggler probe (invoke → commit → invoke again) reproduces the "second success" — the handler reads the committed `OnboardingView` and takes its legitimate second-turn `Append` path (stream dump: ONE stream, ONE `onboarding_started_v1`, winner v2–5 + straggler v6–9, no duplicates); 20 rounds of 5 truly-overlapping staged `StartStream`s racing `SaveChangesAsync` = exactly 1 winner + 4 collisions every round; and the provisioned schema shows the guard is layered (`mt_streams` PK `(tenant_id, id)` + unique `(tenant_id, stream_id, version)` on `mt_events` + events→streams FK), so two committed `StartStream`s for one user are structurally impossible. The old test shape silently assumed all 5 unsynchronized tasks read "no stream yet" before any commit — on the slow coverage-instrumented runner one task started after the winner committed and legitimately succeeded as turn 2. Fix: the test now stages all 5 handler invocations BEFORE any commit (each deterministically reads `view == null`), then races the 5 commits — assertions unchanged (1 winner / 4 collision-typed failures), the collision path now exercised on every run instead of probabilistically. 15/15 green looping the fixed test under coverage; suite 1705/1705. No retry / rerun / loosened assertion. **The live end-to-end validation pass is unblocked.** |
| 2026-06-10 | Slice 3 (PR5 #169 deep-review) | Two deliberate MVP-0 scope reductions of the shipped orchestration handler from the local spec, plus one defense-in-depth gap. **(a) single-call validator reject** — the L2 path makes exactly one structured-output call; a validator-rejected / non-restructure / safety-tier-mismatch proposal maps straight to `Kind=Error` with nothing staged, so the spec's "one re-prompt with constraint feedback before terminal failure" is not implemented. **(b) profile-less adaptation prompt** — `ComposeForAdaptationAsync` renders plan context + escalation + safety tier + deviation + the single triggering log only; the spec's runner-profile block + full recent-logs window are omitted. **(c) metric-value sanitizer bypass** — a client can submit a string value under a numeric metric key and `WorkoutMetricsProjection.ToDisplayMetrics` / `RecentLogFormatter` render it into the nonce-spotlighted adaptation prompt having bypassed the DEC-059 pattern sanitizer (only delimiter-escaping applies). | **(a) + (b) ratified as DEC-080 (recorded as intentional, no carry-forward);** revisit both at Unit 6 eval calibration against the five `TestProfiles` — a single re-prompt buys a second chance on a recoverable reject (rare on a frozen schema; a clean "try again" envelope already surfaces), and the profile block is a personalization gap bounded by the PR4 `adaptation.v1.yaml` template + the funded-key eval re-record. **(c) deferred to the pre-public-release gate** (promoted to `docs/features/backlog.md`): delimiter-escaping + the spotlight nonce still prevent section break-out so it is defense-in-depth, not a full break, and the MVP-0 audience is self + family; the structural fix is validating the metrics bag at the create boundary (numeric canonical keys reject non-numeric JSON; reject keys outside `WorkoutMetricKeys.All`) OR routing every rendered value through the DEC-059 sanitizer — both shift the OpenAPI contract. Dispositions folded into the PR before the squash-merge (#169). |
| 2026-06-10 | Slice 3 (PR6 eval suite) | Three LLM-variance-robustness mechanisms from the Unit 6 spec / Gherkin deferred at build time: **(a) Cohen's-kappa ≥ 0.8 Haiku-judge calibration** against a hand-labelled golden set; **(b) a per-category baseline regression gate** at >2× standard error from a committed baseline; and **(c) the pass-2-of-3 attempt policy**. | **Deferred (user scope call for (a)+(b), audience steer `project_mvp0_audience_and_priorities`; (c) is moot for the shipped architecture).** All three tolerate LLM judge/output variance, which is **moot in deterministic Replay**: the committed cache returns an identical response every run, so 2-of-3 ≡ 1-of-1, the deterministic absorb/nudge/restructure/safety categories have SE ≈ 0, and kappa needs a live judge run. They earn their keep only against live (Record-mode) variance or a larger judged surface. The shipped PR6 suite already enforces the load-bearing gates — asymmetric scoring (under-reaction = hard fail, over-reaction = low-not-penalized), the per-category pass-rate report, and the safety pass-rate ≥ 95% gate (DEC-079) — plus the DEC-074 prompt-hash sentinel. Revisit all three when the judged surface grows (more LLM-authored scenarios), the suite runs live, or a second human joins, mirroring the R-068-style staged-trigger pattern. |
| 2026-06-11 | Slice 3 (live pass) | **Amber safety referral dropped on every non-restructure path** — `EvaluateAdaptationHandler` appends the Amber `SafetySignalRaised` referral only inside `RestructureAsync`; Amber + L0/L1-absorb (incl. the post-restructure cooldown dead-zone) returns with no safety turn. Live repro: "sharp pain in my left shin" note (SG-I01 match) during the cooldown → total silence in the Coach panel. | **Immediate fix — Slice 3B F1** (`slice-3b-live-pass-fixes.md`). Safety hard-rule territory (DEC-019/DEC-030/DEC-079). |
| 2026-06-11 | Slice 3 (live pass) | **Trademark leak in persisted LLM prose** — the live macro generation emitted "Using Daniels' Running Formula, your `VDOT` sits around 38" into `plan_generated_v1`'s `Macro.Rationale`. Existing guards cover the assembled prompt + adaptation output, not the plan-generation structured outputs' prose fields; the leak is persisted and rides the projection toward the API surface. | **Immediate fix — Slice 3B F2.** Prompt prohibition + deterministic output-boundary guard + eval-guard extension to all persisted prose fields. Strengthens the case for the deferred ROADMAP build-time trademark analyzer. |
| 2026-06-11 | Slice 3 (live pass) | **Plan horizon ignores the target event date** — a 10K declared nine weeks out produced a 16-week Base/Build/Peak/Taper macro; race day lands mid-Peak, taper ends six weeks after the race. Generation context apparently lacks current-date/event-date arithmetic and no validator checks the horizon. | **Immediate fix — Slice 3B F3.** Date-aware generation context + deterministic horizon validator (race week inside the final phase's last week, ±1). |
| 2026-06-11 | Slice 3 (live pass) | **Restructure internal-arithmetic gap** — live L2 output proposed Week-1 `WeeklyTargetKm: 24` while its own edited micro week sums to 30 km (Thursday untouched at 6 km); validator has no weekly-target ↔ micro-week-sum consistency rule. Same output also raised Saturday 11 → 12 km inside a fatigue load-cut (suspicious but arguably legitimate — judge territory, not a hard rule). | **Immediate fix — Slice 3B F4** for the deterministic sum-consistency rule (terminal `Kind=Error` per DEC-080 posture). The Saturday-increase judgment call joins the judge-calibration deferral below. |
| 2026-06-11 | Slice 3 (live pass) | **Onboarding Schedule step loops on free-text slot answers** — days/duration given in prose were not merged into slots (`clarification_requested`: "did not provide typical session duration in minutes" after it was stated twice); took four round-trips until one message phrased both slots explicitly. Plus an input-kind mismatch: the structured day-picker rendered under assistant text asking about fitness feel. | **Scheduled into Slice 4** — slot-extraction/merge quality and turn-output input-kind coherence belong with the conversation-quality work; add eval scenarios for multi-slot free-text merges. |
| 2026-06-11 | Slice 3 (live pass) | **Restructure rationale-claim drift** — the rationale promised "Thursday becomes a short, genuinely gentle recovery run" but the diff contains no Thursday change. Exactly the class the Haiku communication judge exists for. | **Deferred — joins the PR6 judge-calibration row above** (kappa/golden-set work); add a rationale-vs-diff grounding scenario when that work runs. |
| 2026-06-11 | Slice 3 (live pass) | **Stale week narratives after adaptation** — Upcoming-weeks list shows adapted volume numbers (24.0 km) beside pre-adaptation prose ("totaling 30 km"). Expected: adaptation deliberately edits targets/workouts without regenerating summaries — but visibly self-contradictory in the UI. | **Deferred — UX polish**, promote to backlog at cycle completion. Options at that point: regenerate summaries on restructure (extra LLM cost) or visually mark narratives as pre-adaptation. |
| 2026-06-11 | Slice 3B (F1) | **Off-plan logs never reach the safety gate** — `EvaluateAdaptationHandler` step 3 returns on a null prescription BEFORE step 4 classifies safety, so an off-plan log with a crisis or injury note produces no `SafetySignalRaised` at all (not even Red). Structural, not an F1 regression: there is no plan stream to append to and the Coach panel is plan-scoped, so surfacing it needs a different vehicle (per-user safety stream, or classify-at-create with a response-level surface). | **Deferred — needs a design decision** on where a plan-less safety turn lives. High-risk subset posture (DEC-079) says don't lose it: schedule with Slice 4's conversation work or promote to backlog at cycle completion. |

---

## Goal & Done-State

Build a real multi-tenant product where you can:

1. Sign up and log in.
2. Complete chat-driven onboarding that builds your user profile.
3. See a generated macro/meso/micro training plan on the home page.
4. Log a workout with as much or as little data as you want (bare minimum: distance + duration + completion; rich path: add RPE, HR, splits, HRV, weather — whatever you've got — plus freeform "what happened" notes).
5. Watch the plan adapt when logged workouts deviate from prescription, with the coach explaining *why* in a persistent chat panel.
6. Ask the coach open-ended questions grounded in your profile + plan + recent history.

"Done" = the above end-to-end loop works, you are personally using it to run, and the eval suite covers the adaptation scenarios that matter.

---

## In Scope

- **Auth:** ASP.NET Identity + JWT; register/login/logout UI; password reset deferred until pre-public-release.
- **Persistence:** PostgreSQL + EF Core (relational entities) + Marten (event-sourced plan state). Both run against the same Postgres instance; clear ownership boundaries per `backend/CLAUDE.md`.
- **Onboarding:** Chat-driven multi-turn flow covering the topics in `docs/planning/interaction-model.md` (primary goal, target event, current fitness, schedule constraints, injury history, preferences).
- **Plan generation + persistence:** Macro/meso/micro tiers per `docs/planning/planning-architecture.md`. Plan is a Marten event-sourced aggregate; projection = current plan document for LLM consumption.
- **Plan view:** Structured UI surface — this-week card, today's workout, upcoming list.
- **Workout logging:** Structured form with required core fields + optional rich metrics (JSONB) + freeform notes. Flexible: bare-minimum logs and rich logs both work; the LLM renders what's present and gracefully handles what isn't.
- **Adaptation loop:** Logged workout → deterministic deviation computation → LLM adaptation prompt with full context (including freeform notes) → structured-output plan modification → event appended to Marten stream → projection updated → plan view re-renders → chat panel surfaces the explanation.
- **Open conversation:** Persistent chat panel for ad-hoc coaching questions. Context-assembler routes by query type per the existing `ContextAssembler` design.
- **Eval-suite extension:** Slice 3 adds adaptation scenarios to the existing M.E.AI.Evaluation infrastructure. CI continues replay-only.

## Out of Scope (Deferred — Designed-For)

These are explicitly not built in this cycle, but the architecture leaves room so they bolt on without schema thrash or refactoring.

- **Apple HealthKit / iOS shim.** Auto-fill of workout metrics. Needs an iOS companion app per DEC-033. Design accommodation: `WorkoutLog.metrics` JSONB column takes whatever HealthKit gives us.
- **Garmin Connect integration.** Post-MVP-1 per `CLAUDE.md`. Design accommodation: same JSONB shape; webhook ingress pipeline is a future slice.
- **Pre-public-release safety scaffolding.** PAR-Q+ extended screening, medical-scope keyword triggers, population-adjusted guardrails, beta participation agreement, full ToS, LLC formation. Blocks public exposure, not personal use.
- **Tiered model routing.** Post-MVP-0 cost optimization per DEC-038. Design accommodation: existing `ICoachingLlm` interface is the natural seam.
- **Voice notes / mid-run logging.** Re-opens the temporal-binding problem (which workout does "I had to walk" refer to?). Further out.
- **Proactive notifications.** Light-touch missed-workout detection *may* land in Slice 4 if it's cheap; full proactive system deferred.
- **Coach personalities, multi-sport, nutrition guidance, injury prediction, social features.** All in `docs/features/backlog.md` as Future.

---

## Slice Structure

Each slice ships top-to-bottom through every layer (DB → repo → controller → frontend → tests) and is usable when done. The product is stoppable after any slice.

**Tier-3 requirements docs live alongside this file** — one per slice, ~90 lines each. They elaborate requirements without crossing into implementation. When a slice's implementation session starts, that session reads the per-slice requirements doc + this cycle plan + the referenced research artifacts, then writes a spec under `docs/specs/`.

| # | Name | Requirements doc |
|---|---|---|
| 0 | Foundation | [`./slice-0-foundation.md`](./slice-0-foundation.md) |
| 1 | Onboarding → Plan | [`./slice-1-onboarding.md`](./slice-1-onboarding.md) |
| 1B | Pre-Slice-2 Hardening | [`./slice-1b-hardening.md`](./slice-1b-hardening.md) |
| 2 | Workout Logging | [`./slice-2-logging.md`](./slice-2-logging.md) |
| 3 | Adaptation Loop | [`./slice-3-adaptation.md`](./slice-3-adaptation.md) |
| 3B | Live-Pass Fixes | [`./slice-3b-live-pass-fixes.md`](./slice-3b-live-pass-fixes.md) |
| 4 | Open Conversation | [`./slice-4-conversation.md`](./slice-4-conversation.md) |

### Slice 0 — Foundation

**Requirements:** [`./slice-0-foundation.md`](./slice-0-foundation.md)


**Acceptance — "I can…"**

- [ ] …run `docker compose up` and have Postgres + API + web all healthy.
- [ ] …hit `POST /api/v1/auth/register` and create an account.
- [ ] …hit `POST /api/v1/auth/login` and receive a JWT.
- [ ] …open the frontend, register/login through the UI, and see an authenticated empty home page.
- [ ] …see CI green on the slice-0 PR with all six required checks passing.

**Scope**

- Backend: `RunCoachDbContext` (EF Core) + Marten registration wired into DI; initial migration applied on startup in development. `Modules/Identity/` module with Identity tables, JWT issuance, register/login/logout endpoints. Global error-handling middleware.
- Frontend: `app/modules/auth/` with login + register pages, JWT stored in Redux + persisted, axios/fetch interceptor attaches the token, protected-route wrapper. Auth store slice.
- Tests: Integration tests for register/login/logout using `WebApplicationFactory` + Testcontainers. Component tests for auth pages. One Playwright happy-path E2E (register → login → see home).
- No business features — no plan, no logging, no coaching. Foundation only.

**Key risks**

- First time wiring Identity + EF Core + Marten + JWT together — integration surprises possible. Allocate time for this.
- Testcontainers + local Postgres configuration on macOS (Colima) — verified in existing CI but not yet exercised at this scope.

**Relevant research artifacts**

- `batch-10b-dotnet-backend-review-practices.md` — backend conventions applied here.
- `batch-10c-ci-quality-gates-private-repo.md` — CI pipeline structure.
- `batch-14a` / `batch-14b` / `batch-14c` / `batch-14f` — CodeRabbit, CodeQL, SonarQube, branch protection patterns.
- `batch-10a-frontend-latest-practices.md` — React 19 + TS + Vite conventions for the auth module.

---

### Slice 1 — Onboarding → Plan

**Requirements:** [`./slice-1-onboarding.md`](./slice-1-onboarding.md)

**Acceptance — "I can…"**

- [x] …complete a multi-turn chat-driven onboarding flow that builds my user profile.
- [x] …see a generated macro/meso/micro training plan on the home page after onboarding completes.
- [x] …reload the page and see the same plan (persisted, not regenerated).
- [x] …re-trigger plan generation from a settings action (for iteration / correction).

**Scope**

- Backend: `Modules/Training/` gains the Marten-backed `Plan` aggregate (events: `PlanGenerated`). `Modules/Coaching/` gains an `OnboardingController` — multi-turn: each POST returns either "next question" (with structured-output schema for the question + which profile field it fills) or "complete, plan generated." Uses the existing `ContextAssembler` and `ClaudeCoachingLlm`. `UserProfile` entity (EF Core) persists onboarding answers. Plan generation invokes the existing brain layer; projection materializes to a structured document.
- Frontend: `app/modules/onboarding/` — guided chat UI (progress indicator, "we're almost done" framing, not the day-to-day chat panel). `app/modules/plan/` — this-week card, today's workout, upcoming list. RTK Query slices.
- Tests: Integration tests for onboarding controller (multi-turn flow, completion). Eval cache extended with onboarding scenarios. Playwright: register → onboard → see plan.

**Key risks**

- Multi-turn onboarding state management — where does the "in-progress onboarding" live? Probably a `UserProfile.OnboardingStatus` column + per-turn requests that pass the accumulating state. Decide at slice-1 plan time.
- Plan projection shape — the existing brain layer emits plan documents; they need to land in a stable projection the frontend can render. Design the projection schema at slice-1 plan time.

**Relevant research artifacts**

- `batch-2a-training-methodologies.md` — training-science basis for plan generation.
- `batch-2b-planning-architecture.md` — macro/meso/micro tier semantics, event-sourcing patterns.
- `batch-4a-coaching-conversation-design.md` — onboarding tone, question ordering, OARS/GROW patterns.
- `batch-6a-llm-eval-strategies.md` + `batch-6b-dotnet-llm-testing-tooling.md` — eval patterns for onboarding scenarios.
- `batch-7a-ichatclient-structured-output-bridge.md` — structured output for per-turn onboarding responses.
- `batch-4b-special-populations-safety.md` — safety considerations for onboarding profile questions (injury history, pregnancy, chronic conditions) even though pre-public-release safety scaffolding is deferred.

---

### Slice 1B — Pre-Slice-2 Hardening

> **Status: Complete — merged 2026-05-15 across PRs #91–#94.** All five acceptance criteria met.

**Requirements:** [`./slice-1b-hardening.md`](./slice-1b-hardening.md)

**Acceptance — "the next class of bug can't happen the same way":**

- [x] …backend DTOs and frontend Zod schemas share a single source of truth via OpenAPI codegen wired into `npm run build`; CI fails when committed generated files drift from the live spec.
- [x] …a Marten event payload can add, rename, or evolve a property without breaking projection of pre-existing streams, and the strategy is exercised by a regression test against a synthetic old-shape stream.
- [x] …the React app survives a child render-time exception with a top-level error boundary that logs and renders a recovery affordance instead of a blank screen.
- [x] …`IIdempotencyStore`, `IPlanGenerationService`, and `RegeneratePlanHandler` each have a DI-resolution regression test of the same shape as `ContextAssemblerDiResolutionTests`, so a future "most-resolvable-parameters" silent regression cannot ship.
- [x] …the Slice 0 deferred follow-up "frontend Zod schemas must mirror `RegisterRequest` DataAnnotations" closes here, subsumed by the codegen above.

**Scope**

- Backend: no production-code changes beyond OpenAPI exposure verification (Swashbuckle is already wired). Adds Marten event upcasting registration (one of: built-in `Upcast`, custom `IEventUpcaster<TOld, TNew>`, or versioned event types — research-resolved). Adds three DI-resolution regression tests. Adds OTel span shape for upcaster invocations.
- Frontend: codegen pipeline (`openapi-typescript` + `openapi-zod-client` or research-recommended alternative); migration of ~12 hand-maintained Zod schemas to generated forms; legacy file deletion. Top-level error boundary in router root. One Playwright test forcing a child throw.
- Tests: regression tests per acceptance criteria above. No new feature tests.

**Key risks**

- Codegen tool produces an awkward shape for the Pattern-B Anthropic-structured-output schemas (`OnboardingTurnOutput`'s six nullable typed slots + `Topic` discriminator) — research prompt R-071 specifically asks the artifact to verify this case. Fallback: feature-flag the codegen for non-Pattern-B endpoints and migrate Pattern-B last once the shape is validated.
- Marten upcasting may need to coordinate across both standard projection registration (`opts.Projections.Add(...)`) and EF projection registration (`opts.Add(...)` per DEC-062). Research prompt R-072 confirms the upcaster intercepts BEFORE the projection's apply method regardless of registration site.
- Generated Zod schemas inflate bundle size more than expected. Research prompt R-071 quantifies the expected delta on a representative endpoint.

**Relevant research artifacts**

- (landed) `batch-24a-openapi-typescript-zod-codegen.md` (R-071) — tooling, build wiring, drift-check.
- (landed) `batch-24b-marten-event-upcasting-strategy.md` (R-072) — Marten 8.32 upcasting strategy across both projection styles.
- (existing) `batch-22b-anthropic-discriminated-structured-output.md` — Pattern B schema shape that the codegen must round-trip cleanly.
- (existing) `batch-23b-marten-ef-projection-registration-regression.md` — DEC-062's `opts.Add(...)` rule that the upcasting strategy must coexist with.

---

### Slice 2 — Workout Logging

**Requirements:** [`./slice-2-logging.md`](./slice-2-logging.md)

**Acceptance — "I can…"**

- [x] …see today's prescribed workout on the home page.
- [x] …open a log form, fill in at minimum distance + duration + completion, save it.
- [x] …optionally expand "more details" and fill in RPE, HR avg/max, calories, splits, HRV, sleep score, weather — whatever I have — without the form yelling at me for missing fields.
- [x] …write freeform "what happened?" notes and have them persisted.
- [x] …see my logged workout appear in a history list, with notes visible.
- [x] …verify via eval that the logged notes + metrics flow into LLM context (no adaptation wired yet — just context injection).

**Scope**

- Backend: `WorkoutLog` entity (EF Core). Required cols: `Id`, `UserId`, `PlannedWorkoutId` (nullable), `LoggedAt`, `Distance`, `Duration`, `CompletionStatus` (enum: complete/partial/skipped), `Notes`. One nullable JSONB col: `Metrics` (takes arbitrary keys: `rpe`, `hrAvg`, `hrMax`, `calories`, `splits`, `hrv`, `sleepScore`, `recoveryScore`, `weather`, `terrain`, etc. — no schema enforcement). Repo + log endpoint. `ContextAssembler` extension to include recent `WorkoutLog.Notes` + `Metrics` keys in the training-history block. *(Data model as shipped, per the Status block: DEC-076 replaced `PlannedWorkoutId` with a nullable server-resolved prescription snapshot (off-plan = null) and split `LoggedAt` into `OccurredOn` + `CreatedOn`; DEC-077 added an `IdempotencyKey` column + unique `(UserId, IdempotencyKey)` index. `Metrics` shipped as a single-sourced `WorkoutMetricKeys` bag per DEC-072.)*
- Frontend: `app/modules/logging/` — today's workout card gets a "Log" action. Log form with collapsed "More details" expander. Render whatever metrics are present in the history list.
- Tests: Integration test for log endpoint. Unit tests for `ContextAssembler` extension (various metric shapes, including empty metrics). Playwright: today's workout → log with minimum → log with rich metrics → both appear in history.

**Key risks**

- JSONB key naming conventions — decide the canonical keys for the metrics most likely to come in (from manual entry now, from HealthKit later). Lock them in a shared constants file so manual logging and future auto-fill write the same shape.
- Metrics-absent LLM prompting — the context assembler must gracefully express "HR not provided" vs. "HR avg 142 bpm" without confusing the coaching prompt. Validate with eval scenarios.

**Relevant research artifacts**

- `batch-3c-wearable-integrations.md` — what metric shapes to anticipate for future HealthKit/Garmin ingestion; informs canonical JSONB key choices.
- `batch-9b-unit-system-design.md` — distance/pace unit handling.
- `batch-10b-dotnet-backend-review-practices.md` — EF Core + JSONB patterns.

---

### Slice 3 — Adaptation Loop

**Requirements:** [`./slice-3-adaptation.md`](./slice-3-adaptation.md)

**Acceptance — "I can…"**

- [ ] …log a workout that deviates meaningfully from plan (e.g., distance way off, or freeform notes indicating walking/injury/external factor).
- [ ] …see the plan adjust in response, with the event stored in the Marten stream.
- [ ] …see the coach's explanation ("I adjusted your plan because…") appear in the chat panel.
- [ ] …verify via eval that adaptation handles the absorb/nudge/restructure cases correctly per DEC-012's escalation ladder (at least levels 1-3).

**Scope**

- Backend: Adaptation prompt + structured-output schema (events: `PlanAdaptedFromLog` with reason + modified workouts). Post-log hook triggers adaptation evaluation. Event appended to Marten stream; projection updated. Coach's explanation persisted as a `ConversationTurn` (new entity — see below).
- Frontend: Plan view re-renders after adaptation. Chat panel appears with the "I adjusted your plan because…" message. Panel is read-only in this slice — interactive input lands in Slice 4.
- Tests: Integration tests for the full log-triggers-adaptation path. Eval suite extended with 5-10 adaptation scenarios spanning DEC-012 levels 1-3 (absorb / nudge / restructure). Replay-mode in CI.
- `ConversationTurn` entity arrives here (not Slice 4) because adaptation explanations are the first conversational content.

**Key risks**

- Adaptation gate logic — when does a log trigger adaptation vs. no-op? Probably: always invoke the LLM adaptation prompt; let the LLM decide "no adjustment needed" and emit that as an event (or not, and skip the stream write). Decide at slice-3 plan time based on cost.
- Structured-output schema stability for plan modifications — the existing `MesoWeekOutput` restructuring lesson from DEC-042 applies; design structurally, not via `[Description]` hints.

**Relevant research artifacts**

- `batch-2b-planning-architecture.md` — event-driven recomposition, DEC-012 escalation ladder mapping, hysteresis thresholds.
- `batch-4a-coaching-conversation-design.md` — how to communicate plan changes (OARS, Elicit-Provide-Elicit, traffic-light shorthand).
- `batch-4b-special-populations-safety.md` — safety gates that must trigger before any pace/volume increase.
- `batch-2c-testing-nondeterministic.md` — adaptation evaluation patterns.
- `batch-6a-llm-eval-strategies.md` — LLM-as-judge patterns for adaptation quality.

---

### Slice 3B — Live-Pass Fixes

**Requirements:** [`./slice-3b-live-pass-fixes.md`](./slice-3b-live-pass-fixes.md)

Hardening mini-slice (Slice 1B precedent) created from the 2026-06-11 live
end-to-end validation pass — the first live run of the full coaching path. The
loop passed; four findings are immediate fixes because they sit in hard-rule
territory (safety, trademark) or make the product visibly wrong to its first real
user (plan horizon, restructure arithmetic). All four live in shipped Slice 3 /
Slice 1 surfaces — no new feature ground.

**Acceptance — "I can…"**

- [ ] …log a workout whose note trips an Amber injury rule on ANY escalation path (including the post-restructure cooldown dead-zone) and see the scripted referral turn in the Coach panel (F1).
- [ ] …generate a live plan whose persisted prose fields (rationales, notes, summaries) contain no trademarked term, enforced by a deterministic output-boundary guard + extended eval guard (F2).
- [ ] …onboard with a dated race goal and get a plan whose horizon lands race week at the end of the taper (deterministic validator rejects mismatches) (F3).
- [ ] …trigger a live restructure whose proposed weekly target equals its own micro-week workout sum within tolerance (validator rejects contradictions as terminal `Kind=Error`) (F4).
- [ ] …re-run the **full live end-to-end validation pass** (fresh account, funded key, real browser) and observe all four fixes at the surface — this re-run is the slice's done-gate; CI/eval coverage alone does not close it.

**Out of scope:** onboarding Schedule slot-merge loop (→ Slice 4), restructure rationale-claim drift (→ judge-calibration deferral row), stale week narratives after adaptation (→ deferred UX polish). See the requirements doc and Captured During Cycle table.

---

### Slice 4 — Open Conversation

**Requirements:** [`./slice-4-conversation.md`](./slice-4-conversation.md)

**Acceptance — "I can…"**

- [ ] …type a question into the chat panel ("how am I doing?", "should I push harder next week?", "my knee feels tight").
- [ ] …see a streaming response grounded in my profile + plan + recent logs.
- [ ] …have the conversation persist across sessions (chat history visible on reload).
- [ ] …see the system handle the three interaction modes from `docs/planning/interaction-model.md` — onboarding (slice 1), proactive adaptation messages (slice 3), and open conversation (this slice).

**Scope**

- Backend: Conversation endpoint (streaming). Full `ConversationTurn` persistence (user turns + assistant turns). `ContextAssembler` routes by query type per the existing design. Possibly a lightweight triage prompt to classify intent.
- Frontend: Chat panel becomes interactive — text input, streaming response rendering, conversation history. Panel is always visible (right rail on desktop, bottom drawer on mobile).
- Tests: Integration tests for the conversation endpoint (streaming, context routing). Eval scenarios for a few representative open-conversation prompts. Playwright: ask question → see grounded response.

**Key risks**

- Streaming response rendering in React + RTK Query — RTK Query isn't ideal for streams; may need raw fetch + state management for the chat panel alone.
- Context-assembler routing quality — the existing design mentions interaction-specific assembly; slice 4 is when that actually gets exercised in production flow (not just eval).

**Relevant research artifacts**

- `batch-4a-coaching-conversation-design.md` — open-conversation tone, intent classification, response patterns.
- `batch-4b-special-populations-safety.md` — keyword triggers for safety escalation (injury, crisis, medical scope).
- `batch-2c-testing-nondeterministic.md` — eval patterns for open-conversation quality.

---

## Architecture Additions

### Backend module layout after this cycle

```
backend/src/RunCoach.Api/
  Program.cs
  Modules/
    Identity/                   # NEW — Slice 0
      AuthController.cs
      JwtIssuer.cs
      UserRegistrationService.cs
      Entities/                 # ApplicationUser (Identity-extended)
    Coaching/                   # EXISTING — extended in Slice 1, 3, 4
      ClaudeCoachingLlm.cs      # existing
      ContextAssembler.cs       # existing, extended in Slice 2
      OnboardingController.cs   # NEW — Slice 1
      AdaptationService.cs      # NEW — Slice 3
      ConversationController.cs # NEW — Slice 4 (read-only in Slice 3)
      Prompts/                  # existing
        coaching-v1.yaml
        onboarding-v1.yaml      # NEW — Slice 1
        adaptation-v1.yaml      # NEW — Slice 3
    Training/                   # EXISTING — extended in Slice 1, 2, 3
      Computations/             # existing (PaceZoneIndex, PaceZone, HR)
      Plan/                     # NEW — Slice 1
        PlanAggregate.cs
        Events/                 # PlanGenerated, PlanAdaptedFromLog, …
        Projections/            # current-plan document projection
      WorkoutLog/               # NEW — Slice 2
        WorkoutLog.cs
        WorkoutLogRepository.cs
        WorkoutLogController.cs
    Common/                     # existing
      BaseController.cs
  Infrastructure/
    ServiceCollectionExtensions.cs  # existing, extended
    RunCoachDbContext.cs            # NEW — Slice 0
    MartenConfiguration.cs          # NEW — Slice 0
    JwtMiddleware.cs                # NEW — Slice 0
    ErrorHandlingMiddleware.cs      # NEW — Slice 0
  Migrations/                       # NEW — Slice 0 onward (EF Core)
```

### Frontend module layout after this cycle

```
frontend/src/app/
  modules/
    auth/                       # NEW — Slice 0
      pages/{LoginPage,RegisterPage}.tsx
      store/authSlice.ts
      hooks/useAuth.ts
    onboarding/                 # NEW — Slice 1
      pages/OnboardingPage.tsx
      components/{ChatFlow,ProgressIndicator}.tsx
    plan/                       # NEW — Slice 1
      pages/HomePage.tsx
      components/{TodayCard,ThisWeek,UpcomingList}.tsx
    logging/                    # NEW — Slice 2
      components/{LogForm,MoreDetailsExpander,HistoryList}.tsx
    coaching/                   # NEW — Slice 3 (read-only) → Slice 4 (interactive)
      components/{ChatPanel,MessageList,ChatInput}.tsx
      store/chatSlice.ts
    common/                     # existing
    app/                        # existing — extended with ChatPanel layout slot
  api/                          # NEW — Slice 0 onward
    apiSlice.ts                 # RTK Query root
    auth.api.ts
    onboarding.api.ts           # Slice 1
    plan.api.ts                 # Slice 1
    workoutLog.api.ts           # Slice 2
    conversation.api.ts         # Slice 4
  pages/                        # existing (home/) — replaced/extended
```

---

## Data Model

### Relational (EF Core)

- **`ApplicationUser`** — extends `IdentityUser`. Identity-managed. No custom cols in this cycle.
- **`UserProfile`** — 1:1 with `ApplicationUser`. Onboarding answers (primary goal, target event + date, current fitness assessment, weekly schedule, injury history, preferences). `OnboardingStatus` column tracks in-progress onboarding.
- **`WorkoutLog`** — FK to `ApplicationUser`, optional FK to `PlannedWorkoutId` (matches a prescribed workout in the Marten projection). Required: `Distance`, `Duration`, `CompletionStatus`, `LoggedAt`, `Notes`. Nullable: `Metrics` (JSONB — arbitrary shape; canonical keys in a shared constants file).
- **`ConversationTurn`** — FK to `ApplicationUser`, `Role` (user/assistant/system-adaptation), `Content`, `CreatedAt`, optional FK to triggering `PlanEventId` (adaptation explanations link to the event that caused them).

### Event-sourced (Marten)

- **`Plan` aggregate** per user.
  - Events (evolve across slices):
    - `PlanGenerated` (Slice 1) — initial plan from onboarding.
    - `PlanAdaptedFromLog` (Slice 3) — plan modification triggered by a workout log. Includes reason + modified workouts.
    - `PlanRestructuredFromConversation` (Slice 4 or later) — plan modification triggered by a chat turn (goal change, injury report). Might land later.
    - `PlanRegenerated` (Slice 1+) — user-triggered regeneration for iteration/correction.
  - Projection: current plan document (macro phase schedule + this-week meso template + active-day micro prescriptions) as a single JSON document for LLM context injection.

### Why this split

- EF Core owns mutable user-state entities (profile, logs, turns) — standard CRUD, relational joins, Identity integration.
- Marten owns the plan — the coaching decisions, adaptation history, and audit trail. Event stream IS the audit trail per DEC-031 and `memory-and-architecture.md`.
- Both run against the same Postgres instance; the ownership boundary is entity-level, not database-level.

---

## Testing Strategy

- **Backend integration tests**: `WebApplicationFactory` + Testcontainers (real Postgres, not in-memory) per `backend/CLAUDE.md`. Every controller gets an integration test; the full log-triggers-adaptation path in Slice 3 is the flagship integration test.
- **Backend unit tests**: services, repositories, `ContextAssembler` extensions, computation extensions. Existing patterns hold.
- **Eval suite**: existing M.E.AI.Evaluation infrastructure. Extended in Slice 1 (onboarding scenarios — verify plan quality across profile types), Slice 3 (adaptation scenarios — absorb/nudge/restructure, freeform-notes interpretation), Slice 4 (open-conversation scenarios — coaching quality, safety). CI runs replay-only with committed fixtures per existing convention.
- **Frontend component tests**: Vitest + React Testing Library for significant components (forms, cards, chat panel).
- **Frontend E2E**: Playwright. One happy-path scenario per slice covering the "I can…" criteria end-to-end. Goal: "did this feature work end-to-end" — not exhaustive edge coverage.
- **Coverage**: maintain the existing 60% project / 70% patch Codecov thresholds. No new thresholds.

---

## Roadmapping Hygiene

This cycle introduces a three-tier roadmapping structure to stop `ROADMAP.md` from growing unboundedly and to make session-start catchup fast.

### Tier 1 — `ROADMAP.md` (front door)

Compacted to:

1. **Status block at top** — current cycle, active slice, next step, blockers, pointer to cycle plan. 10-15 lines max. Mirrored from this doc's Status section.
2. **Strategic links** — decision log, feature backlog, vision docs, forward-path items.
3. **Cycle History** — one-line-per-cycle log at the bottom. Each entry: cycle name, completion date, pointer to the cycle plan + key artifacts (PRs, specs, decisions). No narrative.

The 200-line "What's Been Done" narrative currently in `ROADMAP.md` moves out — decisions are already in `decision-log.md`, implementation details are in completed plan files under `docs/plans/`, git log holds the rest. `ROADMAP.md` is a status-first document, not a history document.

### Tier 2 — Cycle Plan (this doc)

Lives for the cycle duration at `docs/plans/{cycle-name}/cycle-plan.md`. Declares the active slice. Tracks slice acceptance criteria with checkboxes. When the cycle completes, it becomes a historical artifact (referenced from `ROADMAP.md` Cycle History).

### Tier 3 — Per-Slice Spec + Tasks

Each slice moves through a conceptual pipeline. The durable parts are the **artifact shapes and locations** and the **ordering discipline** — not the specific tools used to produce them. Pick the skills that fit the moment; the structure below is what the docs commit to.

1. **(Optional) Preliminary codebase research** — only when the slice's requirements doc + the cycle-plan Slice N section + the named research artifacts don't give enough codebase-grounded context. Output lives under `docs/specs/research-{topic}/`.
2. **Spec** — inputs: the slice's requirements doc (`./slice-N-{name}.md`), this cycle plan's Slice N section, and any relevant research artifacts. Output: `docs/specs/{NN}-spec-{topic}/spec.md` with demoable units, acceptance criteria, and proof-artifact definitions. Existing project precedent: see `docs/specs/05-spec-*` through `09-spec-*`.
3. **Task decomposition** — break the spec into dependency-aware tasks on a task board (or equivalent tracker). Independent tasks are marked for parallel execution.
4. **Execution** — tasks are implemented, tested, and committed per the project's normal conventions (see `backend/CLAUDE.md`, `frontend/CLAUDE.md`).
5. **Validation** — coverage matrix against the spec's acceptance criteria, then code review, before merge.

The `docs/specs/{NN}-spec-{topic}/` convention was adopted from the `claude-workflow` plugin's output shape — that attribution is the only reason the plugin is named here. How each step is actually executed (which skill, which tool, whether a skill at all) is a per-slice judgment call. A small slice can use a hand-written spec and skip the task board; a larger one benefits from more structure. Don't encode the tool choice in these docs.

DEC-008 plan-first rule applies — no slice implementation until the spec has been reviewed. The requirements doc at `./slice-N-{name}.md` is durable across implementation churn; the spec under `docs/specs/` is allowed to churn as implementation reveals detail.

### Per-Slice Hygiene Rule

Each slice's "done" criteria include:

- [ ] Slice acceptance checkboxes in this cycle plan marked complete.
- [ ] Cycle plan's Status section updated: active slice advanced to the next one (or "Cycle complete" if this was the last).
- [ ] `ROADMAP.md` Status block synced from this doc.
- [ ] Follow-ups discovered during the slice captured in the **Captured During Cycle** section. The slice PR description must include a `### Follow-ups found` checklist (empty is fine; omission is not).
- [ ] If the slice produced durable architecture decisions, record them in `docs/decisions/decision-log.md`.
- [ ] Completed slice spec directory under `docs/specs/` stays in place (historical reference) — not deleted, not moved.

### `/catchup` Update

`.claude/commands/catchup.md` updated to walk the new tiers:

1. `ROADMAP.md` Status block — current cycle + active slice pointers.
2. The active cycle plan (path from Status block) — slice structure and progress.
3. The active slice spec under `docs/specs/` if one exists (path from cycle plan Active Slice).
4. Last 5-10 commits on current branch.
5. Working-tree changes vs. `main` (if any).

Summarize in 3-5 sentences: current cycle, active slice, last shipped work, recommended next action.

### Tracking shape

Per-slice work is tracked on a task board (dependency-aware, atomic tasks) with the slice spec under `docs/specs/` holding the connected reasoning. GitHub Issues remain available for cross-cycle items (bugs found in main, cross-repo coordination) but are not the primary per-slice tracker. Revisit at the slice-0/slice-1 boundary if the granularity proves insufficient.

---

## When Agents Encounter Unknowns

The baseline rule lives in the project-root `CLAUDE.md` § Research Protocol and applies to every agent session, not just this cycle. **Never guess at implementation.** This section adds cycle-specific affordances.

### Prompt template

When writing a deep-research prompt, follow the existing `docs/research/prompts/batch-*.md` format:

```markdown
# R-XXX: {Topic}

## Context
{why this question came up — slice N scope, specific decision being blocked}

## Research Question
{primary question + 2-5 sub-questions that would make the answer actionable}

## Why It Matters
{what this unblocks, what happens if we get it wrong}

## Deliverables
- Concrete recommendation with rationale
- Alternatives considered and why rejected
- Library/tool version pins if applicable
- Gotchas, security implications, version compatibility notes
```

### Handoff protocol

1. Write the prompt at `docs/research/prompts/{filename}.md`.
2. Add the entry to `docs/research/research-queue.md` following the existing table format (next `R-XXX` number, Status = `Queued`, Artifact = `(pending)`).
3. Return control to the user: *"I encountered X in slice N, needs research before I proceed. Prompt at `docs/research/prompts/{file}.md`. Please run it in a separate research agent and provide the artifact."*
4. Wait for the artifact to land at `docs/research/artifacts/{file}.md`.
5. Integrate findings into the relevant planning doc, decision-log entry, or active slice spec before resuming implementation.

### Unknowns likely to surface in this cycle

Pre-flagged so agents know these are explicit research triggers, not "pick one and see." This is not a commitment to research all of them — some may be resolved by existing research once an agent reads it. The list exists so "guess and move on" is never the default for these topics.

- **Marten event-sourcing patterns for a per-user plan aggregate** — stream-per-user vs. stream-per-plan, projection update strategy (inline vs. async), snapshot frequency. `batch-2b-planning-architecture.md` has the conceptual model; implementation-time Marten conventions may need a dedicated research pass.
- **RTK Query streaming patterns for LLM responses** — RTK Query's caching model isn't a great fit for streams. Slice 4's chat panel will need either raw fetch + manual state or an alternative library; not obvious which.
- **JWT rotation / refresh-token strategy for long-lived personal-use sessions** — Slice 0 scope decision. Existing research covers CI/quality gates, not client-side token lifecycle.
- **PostgreSQL JSONB query patterns for `WorkoutLog.Metrics`** — Slice 2 stores heterogeneous metric shapes. If any downstream slice needs to query by metric value (e.g., "show me all runs where HR avg > 150"), indexing and query strategy is non-obvious — GIN vs. expression indexes, search performance at scale.
- **Onboarding conversation-state persistence** — Slice 1 multi-turn onboarding needs a state model. Whether in-progress state lives in a column, in the Marten stream, or in the client is a real architectural question.

---

## Pre-Slice-0 Housekeeping

Before Slice 0 proper begins, a small housekeeping pass:

- [ ] Compact `ROADMAP.md` to the new shape (status block + strategic links + Cycle History).
- [ ] Move historical "What's Been Done" narrative out of `ROADMAP.md` — the detail is preserved in `decision-log.md`, existing plan files, git log, and merged PRs. `ROADMAP.md` should not duplicate it.
- [ ] Update `.claude/commands/catchup.md` to the new walk order.
- [ ] Verify `/catchup` on a fresh session and confirm the summary hits "where are we?" in one scan.
- [ ] Commit as a single atomic commit: `chore(roadmap): compact ROADMAP.md and /catchup for slice-based cycle tracking`.

This is a tiny pass — probably 30-60 minutes. It lands before the Slice 0 plan file is written so the new flow is in place when Slice 0 starts.

---

## Forward Path — Carried-Forward Learnings from the Brain

This cycle is built on top of real work that's already landed. Preserved patterns:

- **Deterministic + LLM split** — every slice respects the layering. Deterministic: plan structure, compliance, pace/HR zone computation, ACWR. LLM: coaching reasoning, adaptation narrative, open conversation. Never the other way around.
- **Prompts in versioned YAML** — `onboarding-v1.yaml` and `adaptation-v1.yaml` follow the `coaching-v1.yaml` pattern. Existing `YamlPromptStore` is the vehicle.
- **Constrained decoding via `AnthropicStructuredOutputClient`** — onboarding turns, plan generation, and adaptation all use structured output. Per the DEC-042 lesson: design invariants structurally, don't rely on `[Description]` hints.
- **Eval-first for LLM changes** — adaptation prompt changes are eval-gated. CI replay-only. `EVAL_CACHE_MODE` discipline preserved.
- **`ContextAssembler` as the central context-assembly primitive** — every new prompt invocation goes through it. Extensions for workout logs + conversation history happen here.
- **Floating model aliases** per DEC-037 — `claude-sonnet-4-6` for coaching, `claude-haiku-4-5` for judging. No hard-coded model IDs in application code.
- **Trademark discipline** per `CLAUDE.md` — user-facing text uses "Daniels-Gilbert zones" / "pace-zone index." Onboarding prompt, adaptation prompt, chat responses all subject to this.

---

## References

### Planning & Decisions

- `docs/planning/vision-and-principles.md` — why this exists, design principles.
- `docs/planning/interaction-model.md` — three interaction modes.
- `docs/planning/planning-architecture.md` — macro/meso/micro tiers, event-sourced plan state.
- `docs/planning/memory-and-architecture.md` — context injection strategy, five-layer summarization.
- `docs/planning/safety-and-legal.md` — safety guardrails (pre-public-release items live here).
- `docs/decisions/decision-log.md` — all architectural decisions (DEC-001 through DEC-044).
- `docs/features/backlog.md` — feature backlog by priority tier.
- `backend/CLAUDE.md` — backend conventions, module-first organization, testing patterns.
- `frontend/CLAUDE.md` — frontend conventions.

### Cross-Cutting Research Artifacts

Apply throughout this cycle — read these before starting any slice, not just the one you're working on. Per-slice artifacts live in each slice's "Relevant research artifacts" subsection.

- `batch-1-claude-code-workflow.md` — agent workflow patterns, context management.
- `batch-7b-anthropic-model-ids-versioning.md` — floating model aliases, DEC-037.
- `batch-10a-frontend-latest-practices.md` — React 19 + TS + Vite + Tailwind conventions.
- `batch-10b-dotnet-backend-review-practices.md` — .NET 10 conventions, anti-patterns.
- `batch-10c-ci-quality-gates-private-repo.md` — CI strategy.
- `batch-6a-llm-eval-strategies.md` — eval patterns applied cycle-wide.
- `batch-7a-ichatclient-structured-output-bridge.md` — structured-output bridge pattern used by every LLM call in this cycle.

### Research Queue & Prompts

- `docs/research/research-queue.md` — full queue of topics (R-001 through R-039), status, and artifact pointers.
- `docs/research/prompts/` — deep-research prompts. Follow this format when writing new ones (see [When Agents Encounter Unknowns](#when-agents-encounter-unknowns)).
- `docs/research/artifacts/` — completed research artifacts.

### Historical

- POC 1 plan: `docs/plans/poc-1-context-injection-plan-quality.md`.
- POC 1 LLM testing plan: `docs/plans/poc-1-llm-testing-architecture.md`.
