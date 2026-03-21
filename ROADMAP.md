# RunCoach — Roadmap

Living project state. Read this at the start of every session.

## Current Phase: POC 1 — In Progress

POC 1 initial implementation complete on `feature/poc1-context-injection-v2` (PR #17). Two refactoring streams in progress before final review and merge to main.

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
- 12 research topics across 5 batches — all integrated into planning docs
- 35 decisions recorded in `docs/decisions/decision-log.md`
- Feature backlog prioritized (MVP-0, MVP-1, pre-public, future)
- 4 POCs defined in `docs/planning/poc-roadmap.md` (none started)

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
- Unit 4: Experiments framework — `ExperimentRunner`/`ExperimentExecutor` with 16+ variations across 4 experiment categories, dry-run mode, observation models, suite results
- Architecture: sealed classes, immutable records, `ImmutableArray`/frozen collections, proper DI, structured logging
- Known gaps: system prompt hardcoded in C# (spec calls for YAML), LLM testing strategy needs rework, eval tests not yet run against live API

## Next Up

**POC 1 refactoring and review** — two sub-PRs targeting `feature/poc1-context-injection-v2`, then a full review before merge to main.

### Refactor 1: YAML Prompt Storage & Spec Gaps

The initial implementation hardcodes the system prompt in C#. The spec calls for versioned YAML prompt files (`coaching-v1.yaml`, `context-injection-v1.yaml`). This refactor also addresses minor gaps identified in the v1/v2 comparison (dedicated `PlanResponseParser`, any missing spec deliverables). Will go through full research/spec/plan/PR workflow, targeting `feature/poc1-context-injection-v2` as base branch.

### Refactor 2: LLM Testing Strategy & Anthropic Integration

Rethink the approach to LLM-dependent tests (eval suite) and the Anthropic SDK integration. Includes getting all tests passing. Details provided via cw-research at start. Will go through full research/spec/plan/PR workflow, targeting `feature/poc1-context-injection-v2` as base branch.

### Final Step: Full POC 1 PR Review

After both refactors are merged into `feature/poc1-context-injection-v2`, do a comprehensive review of PR #17 (the entire POC 1 implementation) before merging to main.

## Plan Files

- `docs/plans/setup-steps-3-4-handoff.md` — project scaffolding and tooling setup (complete)
- `docs/plans/quality-pipeline-private-repo.md` — quality pipeline redesign for private repo (complete)
- `docs/plans/poc-1-context-injection-plan-quality.md` — context injection and plan quality POC (active)

## POC Roadmap

Four POCs feed into MVP-0 and MVP-1. See `docs/planning/poc-roadmap.md` for details.

- **POC 1:** Context injection & plan quality → feeds MVP-0 **(in progress — PR #17, refactoring before final review)**
- **POC 2:** Adaptive replanning → feeds MVP-1
- **POC 3:** Tiered planning efficiency → validates architecture
- **POC 4:** Interaction flow → validates UX

## MVP Milestones

- **MVP-0 (Personal validation):** Conversation + plan generation. Builder uses it on own runs.
- **MVP-1 (Friends/testers):** Adds adaptation + Apple Health integration. The differentiator becomes visible.

## Deferred Items

**Infrastructure:**
- Kubernetes (deferred to public beta per DEC-032)
- Garmin integration (deferred to post-MVP-1, Apple Health prioritized per DEC-033)
- Frontend visual design planning (flagged, not yet started)

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
