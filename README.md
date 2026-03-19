# RunCoach — AI-Powered Adaptive Running Coach

An AI running coach that maintains a persistent adaptive coaching relationship. It builds training plans, consumes workout results, and continuously adapts — not a workout tracker, but a planning intelligence layer that complements tools runners already use.

## Repo Structure

```
CLAUDE.md              # AI assistant context (project overview, conventions)
ROADMAP.md             # Living project state — current phase and next steps
backend/               # .NET 10 API (ASP.NET Core, EF Core, Marten, Wolverine)
frontend/              # React 19 SPA (TypeScript, Vite, Redux Toolkit, Tailwind)
docs/
  planning/            # Vision, architecture, safety, coaching persona
  decisions/           # Decision log (DEC-001 through DEC-035)
  features/            # Feature backlog by priority
  research/            # Research queue, prompts, and full artifacts
.claude/
  commands/            # Claude Code slash commands
  rules/               # Conditional rules (migration safety, secrets)
  settings.json        # Hook configuration (formatters, dangerous command blocking)
```

## Quick Start

See ROADMAP.md for current project status and next steps. The project is in pre-development — planning is complete, scaffolding is in progress.

## Tech Stack

- **Backend:** .NET 10 / C# 14, ASP.NET Core controllers, EF Core + Marten (event sourcing), Wolverine, JWT auth
- **Frontend:** React 19 + TypeScript, Vite, React Router v7, Redux Toolkit + RTK Query, Tailwind + shadcn/ui
- **Testing:** xUnit + FluentAssertions + NSubstitute, Vitest + React Testing Library, Playwright E2E
- **Infrastructure:** Docker Compose + Tilt, PostgreSQL, Redis, GitHub Actions CI/CD
- **Quality:** Lefthook, CodeRabbit, Claude Code GitHub Action, CodeQL, Trivy, Codecov, SonarCloud

## Documentation

All planning and research docs live in `docs/`. Key entry points:

- `docs/planning/vision-and-principles.md` — why this exists
- `docs/decisions/decision-log.md` — all 35 decisions with rationale
- `docs/research/research-queue.md` — 12 research topics, 11 integrated
- `docs/planning/poc-roadmap.md` — POC experiments before building
