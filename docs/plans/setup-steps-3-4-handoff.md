# Setup Steps 3-4: Project Scaffolding + Development Workflow Tooling

Handoff document for Claude Code. Complete these two steps to bring the repo from planning-ready to development-ready.

**Prerequisites:** Steps 1-2 are complete. The repo has CLAUDE.md, ROADMAP.md, backend/CLAUDE.md, frontend/CLAUDE.md, .claude/rules/, .claude/commands/, and .claude/settings.json. Planning docs are under docs/.

**Read before starting:** CLAUDE.md (root), ROADMAP.md, and the relevant subdirectory CLAUDE.md for whatever you're working on.

---

## Step 3: Project Scaffolding + Containerization

### 3.1 Backend (.NET 10 API)

Create the .NET solution inside `backend/`:

```bash
cd backend
dotnet new sln -n RunCoach
dotnet new webapi -n RunCoach.Api -o src/RunCoach.Api --no-openapi
dotnet sln add src/RunCoach.Api/RunCoach.Api.csproj
dotnet new xunit -n RunCoach.Api.Tests -o tests/RunCoach.Api.Tests
dotnet sln add tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj
cd tests/RunCoach.Api.Tests && dotnet add reference ../../src/RunCoach.Api/RunCoach.Api.csproj
```

After scaffolding, configure the project to match the conventions in `backend/CLAUDE.md`:

**Directory.Build.props** (backend root):
- TargetFramework: net10.0
- LangVersion: 14
- Nullable: enable
- ImplicitUsings: enable
- TreatWarningsAsErrors: true
- AnalysisLevel: latest-recommended
- EnforceCodeStyleInBuild: true
- Add StyleCop.Analyzers and SonarAnalyzer.CSharp as analyzers (PrivateAssets="all")

**Directory.Packages.props** (backend root — Central Package Management):
- ManagePackageVersionsCentrally: true
- Pin versions for all NuGet packages used across both projects
- Key packages: Swashbuckle.AspNetCore, Npgsql.EntityFrameworkCore.PostgreSQL, Marten, WolverineFx, Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.AspNetCore.Authentication.JwtBearer, OpenTelemetry.*, xunit, FluentAssertions, NSubstitute, Microsoft.AspNetCore.Mvc.Testing, Testcontainers.PostgreSql, coverlet.collector

**.editorconfig** (backend root):
- 4-space indent for .cs, 2-space for .csproj/.json/.yml
- File-scoped namespaces (warning)
- Private field naming: _camelCase (warning)
- Interface naming: IPascalCase (warning)
- Primary constructors preferred (suggestion)
- Suppress noisy StyleCop rules: SA1600 (doc comments), SA1633 (file headers), SA1101 (this.), SA1309 (underscore prefix conflict), SA1200 (using placement)

**RunCoach.Api project:**
- Remove the default WeatherForecast controller and model
- Set up Program.cs with: AddControllers, AddEndpointsApiExplorer, AddSwaggerGen, health checks, CORS (allow localhost:5173)
- Create `Infrastructure/ServiceCollectionExtensions.cs` for service registration helpers
- Create `Modules/Shared/BaseController.cs` — abstract ApiController with `[Route("api/v1/[controller]")]`
- Create `appsettings.json` with connection strings for PostgreSQL and Redis (localhost defaults for dev)
- Create `appsettings.Development.json` with debug logging
- Create `Properties/launchSettings.json` — http profile on port 5000
- Add `public partial class Program;` at the end of Program.cs (enables WebApplicationFactory in tests)

**RunCoach.Api.Tests project:**
- Add project reference to RunCoach.Api
- Add all test packages via Central Package Management
- Create a `SmokeTests.cs` with one test: GET /health returns 200 OK
- Use WebApplicationFactory<Program> with IClassFixture
- Use primary constructor pattern for test class

**Verify:** `dotnet build backend/RunCoach.sln` succeeds with zero warnings. `dotnet test backend/RunCoach.sln` passes.

### 3.2 Frontend (React 19 + TypeScript + Vite)

Create the React app inside `frontend/`:

```bash
cd frontend
npm create vite@latest . -- --template react-ts
npm install
```

After scaffolding, configure to match `frontend/CLAUDE.md`:

**Install dependencies:**
```bash
# Core
npm install react-router-dom @reduxjs/toolkit react-redux react-hook-form @hookform/resolvers zod

# UI
npm install tailwindcss @tailwindcss/vite
npx shadcn@latest init

# Dev
npm install -D vitest @testing-library/react @testing-library/jest-dom jsdom @types/node
npm install -D eslint @eslint/js typescript-eslint eslint-plugin-react-hooks eslint-plugin-react-refresh
npm install -D prettier eslint-config-prettier
```

