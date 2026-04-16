# RunCoach — AI-Powered Adaptive Running Coach

## What This Is

A solo-developer project building an AI running coach that maintains a persistent adaptive coaching relationship. The AI is the coach — it builds training plans, consumes workout results, and continuously adapts. It does NOT do live workout tracking (that's Garmin/Strava/Apple Health territory).

**Current Phase: POC 1 Complete — Merging to Main**
See ROADMAP.md for current status and next steps.

## Tech Stack

- **Backend:** .NET 10 / C# 14, ASP.NET Core controllers, EF Core + Marten (event sourcing on PostgreSQL JSONB), Wolverine (background processing), ASP.NET Core Identity + JWT
- **Frontend:** React 19 + TypeScript (strict), Vite SPA, React Router v7, Redux Toolkit + RTK Query, Tailwind CSS + shadcn/ui, React Hook Form + Zod
- **Testing:** xUnit v3 (MTP runner) + FluentAssertions + NSubstitute + M.E.AI.Evaluation, Vitest + React Testing Library, Playwright E2E
- **Infrastructure:** Docker Compose + Tilt (local dev), Colima, GitHub Actions CI/CD, PostgreSQL + Redis
- **Quality:** Lefthook pre-commit, local `/review-pr` via Max (PR review), Trivy + Codecov (CI), SonarAnalyzer.CSharp + eslint-plugin-sonarjs (build-time analysis), Dependabot
- **LLM:** Claude Sonnet 4.6 via thin adapter interface (`ICoachingLlm`). Prompts in versioned YAML files (`Prompts/`), not code.

## Architecture Principles

- **Deterministic computation layer + LLM coaching layer** — never use LLMs for structured data tasks (pace calculations, zone math, ACWR). LLMs handle coaching conversation, plan narrative, and adaptation reasoning.
- **Event-sourced plan state** via Marten on PostgreSQL. Plans are event streams, not mutable rows.
- **Client-agnostic REST API** — JWT auth, URL-versioned (`/api/v1/`), no browser assumptions. Web SPA and future iOS app are both API clients.
- **Module-first organization** — both backend (`Modules/{Domain}/`) and frontend (`modules/{feature}/`). Technical layers enforced by code, not folders.

## Repo Structure

```
CLAUDE.md              # You are here (always loaded)
ROADMAP.md             # Living project state — read every session
dotnet-tools.json      # Local tool manifest (dotnet aieval)
backend/               # .NET API (has its own CLAUDE.md)
frontend/              # React SPA (has its own CLAUDE.md)
docs/
  planning/            # Vision, architecture, safety, coaching persona
  decisions/           # Decision log (DEC-001 through DEC-039)
  features/            # Feature backlog
  research/            # Research queue, prompts, and artifacts
```

## Quick Start Commands

**Backend** (run from `backend/`):
- `dotnet build` — build all projects
- `dotnet test` — run all tests (evals run in Replay mode from committed cache fixtures)
- `dotnet tool restore` — restore local tools (aieval)

**Frontend** (run from `frontend/`):
- `npm run dev` — start dev server on port 5173
- `npm run build` — type-check + production build
- `npm run test` — run Vitest suite
- `npm run lint` — ESLint check
- `npm run format` — Prettier auto-format

## Development Workflow

### Session Protocol

1. Read ROADMAP.md to understand current state
2. Read the relevant plan file before implementing anything
3. One clear objective per session
4. Run build + tests after every change
5. Commit after every completed task
6. Update ROADMAP.md with progress before ending

### Plan-First Development (DEC-008)

No implementation without a reviewed plan file. Every plan file includes BDD acceptance criteria (Given/When/Then) that become test specs. The cycle: research → plan → annotate → implement → verify.

### Git Standards

- **Trunk-based development** with `main` as trunk
- **Branch naming:** `{type}/{issue-number}-{short-description}` (feature, fix, refactor, etc.)
- **Commit messages:** Conventional Commits — `{type}: {description}` (feat, fix, docs, refactor, test, chore, ci)
- Ask for issue number if unknown before creating a branch

### Research Protocol

Never ad-hoc web search for planning decisions. Always: add topic to research queue → generate prompt → hand off to deep research agent → store artifact → integrate findings. See `docs/research/research-queue.md`.

## Security Rules

- NEVER read, display, or commit files containing secrets (.env, secrets.json, credentials, API keys, tokens, certificates)
- If secrets appear in a diff, STOP and warn immediately
- Secrets go in environment variables or git-ignored files
- Use .NET user-secrets for local dev

## Trademark Rule: VDOT

User-facing surface (coaching prompts, UI strings, README, documentation, API responses, commit messages, PR descriptions) must use "**Daniels-Gilbert zones**", "**pace-zone index**", or generic exercise-physiology terminology — **not** "VDOT". The VDOT mark is actively enforced by The Run SMART Project LLC: it compelled Runalyze to remove all VDOT-named features. A public OSS repo will not fly under the radar. Internal code identifiers, variable names, private implementation, and historical research artifacts may use VDOT freely until DEC-042's pace-calculator rewrite replaces them with `PaceZoneIndexCalculator` and friends. When in doubt, treat anything that will appear in an LLM prompt, a user-visible string, a generated plan, a badge, or a README as user-facing. See `NOTICE` for the full disclaimer text and `docs/research/artifacts/batch-14g-license-trademark-attribution.md` for the precedent research.

## PR Review Protocol

Before merging any PR, run `/review-pr` locally via Claude Code Max subscription. Focus human review on:
1. Business logic correctness — does the code solve the right problem?
2. Architectural consistency — does new code follow established patterns?
3. Test quality — would tests fail if the feature broke?
4. Security — any auth, user input, or secrets code gets mandatory manual review
5. Scope creep — did the AI change things beyond what was asked?
6. Dependency verification — do all referenced packages actually exist?

## Post-Change Checklist

- [ ] `dotnet build` passes (backend changes)
- [ ] `dotnet test` passes (backend changes)
- [ ] `npm run build` passes (frontend changes)
- [ ] `npm run test` passes (frontend changes)
- [ ] No secrets in staged files
- [ ] Commit with conventional commit message

## Environment Quirks

- **Always use absolute paths in Bash `cd` commands** — zoxide (or similar tools) can hijack relative `cd` when CWD persists between tool calls.
- **Solution file is `.slnx` format** (not `.sln`)

## Key References

- `docs/decisions/decision-log.md` — all 39 decisions with rationale
- `docs/planning/vision-and-principles.md` — why this exists, design principles
- `docs/planning/safety-and-legal.md` — legal landscape, safety guardrails
- `docs/research/artifacts/` — full research outputs (11 integrated topics)
