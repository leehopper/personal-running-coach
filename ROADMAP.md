# RunCoach — Roadmap

Living project state. Read this at the start of every session.

## Current Phase: DEC-042 Shipped — Pure-Equation Pace-Zone Calculator

POC 1 productionized on `feature/poc1-context-injection-v2` (tag `poc1-complete`). DEC-042 pure-equation pace-zone calculator shipped via PR #44 — all six zones, `VdotCalculator` renamed to `PaceZoneIndexCalculator`, lookup table deleted, eval cache re-recorded, 14 deep-review findings resolved across 10 atomic fix commits. POC 2 (adaptive replanning) is next.

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
- Unit 1: Deterministic training science layer — formula-based pace-zone index calculator (internal: `VdotCalculator`), pace calculator with 5 zones, all 5 test profiles with simulated history, `WorkoutSummary`/`WeekSummary` models, comprehensive unit tests
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

**DEC-042 + DEC-041: Pure-equation pace-zone calculator + value objects (2026-04-16, complete on `refactor/dec-042-pace-zone-calculator`):**
- `DanielsGilbertEquations` internal static helper — OxygenCost, FractionalUtilization, SolveVelocityForTargetVo2, PredictRaceTimeMinutes (Newton-Raphson)
- `PaceZoneCalculator` / `IPaceZoneCalculator` — six zones (E, M, T, I, R, F) derived purely from equations; no lookup table
- `PaceZoneIndexCalculator` / `IPaceZoneIndexCalculator` — replaces `VdotCalculator`; 14 race distances, duration/velocity guards, low-index Warning log
- `HeartRateZoneCalculator` / `IHeartRateZoneCalculator` — Tanaka max-HR (208−0.7·age), %HRmax bands, optional Karvonen
- DEC-041 value objects: `Distance`, `Pace`, `PaceRange(Fast, Slow)`, `TrainingPaces` rewrite
- Eval cache re-recorded (10 fixtures updated for `TrainingPaces` shape change)
- DI registered all three new calculators as singletons in `ServiceCollectionExtensions`
- README trademark disclaimer updated: confirms public APIs, user-facing strings, and the primary calculator classes are trademark-neutral; two internal-only identifiers still carry the legacy term and are tracked as a follow-up
- `MesoWeekOutput` structured-output schema restructured from a `Days[]` array to seven required `Sunday..Saturday` properties. Constrained decoding can't enforce array length, and the Priya constrained profile (`MaxRunDaysPerWeek: 4`) was triggering the model to hedge with an 8-day response including a placeholder entry. Named properties make the invariant structural. Un-skipped `Priya_Constrained_RespectsExactly4RunDays` — 3/3 Record-mode passes deterministically.
- `PredictRaceTimeMinutes` Newton-Raphson bug fixed: the solver was rooting `F·VO₂ = index` instead of the Daniels relation `VO₂/F = index`. Symptom at index 50 was a 2:19 marathon M-pace and an inverted F<R ordering. Fix flipped the root condition and rewrote the derivative with the quotient rule. All 56 rows of the equation-anchored fixture regenerated; `CalculatePaces_Monotonicity_AllZonesOrderedFromSlowToFast` and `CalculatePaces_MPacePrecision_WithinPublishedTableTolerance` un-skipped; M-pace at index 50 now reproduces Daniels' published ≈ 271 s/km. Eval cache untouched — test profiles used a lookup-table bridge (`TestPaceCalculator`) that always carried the correct Daniels values, so prompt content was stable; only production runtime was affected by the bug.