**vite.config.ts:**
- Add path alias: `~/` resolves to `./src/app`
- Add Tailwind CSS plugin (@tailwindcss/vite)

**tsconfig.json / tsconfig.app.json:**
- Strict mode: true
- Add path alias: `"~/*": ["./src/app/*"]`

**Reorganize `src/` to match module-first structure:**
```
src/
  app/
    pages/
      home/
        home.page.tsx
    modules/
      app/
        app.component.tsx
        app.store.ts
        providers/
      common/
        components/
        hooks/
        utils/
        models/
  main.tsx
  index.css
```

- Remove default Vite boilerplate (App.tsx, App.css, logo, counter)
- Create `app.store.ts` with configureStore (empty initially, RTK Query middleware placeholder)
- Create `app.component.tsx` as the root component with Provider and RouterProvider
- Create a minimal `home.page.tsx` that displays "RunCoach" and confirms the app loads
- Set up Tailwind in `index.css`

**ESLint config (eslint.config.js):**
- typescript-eslint strict config
- react-hooks plugin
- prettier compat (eslint-config-prettier)

**Prettier config (.prettierrc):**
- singleQuote: true
- semi: false (or true — pick one and commit)
- trailingComma: "all"
- printWidth: 100

**Vitest config (vitest.config.ts or in vite.config.ts):**
- environment: jsdom
- setupFiles for @testing-library/jest-dom
- Coverage via v8 provider

**Create one smoke test:**
- `src/app/modules/app/app.component.spec.tsx` — renders without crashing

**Verify:** `npm run build` succeeds. `npm run test` passes. `npm run dev` serves on localhost:5173.

### 3.3 Docker Compose

Create `docker-compose.yml` at the repo root:

**Services:**

1. **postgres** — PostgreSQL 17, port 5432
   - Volume: `pgdata` for persistence
   - Environment: POSTGRES_USER=runcoach, POSTGRES_PASSWORD=runcoach_dev, POSTGRES_DB=runcoach
   - Health check: `pg_isready`

2. **pgadmin** — pgAdmin 4, port 5050
   - Environment: default email/password for local dev
   - Depends on: postgres

3. **redis** — Redis 7, port 6379
   - Health check: `redis-cli ping`

4. **aspire-dashboard** — Aspire Dashboard (standalone), port 18888
   - Image: `mcr.microsoft.com/dotnet/aspire-dashboard:latest`
   - Environment: DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true

5. **api** — .NET API (build from backend/Dockerfile)
   - Port: 5000
   - Environment: connection strings pointing to postgres and redis service names
   - OTEL_EXPORTER_OTLP_ENDPOINT pointing to aspire-dashboard
   - Depends on: postgres, redis
   - Health check: curl /health

6. **web** — React dev server (build from frontend/Dockerfile.dev)
   - Port: 5173
   - Volume mount: frontend/src for hot reload
   - Depends on: api

### 3.4 Dockerfiles

**backend/Dockerfile** (multi-stage):
- Build stage: `mcr.microsoft.com/dotnet/sdk:10.0` — restore, build, publish
- Runtime stage: `mcr.microsoft.com/dotnet/aspnet:10.0` — copy published output, expose 5000, health check
- ENTRYPOINT: `dotnet RunCoach.Api.dll`

**frontend/Dockerfile.dev** (dev only, for Docker Compose):
- Base: `node:22-slim`
- Copy package.json + package-lock.json, run `npm ci`
- CMD: `npm run dev -- --host 0.0.0.0`

**frontend/Dockerfile** (production, for future deployment):
- Build stage: `node:22-slim` — npm ci, npm run build
- Runtime stage: `nginx:alpine` — copy dist/ to /usr/share/nginx/html

### 3.5 Tiltfile

Create `Tiltfile` at the repo root:

```python
# Load docker-compose services
docker_compose('docker-compose.yml')

# Backend live reload
dc_resource('api',
  trigger_mode=TRIGGER_MODE_AUTO,
  labels=['backend'])

# Frontend live reload (Vite HMR handles this, Tilt just manages the container)
dc_resource('web',
  trigger_mode=TRIGGER_MODE_AUTO,
  labels=['frontend'])

# Infrastructure (don't auto-restart)
dc_resource('postgres', labels=['infra'])
dc_resource('redis', labels=['infra'])
dc_resource('pgadmin', labels=['infra'])
dc_resource('aspire-dashboard', labels=['infra'])
```

### 3.6 .gitignore

