# RunCoach — Roadmap

**Current cycle:** MVP-0 + Adaptation Loop — `docs/plans/mvp-0-cycle/cycle-plan.md`
**Active slice:** None. **Slice 4 (Open Conversation) completed 2026-07-07**, closing the cycle's build scope — Slices 0, 1, 1B, 2a, 2b, 3, 3B, and 4 (4A voice re-tune / 4B streaming conversation core / 4C onboarding redesign + km/miles units) are all merged. One-line outcomes: § Cycle History below; close-out detail: the cycle plan's Status ledger + per-slice sections.
**Next step:** **The MVP-0 live end-to-end validation pass PASSED 2026-07-07** (form-first onboard → live-Sonnet plan → log → adapt → converse, verified at the UI surface and in the persisted event store). Remaining close-out is the **MVP-0 close follow-up:** **F-LIVE-1** (live plan-gen stochastically fails macro validation with no server-side retry) is **fixed — DEC-087, bounded corrective-hint macro retry implemented on `fix/f-live-1-plan-gen-retry` (PR in review)**, design `docs/plans/mvp-0-cycle/mvp-0-close-live-pass-fixes.md`; **F-LIVE-2** (meso↔micro plan-layer inconsistency, user-visible) remains — needs its own DEC + spec. Then triage the cycle plan's § Captured During Cycle ledger and plan the next cycle (the pre-launch visual UI refactor is the flagged candidate — § Deferred Items). Findings + dispositions: cycle plan § Captured During Cycle (2026-07-07 rows + "Open follow-ups"); durable summary in memory `project_mvp0_close_live_pass`.
**Blockers:** None.
**Open follow-ups (non-blocking):** distilled in the cycle plan § Captured During Cycle → "Open follow-ups" — headliners: **F-LIVE-2 meso↔micro consistency** (the sole remaining MVP-0 close live-pass finding — F-LIVE-1 plan-gen retry is fixed in DEC-087, PR in review), the `AssembleAsync` legacy-island eval cleanup, the expected-version concurrency decision for both surviving stream handlers (+ its S-01 race-coverage gap), the DEC-047 shared-stream ratification, and the timeline-schema contract tripwire.
**Recent decisions:** DEC-085 (conversation core D1–D4 + streaming posture), DEC-086 (form-first onboarding + display-only km/miles), DEC-087 (bounded corrective-hint macro-plan retry; amends the DEC-073/DEC-080 no-re-prompt posture) — rationale in `docs/decisions/decision-log.md`.

This is the front door. For the full picture on session start, run `/catchup`. For anything deeper than the Status block above, open the cycle plan.

---

## Entry Points

Agents arriving cold should resolve intent to a file before reading:

