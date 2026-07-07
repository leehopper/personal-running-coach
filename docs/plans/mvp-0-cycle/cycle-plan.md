# MVP-0 + Adaptation Loop — Build Cycle Plan

> **Status:** Approved (2026-04-19). Active cycle.

## Status

- **Current Cycle:** MVP-0 + Adaptation Loop
- **Active Slice:** None — **Slice 4C completed 2026-07-07, closing Slice 4 (Open Conversation) and the cycle's build scope.** All build slices are merged.
- **Slice ledger** (one line each; close-out detail in the per-slice sections below and `ROADMAP.md` § Cycle History; decision rationale in `docs/decisions/decision-log.md`):
  - **Slice 0 — Foundation:** merged 2026-04-23 (PRs #49 / #50 / #63; DEC-048–056).
  - **Slice 1 — Onboarding → Plan:** shipped 2026-04-26 (DEC-057–064).
  - **Slice 1B — Pre-Slice-2 Hardening:** merged 2026-05-15 (PRs #91–#94; DEC-066–069).
  - **Slice 2a — Frontend Visual Foundation:** merged 2026-05-30 (PRs #111–#114; DEC-070) — § Slice 2a post-merge verification.
  - **Slice 2b — Workout Logging:** merged 2026-06-07 (PRs #134 / #136 / #145 / #147 / #151 / #154 / #156 / #158; DEC-072–077) — § Slice 2b post-merge verification.
  - **Slice 3 — Adaptation Loop:** code-complete + live-verified 2026-06-11 (PRs #161 / #163 / #165 / #167 / #169 / #172 / #174; DEC-078–080).
  - **Slice 3B — Live-Pass Fixes:** closed 2026-06-14 (PRs #185 / #187 / #190 / #192 / #194; DEC-081–083); the fresh live pass PASSED 2026-06-13.
  - **Slice 4A — Coaching Voice Re-tune:** complete 2026-06-23 (PRs #199 / #206 / #207 / #209 / #211 / #213; DEC-084) — § Slice 4A completion.
  - **Slice 4B — Streaming Conversation Core:** complete 2026-07-01 (PRs #219 / #226 / #230 / #231 / #233 / #235 / #237 / #239; DEC-085) — § Slice 4B completion.
  - **Slice 4C — Onboarding Redesign + km/miles Units:** complete 2026-07-07 (4C-units PRs #242 / #244 / #243 / #252 / #253 / #255; 4C-onboarding PRs A–E #259 / #261 / #263 / #265 / #267; DEC-086) — § Slice 4C completion.
- **Active Slice Spec:** None. Completed spec directories stay under `docs/specs/` (gitignored, working-tree-only) per the Per-Slice Hygiene Rule; slice designs/requirements stay in this directory (`slice-*.md`).
- **Next Step:** The **MVP-0 live end-to-end validation pass re-run** (`ROADMAP.md` § MVP Milestones — protocol, host-run stack recipe, funded-key setup). It is the cycle's remaining close-out gate: the full onboard (now form-first) → plan → log → adapt → converse loop against live Sonnet, fresh account, real browser, verified at the UI surface and in the persisted event store. After it passes: triage § Captured During Cycle (every ledger row gets a disposition) and close the cycle.
- **Blockers:** None. Open non-blocking follow-ups: § Captured During Cycle → "Open follow-ups".

This status block is the single source of truth for "where are we?" — mirrored into `ROADMAP.md` so `/catchup` finds it. Update both whenever a slice completes or the active slice changes. **Replace, don't append:** when a slice completes, collapse its Status entry to a one-line ledger row — the narrative moves to a per-slice completion section below, a `ROADMAP.md` Cycle History row, and the decision log. Slice history never accumulates in this section.

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

### Slice 4A — completion

Slice 4A re-tuned all three active LLM prompts (`onboarding-v1`, `coaching-system.v1`, `adaptation.v1`) plus `coaching-persona.md` to a gruff-direct register across six build PRs (#199 / #206 / #207 / #209 / #211 / #213, 2026-06-18 → 2026-06-23). Close-out status — **complete, no tuning rounds needed**:

- [x] All six build PRs (PR1 scaffolding / PR2 persona / PR3 onboarding / PR4 coaching-system / PR5 adaptation / PR6 onboarding eval) shipped; deterministic `VoiceProseGuard` hard gate + advisory `VoiceRubrics.Restraint` judge wired into plan-gen, adaptation, and onboarding evals; DEC-074 manifest regenerated and affected fixtures re-recorded against a funded key per surface.
- [x] **D4 tuning rounds NOT triggered** — the recorded gruff-direct register was adversarially verified (per-surface assessor + blind challenger, 2026-06-23) to read right on all three surfaces: onboarding (Pattern-B clarifier turns), plan-gen (`coaching-system.v1`: James narrative + 4 profiles), adaptation (Level-2 restructure: lee + priya). All three verdicts survived their blind challenges unrefuted.
- [x] Advisory restraint judge scores: plan-gen 5/5 and adaptation 2/2 at 1.0; the only sub-1.0 (onboarding 0.0) is a confirmed rubric-applicability artifact (rationale/forward-path criteria don't fit a one-line clarifier), not a register failure.
- [x] Deterministic em-dash / exclamation / banned-phrase guards green; **full Coaching eval suite 176/176 in Replay (0 failed, 0 skipped)**.
- [x] KEPT-VERBATIM invariants confirmed intact against the live YAMLs (crisis lines, body/weight/shape/food-labeling ban, rest-as-investment, Medical Boundary, Injury Protocol incl. the "push through" ban, the F4 CURRENT-WEEK CONSISTENCY / GATE-BEFORE-INCREASE guardrails); only the intended D1/D2 register lines were deleted.
- [x] DEC-084 recorded (gruff-direct register + advisory-judge posture + safety lock); Slice 4A marked complete in this Status block and `ROADMAP.md`, with a Cycle History row added.
- [ ] Two non-blocking opportunistic polish items carried forward (see Captured During Cycle, 2026-06-23): the `lee` adaptation "without guilt/concern" softeners; scoping the onboarding restraint rubric to register-only criteria.

### Slice 4B — completion

Slice 4B shipped the streaming conversation core (Q&A + conversational logging, DEC-085 D1–D4) across 7 stacked PRs (#219 / #226 / #230 / #231 / #233 / #235 / #237 / #239, 2026-06-25 → 2026-07-01). Close-out status — **complete**:

- [x] All 7 PRs shipped in dependency order: PR1 streaming adapter (Unit 1, `ICoachingLlm.StreamAsync` + `IncompleteCoachingLlmException`), PR2 user-scoped `Conversation` stream + `/timeline` read (Unit 3), PR3a per-call model override, PR3b intent classifier + conversation context assembly (Unit 4), PR4 the SSE integration gate `POST /api/v1/conversation/messages`, PR5 confirm-then-commit `POST /api/v1/conversation/logs/confirm`, PR6 the frontend streaming UX (`useCoachStream` + `CoachChat` + `LogConfirmationCard`), PR7 the streaming-conversation + classifier-accuracy eval suite.
- [x] DEC-085 records the locked conversation-core decisions (D1–D4) + the streaming-posture corrections + the errored-turn `IncompleteCoachingLlmException` contract; the PR3a correction (no `Temperature 0` — determinism rests on constrained decoding) is folded in.
- [x] PR7's eval suite closes the slice's DEC-074 manifest + classifier-accuracy loop: deterministic chat-safety gating (`ChatSafetyEvalTests`, always-on Red/Amber classification + scripted-content assertions, one funded-key-recorded LLM answer-alongside-referral case), the conversation-answer voice/trademark hard gate (`ConversationAnswerVoiceEvalTests`, three scenarios), and the intent-classifier zero-regression theory + 3x3 confusion matrix with the DEC-085 dangerous-cell hard gate (`IntentClassificationEvalTests` / `IntentConfusionMatrix`). Test-only PR, no production code changed; full backend suite 2041/2041 in Replay.
- [x] Slice 4B marked complete in this Status block and `ROADMAP.md`, with a Cycle History row added.
- [ ] Two non-blocking carryovers remain open (see Captured During Cycle): the `SeedActivePlanAsync` `ITenanted` tenancy-filter test-harness gap (surfaced PR4 #233, still open after PR7 — eval-only, didn't touch the SSE integration-test seed) and the orphaned `ConversationPanel`/`getConversationTurns` read chain (surfaced PR6 #237, dead-code removal deferred pending a decision on whether a Slice-3-style panel will be re-mounted). Both carry into 4C triage.
- [x] The MVP-0 live end-to-end validation pass re-run (`ROADMAP.md` § MVP Milestones) is now **due** — 4C completed 2026-07-07, so all of Slice 4 (4A+4B+4C) has shipped the open-conversation loop. It is the next action (see § Slice 4C — completion).

### Slice 4C — completion

Slice 4C shipped in two parts: **4C-units** (frontend-display-only km/miles, 6 PRs #242 / #244 / #243 / #252 / #253 / #255, 2026-07-02→2026-07-03) and **4C-onboarding** (deterministic form-first intake retiring the conversational path, 5 PRs A–E #259 / #261 / #263 / #265 / #267, 2026-07-05→2026-07-07). Close-out status — **complete**:

- [x] **4C-units** (all 6 PRs merged): a dedicated `UserSettings` store + `GET`/`PUT /api/v1/settings/units`, the shared `unit-format` module (distance + net-new inverse pace + range formatters), a Settings units toggle, and miles↔km conversion wired at every plan/logging/adaptation render site + the log-write input site — storage + coaching prompt stay km-native (DEC-086 D6); amends DEC-041's imperial-display phasing while keeping the `PreferredUnits{Kilometers,Miles}` enum.
- [x] **4C-onboarding** (all 5 PRs A–E merged): PR-A (#259) fixed a latent shared-per-user-Marten-stream bootstrap collision (`StartStreamOrAppendAsync`) + moved the on-plan-card seed to the onboarding event stream; PR-B (#261) deleted the orphaned `ConversationPanel`/`getConversationTurns` read chain; PR-C (#263) added the deterministic `POST /api/v1/onboarding/answers` origination endpoint (no onboarding-time LLM call); PR-D (#265) shipped the single-page form UI; PR-E (#267) hard-cutover-deleted the turn endpoint/handler + extraction stack and relocated `AnthropicSchemaSanitizer` into `Modules/Coaching/`. The two 4B carryovers (the `SeedActivePlanAsync` tenancy seam; the orphaned conversation read chain) were resolved by PR-A and PR-B respectively.
- [x] Slice 4C marked complete in this Status block and `ROADMAP.md`, with a Cycle History row added. Each 4C-onboarding PR ran the per-PR quality cycle (deep-review Opus frontier + full CodeRabbit addressment before squash-merge); PR-E's Frontier deep-review returned clean bar one Improvement-Suggestion follow-up (S-01, below).
- [x] **With 4A+4B+4C all shipped, Slice 4 (Open Conversation) is COMPLETE** — the MVP-0 live end-to-end validation pass re-run (`ROADMAP.md` § MVP Milestones) is now **ungated and is the next action**.
- [ ] Non-blocking follow-ups remain open in § Captured During Cycle: the `AssembleAsync` legacy-island eval cleanup (2026-07-01); the cross-cutting expected-version-concurrency decision for the surviving handlers (concurrency finding #1, PR-C row; post-PR-E the pair is `SubmitStructuredAnswersHandler` + `PostUserConversationTurnHandler`) + the **S-01** concurrent-first-submission test-coverage gap it now shares (PR-E row); the DEC-047 shared-stream-vs-re-separate ratification (PR-A row); the timeline-schema contract tripwire (PR-B row); and the sibling EF-seed test-fixture port (PR-A row).

---

## Captured During Cycle

Running log of "we should also do this" items found during the cycle but intentionally deferred — preserves the affordance the old `ROADMAP.md` Deferred Items section had, scoped to the active cycle so the list doesn't grow unboundedly.

**How to use**

- Any agent (or human) may append an entry when finding work that shouldn't block the current slice but shouldn't be lost.
- Keep the **Open follow-ups** digest below in sync: add a bullet when appending a row that stays open; delete the bullet when the row resolves or gets a final disposition.
- Each slice's PR description includes a `### Follow-ups found` section (empty is fine); items move into this table at slice completion.
- At cycle completion, every entry gets one of four dispositions:
  - (a) promoted to `docs/features/backlog.md`,
  - (b) becomes its own `docs/decisions/decision-log.md` entry,
  - (c) becomes a research prompt (see [When Agents Encounter Unknowns](#when-agents-encounter-unknowns)),
  - (d) scheduled into the next cycle.
- The table does not survive cycle completion un-triaged.

### Open follow-ups (as of 2026-07-07)

The distilled open list for session catchup. Everything else in the full ledger below is resolved or carries a triage disposition — consult the ledger for history and context, not for "what's open."

- **`AssembleAsync` legacy-island eval cleanup** (2026-07-01 row) — zero production callers, but ~90 `ContextAssemblerTests` + 3 `EvalTestBase` helpers route the coaching-system-voice / safety-boundary / logged-workout-context evals through it. Own scoped cleanup: re-point or retire those evals, then delete the island; likely a DEC-074 manifest regen + funded-key fixture re-records. Needs its own DEC + spec.
- **Expected-version concurrency decision for both surviving stream handlers + the S-01 race-coverage gap** (2026-07-06 PR-C + 2026-07-07 PR-E rows) — `SubmitStructuredAnswersHandler` and `PostUserConversationTurnHandler` have no Marten optimistic-concurrency gate (concurrent submissions with *different* idempotency keys can double plan-gen; sketched shape: `ConcurrencyException → 409`), and PR-E's deletion of `OnboardingTurnConcurrencyIntegrationTests` left the shared-stream first-write race untested. Fold a staged-then-raced integration test into whichever work makes the concurrency decision.
- **DEC-047 shared-stream ratification** (2026-07-05 PR-A row) — onboarding + conversation share one physical per-user Marten stream, an emergent divergence from DEC-047's separate `DeterministicGuid(userId, "onboarding")` stream. Ratify the merged stream or re-separate (a projection-identity migration if re-separating).
- **Timeline-schema contract tripwire** (2026-07-05 PR-B row) — deleting `conversation.model.spec.ts` removed the only codegen drift guard for the still-live shared turn enums; add a fully-populated `ConversationTimelineDto` fixture parsed against the generated timeline Zod schema.
- **Sibling EF-seed test-fixture port** (2026-07-05 PR-A row) — `ConfirmConversationalLogEndpointIntegrationTests` + `ConversationControllerIntegrationTests` still EF-insert the profile directly; port to the event-sourced onboarding seed as a fast-follow.
- **Slice 4A voice polish, opportunistic** (2026-06-23 rows) — nudge the two `lee` adaptation "without guilt/concern" softeners toward the `priya` framing on the next deliberate adaptation re-record; scope the onboarding restraint rubric to register-only criteria (test-only).
- **Cycle-completion triage** — every ledger row below gets one of the four dispositions before the cycle closes.

### Full ledger

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
| 2026-06-12 | Slice 3B (F3) | **Regenerate-plan path lacks the F3 error envelope.** `GeneratePlanAsync` is shared by `RegeneratePlanHandler` (Settings → Plan) and now throws `PlanGenerationRejectedException` on a horizon/phase-sum-violating macro, but only the onboarding completion controller maps it to an HTTP-200 `Kind=Error` envelope — the regenerate path surfaces it as a generic 500. Low frequency at MVP-0 (regeneration requires an existing plan + race date mismatch); not in the live-pass finding. (F3 / Slice 3B.) | **Deferred — backlog.** Structural fix: add the `PlanGenerationRejectedException` → `Kind=Error` map in `PlanRenderingController.Regenerate`, consistent with DEC-073/DEC-080. See also DEC-082 open/deferred section. |
| 2026-06-12 | Slice 3B (F3) | **OTel plan-generation metric conflates expected rejection with transport failure.** The `runcoach.plan.generation.completed` metric tags `PlanGenerationRejectedException` as `outcome=failure` identically to a transport/SDK fault; consider a distinct `outcome=rejected` (or a `violation` tag) so dashboards can separate model-quality rejections from infrastructure failures. (F3 / Slice 3B.) | **Deferred — backlog.** Low priority until the metric sees meaningful volume; the change is additive (new tag value) and does not affect any current alert or gate. |
| 2026-06-13 | Slice 3B (F4, PR #192 deep-review) | **Restructure schema `[Description]` lags the F4 gate (finding #4).** `RestructurePlan.RevisedWeeklyTargets` still reads "for upcoming meso weeks … may be empty when only the current week changes," injected into `AdaptationSchema.Frozen` — it contradicts the gate's dependence on a current-week target. The `adaptation.v1.yaml` CURRENT-WEEK CONSISTENCY block is authoritative and reliably produces the current-week target (both committed fixtures carry it: lee 30, priya 46), so the gate is exercised, not silently skipped; the risk is a future model following the stale description and omitting the target (→ `NotApplicable` pass). | **Deferred — DEC-083 open/deferred.** Aligning the `[Description]` busts the adaptation eval cache key (funded-key re-record). A trial re-record on #192 produced a live-Sonnet output that was itself inconsistent (priya target 46 vs week 58), so the re-record must be **curated** (record until a consistent output lands), not blind — folded into the next deliberate adaptation re-record (the slice's live-pass done-gate). |
| 2026-06-13 | Slice 3B (F4, PR #192 deep-review) | **No removal / `Rest` workout semantic (finding #6).** The sparse-upsert restructure model cannot express dropping a run day to rest — there is no `Rest` `WorkoutType` and `RestructureDiffCalculator` never emits a removal (null `After`). A restructure that omits a day and lowers the target is summed against the carried-over (un-dropped) week by the F4 gate and terminally rejected. | **Accepted MVP-0 limitation — DEC-083 open/deferred.** Removal is a spec non-goal (the diff calculator documents "a removal is never produced"); the gate correctly rejects an un-applyable shorten-by-omission rather than persisting a contradictory plan. Revisit with an explicit rest/removal marker if it surfaces in live use. |
| 2026-06-13 | Slice 3B (live-pass re-run, user-observed) | **Coaching persona far too cheery / cheesy — spans ALL LLM output, not just onboarding.** Onboarding gushed ("Love it" opened two consecutive turns; "Love that target", "Great foundation"); the **adaptation/restructure rationale** does the same — concrete example the user flagged: *"Three tough runs in a row ending in bailing on the threshold halfway through — that takes honesty to acknowledge, and it matters."* The target user wants a gruffer, more intense, no-nonsense coach — warmth via competence and directness, not enthusiasm, praise, or emotional validation. This is a persona-voice problem across onboarding + plan-generation narrative + adaptation rationale, not a single-prompt bug. **Scope note: this is about LLM *output*, not the UI.** | **Scheduled into Slice 4 (conversation/persona work); the dedicated full-prompt evaluation (see roadmap) feeds it; update `docs/planning/coaching-persona.md` first.** Re-tune every system prompt (onboarding, plan-gen, `adaptation.v1.yaml`) toward a restrained, direct register; add eval/judge scenarios that penalize sycophancy, filler enthusiasm, and emotional-validation language. The dominant user-facing note of the re-run. **VOICE SPEC LOCKED 2026-06-13 (builder input via targeted Q&A):** (1) **register = gruff & direct** — blunt, short sentences, warmth shown via straight talk + competence, no praise and no validation/feelings opener; (2) **keep non-negotiable:** safety/medical/crisis boundaries AND no-toxic-culture clichés ("no days off" / "pain is temporary" / "push through"); the body/weight/food-labeling guardrail is **retained under the safety cluster** (eating-disorder safety, not a warmth mandate — builder did not mark it keep in the multi-select but it should not be dropped silently; confirm explicitly before removal); the builder did NOT require keeping "no guilt / no miss-counting," so pointed accountability (e.g. naming a missed session factually) is allowed — but as accountability, not shaming; (3) **MI scaffolding:** keep the structural spine (always give a rationale, offer a real choice when one exists, show the forward path) but DELETE mandatory OARS affirmation, process-praise, and "acknowledge feelings first"; (4) **output:** tighter — cut filler/validation, keep the physiological "why" (the rationale is product value); (5) **style rules:** ban em dashes and filler enthusiasm/exclamation/sycophancy across `onboarding-v1` + `coaching-system` + `adaptation.v1` (the prompts are themselves em-dash-heavy, which the model mirrors — rewrite them em-dash-free too). Implementation note: the rewrite busts the DEC-074 prompt-hash manifest + eval cache, so bundle it with the eval re-records + Haiku-judge scenario updates (penalize sycophancy/validation/em-dashes). Root drivers identified in the three active prompts: the "80/20 warmth-to-directness" dial, the OARS "Affirmation" + "process praise" mandates, and the rationale step-1 "Validate what happened". **Slice 4 must plan SEVERAL test → tune → re-record rounds for the voice** — it is subjective and a single rewrite will not land it: iterate live against fresh accounts, adjust the prompts, and re-record eval fixtures + Haiku-judge scenarios each round until the gruff-direct register reads right to the builder. Per-file lever plan (apply at rewrite time): `coaching-system.v1` — flip the L13 80/20 dial, delete L25 process-praise + L23 acknowledge-feelings-first, make OARS Affirmation non-mandatory, add a STYLE block (no em dashes, no exclamation, no opening affirmations, short sentences), keep all SAFETY + anti-toxic + body/food NEVER lines verbatim; `onboarding-v1` — same dial flip + drop L66 acknowledge-feelings + same STYLE block (kills "Love it!"/"Great foundation!"); `adaptation.v1` — rewrite the L73–82 RATIONALE shape (drop step-1 "Validate what happened"; new shape = name the data pattern → state the change → the why → the path back), flip the L78 voice rule, leave the CURRENT-WEEK CONSISTENCY / GATE-BEFORE-INCREASE F4 logic untouched. Distinct from — and to be planned alongside — the builder's pre-MVP **visual UI refactor** (ROADMAP § Deferred Items); both are builder-driven taste work but this row is LLM *output* and that is the UI. |
| 2026-06-13 | Slice 3B (live-pass re-run, user-observed) | **Prompt should ban the em dash.** LLM prose is littered with em dashes (a tell + not the desired voice). Wanted as an explicit style rule in the coaching/onboarding prompts. | **Scheduled into Slice 4 (prompt-style pass, bundled with the persona-tone re-tune above).** Add a "never use em dashes" directive to the shared prompt style block; consider a deterministic post-generation scrub (like the F2 `TrademarkScrubber`) if the model ignores the instruction. |
| 2026-06-13 | Slice 3B (live-pass re-run, user-observed) | **Onboarding is too prose/chat-driven.** The full six-topic flow as back-and-forth free text feels heavy. Preference: a structured starting form for the known fields (goal, event+date, weekly volume, days, duration, units, injuries) with a free-text box per area for nuance/variance the form can't capture, rather than eliciting everything conversationally. | **Scheduled into Slice 4 (onboarding UX redesign) — larger than a prompt change.** Form-first hybrid with optional LLM free-text for nuance; would also sidestep the slot-merge loop (the 2026-06-11 finding) by collecting structured slots directly. Likely a research-prompt item before building. |
| 2026-06-13 | Slice 3B (live-pass re-run, user-observed) | **Unit flexibility not communicated.** The coach asked km-vs-miles only at the very end (Preferences) despite speaking in km the entire flow. The user should be told up front they can speak in either unit and the coach will adapt/normalize. | **Scheduled into Slice 4 (onboarding/prompt pass).** Surface the "talk in km or miles, I'll convert" affordance early (intro copy and/or prompt), and have the model accept either unit in free-text answers from the first turn rather than deferring unit choice to the end. |
| 2026-06-22 | Slice 4A (PR3 #207) | **No onboarding eval exists — the 4A implementation plan's PR3 (Task 4) assumed one.** Task 4 steps 3–4 call for wiring `VoiceProseGuard.AssertClean` into "the onboarding eval" and re-recording "onboarding fixtures" against a funded key. Verified at build time: no eval under `tests/…/Eval/` exercises `onboarding-v1` (the `Eval/` suite is plan-gen / safety-boundary / logged-workout / adaptation only; `PlanGenerationEvalTests` references `OnboardingView` solely as a dated-race *data struct*), and the 20 sonnet + 7 haiku recorded scenarios contain no onboarding fixture. So PR3's prompt rewrite needed **neither a funded key nor a fixture re-record** — editing `onboarding-v1.yaml` only busts the DEC-074 hash manifest (regenerated in the same commit; the `EvalTestBase` static-ctor backstop requires it), and no existing scenario's cache key changed (those derive from `coaching-system.v1` / `adaptation.v1`). Net: the onboarding gruff-direct rewrite ships verified by `OnboardingPromptTests` (structural) + the human tuning rounds only — its **LLM output has zero automated voice coverage**, on the exact surface where the 2026-06-13 live pass observed "Love it!" / "Great foundation!". | **PR3 shipped minimal-but-correct (rewrite + manifest, no key); onboarding voice eval now scheduled as 4A Task 7 / PR6** (build-it-right call, builder-blessed 2026-06-22). Concrete plan in `docs/superpowers/plans/2026-06-17-slice-4a-voice-retune.md` § Task 7: a new `OnboardingVoiceEvalTests` mirroring `AdaptationRestructureEvalTests` — render the real `onboarding-v1` template with **fixed nonces** (byte-stable cache key) + a representative onboarding turn → cached Sonnet `OnboardingTurnOutput` → `VoiceProseGuard.AssertClean` (hard) + advisory `VoiceRubrics.Restraint` judge → committed fixture. Built in the **same funded-key Record session as PR4/PR5** (one run covers all surfaces); no prompt/manifest change. NB the plan deliberately does *not* go through `ComposeForOnboardingAsync` (random per-call nonces bust the cache key, and the eval assembler's null sanitizer would throw). This eval + the PR4/PR5 guard/judge wiring is the "dedicated full-prompt evaluation" this row references — that pointer is dangling (no separate `ROADMAP.md` item exists; this is it). The 4A implementation plan Task 4 was corrected to record the discovery. Promote to backlog at cycle completion if not built by then. |
| 2026-06-23 | Slice 4A (close-out) | **Slice 4A complete — D4 tuning rounds not triggered.** After PR6 shipped the onboarding voice eval, the recorded gruff-direct register was adversarially verified (per-surface assessor + blind challenger over the recorded fixtures, the rewritten prompts, the persona diff, and the advisory restraint-judge verdicts): all three surfaces read right and survived their blind challenges unrefuted; restraint judge plan-gen 5/5 + adaptation 2/2 at 1.0; deterministic guards green; full Coaching eval suite 176/176 in Replay; KEPT-VERBATIM safety/body/anti-toxic invariants confirmed intact in the live YAMLs. | **Resolved — 4A closed, DEC-084 recorded.** The D4 trigger ("run a prompt-only follow-up PR only if the recorded register reads wrong to the builder") was not met, so no tuning PR was opened. ROADMAP + cycle-plan Status updated, Cycle History row added. Two non-blocking polish items split out as the rows below. Next: 4B conversation-core brainstorm. |
| 2026-06-23 | Slice 4A (close-out verification) | **Adaptation `lee` fixture "without guilt" / "without concern" softeners.** Two of 59 prose fields in the `lee` restructure coaching/segment notes ("If legs feel flat again, cut to 10 km without guilt"; "If legs still feel heavy, shorten to 4 km without concern") name and reassure an unexpressed emotion — feelings-adjacent under a register that deleted "acknowledge feelings first". The `priya` fixture proves the clean gruff form exists ("shut it down and log it. That information is useful"). | **Deferred — non-blocking, opportunistic.** Bounded and consistent with the kept "rest as investment, not punishment" spine; ships as-is (does not make the register read wrong). On the next deliberate adaptation re-record, nudge those two notes toward the `priya`-style framing (state the data reason, drop "without guilt"/"without concern"). Not worth a funded-key re-record by itself. Recorded in DEC-084 § Open/deferred. |
| 2026-06-23 | Slice 4A (close-out verification) | **Onboarding restraint rubric scores short clarifier turns 0.0.** The advisory `VoiceRubrics.Restraint` judge applies `keeps_rationale` / `offers_forward_path` (criteria authored for prose-heavy plan/adaptation output) to a one-line onboarding intake question where they don't belong, producing a misleading 0.0 while the three register criteria (direct_register / no_validation_opener / no_filler_enthusiasm) all pass. | **Deferred — non-blocking, test-only.** Scope the onboarding coaching-voice eval rubric to register-only criteria so short clarifier turns stop scoring 0.0 on inapplicable criteria. Test-only change (no prompt edit, no fixture re-record). Recorded in DEC-084 § Open/deferred. |
| 2026-06-26 | Slice 4B (PR3a) | **Classifier `Temperature 0` is not implementable — SDK deprecates all sampling controls.** Anthropic SDK 12.31.0 marks `MessageCreateParams.Temperature`/`TopP`/`TopK` `[Obsolete]`; the API rejects any non-default value with HTTP 400 on current models (Sonnet 4.6 / Haiku 4.5) and there is no `seed`. The spec/DEC-085 "classifier at Temperature 0" premise (D3, Unit 4, Unit 7 eval) cannot be met. Verified by reflection probe + the `CS0618` build error. | **Resolved in PR3a (forced correction).** PR3a adds only a per-call `modelOverride` on `GenerateStructuredAsync` (no temperature param); classifier determinism rests on constrained decoding (the byte-stable frozen schema) + a deterministic prompt. DEC-085 § PR3a correction, the design doc, and the spec/feature/pr-strategy were updated. The Unit 7 eval gate becomes "stable under constrained decoding". |
| 2026-06-27 | Slice 4B (PR4 #233 review) | **SSE "seed active plan" tests don't actually exercise plan-grounding — `SeedActivePlanAsync` sets an explicit `TenantId`.** The integration helper seeds `RunnerOnboardingProfile` (`ITenanted`, behind the Wolverine EF tenant filter) with `TenantId = userId.ToString()`, which the authenticated SSE request's EF read filters out (`CurrentPlanId` → null → "No active plan"), so the candidate-prescription card *and* the answer-context plan grounding silently resolve empty under the SSE endpoint. No existing test caught it: the answer path tolerates a null plan and `StubCoachingLlm` ignores the prompt. Surfaced while trying to assert the on-plan card prescription for PR4's F3 finding. **Not a product bug** — production creates the profile under the request's own tenant (`ConversationTimelineControllerIntegrationTests` seeds *without* an explicit `TenantId` and its request reads the plan fine). | **Deferred — test-harness, non-blocking.** Align `SeedActivePlanAsync` with the timeline test's seeding (drop the explicit `TenantId`) and restore the end-to-end on-plan card-prescription assertion; fold into the next 4B PR that touches the SSE plan-grounding surface. **Still open after PR5 (#235, 2026-06-29)** — PR5 is the confirm-then-commit endpoint and does not touch the SSE plan-grounding surface, so it carries to **PR6** (frontend streaming UX consumes the SSE + confirm surfaces) or PR7. The pace-band-swap risk is already guarded by the `CandidatePrescriptionDtoTests` unit test merged in PR4 (#233). **Still open after PR6 (#237, 2026-06-30)** — PR6 is frontend-only and does not touch the backend SSE integration-test seed, so the `SeedActivePlanAsync` alignment carries to **PR7** (the last 4B PR). **Still open after PR7 (#239, 2026-07-01)** — PR7 is the eval suite (test-only, `backend/tests/.../Eval/Conversation/` + `EvalTestBase`), not an integration-test change, so it never touched `SeedActivePlanAsync`. Slice 4B is now closed with this gap unresolved; **carries to Slice 4C** (or a dedicated fix, whichever lands first) for triage. |
| 2026-06-30 | Slice 4B (PR6 #237) | **`ConversationPanel` + the `getConversationTurns` read chain orphaned after the home route swapped to `CoachChat`.** PR6 mounts the timeline-backed `CoachChat` on `home.page.tsx`, replacing the Slice 3 read-only `ConversationPanel`; tree-wide, `getConversationTurns` → `useGetConversationTurnsQuery`/`useLazyGetConversationTurnsQuery` → `useConversationTurns`, and `ConversationPanel`, are now referenced only by their own specs. The deep-review also caught a cold-start race in `useCoachStream` (the optimistic `updateQueryData` silently no-ops on a not-yet-loaded timeline cache, dropping the streamed turn). | **Cold-start race fixed in-PR** (`patches.length === 0` → `invalidateTags(['Conversation'])` fallback, verified against the installed RTK 2.12.0; `upsertQueryData` rejected — clobbered by the resolving GET); the stale `conversation.api.ts` comment was reworded (no longer claims a mounted panel). **Dead-code removal deferred** — keep the turns endpoint/hook/panel if a Slice 3 "Explain-the-change" surface may be re-mounted; otherwise delete `getConversationTurns` + the two generated hooks + `useConversationTurns` + `ConversationPanel` (and their specs + the turns dispatches in `conversation.api.spec.ts`/`workout-log.api.spec.ts`). Confirm no planned re-mount first. **Still open at Slice 4B close (PR7 #239, 2026-07-01)** — PR7 is a backend eval-only PR and never touched the frontend read chain; Slice 4B is closed with this dead-code decision unmade. **Carries to Slice 4C** to confirm no re-mount is planned and delete if so. |
| 2026-07-01 | Slice 4C (brainstorm triage) | The three open Slice-4 live-pass items — **onboarding too prose/chat-driven** (2026-06-13), the **WeeklySchedule slot-merge loop + input-kind mismatch** (2026-06-11), and **km/miles unit flexibility not communicated** (2026-06-13) — plus the two 4B carryovers (the **`SeedActivePlanAsync` `ITenanted` test-harness gap** and the **orphaned `ConversationPanel`/`getConversationTurns` read chain**) are the entire input to Slice 4C. Brainstormed + designed 2026-07-01 over a five-surface code map (load-bearing claims verified). | **Triaged into Slice 4C — DEC-086, design [`./slice-4c-onboarding-units.md`](./slice-4c-onboarding-units.md).** Decomposed into **4C-units** (frontend-display-only km/miles over a dedicated `UserSettings` store — unblocked, ships first) and **4C-onboarding** (hybrid form-first intake that originates `AnswerCaptured` events deterministically, dissolving the slot-merge loop + input-kind mismatch — research-gated on R-085 `batch-31a`). Cleanup: delete the orphaned conversation read chain (no Slice-3 "Explain-the-change" panel re-mount planned) + fix the `SeedActivePlanAsync` tenancy seam. The individual dispositions in the rows above are superseded by the 4C design doc; coach prose stays km-native for MVP-0 (D6, deferred with DEC-041's LLM-speaks-miles half). |
| 2026-07-01 | Slice 4C-units (spec, codebase map) | **`ContextAssembler.AssembleAsync` is a legacy island the eval suite pins — not the trivial "dead path" the 4C design assumed.** Verified: `AssembleAsync` (the "legacy plan-generation path") has zero production callers, and its `BuildStartSections`/`BuildMiddleSections`/`BuildEndSections`/`ApplyOverflowCascade`/`FormatPreferences`/`BuildUserProfileSection` subtree is self-contained — **not** shared with the live `ComposeForOnboardingAsync`/`ComposeForPlanGenerationAsync`/`ComposeForAdaptationAsync` methods (which build prompts via `PromptRenderer`). It survives only because ~90 `ContextAssemblerTests` + 3 `EvalTestBase` helpers route the coaching-system-voice / safety-boundary / logged-workout-context evals through it — i.e. a slice of the eval suite validates a prompt-assembly path production never runs. Surfaced because the design doc's "delete the dead `FormatPreferences`/`UserPreferences`/`AssembleAsync` path" contradicted ground truth. | **Dropped from 4C-units; its own scoped cleanup (builder decision 2026-07-01).** 4C-units stays purely additive (no dead-code deletion, no manifest/fixture churn). Cleanup = decide whether to re-point the coaching-system/safety/logged-workout evals onto a real production prompt path (or retire the coverage) and delete the legacy island + its ~90 tests + the `UserPreferences`/Training `UserProfile` records — likely a DEC-074 manifest regen + funded-key fixture re-records. Needs its own DEC + spec. Recorded in the 4C design doc § Open/deferred items and the `20-spec-slice-4c-units` Locked context. |
| 2026-07-04 | Slice 4C-onboarding (R-085 integration) | **R-085 (`batch-31a`) form-first onboarding research landed + integrated.** Artifact at `docs/research/artifacts/batch-31a-form-first-onboarding-redesign.md`; every load-bearing claim verified against `backend/src`/`frontend/src`. Resolves the D2 "how": one `SubmitStructuredAnswers` `[AggregateHandler]` over `FetchForWriting<OnboardingView>` appends the **existing** `AnswerCaptured` event whole-record-per-topic (no new event type; already the codebase idiom); **no onboarding-time LLM call** (nuance = stored verbatim text on existing slot free-text fields, rendered into later coaching prompts — refines D2's "optional LLM nuance"); both `slice-4-conversation.md` carry-forwards (server `SuggestedInputType`; state-machine contract) **obsoleted**; conversational path retired via an `onboarding.formFirst` flag cutover (in-flight streams complete via the form, zero migration) **[R-085's recommendation — SUPERSEDED 2026-07-04: the builder ratified a hard cutover with no feature flag; see the 2026-07-04 spec+verify row below]**; multi-slot free-text-merge evals retired; day-of-week = radix `ToggleGroup`, no new dependency. **Load-bearing audit finding:** the `Description`-field reuse is not uniform — `TargetEventAnswer` alone has no free-text field, so a nuance box there is the one path that busts `OnboardingSchema.Frozen` + the DEC-074 manifest (default: omit it). | **Integrated — DEC-086 § "Research integration", design doc § "R-085 findings integrated", research-queue R-085 → Integrated. The 4C-onboarding research gate is clear; its spec is unblocked.** The two 4B cleanup carryovers (rows above: `SeedActivePlanAsync` tenancy seam, orphaned `ConversationPanel`/`getConversationTurns` chain) remain open and unaffected — they ride with whichever 4C sub-slice lands first. |
| 2026-07-04 | Slice 4C-onboarding (spec + adversarial verify) | **4C-onboarding spec WRITTEN + adversarially verified.** Gitignored working-tree-only at `docs/specs/21-spec-slice-4c-onboarding/` (`spec.md` 5 demoable units, `pr-strategy.md` 5 PRs, 5 Gherkin features, `codebase-map.md`), built from a 6-reader ground-truth sweep then a 14-challenger adversarial-verify pass (all load-bearing claims held). **Two verified corrections to R-085 / the 2026-07-04 integration row above:** (1) **`[AggregateHandler]`/`FetchForWriting` is NOT the codebase idiom** — zero usages across `backend/src`; `OnboardingTurnHandler`/`SubmitUserTurn`/`RegeneratePlanHandler`/`EvaluateAdaptationHandler` each explicitly disclaim it → the spec uses the plain-static-handler convention (like `SubmitUserTurn`). (2) **`AnthropicSchemaSanitizer` is SHARED** by `AdaptationSchema.cs:36` + `ClassifierSchema.cs:40` → PR-E **relocates** it out of the `Onboarding` namespace, does not delete (only `AnthropicContentBlock`/`AnthropicContentBlockType` are onboarding-only). Also: `getConversationTurns` is referenced by `workout-log.api.spec.ts:165,198` (repoint in the cleanup PR). Builder-ratified 2026-07-04: hard cutover (no feature flag — zero migration concerns) + both cleanups in-spec. | **Spec ready for builder review → task breakdown.** 5-PR plan: PR-A (`SeedActivePlanAsync` tenancy seam) + PR-B (delete orphaned `ConversationPanel`/`getConversationTurns` chain) are independent, land first off `main`; then `PR-C → PR-D → PR-E` (deterministic `POST /api/v1/onboarding/answers` origination → single-page form UI → retire the conversational path). **Supersedes the two 4B cleanup carryover rows above** (`SeedActivePlanAsync` tenancy seam; orphaned `ConversationPanel` chain) — they become PR-A/PR-B. |
| 2026-07-05 | Slice 4C-onboarding (PR-B, deep-review) | **Deleting the frontend `conversation.model.spec.ts` codegen contract tripwire (per DU-4 / codebase-map §5a) removes the repo's only drift guard for the shared `ConversationTurnDto` + `EscalationLevel`/`SafetyTier`/`AdaptationKind` enums.** That same union is still live in the composed timeline (`ConversationTimelineDto.turns[].proactive`; `GetApiV1ConversationTimelineResponse` carries the identical non-nullable-`$ref` divergence the deleted spec was built to catch), yet nothing parses a fully-populated timeline fixture against the generated timeline Zod schema — so a backend enum renumber could regenerate the schema while the hand-written model stays stale, silently misrouting safety-tier/adaptation styling with no CI signal. The tripwire was deliberately scoped to the dead `/turns` endpoint and deleted with PR-B; a replacement timeline-schema tripwire is additive test work beyond PR-B's deletion mandate. (Surfaced by the PR-B deep-review test-analyzer; two sibling test-analyzer findings — a lost frontend fixture-copy trademark sweep and a `BeforeAfterDiff` motion-reduce assertion — were rejected: production trademark protection is backend-side, and `before-after-diff.component.tsx:56,64` already carry the `motion-reduce:` pairings and is untouched by PR-B.) | Deferred, non-blocking. Add a timeline-schema contract tripwire (fully-populated `ConversationTimelineDto` fixture parsed against `GetApiV1ConversationTimelineResponse`) — natural fit for whichever 4C-onboarding PR next touches the conversation/timeline surface, or its own small frontend-test PR. |
| 2026-07-05 | Slice 4C-onboarding (PR-A #259, merged) | **PR-A shipped — and corrected the spec's tenancy-seam diagnosis.** The `SeedActivePlanAsync` `ITenanted` mismatch (2026-06-27 row above) was not the bug: onboarding and conversation share **one physical per-user Marten stream** (`OnboardingView` + `ConversationView` are both single-stream projections keyed by the bare `userId`), and each handler decided bootstrap-vs-append off **its own projection document's** existence rather than the **physical stream's** — so a `StartStream` collided with an already-existing stream in both orderings (`onboard → chat` 500s in `PostUserConversationTurnHandler`; the uncaught `chat → onboard` reverse in `OnboardingTurnHandler` permanently blocks onboarding completion, since nothing commits). `POST /api/v1/conversation/messages` is auth-only-gated (chat-before-onboard is a supported state per `GreenQuestion_WithNoActivePlan_StillStreamsAnAnswer`), so both orderings are reachable by any client — this was a latent production bug, not a test-harness gap. Net-new `MartenEventStreamExtensions.StartStreamOrAppendAsync<TAggregate>` (decides bootstrap-vs-append on `FetchStreamStateAsync` physical-stream existence) fixes both handlers; full backend suite 2051/2051 green in Replay. Also completed DU-5 (the on-plan card assertion): the manually EF-seeded profile row was being **deleted outright** by the single-stream projection's first `UserMessagePosted` (null snapshot → `default` branch → Marten deletes the row) — not tenant-filtered — so `SeedActivePlanAsync` now seeds through the onboarding **event stream** (`OnboardingStarted`+`PlanLinkedToUser`+`OnboardingCompleted`) instead of a direct EF insert. | **Merged 2026-07-05 (#259).** Surfaced two new follow-ups, both deferred and non-blocking: (1) **DEC-047 divergence** — DEC-047 specifies onboarding on a *separate* `DeterministicGuid(userId,"onboarding")` stream ("commingling would force both projections to event-filter" — exactly the footgun hit here); the shipped bare-`userId` sharing is an emergent divergence. Needs a ratify-the-merged-stream-vs-re-separate call (a projection-identity migration if re-separating) — natural fit for whichever of `PR-C → PR-D → PR-E` first touches onboarding stream identity. (2) **Sibling EF-seed test fixtures** (`ConfirmConversationalLogEndpointIntegrationTests`, `ConversationControllerIntegrationTests`) still EF-insert the profile directly — correct today (they don't reproject) — but should port to the event-sourced seed as a fast-follow. |
| 2026-07-06 | Slice 4C-onboarding (PR-C #263, merged) | **PR-C shipped — the additive deterministic form-answer origination endpoint `POST /api/v1/onboarding/answers` (DU-1):** a plain static Wolverine `SubmitStructuredAnswersHandler` originates one whole-record `AnswerCaptured` per submitted topic with **no onboarding-time LLM call**, the completion gate as the SOLE plan-gen authority, then inline plan-gen (`GeneratePlanAsync` + `PlanLinkedToUser` + `OnboardingCompleted`); plus 6 loosened wire DTOs + `SubmitStructuredAnswersRequestMapper` (FR-1.8 deterministic validation replacing the retired LLM validator). Two items carry forward: **(1, DEFERRED) concurrency finding #1** — the plain static handler has no Marten optimistic-concurrency gate, so two concurrent submissions with *different* idempotency keys both pass the completion gate and both generate a plan, leaving `CurrentPlanId` nondeterministic. Inherited verbatim from `OnboardingTurnHandler` (DP-2 deliberately mirrored the unprotected plain-static convention over `[AggregateHandler]`/`FetchForWriting`), so patching only the new handler would diverge from DP-2 + the shipped turn handler — needs a **cross-cutting expected-version-concurrency decision applied to BOTH onboarding handlers** (`ConcurrencyException → 409`). **(2)** the generated request DTO marks all topic slots `required` despite being C#-nullable — the repo-wide `RequireNonNullablePropertiesSchemaFilter` `$ref` artifact (structural fix tracked #163); runtime binding correctly treats topics as optional, PR-D hand-writes accurate optional-topic models. | **Merged 2026-07-06 (#263).** Deep-review (Frontier) + CodeRabbit addressed in follow-up `b1e1a91`: the load-bearing fix widened `TryNormalizeOptionalDuration`'s catch to `FormatException or OverflowException` so a huge-but-valid `xsd:duration` (`P1000000000D`) → 400 not an unhandled 500 (the duration twin of the closed `1e400`→Infinity distance hole); plus integration deep-equality assertions + S125 comment hygiene; two reasoned declines CodeRabbit accepted (#163 schema artifact; framework-standard model-binding 400 for a missing idempotency key). Practical mitigation for #1 lands in PR-D (disable submit-on-click); MVP-0 audience self+family. 34 mapper-unit + 13 integration tests, full backend suite green in Replay. |
| 2026-07-06 | Slice 4C-onboarding (PR-D #265, merged) | **PR-D shipped — the form-first onboarding UI (DU-2):** a single-page, mobile-first structured form over `POST /api/v1/onboarding/answers` replacing the turn-by-turn LLM-mediated chat — units-first (the km/miles picker gates the numeric fields, never a silent km default), unit-aware distances converted to canonical km at the wire via the shared `unit-format.helpers` (storage/prompt stay km-native, DEC-086), RHF + Zod (DEC-075 `z.input`/`z.output`), a day-of-week `ToggleGroup`, success-gated `Onboarding` tag invalidation so `OnboardingRedirectGuard` redirects on completion; deletes the turn-input dispatcher + `onboarding-chat` + the Redux `onboarding.slice` + turn DTOs (FR-2.8). | **Merged 2026-07-06 (#265).** Deep-review + CodeRabbit addressed before merge (`f3d4cf6`/`caed189`): a MEDIUM validity-gating bug — a hidden `TargetEvent` field (e.g. a `0` race distance) could permanently disable submit after the goal switched away — fixed in the deterministic schema layer by gating the race-only fields (`eventDistance` range / `eventDate` + `targetFinishTime` format) on the race goal (a `deferRange` field option + a race-gated refine returning issue descriptors, no `set-state-in-effect`), plus a units-change reseed/remount test and five CodeRabbit nits. Frontend build/lint green, 622 tests. The practical mitigation for concurrency finding #1 (disable submit-on-click) landed here. |
| 2026-07-07 | Slice 4C-onboarding (PR-E #267, merged) | **PR-E shipped — retired the conversational onboarding path (DU-3), the final 4C-onboarding PR; hard cutover, no feature flag (DP-1).** Deleted `POST /turns` + `POST /answers/revise`, `OnboardingTurnHandler`, `SubmitUserTurn`, `OnboardingTurnOutput` + the `OnboardingTurnOutputValidator*` trio, `ExtractedAnswer` + the 6 `*Extraction` records, `OnboardingSchema`, `SuggestedInputType`, `AnthropicContentBlock`/`AnthropicContentBlockType`, the turn/revise DTOs, `OnboardingProgressDto`, and `Prompts/onboarding-v1.yaml` (DEC-074 manifest regenerated); **relocated** `AnthropicSchemaSanitizer` out of the `Onboarding` namespace into `Modules/Coaching/` (shared by `AdaptationSchema` + `ClassifierSchema`); trimmed the `ContextAssembler` ctor 6→4 args (dropped the onboarding-only `IHostEnvironment` + `IOptions<PromptStoreSettings>`) and deleted the now-orphaned `ComposeForOnboardingAsync`/`BuildOnboardingUserMessage`/`LoadOnboardingSystemPromptAsync`; salvaged two live event-shape facts into `OnboardingProjectionTests`; retargeted the DI-resolution guard onto `ComposeForClassificationAsync`; regenerated `swagger.json` + the zod/rtk client + barrel. +149/−7046 across 83 files; `dotnet build` 0/0, backend 1949/1949 + frontend 622 green in Replay. | **Merged 2026-07-07 (#267).** A Frontier deep-review (6 Opus agents → Sonnet validation → Opus blind challenge) returned the PR clean — one Improvement-Suggestion follow-up survived (S-01, below); no correctness/security/cross-file/convention findings. **(1, DEFERRED — S-01, test coverage) concurrent first-submission race on the shared per-user Marten stream is now untested.** Deleting `OnboardingTurnConcurrencyIntegrationTests` (the 2026-06-09 row above — the DEC-057 `EventAppendMode.Rich` stream-collision regression guard) removed the only test that empirically drove concurrent first-writes and proved the exactly-one-winner property; the surviving `SubmitStructuredAnswersHandler` + `PostUserConversationTurnHandler` bootstrap the same physical stream via `StartStreamOrAppendAsync` but no surviving test exercises the race — a `Rich`→`Quick` regression would pass the whole suite. Not restored here (a Testcontainers concurrency-race test is scope-expanding + flaky-prone for a retirement PR, and it overlaps the already-tracked concurrency-decision item #1 from the PR-C row above). Fold a two-phase staged-then-raced integration test onto the surviving handlers into whichever future work makes the cross-cutting expected-version-concurrency decision (`ConcurrencyException → 409`). |

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

- [x] …log a workout whose note trips an Amber injury rule on ANY escalation path (including the post-restructure cooldown dead-zone) and see the scripted referral turn in the Coach panel (F1).
- [x] …generate a live plan whose persisted prose fields (rationales, notes, summaries) contain no trademarked term, enforced by a deterministic output-boundary guard + extended eval guard (F2).
- [x] …onboard with a dated race goal and get a plan whose horizon lands race week at the end of the taper (deterministic validator rejects mismatches) (F3).
- [x] …trigger a live restructure whose proposed weekly target equals its own micro-week workout sum within tolerance (validator rejects contradictions as terminal `Kind=Error`) (F4).
- [x] …re-run the **full live end-to-end validation pass** (fresh account, funded key, real browser) and observe all four fixes at the surface — this re-run is the slice's done-gate; CI/eval coverage alone does not close it.

**Out of scope:** onboarding Schedule slot-merge loop (→ Slice 4), restructure rationale-claim drift (→ judge-calibration deferral row), stale week narratives after adaptation (→ deferred UX polish). See the requirements doc and Captured During Cycle table.

---

### Slice 4 — Open Conversation

**Requirements:** [`./slice-4-conversation.md`](./slice-4-conversation.md)

**Decomposition (2026-06-17 → 2026-06-24):** Slice 4 split into **4A** (voice re-tune — COMPLETE 2026-06-23, DEC-084), **4B** (conversation core — brainstormed + designed 2026-06-24, [`./slice-4b-conversation-core.md`](./slice-4b-conversation-core.md); **COMPLETE 2026-07-01, DEC-085, all 7 PRs merged #219–#239**), and **4C** (onboarding redesign + km/miles units — **COMPLETE 2026-07-07, DEC-086**, design [`./slice-4c-onboarding-units.md`](./slice-4c-onboarding-units.md); shipped as **4C-units** [frontend-display-only km/miles + a `UserSettings` store, 6 PRs, 2026-07-03] and **4C-onboarding** [deterministic form-first intake retiring the conversational path, 5 PRs A–E #259 / #261 / #263 / #265 / #267, 2026-07-07], with the two cleanup tasks folded into PR-A/PR-B). 4B locked four conversation-core decisions — user-scoped conversation + plan-scoped adaptation; Q&A + conversational logging; classify-then-route via a structured pre-call; confirm-then-commit for parsed logs — and resolved two pre-spec research unknowns (integrated 2026-06-24): **R-083** (`batch-30b`, streaming-eval harness — buffer-then-assert over coalesced fixtures; informs the eval unit) and **R-084** (`batch-30c`, Anthropic SDK streaming exception mapping onto DEC-073 — throw-based `StreamAsync`; informs the streaming adapter). The acceptance criteria below are the original Slice 4 framing; the 4B design doc carries the authoritative conversation-core scope (now shipped).

**Acceptance — "I can…"**

- [x] …type a question into the chat panel ("how am I doing?", "should I push harder next week?", "my knee feels tight"). Shipped as `CoachChat` (PR6, #237) over `POST /api/v1/conversation/messages` (PR4, #233).
- [x] …see a streaming response grounded in my profile + plan + recent logs. Shipped via `ICoachingLlm.StreamAsync` (PR1, #219) + `ContextAssembler.ComposeForConversationAsync` grounding.
- [x] …have the conversation persist across sessions (chat history visible on reload). Shipped as the user-scoped `Conversation` event stream + `/timeline` read (PR2, #226).
- [x] …see the system handle the three interaction modes from `docs/planning/interaction-model.md` — onboarding (slice 1), proactive adaptation messages (slice 3), and open conversation (this slice). All three now live on `main`.

**Scope**

- Backend: Conversation endpoint (streaming). Full `ConversationTurn` persistence (user turns + assistant turns). `ContextAssembler` routes by query type per the existing design. Possibly a lightweight triage prompt to classify intent.
- Frontend: Chat panel becomes interactive — text input, streaming response rendering, conversation history. Panel is always visible (right rail on desktop, bottom drawer on mobile).
- Tests: Integration tests for the conversation endpoint (streaming, context routing). Eval scenarios for a few representative open-conversation prompts. Playwright: ask question → see grounded response.

**Key risks**

- ~~Streaming response rendering in React + RTK Query — RTK Query isn't ideal for streams; may need raw fetch + state management for the chat panel alone.~~ **Resolved by R-082 (`batch-30a`, integrated 2026-06-16):** SSE-over-`fetch` (POST + `ReadableStream`) transport; the live turn renders from **local React state** (not the RTK Query cache), and the completed server-authoritative turn reconciles into `getConversationTurns` via `upsertQueryData`/`updateQueryData` (merge-by-id, once — no blanket refetch). Backend exposes the existing `SanitizationAuditChatClient.GetStreamingResponseAsync` through a net-new `ICoachingLlm.StreamAsync`, piping `IAsyncEnumerable<ChatResponseUpdate>` to the wire with buffering disabled and `HttpContext.RequestAborted` cancelling the upstream Anthropic call. Persistence: user-turn-first + assistant-turn-on-complete via `FetchForWriting` guarded by `IIdempotencyStore` (`in_flight` state); mid-stream death persists `errored`/discards (never a silent partial). Safety: pre-call SafetyGate + abort-only mid-stream + async post-stream judge; render with `react-markdown` (no `rehype-raw`). The pattern is **neutral** to the plan-scoped-vs-user-scoped projection decision (still to be made deliberately in the conversation-core spec). DEC entry locks at spec-writing time.
- Context-assembler routing quality — the existing design mentions interaction-specific assembly; slice 4 is when that actually gets exercised in production flow (not just eval).

**Relevant research artifacts**

- `batch-30a-streaming-llm-conversation-transport.md` (R-082) — end-to-end streaming design: SSE-over-`fetch` transport, RTK Query reconciliation, server pipeline, persist-on-complete idempotency, mid-stream safety posture. **The conversation-core gate.**
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
- [ ] Both Status blocks collapsed to one-line ledger entries for the finished slice (**replace, don't append** — narrative goes to the completion section / Cycle History row / decision log).
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
- **RTK Query streaming patterns for LLM responses** — ~~RTK Query's caching model isn't a great fit for streams. Slice 4's chat panel will need either raw fetch + manual state or an alternative library; not obvious which.~~ **Resolved 2026-06-16 by R-082 (`batch-30a`):** raw `fetch` + `ReadableStream` into local React state for the live turn (NOT a second data-fetching library), reconciled into the `getConversationTurns` cache via `upsertQueryData`/`updateQueryData` once on completion. SSE-over-`fetch` transport end-to-end. See the Slice 4 § Key risks entry and the artifact for the full design.
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