Create `.gitignore` at the repo root covering:
- .NET: bin/, obj/, *.user, *.suo, .vs/
- Node: node_modules/, dist/, .vite/
- IDE: .idea/, .vscode/ (except shared settings if any)
- OS: .DS_Store, Thumbs.db
- Secrets: .env, .env.*, appsettings.Local.json, *.pfx
- Docker: pgdata (if used as local volume path)

### Step 3 Verification

- [ ] `dotnet build backend/RunCoach.sln` — zero warnings
- [ ] `dotnet test backend/RunCoach.sln` — smoke test passes
- [ ] `cd frontend && npm run build` — builds successfully
- [ ] `cd frontend && npm run test` — smoke test passes
- [ ] `docker compose up -d postgres redis` — services start and pass health checks
- [ ] `docker compose up -d` — all services start (API connects to Postgres, frontend serves)
- [ ] `tilt up` — Tilt dashboard shows all services green
- [ ] No secrets in any committed file

---

## Step 4: Development Workflow Tooling

### 4.1 Lefthook (pre-commit hooks)

```bash
# From repo root
npm install -D lefthook @commitlint/cli @commitlint/config-conventional
npx lefthook install
```

Create `lefthook.yml` at repo root (see DEC-034 for the full config):
- pre-commit: parallel dotnet format + eslint + prettier with staged file routing
- commit-msg: commitlint
- pre-push: parallel dotnet test (unit only) + tsc --noEmit

Create `commitlint.config.js`:
```js
module.exports = { extends: ['@commitlint/config-conventional'] }
```

**Verify:** Make a test commit with a bad message — commitlint should reject it. Make a commit with an unformatted .ts file — prettier should auto-fix it.

### 4.2 CodeRabbit (PR review)

Create `.coderabbit.yaml` at repo root:
```yaml
language: en-US
reviews:
  auto_review:
    enabled: true
    drafts: false
  path_instructions:
    - path: "backend/**"
      instructions: "Review for .NET 10 / C# 14 best practices, EF Core patterns, and adherence to the module-first architecture described in backend/CLAUDE.md."
    - path: "frontend/**"
      instructions: "Review for React 19 / TypeScript strict patterns, RTK Query usage, and adherence to the module-first structure described in frontend/CLAUDE.md."
```

Install the CodeRabbit GitHub App on the repository (done via GitHub UI, not CLI).

### 4.3 Claude Code GitHub Action (PR review)

Create `.github/workflows/claude-review.yml`:
- Trigger: pull_request (opened, synchronize), issue_comment (for @claude mentions)
- Uses: `anthropics/claude-code-action@v1`
- Model: claude-sonnet-4-5-20250514 (or latest)
- Prompt: review against CLAUDE.md standards, focus on architectural consistency, unnecessary complexity, pattern drift
- Requires: ANTHROPIC_API_KEY secret in repo settings

### 4.4 GitHub Actions CI

Create `.github/workflows/ci.yml`:
- Trigger: push to main, pull_request
- Path filtering via dorny/paths-filter

**Backend job (runs when backend/ changes):**
- dotnet restore, build (TreatWarningsAsErrors), test with coverage
- Upload coverage to Codecov with backend flag

**Frontend job (runs when frontend/ changes):**
- npm ci, build, test with coverage
- Upload coverage to Codecov with frontend flag

**Security job (always runs):**
- CodeQL analysis for csharp and javascript

**Gate job (always runs):**
- Depends on backend + frontend jobs
- Checks upstream results, succeeds if all required jobs passed or were skipped

### 4.5 Dependabot

Create `.github/dependabot.yml`:
- nuget: backend directory, weekly schedule, group minor/patch
- npm: frontend directory, weekly schedule, group minor/patch
- github-actions: root, weekly schedule
- docker: root, weekly schedule

### 4.6 dotnet/skills Plugin

Install the Microsoft dotnet/skills Claude Code plugin:
```bash
claude plugin install dotnet/skills
```

This provides on-demand skills for EF Core query optimization, MSBuild diagnostics, and .NET upgrade guidance.

### Step 4 Verification

- [ ] `npx lefthook run pre-commit` — runs dotnet format and eslint/prettier
- [ ] Bad commit message is rejected by commitlint
- [ ] `.coderabbit.yaml` is committed and CodeRabbit app is installed
- [ ] `.github/workflows/ci.yml` passes on a test PR
- [ ] `.github/dependabot.yml` is committed
- [ ] dotnet/skills plugin is installed and accessible via Claude Code

---

## After Steps 3-4

Update ROADMAP.md to mark Steps 3 and 4 complete. The repo is now development-ready. Next: implement POC 1 following `docs/plans/poc-1-context-injection-plan-quality.md`.
