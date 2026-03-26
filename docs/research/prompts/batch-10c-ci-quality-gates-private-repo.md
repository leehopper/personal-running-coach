# Research Prompt: Batch 10c — R-023

# CI/CD Pipeline and Quality Gates for Private Repos (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: CI/CD pipeline best practices and quality gate design for a private GitHub repository using GitHub Actions

Context: I'm building RunCoach, an AI-powered running coach with a .NET 10 backend and React 19 frontend in a private GitHub repo. The project uses AI-assisted development (Claude Code) — meaning most code is AI-generated and needs deterministic quality enforcement. I have a five-layer quality pipeline (DEC-034):

1. **Pre-commit (Lefthook):** `dotnet format` + ESLint + Prettier auto-fix on staged files, commitlint for conventional commits
2. **PR review:** Local `/review-pr` via Claude Code Max (AI reviewing AI-generated code)
3. **CI gates (GitHub Actions):** Build, test, Trivy security scanning, Codecov coverage
4. **Build-time analysis:** SonarAnalyzer.CSharp + eslint-plugin-sonarjs (not SonarCloud — requires public repo)
5. **Human review:** Checklist-based (business logic, architecture, test quality, security, scope, dependencies)

Current CI setup (`ci.yml`):

- Trigger: `push` to main + `pull_request` to main
- Path-filtered: backend changes trigger backend jobs, frontend changes trigger frontend jobs
- Backend: `dotnet build` → `dotnet test` with coverlet → Codecov upload
- Frontend: `npm ci` → `npm run lint` → `npm run build` → `npm run test` with coverage → Codecov upload
- Security: Trivy filesystem scan (critical+high severity)
- All GitHub Actions SHA-pinned to commit hashes with version comments

Private repo constraints:

- **No free CodeRabbit** (requires public repo)
- **No free CodeQL** (requires GitHub Advanced Security, which requires GitHub Team + Code Security add-on)
- **No free SonarCloud** (requires public repo)
- **No branch protection rules** (requires GitHub Pro for private repos)
- **No merge queues** (requires GitHub Team)
- Using alternatives: Trivy (replaces CodeQL), SonarAnalyzer.CSharp in-build (replaces SonarCloud), local `/review-pr` (replaces CodeRabbit)

What I need to learn:

### 1. GitHub Actions Best Practices (2026)

- SHA-pinning: we already do this — any improvements? Should we use Dependabot to update pinned SHAs?
- Caching strategies: NuGet cache, npm cache, Docker layer cache — what's the current best approach?
- Path filtering: are there better approaches than our current `paths:` filter? Matrix strategies?
- Concurrency: how to cancel in-progress CI runs when a new push arrives?
- Job dependencies: optimal job graph for a monorepo (backend + frontend)
- Reusable workflows: when to extract into reusable workflows vs keep inline
- Artifact management: how to share build artifacts between jobs efficiently
- Security hardening: `permissions:` block, OIDC for cloud deployments, secrets handling
- Status checks: how to require CI passage without branch protection rules (private repo workaround)
- Self-hosted runners: when do they make sense for a solo developer?

### 2. Trivy Configuration for Maximum Effectiveness

- Current config: filesystem scan with severity HIGH,CRITICAL. What are we missing?
- IaC scanning: we have Docker Compose, Dockerfiles, Tiltfile — is Trivy catching IaC issues?
- Secret scanning: is Trivy's secret scanner sufficient, or do we need `gitleaks` or `trufflehog`?
- SARIF output: should we upload to GitHub's security tab (does it work for private repos)?
- Vulnerability database freshness: how often does Trivy update? Should we force-update in CI?
- Suppression: `.trivyignore` best practices, when to suppress vs fix
- Container image scanning: we're not deploying images yet — when should we add this?
- License scanning: useful for pre-public release — how to configure?
- Configuration-as-code: `trivy.yaml` vs CLI flags — what's the recommended approach?

### 3. Codecov Configuration and Coverage Strategy