**PR #44 deep-review fix round (2026-04-17, complete on `refactor/dec-042-pace-zone-calculator`):**
- Deep-review (`deep-review:deep-review`) surfaced 14 findings across 7 concern-parallel agents — 2 main + 12 suggestions after Opus blind-challenge; all resolved across 10 atomic commits.
- Functional: (bug-1) `ContextAssembler.BuildTrainingPacesSection` now renders `FastRepetitionPace` when populated; `context-injection-v1.yaml` layout spec updated. (type-1+test-4) `PaceRange.Fast`/`.Slow` dropped `init` setters to block `with`-expression invariant bypass, matching `IntRange`; regression test added. (bug-2) `ContextAssemblerTests` asserts the real `"Pace-zone index:"` label instead of coincidental `"VDOT"` match.
- Coverage: (test-1+test-2) HR-zone guard (`restingHr >= maxHr`) and cross-zone ordering tests across multiple maxHr values, both %HRmax and Karvonen modes. (test-3) `Distance.FromMeters` NaN/Infinity guard tests. (test-5) `MesoWeekOutput` JSON schema `required` array contents pinned (7 day names + 5 scalars) so a schema-generator regression re-opening the 8-day bug is caught in CI.
- Quality: (simplify-1+simplify-3) 10 equation constants in `DanielsGilbertEquations` and `PaceZoneCalculator` converted `static readonly double` → `const double`. (simplify-2) unreachable `fractionalUtilization <= 0` guard removed from `PaceZoneIndexCalculator` (F(t) is strictly > 0.8 for all finite t). (conv-2+conv-3) AAA comment markers added to `PaceTests`; two two-part test names renamed to three-part `Method_Scenario_Expected`. (test-6) `SmokeTests` comment corrected — CI does not currently set `RUNCOACH_INTEGRATION_TESTS`.
- Tests: 548 → 563 passing (+15), 0 failures, 1 opt-in skip unchanged. Security-reviewer and cross-file-impact agents both returned 0 findings.

**OSS Quality Tooling Restoration (2026-04-15, complete — DEC-043):**
- Dual-license architecture: Apache-2.0 (code) + CC-BY-NC-SA-4.0 (coaching prompts), NOTICE, THIRD-PARTY-NOTICES.md
- VDOT trademark avoidance: user-facing surface renamed to "Daniels-Gilbert zones" / "pace-zone index" per Runalyze enforcement precedent; VDOT-avoidance rule encoded in all 3 CLAUDE.md and 3 REVIEW.md files; internal code identifier rename delegated to DEC-042
- CodeRabbit restored: `.coderabbit.yaml` schema v2, profile=chill, module-scoped path_instructions
- CodeQL restored: v4.35.1, `security-extended` queries, matrix [csharp, javascript-typescript], build-mode=manual for C#
- SonarQube Cloud restored: two-project monorepo (backend OpenCover, frontend LCOV), advisory dashboard alongside existing build-time analyzers
- License-compliance CI: `dependency-review-action` v4.9.0 PR gate (allow-licenses model) + `sbom-action` v0.24.0 weekly SBOM; NuGet Automatic Dependency Submission enabled
- Branch protection: `main-protection` ruleset with 6 required checks [CI Gate, Analyze (csharp), Analyze (javascript-typescript), Backend analysis, Frontend analysis, License & dependency review], squash-only merge, admin bypass with audit trail
- Action SHAs upgraded to Node.js 24 where available (setup-node v6.3.0, sonarqube-scan-action v7.1.0, sbom-action v0.24.0); dependency-review-action v4.9.0 remains on Node.js 20 (no upgrade available, monitor for v5.x before June 2026 forced migration)
- One-authority-per-signal partitioning: CodeQL=SAST, Codecov=Cobertura coverage, SonarQube Cloud=OpenCover dashboard, dependency-review-action=license+CVE gate
- Spec: `docs/specs/09-spec-oss-tooling-restoration/09-spec-oss-tooling-restoration.md`

**Branch status:** POC 1 productionized and merged to `main`. DEC-042 shipped to `main` via PR #44 (merged 2026-04-17). `fix/daniels-pace-table` deleted from local and remote.

