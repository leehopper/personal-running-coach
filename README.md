# RunCoach — AI-Powered Adaptive Running Coach

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Prompts: CC BY-NC-SA 4.0](https://img.shields.io/badge/Prompts-CC_BY--NC--SA_4.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc-sa/4.0/)

An AI running coach that maintains a persistent adaptive coaching relationship. It builds training plans, consumes workout results, and continuously adapts — not a workout tracker, but a planning intelligence layer that complements tools runners already use.

## Current Status

**Phase: POC 1 productionized.** Context injection, plan generation, eval suite with safety assertions, and structured outputs are implemented and ready for MVP-0 development. See [ROADMAP.md](ROADMAP.md) for details.

## Prerequisites

- .NET 10 SDK
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
# Backend (eval tests run in Replay mode from committed cache fixtures — no API calls)
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
LICENSE                # Apache-2.0 (code)
NOTICE                 # Attribution, scientific citations, trademark disclaimers
THIRD-PARTY-NOTICES.md # Per-dependency license list for NuGet and npm
backend/               # .NET 10 API (ASP.NET Core, EF Core, Marten, Wolverine)
  src/RunCoach.Api/Prompts/
    LICENSE            # CC-BY-NC-SA-4.0 (coaching prompts only)
    README.md          # Dual-license boundary explanation
frontend/              # React 19 SPA (TypeScript, Vite, Redux Toolkit, Tailwind)
docs/
  planning/            # Vision, architecture, safety, coaching persona
  decisions/           # Decision log
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
- **Quality:** Lefthook pre-commit, CodeRabbit + CodeQL + SonarQube Cloud in CI, SonarAnalyzer.CSharp + eslint-plugin-sonarjs at build time, Trivy, Codecov

## License

RunCoach uses a **dual-license** architecture:

| Scope | License |
| --- | --- |
| All code, tests, infrastructure, and documentation outside `backend/src/RunCoach.Api/Prompts/` | [Apache License 2.0](LICENSE) |
| YAML files under `backend/src/RunCoach.Api/Prompts/` (coaching persona and methodology prompts) | [Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International](backend/src/RunCoach.Api/Prompts/LICENSE) |

The code layer is a generic deterministic-plus-LLM framework and belongs to
the OSS commons. The coaching prompts encode the product-specific
methodology and are held under a non-commercial share-alike license so the
coaching IP cannot be lifted wholesale into a competing commercial product
without a separate agreement. You are free to build commercial products on
top of the RunCoach code as long as the coaching prompts are replaced or
separately licensed. See
[`backend/src/RunCoach.Api/Prompts/README.md`](backend/src/RunCoach.Api/Prompts/README.md)
for the full dual-license explanation.

## Trademark disclaimer

"**VDOT**" is a trademark of The Run SMART Project LLC and is **not** used
as the name of any feature, label, API, or user-facing string in this
project. RunCoach's deterministic pace-zone layer is derived independently
from the public-domain Daniels-Gilbert oxygen-cost and race-prediction
equations (Daniels & Gilbert, 1979, *Oxygen Power*). User-facing surface in
this project refers to these values as "**Daniels-Gilbert zones**" or
"**pace-zone index**". RunCoach is not affiliated with, endorsed by, or
licensed by The Run SMART Project LLC.

This disclaimer is informed by the Runalyze enforcement precedent
documented in `docs/research/artifacts/batch-14g-license-trademark-attribution.md`,
where The Run SMART Project LLC compelled Runalyze to remove all VDOT-named
features from a product with a much larger user base. The mark is actively
enforced, so the avoidance rule applies even though the underlying
mathematics is public-domain.

Internal code identifiers in this repository (for example the class name
`VdotCalculator`) currently still use the term. Those identifiers are
scheduled for rename alongside the pace-calculator rewrite captured in
DEC-042 and are not exposed on any API surface or user-facing string.

## Attribution

Scientific and methodological citations, plus the full third-party license
list, live in these files:

- [`NOTICE`](NOTICE) — attribution for the Daniels-Gilbert equations (1979 *Oxygen Power*), the Tanaka max-HR formula (2001), and the core OSS stack
- [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) — per-dependency SPDX license identifiers for all bundled NuGet and npm packages

The `THIRD-PARTY-NOTICES.md` file is initially hand-curated; once the
license-compliance CI (Unit 6 of the OSS tooling restoration) lands, it
will be regenerated automatically by a scheduled SBOM workflow.

## Documentation

All planning and research docs live in `docs/`. Key entry points:

- `docs/planning/vision-and-principles.md` — why this exists
- `docs/decisions/decision-log.md` — all decisions with rationale
- `docs/research/research-queue.md` — research topics and artifacts
- `docs/planning/poc-roadmap.md` — POC experiments feeding into MVP
- `docs/planning/unit-system-design.md` — metric/imperial unit architecture