- Current config: 60% project threshold, 70% patch threshold, backend/frontend flags with carryforward
- Are these thresholds appropriate for a new project? Should they ramp up over time?
- Carryforward: we use it — any gotchas?
- PR decoration: how to get useful coverage comments on PRs without branch protection?
- Component coverage: should we set different thresholds for computation layer (high) vs API layer (medium) vs UI (lower)?
- Missing lines annotation: how to get inline coverage annotations in PR diffs?
- Codecov alternatives: is there something better for private repos? Coveralls?

### 4. Lefthook Pre-commit Best Practices

- Current setup: formatters auto-fix + restage, commitlint on commit-msg, tests on pre-push
- Performance: how to keep pre-commit fast as the codebase grows?
- Selective running: only run checks on changed files (we do this — any improvements?)
- Pre-push: we run `dotnet test` and `tsc --noEmit` — is this too slow? Should these be CI-only?
- Security hooks: should we add a pre-commit secret scan (gitleaks)?
- Parallel execution: can Lefthook run backend and frontend hooks in parallel?

### 5. Docker Compose + Tilt Local Dev

- Current setup: PostgreSQL, pgAdmin, Redis, Aspire Dashboard, API, Web — all in docker-compose.yml
- Tilt: watching file changes, live reload — best practices for .NET + React
- Image optimization: multi-stage Dockerfiles — are we following current best practices?
- Colima (macOS Docker runtime): any performance tuning we should do?
- Dev/prod parity: how to keep local Docker setup aligned with production deployment?

### 6. Dependabot Strategy

- Version updates: how aggressive should we be? Auto-merge patches? Group minor updates?
- Security alerts: how to triage effectively for a solo developer?
- NuGet + npm: any differences in Dependabot behavior between ecosystems?
- Grouped updates: Dependabot groups — how to configure for "update all testing packages together"?
- Ignoring: when to ignore vs pin vs update

### 7. Quality Gates Without Branch Protection

- The biggest private repo constraint: can't require status checks on PRs
- Workarounds: Lefthook pre-push hooks, CI as informational (trust the developer), GitHub Apps that enforce checks?
- How do other solo developers handle this?
- Is GitHub Pro ($4/mo) worth it just for branch protection?
- Alternative: can we use a GitHub Action to comment "CI failed" on PRs as a soft gate?

### 8. AI-Generated Code Quality Concerns

- What's different about reviewing AI-generated code vs human-written code?
- Common failure modes: dependency hallucination (packages that don't exist), over-engineering, scope creep, inconsistent patterns across sessions
- What CI checks specifically help catch AI-generated code issues?
- Should we add a "dependency existence check" to CI?
- How to detect and prevent AI introducing vulnerabilities it doesn't understand?

### 9. Trunk-Based Development CI Patterns

- We use trunk-based development with `main` as trunk and short-lived feature branches
- How should CI differ between `push to main` and `pull_request`?
- Should we run different test suites? (e.g., full test suite on main, fast tests on PR)
- Release automation: when to add automated releases/tags?
- Rollback strategy: how to quickly revert a bad merge to main?

### 10. Future-Proofing for Scale

- What should we set up now that's hard to retrofit later?
- Performance regression testing: GitHub runner variance makes this unreliable — any workarounds?
- Integration testing in CI: when to add Testcontainers-based tests (requires Docker-in-Docker or service containers)
- E2E testing in CI: when to add Playwright tests, how to run against a real backend
- Monitoring/alerting: when does a solo project need CI monitoring (Datadog, etc.)?

Output I need:

- For each area: specific, actionable recommendations with "do this now" vs "do this at MVP-0" vs "do this at public release"
- CI configuration improvements: concrete YAML snippets or config changes
- Private repo workarounds: ranked by effectiveness and effort
- Cross-cutting review rules for the root REVIEW.md that enforce CI/CD standards
- Cost analysis: what's worth paying for (GitHub Pro, Codecov Pro, etc.) vs free alternatives
