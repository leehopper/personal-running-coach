# RunCoach — AI-Powered Adaptive Running Coach

An AI running coach that maintains a persistent adaptive coaching relationship. It builds training plans, consumes workout results, and continuously adapts — not a workout tracker, but a planning intelligence layer that complements tools runners already use.

## Current Status

**Phase: POC 1 productionized.** Context injection, plan generation, eval suite with safety assertions, and structured outputs are implemented and ready for MVP-0 development. See ROADMAP.md for details.

## Prerequisites

- .NET 10 SDK (preview)
- Node.js 22+
- Docker / Colima (for PostgreSQL, Redis, pgAdmin)

## Quick Start

### Run the backend

```bash
# Start infrastructure
docker compose up -d postgres redis

# Run the API
cd backend
dotnet run --project src/RunCoach.Api
```

### Run tests

```bash
# Backend (290 tests, eval tests use cached responses)
cd backend
EVAL_CACHE_MODE=Replay dotnet test

# Frontend
cd frontend
npm test
```

### Re-record eval cache (when prompts change)

```bash
# Full re-record: deletes stale entries, records fresh, extends TTL, verifies Replay
./backend/tests/scripts/rerecord-eval-cache.sh

# Then commit the updated cache
git add backend/tests/eval-cache/
git commit -m "chore: re-record eval cache fixtures"
```

## Repo Structure

```
CLAUDE.md              # AI assistant context (project overview, conventions)
ROADMAP.md             # Living project state — current phase and next steps
backend/               # .NET 10 API (ASP.NET Core, EF Core, Marten, Wolverine)
frontend/              # React 19 SPA (TypeScript, Vite, Redux Toolkit, Tailwind)
docs/
  planning/            # Vision, architecture, safety, coaching persona
  decisions/           # Decision log (DEC-001 through DEC-041)
  features/            # Feature backlog by priority
  research/            # Research queue, prompts, and full artifacts
  specs/               # Spec-driven development artifacts
```

## Tech Stack

- **Backend:** .NET 10 / C# 14, ASP.NET Core controllers, EF Core + Marten (event sourcing), Wolverine, JWT auth
- **Frontend:** React 19 + TypeScript, Vite, React Router v7, Redux Toolkit + RTK Query, Tailwind + shadcn/ui
- **Testing:** xUnit v3 + FluentAssertions + NSubstitute, M.E.AI.Evaluation (eval caching), Vitest + RTL, Playwright E2E
- **LLM:** Claude Sonnet 4.6 (coaching) + Haiku 4.5 (judging) via Anthropic .NET SDK, structured outputs with constrained decoding
- **Infrastructure:** Docker Compose + Tilt, PostgreSQL, Redis, GitHub Actions CI/CD
- **Quality:** Lefthook pre-commit, SonarAnalyzer.CSharp + eslint-plugin-sonarjs, Trivy, Codecov

## Documentation

All planning and research docs live in `docs/`. Key entry points:

- `docs/planning/vision-and-principles.md` — why this exists
- `docs/decisions/decision-log.md` — all decisions with rationale
- `docs/research/research-queue.md` — research topics and artifacts
- `docs/planning/poc-roadmap.md` — POC experiments feeding into MVP
- `docs/planning/unit-system-design.md` — metric/imperial unit architecture
