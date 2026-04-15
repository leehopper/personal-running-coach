# RunCoach — Roadmap

Living project state. Read this at the start of every session.

## Current Phase: POC 1 Complete — Merging to Main

POC 1 productionized on `feature/poc1-context-injection-v2`. All POC scaffolding removed (console app, experiment prompts, spec artifacts, poc1 naming). TestProfiles relocated to test project. Eval cache moved from `backend/poc1-eval-cache/` to `backend/tests/eval-cache/`. 290 tests passing in Replay mode (0 failures). Tag `poc1-complete` preserves full POC history.

### Setup Steps

- [x] Tech stack decisions (DEC-031)
- [x] Infrastructure decisions (DEC-032, DEC-033)
- [x] Quality pipeline design (DEC-034)
- [x] Coding standards and conventions (DEC-035)
- [x] Repo restructure + context infrastructure (Step 2)
- [x] Project scaffolding + containerization (Step 3)
- [x] Development workflow tooling (Step 4)
- [x] Open questions cleanup (Step 5)
- [x] POC 1 plan file written (Step 6)
- [x] Verify setup end-to-end (Step 7)

### What's Been Done

**Planning phase (complete):**
- Vision, architecture, safety, coaching persona, interaction model fully designed
- 18 research topics across 8 batches — all integrated into planning docs
- 39 decisions recorded in `docs/decisions/decision-log.md`
- Feature backlog prioritized (MVP-0, MVP-1, pre-public, future)
- 4 POCs defined in `docs/planning/poc-roadmap.md`

**Repo scaffolding (Cowork sessions):**
- Assessed and integrated R-012 research (AI PR review and quality tooling) → DEC-034
- Synthesized coding standards from 4 external sources → DEC-035
- Restructured repo from planning-only to monorepo layout (docs/ + backend/ + frontend/)
- Created context infrastructure: CLAUDE.md (root, 87 lines), ROADMAP.md, backend/CLAUDE.md, frontend/CLAUDE.md
- Created .claude/commands/catchup.md, .claude/rules/ (ef-migrations, secrets-safety), .claude/settings.json (hooks)
- Cleaned up open questions — added POC routing, updated stale DEC-024 references to DEC-033
- Wrote POC 1 plan file with full data model, context injection template, 5 test profiles, BDD acceptance criteria
- Wrote Steps 3-4 handoff document for Claude Code

**Project scaffolding (Claude Code — 2026-03-19):**
- Backend: .NET 10 solution with RunCoach.Api + RunCoach.Api.Tests, Directory.Build.props (TreatWarningsAsErrors, analyzers), Central Package Management, .editorconfig, smoke test (GET /health → 200 OK)
- Frontend: React 19 + Vite + TypeScript strict, Tailwind CSS v4, Redux Toolkit, React Router v7, module-first structure, ESLint + Prettier, Vitest smoke test
- Docker: docker-compose.yml (postgres, pgadmin, redis, aspire-dashboard, api, web), multi-stage Dockerfiles, Tiltfile
- Tooling: Lefthook (pre-commit/commit-msg/pre-push), commitlint, CI pipeline (path-filtered, Codecov), Dependabot
- Deviation: `Modules/Shared` → `Modules/Common` (CA1716 reserved keyword conflict with TreatWarningsAsErrors)

**Quality pipeline implementation (Claude Code — 2026-03-19, complete):**
- Replaced CodeQL with Trivy (filesystem, IaC, and secrets scanning) in CI
- Added eslint-plugin-sonarjs to frontend ESLint config
- Created codecov.yml with 60% project / 70% patch thresholds, backend/frontend flags with carryforward
- Fixed Lefthook: formatters now auto-fix + restage, proper `root:` scoping
- Enabled Dependabot vulnerability alerts
- Branch protection deferred (requires GitHub Pro for private repos)