**Daniels pace table — DEC-040 to DEC-042 trajectory:**
- DEC-040 row-shift patch shipped to main 2026-03-25..26 across commits `934f1de`, `0a6e813`, `fbadeda`, `54c4c9c` (row-shift correction, edition citation, eval cache re-record, `PaceRange` invariant).
- 2026-04-14 computational audit found residual anomalies at pace-zone index 49→50 in the Interval (+1.55 pp) and Repetition (+3.69 pp) columns. R-019 had only verified pace-zone index 50; rows 30–49 carried a second error class.
- R-025 (batch 11) established the pure-equation design direction. R-026 through R-031 plus R-034 (batch 12) closed every gap: equation reference, coefficient stability, exact T/I constants (88.0% / 97.3%), five-zone + F, `VdotCalculator` verified correct with 5 missing distances and missing input guards, Tanaka as the HR formula, legal safety for equation-derived fixtures only. R-035 (batch 13) resolved the last remaining gap — R-pace adopts R-028's 3K-race-prediction-with-multipliers formulation (max \|err\| ≤ 1.1 s vs. Daniels' published tables), with `R-800 = 2 × R-400` as a simpler-equal rule. F-pace is a Newton-Raphson solve at 800 m race distance.
- **DEC-040 is superseded by DEC-042**, which replaces the lookup table entirely with a pure-equation `PaceZoneCalculator`. DEC-042 is **Approved — ready for implementation**. See `docs/decisions/decision-log.md` § DEC-042 for full scope.

## Next Up

### 1. POC 2: Adaptive replanning

Next POC in the roadmap. Plan file needed. No blockers.

## Plan Files

- `docs/plans/setup-steps-3-4-handoff.md` — project scaffolding and tooling setup (complete)
- `docs/plans/quality-pipeline-private-repo.md` — quality pipeline redesign for private repo (complete)
- `docs/plans/poc-1-llm-testing-architecture.md` — LLM testing architecture refactor (complete)
- `docs/plans/poc-1-context-injection-plan-quality.md` — context injection and plan quality POC (complete)
- `docs/planning/unit-system-design.md` — unit system architecture (planned, DEC-041)

## POC Roadmap

Four POCs feed into MVP-0 and MVP-1. See `docs/planning/poc-roadmap.md` for details.

- **POC 1:** Context injection & plan quality → feeds MVP-0 **(complete, merged to main, DEC-042 PR pending)**
- **POC 2:** Adaptive replanning → feeds MVP-1
- **POC 3:** Tiered planning efficiency → validates architecture
- **POC 4:** Interaction flow → validates UX

## MVP Milestones

- **MVP-0 (Personal validation):** Conversation + plan generation. Builder uses it on own runs.
- **MVP-1 (Friends/testers):** Adds adaptation + Apple Health integration. The differentiator becomes visible.

## Deferred Items

**Unit system refactor (partial — DEC-041):**
- `Distance`, `Pace`, `PaceRange(Fast, Slow)`, and `TrainingPaces` value objects shipped with DEC-042.
- Remaining DEC-041 scope deferred to pre-MVP-0: `StandardRace` enum, `UnitPreference` enum, EF Core `ValueConverter` mappings, full controller-layer adoption.
- See `docs/planning/unit-system-design.md` for full design.

**POC 1 cleanup (remaining items):**
- `EvalTestBase` relative path navigation (`"../../../../../"`) — fragile if structure changes
- `AsIChatClient()` not on `ICoachingLlm` interface — add to interface or mark internal
- `WeekGroup` nested record uses mutable `List<WorkoutSummary>` — use `IReadOnlyList`
- Nested types in `YamlPromptStore` — extract to own files or document as intentional

