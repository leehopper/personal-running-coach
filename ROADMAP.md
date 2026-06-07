# RunCoach — Roadmap

**Current cycle:** MVP-0 + Adaptation Loop — `docs/plans/mvp-0-cycle/cycle-plan.md`
**Active slice:** **None — between slices. Slice 2b (Workout Logging proper) complete, merged 2026-06-07; Slice 3 (Adaptation Loop) is next (not started).** All 8 of Slice 2b's stacked PRs landed: #134 PR1 `plan-anchor` + #136 PR6a `fe-infra` (2026-06-01); #145 PR2 `workoutlog-entity` (2026-06-05); #147 PR3 `create` + #151 PR4 `query` + #154 PR5 `context-assembler` (2026-06-06); #156 PR6b `log-form` + #158 PR7 `history+e2e` (2026-06-07). Slice 2 (Workout Logging) was decomposed 2026-05-18 into **2a Frontend Visual Foundation** (palette + semantic design tokens, light/dark, Tailwind v4 `@theme`, shadcn/ui install, typography/spacing scale, surface migration) and **2b Workout Logging proper** (data model, endpoints, `ContextAssembler` extension, log form / today's card / history list) — **both now complete**. **Sub-project 2a — merged 2026-05-30** across PRs #111–#114 (foundation → contrast-gate → auth/onboarding migration → home/settings + dark-mode toggle), spec `docs/specs/15-spec-slice-2a-frontend-foundation/`. **Sub-project 2b — merged 2026-06-07** across the 8 PRs above, spec `docs/specs/16-spec-slice-2b-workout-logging/`. Slices 0, 1, and 1B are also complete and merged.
**Next step:** **Begin Slice 3 (Adaptation Loop).** Read the requirements doc `docs/plans/mvp-0-cycle/slice-3-adaptation.md` + the cycle plan + the referenced research, then write the Slice 3 spec under `docs/specs/`. Slice 3 wires the adaptation loop end-to-end: logged workout → deterministic deviation computation → LLM adaptation prompt (consuming the recent-log context PR5 now injects) → structured-output plan modification → Marten event appended → projection updated → plan view re-renders → chat panel surfaces the explanation. DEC-073 (synchronous in-handler LLM-failure policy — decided pre-2b, first live here) applies. Slice 3 also gates the **live end-to-end validation pass** (§ MVP Milestones): once the onboard → plan → log → adapt loop is whole, drive a real browser run against a funded Anthropic account. The Slice 2b brainstorm resolutions and stacked-PR record live in `docs/plans/mvp-0-cycle/slice-2-logging.md` and the cycle plan.
**Blockers:** None.

**Slice 2b pre-spec decisions:** DEC-072 (`WorkoutLog.Metrics` open `jsonb` bag + single-sourced canonical keys + first EF `ValueConverter`, R-077/R-080-P2), DEC-073 (synchronous LLM-failure policy: SDK-only retry + `Kind=Error` envelope, R-078), DEC-074 (eval-cache↔prompt SHA-256 sentinel, R-080-P1), DEC-075 (logging-form conventions, R-079). Batch 28; load-bearing claims independently re-verified before lock.
**Slice 2b in-flight decision:** DEC-077 (EF-native idempotency on the `WorkoutLog` create path — an `IdempotencyKey` column + unique `(UserId, IdempotencyKey)` index inserted in the **same** `SaveChanges` as the row, catching SQLSTATE 23505 to re-read the prior `WorkoutLogId`; the Marten-document `IIdempotencyStore` stays the mechanism for the LLM/handler endpoints). Ratified mid-PR3: the 2b spec's carried-over "co-transactional Marten marker" phrasing is impossible for a pure-EF immutable fact with no Marten stream or handler (R-081 / `batch-29a`).
**Slice 2a decision:** DEC-070 (frontend design-token architecture — two-tier Catppuccin hybrid tokens + shadcn/ui on Tailwind v4 + class-based dark mode, R-075).
**Slice 1B decisions:** DEC-066 (OpenAPI → TS+Zod codegen, R-071), DEC-067 (Marten upcasting, R-072), DEC-068 (error boundary, R-073), DEC-069 (client OTel, R-074).
**Slice 1 decisions:** DEC-057 through DEC-064 — see decision log.

