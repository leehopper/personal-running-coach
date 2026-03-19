# RunCoach — Roadmap

Living project state. Read this at the start of every session.

## Current Phase: Development-Ready

Planning and setup are complete. The repo is fully scaffolded with backend, frontend, Docker, and CI/CD tooling. Ready for POC 1 implementation.

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
- Tooling: Lefthook (pre-commit/commit-msg/pre-push), commitlint, CI pipeline (path-filtered, CodeQL, Codecov), Dependabot
- Deviation: `Modules/Shared` → `Modules/Common` (CA1716 reserved keyword conflict with TreatWarningsAsErrors)

## Next Up

**Implement POC 1** following `docs/plans/poc-1-context-injection-plan-quality.md`. This is the first real development work — a prompt engineering experiment to validate the coaching intelligence before building infrastructure.

## Plan Files

- `docs/plans/setup-steps-3-4-handoff.md` — project scaffolding and tooling setup
- `docs/plans/poc-1-context-injection-plan-quality.md` — context injection and plan quality POC

## POC Roadmap

Four POCs feed into MVP-0 and MVP-1. See `docs/planning/poc-roadmap.md` for details.

- **POC 1:** Context injection & plan quality → feeds MVP-0
- **POC 2:** Adaptive replanning → feeds MVP-1
- **POC 3:** Tiered planning efficiency → validates architecture
- **POC 4:** Interaction flow → validates UX

## MVP Milestones

- **MVP-0 (Personal validation):** Conversation + plan generation. Builder uses it on own runs.
- **MVP-1 (Friends/testers):** Adds adaptation + Apple Health integration. The differentiator becomes visible.

## Deferred Items

- Kubernetes (deferred to public beta per DEC-032)
- Garmin integration (deferred to post-MVP-1, Apple Health prioritized per DEC-033)
- Frontend visual design planning (flagged, not yet started)
- Performance regression testing in CI (deferred per DEC-034)
- SonarCloud dashboard (deferred — SonarAnalyzer.CSharp NuGet covers ~90% of value in-build; add dashboard when codebase grows)
- CodeRabbit PR review (deferred — repo is private, CodeRabbit is free for OSS only; using local `/review-pr` via Max subscription instead)
- Claude Code GitHub Action for PR review (deferred — requires paid API key; using local `/review-pr` via Max subscription instead)
- Public repo visibility (deferred — keeping private to protect coaching prompt IP; revisit when/if free OSS tooling tier becomes worth it)
