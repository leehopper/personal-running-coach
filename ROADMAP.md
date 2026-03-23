# RunCoach — Roadmap

Living project state. Read this at the start of every session.

## Current Phase: POC 1 — PR #18 Merged, PR #17 Ready for Final Review

POC 1 complete on `feature/poc1-context-injection-v2` (PR #17). Eval refactor branch merged (PR #18). All review fixes applied, CI passing with 290 tests in Replay mode. Ready for final review of PR #17 → main.

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

**Branch status:** `feature/poc1-context-injection-v2` — PR #18 merged. PR #17 open against `main`, ready for final review.

## Next Up

### 1. Merge PR #17 to main

PR #17 reviewed (3 review workflows completed). Minor code fixes applied on this branch (password removal, comment fixes, ILogger, formatting, caching). Known data issue (DEC-040: pace table off-by-one from VDOT 50-85) deferred to follow-up PR — does not block merge since the POC is a validation exercise, not production coaching.

### 2. Post-merge: Daniels pace table fix (DEC-040)

Recompute the entire VDOT 30-85 pace table from Daniels-Gilbert equations. Current table has an off-by-one row shift from VDOT 50 onward (R-019 confirmed). Separate PR on a new branch.

### 3. Post-merge: Unit system value objects (DEC-041)

Replace raw `decimal DistanceKm` / `TimeSpan AveragePacePerKm` with typed value objects (`Distance`, `Pace`, `PaceRange`). Foundation for imperial support in MVP-1. Separate PR, see `docs/planning/unit-system-design.md`.

### 4. POC 2: Adaptive replanning

Next POC in the roadmap. Plan file needed.

## Plan Files

- `docs/plans/setup-steps-3-4-handoff.md` — project scaffolding and tooling setup (complete)
- `docs/plans/quality-pipeline-private-repo.md` — quality pipeline redesign for private repo (complete)
- `docs/plans/poc-1-llm-testing-architecture.md` — LLM testing architecture refactor (complete)
- `docs/plans/poc-1-context-injection-plan-quality.md` — context injection and plan quality POC (complete)
- `docs/planning/unit-system-design.md` — unit system architecture (planned, DEC-041)

## POC Roadmap

Four POCs feed into MVP-0 and MVP-1. See `docs/planning/poc-roadmap.md` for details.

- **POC 1:** Context injection & plan quality → feeds MVP-0 **(complete — all review fixes done, ready to merge)**
- **POC 2:** Adaptive replanning → feeds MVP-1
- **POC 3:** Tiered planning efficiency → validates architecture
- **POC 4:** Interaction flow → validates UX

## MVP Milestones

- **MVP-0 (Personal validation):** Conversation + plan generation. Builder uses it on own runs.
- **MVP-1 (Friends/testers):** Adds adaptation + Apple Health integration. The differentiator becomes visible.

## Deferred Items

**Daniels pace table fix (immediate post-merge, DEC-040):**
- Off-by-one row shift from VDOT 50-85 — every entry contains the next VDOT level's paces
- Recompute entire table from Daniels-Gilbert equations, cross-reference against 4th edition book tables
- Standardize both calculators on 4th edition references
- See R-019 research artifact for full analysis

**Unit system refactor (pre-MVP-0, DEC-041):**
- Replace raw `decimal DistanceKm` / `TimeSpan AveragePacePerKm` with `Distance`, `Pace`, `PaceRange` value objects
- Internal canonical storage in meters and seconds-per-km
- `StandardRace` enum, `UnitPreference` enum, EF Core `ValueConverter` mappings
- `PaceRange` naming: `Fast`/`Slow` instead of `Min`/`Max`
- See `docs/planning/unit-system-design.md` for full design

**POC 1 cleanup (before promoting patterns to production):**
- `TestProfiles` in `src/RunCoach.Api/` — move to shared project or test infrastructure
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

**Quality tooling (restore when repo goes public or upgrades to Pro):**
- CodeRabbit PR review (free for OSS only; using local `/review-pr` via Max instead)
- CodeQL security scanning (requires GitHub Team + Code Security; using Trivy instead)
- SonarCloud dashboard (free for OSS only; using SonarAnalyzer.CSharp + eslint-plugin-sonarjs in-build instead)
- Claude Code GitHub Action for PR review (requires API key; using local `/review-pr` via Max instead)
- Branch protection rules or rulesets on `main` (both require GitHub Pro for private repos)

**Quality tooling (add later regardless of visibility):**
- Performance regression testing in CI (deferred per DEC-034 — GitHub runner variance makes detection unreliable)
- Trivy container image scanning (add when deploying Docker images)
- License compliance scheduled workflow (add pre-public release)

**Strategic:**
- Public repo visibility (keeping private to protect coaching prompt IP; revisit when/if free OSS tooling becomes worth it)
