# RunCoach — AI-Powered Adaptive Running Coach

## What This Is

A solo-developer project building an AI running coach that maintains a persistent adaptive coaching relationship. The AI is the coach — it builds training plans, consumes workout results, and continuously adapts. It does NOT do live workout tracking (that's Garmin/Strava/Apple Health territory).

**Current Phase: Pre-development (planning complete, repo scaffolding in progress)**
See ROADMAP.md for current status and next steps.

## Tech Stack

- **Backend:** .NET 10 / C# 14, ASP.NET Core controllers, EF Core + Marten (event sourcing on PostgreSQL JSONB), Wolverine (background processing), ASP.NET Core Identity + JWT
- **Frontend:** React 19 + TypeScript (strict), Vite SPA, React Router v7, Redux Toolkit + RTK Query, Tailwind CSS + shadcn/ui, React Hook Form + Zod
- **Testing:** xUnit + FluentAssertions + NSubstitute, Vitest + React Testing Library, Playwright E2E
- **Infrastructure:** Docker Compose + Tilt (local dev), Colima, GitHub Actions CI/CD, PostgreSQL + Redis
- **Quality:** Lefthook pre-commit, CodeRabbit + Claude Code GitHub Action (PR review), CodeQL + Trivy + Codecov (CI), SonarCloud
- **LLM:** Claude Sonnet 4.5 via thin adapter interface. Prompts in versioned config files, not code.

## Architecture Principles

- **Deterministic computation layer + LLM coaching layer** — never use LLMs for structured data tasks (pace calculations, zone math, ACWR). LLMs handle coaching conversation, plan narrative, and adaptation reasoning.
- **Event-sourced plan state** via Marten on PostgreSQL. Plans are event streams, not mutable rows.
- **Client-agnostic REST API** — JWT auth, URL-versioned (`/api/v1/`), no browser assumptions. Web SPA and future iOS app are both API clients.
- **Module-first organization** — both backend (`Modules/{Domain}/`) and frontend (`modules/{feature}/`). Technical layers enforced by code, not folders.

## Repo Structure

```
CLAUDE.md              # You are here (always loaded)
ROADMAP.md             # Living project state — read every session
backend/               # .NET API (has its own CLAUDE.md)
frontend/              # React SPA (has its own CLAUDE.md)
docs/
  planning/            # Vision, architecture, safety, coaching persona
  decisions/           # Decision log (DEC-001 through DEC-035)
  features/            # Feature backlog
  research/            # Research queue, prompts, and artifacts
```

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

## Post-Change Checklist

- [ ] `dotnet build` passes (backend changes)
- [ ] `dotnet test` passes (backend changes)
- [ ] `npm run build` passes (frontend changes)
- [ ] `npm run test` passes (frontend changes)
- [ ] No secrets in staged files
- [ ] Commit with conventional commit message

## Key References

- `docs/decisions/decision-log.md` — all 35 decisions with rationale
- `docs/planning/vision-and-principles.md` — why this exists, design principles
- `docs/planning/safety-and-legal.md` — legal landscape, safety guardrails
- `docs/research/artifacts/` — full research outputs (11 integrated topics)