This is the front door. For the full picture on session start, run `/catchup`. For anything deeper than the Status block above, open the cycle plan.

---

## Entry Points

Agents arriving cold should resolve intent to a file before reading:

- **"What should I work on?"** → active cycle plan (pointer above).
- **"What's the active slice doing?"** → active slice spec under `docs/specs/` (pointer in cycle plan's Status section, once a slice is underway).
- **"How does X work?"** → `docs/planning/{topic}.md` + the relevant module under `backend/src/RunCoach.Api/Modules/` or `frontend/src/app/modules/`.
- **"Why was X decided?"** → `docs/decisions/decision-log.md` (DEC-001 through DEC-076).
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
- Decision log: `docs/decisions/decision-log.md` (76 entries)
- Feature backlog: `docs/features/backlog.md`
- Research queue & artifacts: `docs/research/research-queue.md`, `docs/research/artifacts/`
- POC roadmap (historical framing, superseded by cycle plans): `docs/planning/poc-roadmap.md`

---

## MVP Milestones

- **MVP-0 (personal validation):** Onboarding + plan generation + workout logging + adaptation loop. Builder uses it on own runs. **Currently building** — see cycle plan.
  - **Live end-to-end validation pass — gated after Slice 3 (Adaptation Loop).** CI and the local suite exercise every LLM surface in **Replay mode** against the committed eval cache (zero real API calls), so the live coaching path is never proven by automated tests. Once Slice 3 lands the full **onboard → plan → log → adapt** loop, do a real end-to-end run: bring up the Path B host-run stack (`docker compose up -d postgres redis` + `dotnet run` API + Vite, per `CONTRIBUTING.md`), point `Anthropic:ApiKey` (the `runcoach-api` user-secrets store, **not** `runcoach-api-tests`) at a **funded** Anthropic account, and drive the whole flow in the browser. This is the point where testing the full flow is genuinely meaningful, and it backstops the stub/Replay gap — proven necessary by the Wolverine 6 onboarding 500 (DEC-071 / PR #129) that surfaced only under a live turn. Re-run the full pass at MVP-0 close after Slice 4 (open conversation) completes the loop.
- **MVP-1 (friends / testers):** Adds proactive coaching + Apple Health integration. The adaptive differentiator becomes externally visible.

---

## Cycle History

Chronological log of completed cycles / phases, most recent first. One line per cycle — full detail lives in the linked artifacts (decision log, plan files, PRs).

| Cycle / Phase | Completed | Primary Artifacts | Key Outcomes |
|---|---|---|---|
| MVP-0 Slice 2b — Workout Logging | 2026-06-07 | PRs #134 / #136 / #145 / #147 / #151 / #154 / #156 / #158; spec `docs/specs/16-spec-slice-2b-workout-logging/`; DEC-072–077; `batch-28{a–d}` + `batch-29a` research | `WorkoutLog` EF entity (open `jsonb` metrics bag + single-sourced `WorkoutMetricKeys` + EF-10 optional complex-type prescription snapshot + the repo's first `ValueConverter`s) with migration + repository; `POST /api/v1/workouts/logs` create (server-authoritative snapshot resolution + EF-native idempotency on a unique `(UserId, IdempotencyKey)` index, DEC-077) and `POST .../logs/query` DB-driven keyset history endpoint; `ContextAssembler` recent-log injection (compact Layer-1 one-liners, eval-proofed); `PlanStartDate` calendar anchor + pure `PlanCalendar` date→slot mapper; `/log` logging form (RHF/Zod derived from generated create-request Zod, pessimistic create + toast) and `/history` ISO-week-grouped sparse-`<dl>` surface; Playwright E2E for the log→history journey. |
| MVP-0 Slice 2a — Frontend Visual Foundation | 2026-05-30 | PRs #111–#114; spec `docs/specs/15-spec-slice-2a-frontend-foundation/`; DEC-070; `batch-26a` research | shadcn/ui actually installed (`new-york`, `components.json`, `cn()`); two-tier Catppuccin design tokens (Latte/Mocha) in `index.css` gated by a `check-contrast` WCAG script (pre-commit + CI); class-based dark mode via `ThemeProvider` + no-flash script + 3-state Settings toggle; auth / onboarding / home / settings surfaces migrated onto semantic tokens; `tw-animate-css` for Radix enter/exit (`motion/react` still deferred per DEC-063). |
| MVP-0 Slice 0 — Foundation | 2026-04-23 | PRs #49 / #50 / #63; spec `docs/specs/12-spec-slice-0-foundation/` | Persistence substrate (EF + Marten + Wolverine), auth API (register / login / me / logout / xsrf on `CookieOrBearer` with antiforgery + timing-safe login + Identity-error → DTO-bucket mapping), and cookie-session frontend (RTK Query + React Hook Form + Zod + Playwright happy-path). DEC-048 through DEC-056 landed along the way. |
| Spec 11 — TestPaceCalculator migration + VDOT residue scrub + eval cache re-record | 2026-04-18 | PR #45 | Closed both DEC-042 follow-ups: `TestPaceCalculator` bridge deleted and all four race-carrying profiles migrated to real `PaceZoneCalculator`; `FitnessEstimate.EstimatedVdot` → `EstimatedPaceZoneIndex`; `RaceTime` XML doc and four `AssessmentBasis` literals scrubbed; parameterized `ContextAssemblerTests` Theory guards full assembled prompt against VDOT regression for all 5 profiles; Sonnet + Haiku eval cache re-recorded. |
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
- **Trademark build-time analyzer** — Roslyn rule that flags "VDOT" (case-insensitive, with explicit carve-outs in `docs/`, `NOTICE`, `CLAUDE.md`, `README.md`, and the existing live-guard assertions) as a compile error in `Prompts/*` and API response paths. DEC-042's runtime check in `ContextAssemblerTests` is the current safety net; promote to compile-time before the first non-builder contributor joins the repo. Surfaced in the Slice 1B production-grade gap audit (2026-04-27).
- **Reduced-motion build-time lint rule** — ESLint rule that flags Tailwind `transition-*` / `animate-*` utilities lacking a paired `motion-reduce:` variant (e.g. `motion-reduce:transition-none`, `motion-reduce:animate-none`), enforcing the DEC-063 reduced-motion contract (WCAG 2.3.3) in `npm run lint` (the ESLint hard-gate layer) instead of leaving it to CodeRabbit / human review. No off-the-shelf rule covers this class-pairing semantic — needs a custom ESLint rule (or an `eslint-plugin-tailwindcss` extension); treat as a Research Protocol item before implementing. Recurs in CodeRabbit reviews (most recently PR #113, 2026-05-29).

### Test parallelism — per-collection database isolation (DEC-064 deferred reversal)

Restore xunit collection-level parallelism by partitioning `RunCoachAppFactory` into `[Collection]`-scoped fixtures, each owning its own `PostgreSqlContainer` (or schema), Marten `IDocumentStore`, and Wolverine host. Current sequential mode (DEC-064) runs the full 1054-test suite in ~1m47s locally on macOS Colima and ~1m48s on CI Linux — fine for occasional full runs, slow for tight iteration. Reconsider triggers: (a) suite wall-clock exceeds 3 minutes locally, (b) integration test count crosses ~150, (c) a contributor joins and burns time waiting on full runs. Implementation path is documented in DEC-064 § Alternatives; the daily-driver workaround is `dotnet test --filter-not-trait "Category=Integration"` (977 unit + eval tests, ~3s).

### Pre-public-release gate (from `docs/features/backlog.md`)

Everything under "Pre-Public Release" in the feature backlog — extended health screening (PAR-Q+), expanded medical-scope keyword triggers, population-adjusted safety guardrails, beta participation agreement, LLC formation, privacy policy, full ToS. Required before anyone beyond the builder and trusted friends uses the product. Tracked in the feature backlog, not here.