**DEC-042 follow-ups (post-PR):**
- `TestPaceCalculator` lookup-table bridge (`backend/tests/.../Profiles/TestPaceCalculator.cs`) is a transitional class carrying the old Daniels lookup values for test profiles. It kept eval-cache prompt content stable during the rewrite, but means the eval tests don't exercise the real `PaceZoneCalculator`. Migrate `TestProfiles` to call `PaceZoneCalculator` directly, delete the bridge, and re-record the Sonnet eval cache. The Newton-Raphson fix brought equation outputs into alignment with Daniels-published values at integer indices, so the migration should produce only minor cache-key drift.
- Three internal identifiers still carry the legacy trademarked term: `FitnessEstimate.EstimatedVdot` property, an XML doc comment on `RaceTime`, and the `"VDOT"` literal inside `TestProfiles.Lee().AssessmentBasis` (surfaced by PR #44 deep-review). The first two are internal-code exempt per REVIEW.md carve-out and harmless. The third is test-fixture content that flows into eval-cache prompt text — scrubbing it requires a Sonnet fixture re-record, so consolidate with the `TestPaceCalculator` migration above.

**Structured output post-deserialization validation (pre-MVP-0, surfaced 2026-04-15):**
Anthropic's constrained decoding enforces property names, types, and `additionalProperties: false`, but does **not** enforce `minItems`/`maxItems` on arrays or numerical `minimum`/`maximum` on scalars. This means the `[Description]` text on structured output records is a hint to the model, not a hard gate — the model can and does violate it under non-deterministic conditions.
- **`MesoWeekOutput` addressed (2026-04-17, shipped with DEC-042):** The 7-day invariant was made structural by replacing `Days: MesoDaySlotOutput[]` with seven required `Sunday..Saturday` properties. The 8-day Priya flake is eliminated by construction; un-Skip'd `Priya_Constrained_RespectsExactly4RunDays` passes deterministically.
- **Still open:** Audit `MacroPlanOutput`, `MicroWorkoutListOutput`, and any future structured outputs for similar invariants that depend on schema-description hints rather than structural guarantees. Consider either (a) schema restructuring (the `MesoWeekOutput` pattern), or (b) a post-deserialization validation + retry layer for cases where structural encoding is impractical. Design considerations: retry budget, whether to surface validation failures or silently retry, violation-rate logging for prompt-tuning feedback, and prompt reinforcement in the system prompt body.
- **Eval gap audit:** Audit the eval suite for assertions that depend on LLM compliance with schema descriptions rather than structurally-enforced constraints. `Priya_Constrained_RespectsExactly4RunDays` surfaced this class of bug; other eval tests may have latent exposure.

**Infrastructure:**
- Kubernetes (deferred to public beta per DEC-032)
- Garmin integration (deferred to post-MVP-1, Apple Health prioritized per DEC-033)
- Frontend visual design planning (flagged, not yet started)

**Cost optimization (post-MVP-0, DEC-038):**
- Tiered model routing: Haiku for simple tasks, Sonnet for coaching, Opus for complex replans + eval judging (~60% cost reduction)
- Batch API for eval runs and scheduled background tasks (50% discount)
- Opus 4.6 as eval judge (replaces Haiku — better reasoning for quality assurance)

**Quality tooling (permanently cut or deferred with reconsider-triggers — see DEC-043):**
- Claude Code GitHub Action — **permanently cut**. Replaced by local `/review-pr` via Max + user's deep-review skill. No `@claude` mentions on PRs. Do not re-propose.
- Snyk — **deferred** (R-039). Four reconsider-triggers: (1) PII ingestion — Snyk's container scanning adds value when we handle personal health data; (2) container deployment — Snyk Container covers base-image CVEs Trivy may lag on; (3) second contributor — `@snyk/protect` patching reduces the "everyone must run Dependabot updates" coordination cost; (4) Dependabot miss — if a high-severity CVE in a transitive dep goes unpatched for >30 days, Snyk's proprietary DB may have caught it.
- Codacy — **deferred** (R-040). Zero residual value. Same SonarAnalyzer.CSharp + eslint-plugin-sonarjs the project already runs. Reconsider only if a language module (Python, Rust) is added outside SonarQube Cloud's free-tier coverage.
- CODEOWNERS — **deferred** until first external contributor joins (same trigger as Snyk reconsider #3).

**Quality tooling (add later regardless of visibility):**
- Performance regression testing in CI (deferred per DEC-034 — GitHub runner variance makes detection unreliable)
- Trivy container image scanning (add when deploying Docker images)