**POC 1 initial implementation (Claude Code — 2026-03-21, PR #17):**
- Unit 1: Deterministic training science layer — formula-based VDOT calculator, pace calculator with 5 zones, all 5 test profiles with simulated history, `WorkoutSummary`/`WeekSummary` models, comprehensive unit tests
- Unit 2: Coaching prompt & context assembly — `ClaudeCoachingLlm` adapter (sealed, disposable, injectable), structured `AssembledPrompt` with positional sections, token estimation with overflow cascade, `ContextAssembler` with 15K budget enforcement, console app via `Host.CreateApplicationBuilder`
- Unit 3: Eval suite — `PlanGenerationEvalTests` (5 profiles) + `SafetyBoundaryEvalTests` (5 safety scenarios), tagged `[Trait("Category", "Eval")]`, structured result output
- Unit 4: Experiments framework — `ExperimentRunner`/`ExperimentExecutor` with 16+ variations across 4 experiment categories, dry-run mode, observation models, suite results **(removed in PR #18 review fixes — superseded by eval suite)**
- Architecture: sealed classes, immutable records, `ImmutableArray`/frozen collections, proper DI, structured logging

**Refactor 1: YAML Prompt Storage (Claude Code — 2026-03-21, complete on `feature/poc1-eval-refactor`):**
- `IPromptStore` / `YamlPromptStore` with `ConcurrentDictionary` cache, version selection from config
- `PromptRenderer` with simple token replacement (`{{profile}}`, `{{training_history}}`, etc.)
- Migrated `coaching-v1.yaml` / `coaching-v2.yaml` to new schema (static system prompt + context template)
- `ContextAssembler` refactored to use `IPromptStore` for prompt loading
- Console app updated to load YAML prompts with correct content root

**Refactor 2: LLM Testing Architecture (Claude Code — 2026-03-21/22, complete on `feature/poc1-eval-refactor`):**
- Structured output foundation: `MacroPlanOutput`, `MesoWeekOutput`, `MicroWorkoutListOutput` + enum types, `JsonSchemaHelper` with `additionalProperties: false`, `GenerateStructuredAsync<T>` on `ClaudeCoachingLlm`, IChatClient bridge
- M.E.AI.Evaluation infrastructure: `DiskBasedReportingConfiguration` with response caching, `EvalTestBase` rewritten with cached Sonnet + Haiku clients, `PlanConstraintEvaluator` (deterministic) + `SafetyRubricEvaluator` (LLM-as-judge)
- `AnthropicStructuredOutputClient` — custom `DelegatingChatClient` that bridges `ForJsonSchema()` to native Anthropic constrained decoding (DEC-037). The SDK's IChatClient bridge silently drops the schema.
- Model IDs switched to floating aliases: `claude-sonnet-4-6` (coaching), `claude-haiku-4-5` (judging) per DEC-037
- All 17 eval tests pass (5 plan generation + 5 safety + 6 infra + 1 spike). Cached re-run <1 second.
- Safety verdict types: `SafetyVerdict`, `SafetyCriterionResult` with structured output for guaranteed parseable judge responses
- Pre-defined rubric configs for all 5 safety scenarios: Medical, Overtraining, Injury, Crisis, Nutrition
- Code review fixes: constrained decoding for judge calls (SafetyRubricEvaluator now uses ForJsonSchema via AnthropicStructuredOutputClient), removed ambiguous IHostEnvironment DI constructor from YamlPromptStore

**Eval cache CI + cleanup (spec: `03-spec-eval-cache-ci`, 2026-03-22, complete on `feature/poc1-eval-refactor`):**
- xUnit v3 upgrade (v2.9.3 → 3.2.2, MTP runner, TestContext.Current.CancellationToken)
- `EVAL_CACHE_MODE` (Record/Replay/Auto) with `ReplayGuardChatClient` for descriptive cache miss errors
- 22 cache scenarios committed as golden fixtures, CI runs in Replay mode (zero API calls)
- Code cleanup: SplitMessages `\n\n` concat, ConvertSchema null-check, ~75 CancellationToken sites, dead code deleted
- CI: `pull_request` trigger runs on all PRs, coverage restored via `coverlet.msbuild`, `dotnet aieval` installed
- Review fix: wired ReplayGuardChatClient as DelegatingChatClient, fail-fast on Record without API key
- 391 tests passing, 0 warnings, 0 suppressions

**PR #18 review fixes (spec: `04-spec-pr18-review-fixes`, 2026-03-22, complete on `feature/poc1-eval-refactor`):**
- CI fix: `actions/setup-node@v6` → `@v4` (v6 doesn't exist), audited all action versions
- Removed experiment infrastructure: 17 source + 5 test files deleted (~800 LOC), one-time POC exploration superseded by eval suite
- Simplified `ContextAssembler`: removed parameterless constructor, `SystemPromptText` constant, sync `Assemble()` method; `IPromptStore` now required
- Converted `ContextAssemblerTests` (28 methods) and `EvalTestBase` to async `AssembleAsync` with `IPromptStore`
- `AnthropicStructuredOutputClient`: added `.ConfigureAwait(false)` on all 3 await calls
- `YamlPromptStore`: fixed race condition with `GetOrAdd`/`Lazy<Task<T>>`, added cache key `::` separator validation
- `PlanConstraintEvaluator`: added upper-bound check for fast pace tolerance (symmetric with easy pace)
- Replaced `Trace.WriteLine` with `TestContext.Current.SendDiagnosticMessage` (xUnit v3 visible output)
- Removed dead `ExtractJson` method from `PlanGenerationEvalTests`
- Added `FUTURE:` comment on unused `ContextTemplate` (SonarAnalyzer S1135 compliant)
- New eval cache fixtures committed (cache keys changed due to IPromptStore refactor)
- 292 tests passing, 0 warnings, 0 suppressions, validation: PASS (all 6 gates)

**PR #18 review round 2 (spec: `05-spec-pr18-review-round2`, 2026-03-22, complete on `feature/poc1-eval-refactor`):**
- CI security hardening: SHA-pinned all 12 GitHub Actions to commit SHAs with version comments
- Moved PostgreSQL password from `appsettings.json` to `appsettings.Development.json`
- Cache TTL fix (DEC-039): extended all 44 `entry.json` fixtures to `9999-12-31` expiration, added `.gitattributes` binary markers, documented re-recording workflow
- Test hygiene: refactored `ParseCacheMode` to accept parameter injection (no more `Environment.SetEnvironmentVariable` in tests), removed tautological test, sealed 3 test classes, added `LogCacheHit` call site
- Code quality: `WriteEvalResult` → async `WriteEvalResultAsync`, named `PaceTolerancePercent` constant, word-boundary regex for crisis hotline assertions, text-only limitation comment on `SplitMessages`, documented `T[]` as intentional for structured output JSON deserialization
- Research: R-017 (eval cache TTL best practices) → DEC-039
- 290 tests passing, 0 warnings, 0 suppressions, validation: PASS (all 6 gates)

**CI filter fix (2026-03-22, complete — merged via PR #18):**
- MTP ignores VSTest `--filter` syntax (MTP0001 warning) — eval tests were running unfiltered in CI
- Fixed: all tests now run in CI including eval in Replay mode via committed cache fixtures
- Used `TestingPlatformCommandLineArguments` with MTP-native `--filter-not-trait` for future use if needed
- Removed `ParseCacheMode` null test case that conflicted with `EVAL_CACHE_MODE` env var in CI
- Research: R-018 (xUnit v3 MTP filtering) — `coverlet.MTP` incompatible with xUnit v3's bundled MTP 1.x, staying on `coverlet.msbuild`

**Branch status:** POC 1 productionized and merged to `main`. Follow-up branch `fix/daniels-pace-table` holds the 2026-04-14 integrity-fence audit work; that branch is now **discardable** — see Next Up item 2.

**Daniels pace table — DEC-040 to DEC-042 trajectory:**
- DEC-040 row-shift patch shipped to main 2026-03-25..26 across commits `934f1de`, `0a6e813`, `fbadeda`, `54c4c9c` (row-shift correction, edition citation, eval cache re-record, `PaceRange` invariant).
- 2026-04-14 computational audit found residual anomalies at VDOT 49→50 in the Interval (+1.55 pp) and Repetition (+3.69 pp) columns. R-019 had only verified VDOT 50; rows 30–49 carried a second error class.
- R-025 (batch 11) established the pure-equation design direction. R-026 through R-031 plus R-034 (batch 12) closed every gap: equation reference, coefficient stability, exact T/I constants (88.0% / 97.3%), five-zone + F, `VdotCalculator` verified correct with 5 missing distances and missing input guards, Tanaka as the HR formula, legal safety for equation-derived fixtures only. R-035 (batch 13) resolved the last remaining gap — R-pace adopts R-028's 3K-race-prediction-with-multipliers formulation (max \|err\| ≤ 1.1 s vs. Daniels' published tables), with `R-800 = 2 × R-400` as a simpler-equal rule. F-pace is a Newton-Raphson solve at 800 m race distance.
- **DEC-040 is superseded by DEC-042**, which replaces the lookup table entirely with a pure-equation `PaceZoneCalculator`. DEC-042 is **Approved — ready for implementation**. See `docs/decisions/decision-log.md` § DEC-042 for full scope.

## Next Up

### 1. DEC-042 implementation — pure-equation `PaceZoneCalculator`

Unblocked. Single PR, scope per `docs/decisions/decision-log.md` § DEC-042:
- Delete current `PaceCalculator` lookup table (legally unsafe per R-034; the `fix/daniels-pace-table` integrity fence is also discarded at this point — the fence embeds book-transcribed values and was only ever a stepping stone).
- New `PaceZoneCalculator` + internal `DanielsGilbertEquations` helper. Three Newton-Raphson call sites: 42,195 m (Marathon), 3,000 m (Repetition), 800 m (Fast Repetition).
- Hybrid derivation: closed-form quadratic inversion for E (70% / 59%), T (88.0%), I (97.3%); Newton-Raphson race prediction for M, R (with R-200 = 0.9295 × (200/3000) × t₃ₖ, R-400 = 0.9450 × (400/3000) × t₃ₖ, R-800 = 2 × R-400), and F (F-400 = t₈₀₀ / 2, F-200 = t₈₀₀ / 4).
- Add 5 missing race distances and input-validation guards to `VdotCalculator` (3.5–300 min duration, velocity > 50 m/min, VDOT 25–90 range with low-VDOT warning).
- Replace `EstimateMaxHr = 220 − age` with Tanaka `208 − 0.7·age`.
- New `HeartRateZoneCalculator` (separate from pace) with Daniels' %HRmax bands.
- Equation-derived golden fixture with provenance header citing the 1979 *Oxygen Power* monograph; replaces the old integrity fence.
- DEC-041 value objects (`Distance`, `Pace`, `PaceRange(Fast, Slow)`) land in the same PR.
- Trademark disclaimer in README.

### 2. DEC-041: Unit system value objects (lands with DEC-042)

Kitchen-sink alignment: DEC-041's `Distance`/`Pace`/`PaceRange(Fast, Slow)` value objects are in scope for the DEC-042 PR. This is a deliberate cutover — DEC-042 has to replace the calculator's return types anyway, so wrapping them as typed values is essentially free at that point. See `docs/planning/unit-system-design.md` for the full design.

### 3. OSS Quality Tooling Restoration

Repo was flipped from private to public on 2026-04-15. DEC-034's private-repo amendment stripped CodeRabbit, CodeQL, SonarQube Cloud, and branch protection out of the pipeline; those become restorable now. Research document and handoff prompts live at `docs/specs/research-oss-tooling-restoration/research-oss-tooling-restoration.md`. Scope:

- **Restore:** CodeRabbit (`.coderabbit.yaml` with module-scoped path instructions, coaching prompts explicitly no-touch), CodeQL (C# manual build-mode against `.slnx` + javascript-typescript, `security-extended` suite, no overlap with existing build-time Sonar rules), SonarQube Cloud (CI-based analysis via `dotnet-sonarscanner`, Codecov remains coverage authority, MCP server integration is user-side not CI-side), branch protection ruleset on `main` (soft-launch sequencing — `continue-on-error` first week, then required).
- **Cut permanently:** Claude Code GitHub Action. Replaced by local `/review-pr` + user's deep-review skill. Do not re-propose.
- **License, trademark, and attribution pass (item 8):** Pick a `LICENSE` (user decision — research prompt R-LIC compares MIT / Apache-2.0 / MPL-2.0 / AGPL-3.0 / BUSL / PolyForm NC), commit `NOTICE` / `THIRD-PARTY-NOTICES.md`, extend DEC-042's Daniels trademark disclaimer into a broader README attribution section, choose a provenance convention for `docs/research/artifacts/`, update root `package.json` `license` field to match.
- **License-compliance scheduled workflow (item 9):** PR-triggered `actions/dependency-review-action` with license allow/denylist keyed to the chosen repo license, plus a weekly scheduled SBOM workflow (FOSSA vs Syft to be decided in research prompt R-LCC). Pre-public trigger has fired.
- **Explicit deferrals (captured here so future sessions do not re-propose):** Snyk (R-SN — superseded by Dependabot + Trivy + CodeQL; reconsider when we deploy container images or hit an unfixable transitive-dep CVE), Codacy (R-CD — its only unique value is multi-tool consolidation, which SonarQube Cloud + CodeQL make redundant; reconsider if we add a Python or Rust module outside SonarQube Cloud's free-tier coverage).

Not blocked by DEC-042 implementation — can land in parallel.

### 4. POC 2: Adaptive replanning

Next POC in the roadmap. Plan file needed. Not blocked by any of the above. Can start in parallel with DEC-042 implementation if capacity allows.

## Plan Files

- `docs/plans/setup-steps-3-4-handoff.md` — project scaffolding and tooling setup (complete)
- `docs/plans/quality-pipeline-private-repo.md` — quality pipeline redesign for private repo (complete)
- `docs/plans/poc-1-llm-testing-architecture.md` — LLM testing architecture refactor (complete)
- `docs/plans/poc-1-context-injection-plan-quality.md` — context injection and plan quality POC (complete)
- `docs/planning/unit-system-design.md` — unit system architecture (planned, DEC-041)

## POC Roadmap

Four POCs feed into MVP-0 and MVP-1. See `docs/planning/poc-roadmap.md` for details.

- **POC 1:** Context injection & plan quality → feeds MVP-0 **(productionized, merging to main)**
- **POC 2:** Adaptive replanning → feeds MVP-1
- **POC 3:** Tiered planning efficiency → validates architecture
- **POC 4:** Interaction flow → validates UX

## MVP Milestones

- **MVP-0 (Personal validation):** Conversation + plan generation. Builder uses it on own runs.
- **MVP-1 (Friends/testers):** Adds adaptation + Apple Health integration. The differentiator becomes visible.

## Deferred Items

**Pure-equation pace zones (DEC-042) — implementation deferred pending R-035:**
- Design direction is crystallized and captured in DEC-042. Zone-derivation methodology is pinned for E/T/I (70%/59%, 88.0%, 97.3%) and M (Newton-Raphson at 42,195 m). Only R-pace formulation is open, pending R-035.
- Implementation scope is fully specified in DEC-042 (hybrid derivation, optional F zone, missing distances, input guards, Tanaka HR, separate `HeartRateZoneCalculator`, DEC-041 value-object integration, equation-derived fixture, trademark disclaimer).
- R-032 (multi-methodology interface extensibility) and R-033 (LLM pace-zone consumption precision) remain deferred as non-blocking for DEC-042 correctness. R-032 is addressed proactively via `IPaceZoneCalculator` interface shape.

**Unit system refactor (pre-MVP-0, DEC-041):**
- Replace raw `decimal DistanceKm` / `TimeSpan AveragePacePerKm` with `Distance`, `Pace`, `PaceRange` value objects
- Internal canonical storage in meters and seconds-per-km
- `StandardRace` enum, `UnitPreference` enum, EF Core `ValueConverter` mappings
- `PaceRange` naming: `Fast`/`Slow` instead of `Min`/`Max`
- See `docs/planning/unit-system-design.md` for full design

**POC 1 cleanup (remaining items):**
- `ContextAssembler` uses `DateTime.UtcNow` directly — inject `TimeProvider` for testability
- `EvalTestBase` relative path navigation (`"../../../../../"`) — fragile if structure changes
- `AsIChatClient()` not on `ICoachingLlm` interface — add to interface or mark internal
- `WeekGroup` nested record uses mutable `List<WorkoutSummary>` — use `IReadOnlyList`
- Nested types in `PaceCalculator` and `YamlPromptStore` — extract to own files or document as intentional

**Infrastructure:**
- Kubernetes (deferred to public beta per DEC-032)
- Garmin integration (deferred to post-MVP-1, Apple Health prioritized per DEC-033)
- Frontend visual design planning (flagged, not yet started)

**Cost optimization (post-MVP-0, DEC-038):**
- Tiered model routing: Haiku for simple tasks, Sonnet for coaching, Opus for complex replans + eval judging (~60% cost reduction)
- Batch API for eval runs and scheduled background tasks (50% discount)
- Opus 4.6 as eval judge (replaces Haiku — better reasoning for quality assurance)

**Quality tooling — moved to active "OSS Quality Tooling Restoration" under Next Up §3** as of 2026-04-15 public flip:
- CodeRabbit PR review, CodeQL, SonarQube Cloud (formerly SonarCloud), branch protection — all now restorable
- License-compliance scheduled workflow — pre-public trigger has fired

**Quality tooling (permanently cut or deferred with reconsider-triggers):**
- Claude Code GitHub Action — **permanently cut**. Replaced by local `/review-pr` via Max + user's deep-review skill. No `@claude` mentions on PRs. Do not re-propose.
- Snyk — **deferred**. Rejected in DEC-034 with trigger "reconsider if repo goes private"; public flip is the inverse trigger but Dependabot + Trivy + CodeQL cover the same signals for this stack. Reconsider when deploying container images or when an unfixable transitive-dep CVE appears. Research prompt R-SN in `docs/specs/research-oss-tooling-restoration/` if fresh 2026 data is needed before the reconsider.
- Codacy — **deferred**. Was DEC-034's optional "Phase 2" SAST layer; Phase 2 never arrived. SonarQube Cloud + CodeQL + existing build-time analyzers cover all Codacy-unique signals except multi-tool consolidation, which our intentionally-small stack does not need. Reconsider if we add a language module (Python, Rust) outside SonarQube Cloud's free-tier coverage. Research prompt R-CD in `docs/specs/research-oss-tooling-restoration/` if fresh data is needed.

**Quality tooling (add later regardless of visibility):**
- Performance regression testing in CI (deferred per DEC-034 — GitHub runner variance makes detection unreliable)
- Trivy container image scanning (add when deploying Docker images)

**Strategic:**
- Public repo visibility (keeping private to protect coaching prompt IP; revisit when/if free OSS tooling becomes worth it)