- **"What should I work on?"** → active cycle plan (pointer above).
- **"What's the active slice doing?"** → active slice spec under `docs/specs/` (pointer in cycle plan's Status section, once a slice is underway).
- **"How does X work?"** → `docs/planning/{topic}.md` + the relevant module under `backend/src/RunCoach.Api/Modules/` or `frontend/src/app/modules/`.
- **"Why was X decided?"** → `docs/decisions/decision-log.md`.
- **"Has this been researched?"** → `docs/research/research-queue.md` + `docs/research/artifacts/`.
- **"What are the rules for code changes?"** → root `CLAUDE.md`, `backend/CLAUDE.md`, `frontend/CLAUDE.md`, `REVIEW.md` files (root / backend / frontend).
- **"I found an unknown — can I just pick one and move on?"** → No. See `CLAUDE.md` § Research Protocol and the active cycle plan's "When Agents Encounter Unknowns" section.
- **"Where do I capture a 'we should also do this' item?"** → the active cycle plan's "Captured During Cycle" section (scoped to the cycle); or the "Deferred Items (Cross-Cycle)" section below for items that span cycles.

---

## Strategic Links

- Vision & principles: `docs/planning/vision-and-principles.md`
- Interaction model (three modes): `docs/planning/interaction-model.md`
- Planning architecture (macro/meso/micro, event-sourced plan): `docs/planning/planning-architecture.md`
- Memory & context injection strategy: `docs/planning/memory-and-architecture.md`
- Coaching persona playbooks: `docs/planning/coaching-persona.md`
- Safety & legal: `docs/planning/safety-and-legal.md`
- Self-optimization: `docs/planning/self-optimization.md`
- Unit system design: `docs/planning/unit-system-design.md`
- Decision log: `docs/decisions/decision-log.md`
- Feature backlog: `docs/features/backlog.md`
- Research queue & artifacts: `docs/research/research-queue.md`, `docs/research/artifacts/`
- POC roadmap (historical framing, superseded by cycle plans): `docs/planning/poc-roadmap.md`

---

## MVP Milestones

- **MVP-0 (personal validation):** Onboarding + plan generation + workout logging + adaptation loop + open conversation. Builder uses it on own runs. **All build slices complete (2026-07-07); the live end-to-end validation pass PASSED 2026-07-07 — remaining close-out is the MVP-0 close follow-up (the two findings F-LIVE-1/F-LIVE-2 + ledger triage), deferred to a clean session.**
  - **Live end-to-end validation pass:** CI and the local suite exercise every LLM surface in **Replay mode** against the committed eval cache (zero real API calls), so the live coaching path is never proven by automated tests. The first pass (2026-06-11) proved the loop live and surfaced six findings Replay structurally cannot catch (triaged into Slice 3B / Slice 4 / cycle-plan deferrals); the Slice 3B re-run passed 2026-06-13. **The MVP-0 close pass PASSED 2026-07-07** — fresh account, funded key, real browser (Playwright MCP; the Claude-in-Chrome extension was not connected), the full onboard (form-first) → plan → log → adapt → converse loop verified at the UI surface and in the persisted event store. It surfaced three findings Replay can't catch, now tracked as the MVP-0 close follow-up (cycle plan § Captured During Cycle, 2026-07-07 MVP-0 close-out row): **F-LIVE-1** live plan-gen stochastically fails `PhaseSumMismatch` with no server-side retry, **F-LIVE-2** meso↔micro plan-layer inconsistency (user-visible), **F-LIVE-3** cosmetic coach day-attribution slip. Stack recipe (for the re-run / follow-up work): Path B host-run (`docker compose up -d postgres redis` + `dotnet run` API + Vite, per `CONTRIBUTING.md`), `Anthropic:ApiKey` in the `runcoach-api` user-secrets store (**not** `runcoach-api-tests`) pointed at a **funded** account.
- **MVP-1 (friends / testers):** Adds proactive coaching + Apple Health integration. The adaptive differentiator becomes externally visible.

---

## Cycle History

Chronological log of completed cycles / phases, most recent first. One line per cycle — full detail lives in the linked artifacts (decision log, plan files, PRs).

| Cycle / Phase | Completed | Primary Artifacts | Key Outcomes |
|---|---|---|---|
| MVP-0 Slice 4C — Onboarding Redesign + km/miles Units | 2026-07-07 | PRs #242 / #244 / #243 / #252 / #253 / #255 (4C-units) + #259 / #261 / #263 / #265 / #267 (4C-onboarding, PRs A–E); specs `docs/specs/20-spec-slice-4c-units/` + `docs/specs/21-spec-slice-4c-onboarding/` (gitignored); design `docs/plans/mvp-0-cycle/slice-4c-onboarding-units.md`; DEC-086 | Two-part slice. **4C-units** (2026-07-03): frontend-display-only km/miles over a dedicated `UserSettings` store (`GET`/`PUT /api/v1/settings/units`), a shared `unit-format` module (distance + net-new inverse pace + range formatters), a Settings units toggle, and miles↔km conversion wired at every plan/logging/adaptation render site + the log-write input site — storage + coaching prompt stay km-native (DEC-086 D6); amends DEC-041's imperial-display phasing while keeping the `PreferredUnits{Kilometers,Miles}` enum. **4C-onboarding** (2026-07-07): retired the turn-by-turn LLM-mediated onboarding for a deterministic form-first intake — `POST /api/v1/onboarding/answers` originates whole-record `AnswerCaptured` events with no onboarding-time LLM call (`SubmitStructuredAnswersHandler` + `SubmitStructuredAnswersRequestMapper` deterministic validation), a single-page mobile-first form UI, and PR-E's hard-cutover deletion of the turn endpoint/handler + extraction stack (`AnthropicSchemaSanitizer` relocated to `Modules/Coaching/`, shared by adaptation + classifier schemas). PR-A also fixed a latent shared-per-user-Marten-stream bootstrap collision (`StartStreamOrAppendAsync`) and PR-B deleted the orphaned Slice-3 `ConversationPanel`/`getConversationTurns` chain (the two 4B carryovers). **With 4A+4B+4C done, Slice 4 (Open Conversation) is COMPLETE**; the MVP-0 live end-to-end validation pass re-run is now ungated. |
| MVP-0 Slice 4B — Streaming Conversation Core | 2026-07-01 | PRs #219 / #226 / #230 / #231 / #233 / #235 / #237 / #239; spec `docs/specs/19-spec-slice-4b-conversation-core/` (gitignored); design `docs/plans/mvp-0-cycle/slice-4b-conversation-core.md`; DEC-085 | Net-new streaming Q&A + conversational logging over the coaching relationship: `ICoachingLlm.StreamAsync` (Anthropic + M.E.AI streaming bridge, `IncompleteCoachingLlmException` errored-turn contract, per-delta trademark scrubbing) and the user-scoped `Conversation` event stream + composed `/timeline` read (PR1/PR2); the Haiku `MessageIntent {Question\|WorkoutLog\|Ambiguous}` classify-then-route pre-call with DEC-085 bias-to-ask coercion, and deterministic server-side unit conversion for logged actuals (PR3a/PR3b); the SSE endpoint `POST /api/v1/conversation/messages` (PR4) and the confirm-then-commit `POST /api/v1/conversation/logs/confirm` (PR5, an advisory draft that commits nothing until an explicit user confirm); the React streaming UX (`useCoachStream`, `CoachChat`, `LogConfirmationCard`) replacing the Slice-3 read-only conversation panel on the home route (PR6); and the streaming-conversation + classifier-accuracy eval suite (`ChatSafetyEvalTests`, `ConversationAnswerVoiceEvalTests`, `IntentClassificationEvalTests` + `IntentConfusionMatrix`, PR7). Full backend suite 2041/2041 in Replay. Two non-blocking carryovers open for 4C triage: the `SeedActivePlanAsync` tenancy-filter test-harness gap, and the orphaned `ConversationPanel`/`getConversationTurns` read chain (dead-code removal deferred). |
| MVP-0 Slice 4A — Coaching Voice Re-tune | 2026-06-23 | PRs #199 / #206 / #207 / #209 / #211 / #213; design `docs/plans/mvp-0-cycle/slice-4a-voice-retune.md`; plan `docs/superpowers/plans/2026-06-17-slice-4a-voice-retune.md`; DEC-084 | Re-tuned all three active LLM prompts (`onboarding-v1`, `coaching-system.v1` = plan-gen + conversation, `adaptation.v1` = restructure rationale) plus `coaching-persona.md` from a cheery/validating register to **gruff-direct** (warmth via competence; mandatory OARS Affirmation + process praise + feelings-first opener deleted; MI spine + all safety/body/anti-toxic lines kept verbatim; pointed factual accountability newly allowed). Enforcement: deterministic `VoiceProseGuard` (em-dash / exclamation / banned-phrase) **hard gate** + advisory `VoiceRubrics.Restraint` Haiku judge (recorded, not gated); no runtime scrubber. DEC-074 hash manifest regenerated and affected fixtures re-recorded against a funded key per surface. **Close-out: register adversarially verified to read right across all three surfaces (restraint judge plan-gen 5/5 + adaptation 2/2 at 1.0; mechanical guards green; full Coaching eval suite 176/176 in Replay; KEPT-VERBATIM invariants confirmed intact) — D4 tuning rounds not triggered, no further PR needed.** Two non-blocking opportunistic polish items carried forward (the `lee` adaptation "without guilt/concern" softeners; scoping the onboarding restraint rubric to register-only criteria). 4B (R-082 streaming conversation core) + 4C (onboarding redesign + km/miles units) remain as their own slices. |
| MVP-0 Slice 3B — Live-Pass Fixes | 2026-06-14 | PRs #185 / #187 / #190 / #192 / #194; requirements `docs/plans/mvp-0-cycle/slice-3b-live-pass-fixes.md`; DEC-081 / DEC-082 / DEC-083 | Hardening mini-slice from the 2026-06-11 live pass's four immediate findings. **F1** Amber safety referral hoisted to a pre-escalation step so every Amber outcome surfaces it, deduped per log (DEC-081 records the referral-commits-on-terminal-L2-failure exception to DEC-080). **F2** deterministic trademark scrub on all persisted LLM prose. **F3** date-aware plan horizon — an app-local `ILocalDateProvider` "today" seam feeds a deterministic `PlanHorizonCalculator` pinned into the macro prompt, and `MacroPlanOutputValidator` terminally rejects a macro whose total weeks miss race week by more than ±1 (DEC-082). **F4** `RestructureConsistencyCheck` post-validation L2 gate — a restructure's revised current-week target must equal the exact running-only sum of the week's resulting workouts, else terminal `Kind=Error` (DEC-083). Done-gate met: a fresh live pass 2026-06-13 verified all four at the UI surface and in the persisted event store; the two funded-key eval re-records (F3 dated-event macro fixture; F4 schema-`[Description]` realignment) landed in #194. |
| MVP-0 Slice 3 — Adaptation Loop (code-complete) | 2026-06-11 | PRs #161 / #163 / #165 / #167 / #169 / #172 / #174; spec `docs/specs/17-spec-slice-3-adaptation/`; DEC-078 / DEC-079 / DEC-080 | Deterministic `SafetyGate` (DEC-079 high-risk crisis / referral / injury / RED-S subset, ReDoS-guarded) + `RecentLogSanitizer` seam; `DeviationEngine` + `EscalationClassifier` (pace-band deviation + asymmetric hysteresis; L0 absorb / L1 nudge in code, L2 restructure = first LLM call); Pattern-B `PlanAdaptationOutput` + validator + versioned `adaptation.v1.yaml`, DEC-073 LLM-failure policy live (`Kind=Error` HTTP-200 envelope); synchronous events-only `EvaluateAdaptationHandler` orchestration (`WorkoutLogId`-keyed idempotency, DEC-080 scope reductions); `PlanAdaptedFromLog` / `SafetySignalRaised` events + Marten-projected `ConversationLogView` + read-only `GET /api/v1/conversation/turns`; deterministic calibration eval + DEC-074 prompt-hash sentinel + Replay-mode L2-restructure / Haiku-judge eval (surfaced the `AdaptationSchema.Frozen` `$ref` production-bug fix); read-only Explain-the-change chat panel. **Gated live pass executed 2026-06-11 — loop verified live (first L2 restructure against live Sonnet); six findings triaged into Slice 3B (`slice-3b-live-pass-fixes.md`) / Slice 4 / cycle-plan deferrals; pass re-run pending on 3B.** |
| MVP-0 Slice 2b — Workout Logging | 2026-06-07 | PRs #134 / #136 / #145 / #147 / #151 / #154 / #156 / #158; spec `docs/specs/16-spec-slice-2b-workout-logging/`; DEC-072–077; `batch-28{a–d}` + `batch-29a` research | `WorkoutLog` EF entity (open `jsonb` metrics bag + single-sourced `WorkoutMetricKeys` + EF-10 optional complex-type prescription snapshot + the repo's first `ValueConverter`s) with migration + repository; `POST /api/v1/workouts/logs` create (server-authoritative snapshot resolution + EF-native idempotency on a unique `(UserId, IdempotencyKey)` index, DEC-077) and `POST .../logs/query` DB-driven keyset history endpoint; `ContextAssembler` recent-log injection (compact Layer-1 one-liners, eval-proofed); `PlanStartDate` calendar anchor + pure `PlanCalendar` date→slot mapper; `/log` logging form (RHF/Zod derived from generated create-request Zod, pessimistic create + toast) and `/history` ISO-week-grouped sparse-`<dl>` surface; Playwright E2E for the log→history journey. |
| MVP-0 Slice 2a — Frontend Visual Foundation | 2026-05-30 | PRs #111–#114; spec `docs/specs/15-spec-slice-2a-frontend-foundation/`; DEC-070; `batch-26a` research | shadcn/ui actually installed (`new-york`, `components.json`, `cn()`); two-tier Catppuccin design tokens (Latte/Mocha) in `index.css` gated by a `check-contrast` WCAG script (pre-commit + CI); class-based dark mode via `ThemeProvider` + no-flash script + 3-state Settings toggle; auth / onboarding / home / settings surfaces migrated onto semantic tokens; `tw-animate-css` for Radix enter/exit (`motion/react` still deferred per DEC-063). |
| MVP-0 Slice 1B — Pre-Slice-2 Hardening | 2026-05-15 | PRs #91–#94; requirements `docs/plans/mvp-0-cycle/slice-1b-hardening.md`; DEC-066–069; `batch-24`/`batch-25` research | Structurally guarded Slice 1's four contract-drift bug classes: OpenAPI → TS/Zod codegen with a committed `backend/openapi/swagger.json` + `git diff --exit-code` drift gate (DEC-066); Marten event upcasting via versioned CLR event types + a synthetic-old-row regression test (DEC-067); a top-level React error boundary + `POST /api/v1/client-errors` with client-OTel trace correlation (DEC-068 / DEC-069); and three DI-resolution regression tests (`IIdempotencyStore` / `IPlanGenerationService` / `RegeneratePlanHandler`). |
| MVP-0 Slice 1 — Onboarding → Plan | 2026-04-26 | requirements `docs/plans/mvp-0-cycle/slice-1-onboarding.md`; DEC-057–064 | Multi-turn chat-driven onboarding building the runner profile (Pattern-B structured-output turns, layered prompt-injection sanitizer), macro/meso/micro plan generation rendered on the home page, persisted-plan reload, and settings-triggered regeneration. End-to-end debugging surfaced four contract-drift bug classes (PascalCase wire leak, RTK tag-invalidation race, multi-select clarification dead-end, `Completed`/`Total` field rename) patched at the call site — Slice 1B closed the structural gaps. |
| MVP-0 Slice 0 — Foundation | 2026-04-23 | PRs #49 / #50 / #63; spec `docs/specs/12-spec-slice-0-foundation/` | Persistence substrate (EF + Marten + Wolverine), auth API (register / login / me / logout / xsrf on `CookieOrBearer` with antiforgery + timing-safe login + Identity-error → DTO-bucket mapping), and cookie-session frontend (RTK Query + React Hook Form + Zod + Playwright happy-path). DEC-048 through DEC-056 landed along the way. |
| Spec 11 — TestPaceCalculator migration + `VDOT` residue scrub + eval cache re-record | 2026-04-18 | PR #45 | Closed both DEC-042 follow-ups: `TestPaceCalculator` bridge deleted and all four race-carrying profiles migrated to real `PaceZoneCalculator`; `FitnessEstimate.EstimatedVdot` → `EstimatedPaceZoneIndex`; `RaceTime` XML doc and four `AssessmentBasis` literals scrubbed; parameterized `ContextAssemblerTests` Theory guards full assembled prompt against `VDOT` regression for all 5 profiles; Sonnet + Haiku eval cache re-recorded. |
| DEC-042 pure-equation pace-zone calculator + DEC-041 value objects | 2026-04-17 | PR #44; `batch-11`, `batch-12a-g`, `batch-13` research | Replaced Daniels lookup table with `DanielsGilbertEquations` + `PaceZoneCalculator`; `VdotCalculator` → `PaceZoneIndexCalculator`; `Distance`/`Pace`/`PaceRange` value objects; eval cache re-recorded. |
| OSS quality tooling restoration (DEC-043) | 2026-04-15 | `docs/specs/09-spec-oss-tooling-restoration/`; `batch-14a-h` research | CodeRabbit / CodeQL / SonarQube Cloud / license-compliance pipeline; `main-protection` ruleset; one-authority-per-signal partitioning. |
| POC 1 review rounds + CI filter fix | 2026-03-22 | `docs/specs/05-spec-*` through `08-spec-*`; PR #18 | DEC-037, DEC-039; xUnit v3 + MTP migration; committed eval-cache CI. |
| POC 1 eval refactor | 2026-03-21 | `docs/plans/poc-1-llm-testing-architecture.md` | M.E.AI.Evaluation infrastructure; `AnthropicStructuredOutputClient`; YAML prompt storage. |
| POC 1 initial implementation | 2026-03-21 | `docs/plans/poc-1-context-injection-plan-quality.md`; PR #17 | Training-science computation layer; `ContextAssembler`; `ClaudeCoachingLlm`. |
| Project scaffolding + quality pipeline | 2026-03-19 | `docs/plans/setup-steps-3-4-handoff.md`; `docs/plans/quality-pipeline-private-repo.md` | DEC-031 through DEC-036; .NET 10 / React 19 scaffolding; Docker + Tilt; Lefthook + commitlint. |
| Planning phase | 2026-03-18 | `docs/planning/*.md`; 18 research artifacts (batches 1-9) | DEC-001 through DEC-030; vision, architecture, safety, coaching persona, interaction model, tiered plan model. |

---

## Deferred Items (Cross-Cycle)

Items that span cycles or are permanently deferred. **Active-cycle follow-ups live in the cycle plan's "Captured During Cycle" section, not here.** This section is only for items that outlive a single cycle.

### From DEC-041 (unit system — partial shipment)

Shipped with DEC-042: `Distance`, `Pace`, `PaceRange(Fast, Slow)`, `TrainingPaces` value objects. Remaining scope deferred to pre-MVP-0: `StandardRace` enum, `UnitPreference` enum, EF Core `ValueConverter` mappings, full controller-layer adoption. See `docs/planning/unit-system-design.md`.

### From POC 1 cleanup

- `EvalTestBase` relative path navigation (`"../../../../../"`) — fragile if structure changes.
- `AsIChatClient()` not on `ICoachingLlm` interface — add to interface or mark internal.
- `WeekGroup` nested record — (a) move to its own file under `Modules/Coaching/` (the `private sealed record` inside `ContextAssembler.cs` violates one-type-per-file; the carve-out is for serialization shapes only, and `WeekGroup` is an aggregation result), and (b) change `List<WorkoutSummary>` to `IReadOnlyList`. Surfaced again in PR #77 deep-review (conv-1).
- Nested types in `YamlPromptStore` — extract to own files or document as intentional.

> **Slice 1 in-cycle PR #77 deep-review follow-ups** (split `ContextAssembler` ctors / derive `Neutralized` / factory methods on `OnboardingTurnOutputValidationResult` / Pattern-B-Invariant permanent-design note) live in the cycle plan's "Captured During Cycle" table — they're scoped within slice 1, not cross-cycle.

### Structured output post-deserialization validation (pre-MVP-0)

Anthropic's constrained decoding enforces property names, types, and `additionalProperties: false`, but does NOT enforce `minItems`/`maxItems` on arrays or numerical `minimum`/`maximum` on scalars. `MesoWeekOutput` addressed structurally via DEC-042. Still open: audit `MacroPlanOutput`, `MicroWorkoutListOutput`, and any future structured outputs for similar invariants; audit eval suite for assertions that depend on LLM compliance with schema descriptions rather than structural enforcement.

### Infrastructure

- Kubernetes — deferred to public beta per DEC-032.
- Garmin Connect integration — deferred to post-MVP-1; Apple Health prioritized per DEC-033.
- Frontend visual design planning — flagged, not yet started.
- **Full visual UI refactor — REQUIRED before MVP launch (user-requested 2026-06-13).** The builder has a full redesign of the web UI in mind (distinct from the Slice 2a token/foundation work, which only established the palette + semantic tokens + dark mode). Not to be started now; schedule as its own pre-launch slice/cycle after the adaptation loop and conversation work land. Scope to be brainstormed at planning time. Pairs with — but is separate from — the LLM-output persona/voice re-tune (cycle plan § Captured During Cycle, 2026-06-13), which is about prompt output, not the UI.
- **Marten 9 / Wolverine 6 upgrade** — ✅ Shipped 2026-05-30 (DEC-071, PR #125): Marten 9.2.1 + Wolverine 6.1.0 on JasperFx 2.0. Remaining levers deferred:
  - **QuickAppend append mode** — Marten 9 made `QuickWithServerTimestamps` the default; we deliberately kept `EventAppendMode.Rich` (DEC-071). ~50% append throughput + fewer skips under contention. Adopt only after validating nothing depends on Rich's client-side timestamps/metadata. Not a POC constraint; revisit at pre-MVP-0 scale.
  - **`Marten.PgVector` for the coaching/LLM layer** — Marten 9.3 ships `UsePgVector()` + a `VectorProjection` base; embeddings live in the same Postgres. Candidate for similar-athlete / session-history retrieval feeding LLM context. Evaluate if RAG-style retrieval enters scope.

### Cost optimization (post-MVP-0, DEC-038)

Tiered model routing (Haiku / Sonnet / Opus) for ~60% cost reduction; Batch API for eval runs (50% discount); Opus 4.6 as eval judge.

### Quality tooling (DEC-043 — deferred / cut)

- Claude Code GitHub Action — **permanently cut.** Replaced by local `/review-pr` + user's `deep-review` skill. Do not re-propose.
- Snyk — **deferred** (R-039). Reconsider triggers: PII ingestion, container deployment, second contributor, Dependabot miss >30 days on high-severity transitive CVE.
- Codacy — **deferred** (R-040). Reconsider only if a language module outside SonarQube Cloud free-tier coverage is added.
- CODEOWNERS — **deferred** until first external contributor joins.

### Quality tooling (add later regardless of visibility)

- Performance regression testing in CI — deferred per DEC-034 (GitHub runner variance).
- Trivy container image scanning — add when deploying Docker images.
- **Trademark build-time analyzer** — Roslyn rule that flags `VDOT` (case-insensitive, with explicit carve-outs in `docs/`, `NOTICE`, `CLAUDE.md`, `README.md`, and the existing live-guard assertions) as a compile error in `Prompts/*` and API response paths. DEC-042's runtime check in `ContextAssemblerTests` is the current safety net; promote to compile-time before the first non-builder contributor joins the repo. Surfaced in the Slice 1B production-grade gap audit (2026-04-27).
- **Reduced-motion build-time lint rule** — ESLint rule that flags Tailwind `transition-*` / `animate-*` utilities lacking a paired `motion-reduce:` variant (e.g. `motion-reduce:transition-none`, `motion-reduce:animate-none`), enforcing the DEC-063 reduced-motion contract (WCAG 2.3.3) in `npm run lint` (the ESLint hard-gate layer) instead of leaving it to CodeRabbit / human review. No off-the-shelf rule covers this class-pairing semantic — needs a custom ESLint rule (or an `eslint-plugin-tailwindcss` extension); treat as a Research Protocol item before implementing. Recurs in CodeRabbit reviews (most recently PR #113, 2026-05-29).

### Test parallelism — per-collection database isolation (DEC-064 deferred reversal)

Restore xunit collection-level parallelism by partitioning `RunCoachAppFactory` into `[Collection]`-scoped fixtures, each owning its own `PostgreSqlContainer` (or schema), Marten `IDocumentStore`, and Wolverine host. Current sequential mode (DEC-064) runs the full 1054-test suite in ~1m47s locally on macOS Colima and ~1m48s on CI Linux — fine for occasional full runs, slow for tight iteration. Reconsider triggers: (a) suite wall-clock exceeds 3 minutes locally, (b) integration test count crosses ~150, (c) a contributor joins and burns time waiting on full runs. Implementation path is documented in DEC-064 § Alternatives; the daily-driver workaround is `dotnet test --filter-not-trait "Category=Integration"` (977 unit + eval tests, ~3s).

### Pre-public-release gate (from `docs/features/backlog.md`)

Everything under "Pre-Public Release" in the feature backlog — extended health screening (PAR-Q+), expanded medical-scope keyword triggers, population-adjusted safety guardrails, beta participation agreement, LLC formation, privacy policy, full ToS. Required before anyone beyond the builder and trusted friends uses the product. Tracked in the feature backlog, not here.
