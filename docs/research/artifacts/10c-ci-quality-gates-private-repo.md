# CI/CD pipeline mastery for private GitHub repos in 2026

**The single most impactful action for RunCoach's quality pipeline is spending $4/month on GitHub Pro.** Without it, every quality gate is advisory — a dangerous gap when AI generates most of your code. Beyond that, the five-layer pipeline is architecturally sound but has specific configuration gaps: Trivy scans only vulnerabilities (missing IaC and secrets), Codecov thresholds should use `target: auto` instead of fixed percentages, and no CI check verifies that AI-hallucinated packages actually exist. This report provides exact configurations for all 10 research areas, prioritized into three phases: do now, do at MVP, and do at public release.

---

## 1. GitHub Actions: the hardened monorepo workflow

### SHA-pinning and Dependabot

SHA-pinning became non-negotiable after two major supply chain attacks: **tj-actions/changed-files in March 2025** and **aquasecurity/trivy-action on March 19, 2026**, where 75 of 76 version tags were force-pushed with infostealer payloads that exfiltrated CI secrets before legitimate scans ran. Dependabot fully supports updating SHA-pinned actions and now bumps version comments automatically. Configure it to track all four ecosystems:

```yaml
# .github/dependabot.yml
version: 2
updates:
  - package-ecosystem: "github-actions"
    directory: "/"
    schedule:
      interval: "weekly"
      day: "monday"
    groups:
      actions-all:
        patterns: ["*"]
    commit-message:
      prefix: "deps(actions):"
```

### Caching that actually works

Both `setup-dotnet` and `setup-node` now have built-in caching that eliminates the need for separate `actions/cache` steps. For NuGet, this requires lock files — add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` to `Directory.Build.props` and use `--locked-mode` restore. For npm, the built-in cache stores `~/.npm` (the global cache), not `node_modules` — GitHub explicitly warns against caching `node_modules`.

```yaml
- uses: actions/setup-dotnet@<SHA> # v5
  with:
    dotnet-version: '10.0.x'
    cache: true
    cache-dependency-path: '**/packages.lock.json'
- run: dotnet restore --locked-mode

- uses: actions/setup-node@<SHA> # v4
  with:
    node-version: '22'
    cache: 'npm'
    cache-dependency-path: 'frontend/package-lock.json'
- run: npm ci
```

### Path filtering and the gate job pattern

The built-in `paths:` trigger filter skips entire workflows but cannot conditionally skip individual jobs. For a monorepo, **`dorny/paths-filter`** is the standard solution — it creates a shared "detect changes" job whose outputs control which downstream jobs run. Avoid `tj-actions/changed-files` (the compromised action from 2025). The critical architectural pattern is a **gate job** that aggregates all conditional job results into a single pass/fail status:

```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.head_ref || github.run_id }}
  cancel-in-progress: true

permissions:
  contents: read

jobs:
  changes:
    runs-on: ubuntu-latest
    permissions:
      pull-requests: read
    outputs:
      backend: ${{ steps.filter.outputs.backend }}
      frontend: ${{ steps.filter.outputs.frontend }}
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: dorny/paths-filter@<SHA> # v3
        id: filter
        with:
          filters: |
            backend:
              - 'backend/**'
              - 'Directory.Build.props'
              - '*.sln'
            frontend:
              - 'frontend/**'

  backend:
    needs: changes
    if: needs.changes.outputs.backend == 'true'
    runs-on: ubuntu-latest
    # ... build + test steps

  frontend:
    needs: changes
    if: needs.changes.outputs.frontend == 'true'
    runs-on: ubuntu-latest
    # ... lint + build + test steps

  ci-gate:
    if: always()
    needs: [backend, frontend]
    runs-on: ubuntu-latest
    steps:
      - name: Verify all jobs passed
        run: |
          if [[ "${{ needs.backend.result }}" == "failure" || \
                "${{ needs.frontend.result }}" == "failure" ]]; then
            echo "❌ CI failed"
            exit 1
          fi
          echo "✅ All checks passed (or skipped)"
```

The `concurrency` block uses `github.head_ref` (defined only for PRs) to cancel in-progress runs when a new push arrives on the same branch, while falling back to `github.run_id` for pushes to main so those are never cancelled.

### Security hardening

Set **`permissions: contents: read`** at the workflow level and escalate per-job. Never echo user-controlled input (PR titles, issue bodies) directly in `run:` blocks. Avoid `pull_request_target` unless absolutely necessary. Use OIDC for cloud deployments when you reach that stage — no long-lived secrets.

### Self-hosted runners

Not needed. A .NET + React pipeline typically takes **3–8 minutes**. At 20 pushes/day, that's ~2,400 minutes/month — within the 2,000 free minutes (3,000 with Pro). Revisit only if you consistently exceed limits or need specialized hardware.

---

## 2. Trivy: from basic scan to comprehensive security gate

### The March 2026 supply chain attack changes everything

On **March 19, 2026**, a threat actor compromised `aquasecurity/trivy-action` by force-pushing 75 of 76 version tags with infostealer malware. The malicious code exfiltrated CI secrets (AWS, GCP, Azure credentials, SSH keys, API tokens) *before* the legitimate scan ran, so pipelines appeared normal. **Immediate action**: pin to verified safe SHA `@57a97c7e7821a5776cebc9bb87c984fa69cba8f1`, and if any pipeline ran after March 19 using version tags, rotate all accessible secrets.

### What your current scan misses

Running `trivy fs` with only the default `vuln` and `secret` scanners means you're not scanning Dockerfiles for misconfigurations (running as root, untagged base images) or checking licenses. Add `misconfig` to scanners. Docker Compose support is spotty — supplement with **hadolint** for robust Dockerfile linting.

For secrets, Trivy's built-in scanner covers 50+ patterns but **does not scan git history** and has no entropy analysis. Add **Gitleaks** as a complement — it's fast (~200ms for staged files), scans git history, and integrates with both Lefthook and CI. TruffleHog offers secret verification (checks if credentials are still active) but is heavier.

### Configuration-as-code with trivy.yaml

```yaml
# trivy.yaml — committed to repo root
scan:
  scanners:
    - vuln
    - misconfig
    - secret
  severity:
    - HIGH
    - CRITICAL
  skip-dirs:
    - node_modules
    - .git
    - bin
    - obj
    - TestResults

misconfiguration:
  scanners:
    - dockerfile

exit-code: 1
timeout: 10m0s
```

### SARIF, database freshness, and suppression

**SARIF upload to GitHub's security tab requires GitHub Code Security at $30/committer/month** — not worth it for a solo developer. Instead, output results as table format in CI logs and upload JSON as a workflow artifact. The vulnerability database updates **every 6 hours**; trivy-action's built-in caching handles this automatically — don't force manual DB downloads. For suppressions, use `.trivyignore` with comments and expiry dates (`exp:2026-06-01 CVE-2025-67890`), and version-control all suppressions.

### Container image scanning and license audit

Add image scanning as soon as Docker images are built in CI — filesystem scans catch your dependency vulnerabilities, but image scans catch **base image OS package vulnerabilities**. They're complementary. For license scanning pre-public release, add `license` to scanners and configure forbidden licenses (AGPL-3.0, SSPL-1.0, BUSL-1.1).

---

## 3. Codecov: target auto, component thresholds, and the free tier

### Rethinking thresholds

Fixed thresholds (60% project, 70% patch) are brittle — they either block legitimate refactors or become irrelevant as coverage grows. **`target: auto`** compares against the base commit, so coverage can only go down by a specified threshold. Set `threshold: 5%` for project (allows coverage dips from deletions) and **`target: 80%`** for patch (new code should be well-tested). The current 70% patch target is too low.

### Component coverage for tiered enforcement

Codecov Components allow different thresholds per directory — defined entirely in `codecov.yml`, no upload changes needed:

```yaml
component_management:
  individual_components:
    - component_id: computation
      name: Computation Engine
      paths: [src/Backend/Computation/]
      statuses:
        - type: patch
          target: 90%
    - component_id: api
      name: API Layer
      paths: [src/Backend/Api/]
      statuses:
        - type: patch
          target: 80%
    - component_id: frontend
      name: React UI
      paths: [src/Frontend/src/]
      statuses:
        - type: patch
          target: 65%
```

### Carryforward gotchas

Carryforward is essential for monorepos where you only run tests for changed code, but it has pitfalls: you **must upload all coverage initially** to establish a baseline, carried-forward data can become stale with no automatic expiration, and by default carryforward flags don't appear in PR comments (set `show_carryforward_flags: true`). Path matching in flag definitions uses globs, not regexes — mismatches silently fail.

### Cost: free for solo developers

**Codecov is free for 1 user on private repos.** PR comments, inline annotations, and basic flags all work on the free tier. Component coverage and advanced features require the Pro plan ($12/user/month), but the free tier is sufficient through MVP. Coveralls charges $5/month per repo — Codecov is the better deal for a monorepo.

---

## 4. Lefthook: fast local gates with parallel execution

### The optimized configuration

Lefthook's Go-based architecture adds ~20ms overhead. The key optimization levers are `parallel: true` (runs all commands concurrently), `root` (scopes to directories — skips entirely if no matching staged files), and `stage_fixed: true` (auto-restages after auto-fix).

```yaml
# lefthook.yml
output:
  - summary
  - failure

pre-commit:
  parallel: true
  commands:
    gitleaks:
      run: gitleaks protect --staged --redact --no-banner
      fail_text: "🔑 Secrets detected in staged files"
    dotnet-format:
      root: "backend/"
      glob: "*.{cs,csx}"
      run: dotnet format --include {staged_files} --no-restore --verbosity quiet
      stage_fixed: true
    eslint:
      root: "frontend/"
      glob: "*.{js,jsx,ts,tsx}"
      run: npx eslint --fix --max-warnings=0 {staged_files}
      stage_fixed: true
    prettier:
      root: "frontend/"
      glob: "*.{js,jsx,ts,tsx,json,css,md,yml}"
      run: npx prettier --write {staged_files}
      stage_fixed: true

commit-msg:
  commands:
    commitlint:
      run: npx commitlint --edit {1}

pre-push:
  parallel: true
  commands:
    dotnet-build:
      root: "backend/"
      glob: "*.{cs,csx,csproj,sln}"
      run: dotnet build --no-restore --verbosity quiet
    tsc-check:
      root: "frontend/"
      glob: "*.{ts,tsx}"
      run: npx tsc --noEmit
```

**Move `dotnet test` to CI-only.** It requires building the entire solution (30s–2min+), which is disruptive on every push. Keep `dotnet build` on pre-push (catches compilation errors quickly) and `tsc --noEmit` (3–10 seconds, catches real type errors). Developers who need to skip slow hooks can use `lefthook-local.yml` (gitignored).

---

## 5. AI-generated code demands deterministic verification

### The scale of the problem

Research shows **approximately 45% of AI-generated code contains security flaws**. PRs are 18% larger with AI assistance, incidents per PR rise 24%, and change failure rates increase 30%. AI code is "confidently incomplete" — it compiles, reads well, and has clear comments, but this surface quality masks missing edge cases, insecure defaults, and behavioral vulnerabilities that emerge from component interactions.

### Package hallucination ("slopsquatting") is a real CI gap

**21.7% of package names from open-source models and 5.2% from commercial models are hallucinated.** This creates a supply chain attack vector — attackers register AI-hallucinated package names with malicious code. Add a dependency existence verification step to CI:

```yaml
- name: Verify npm packages exist
  run: |
    jq -r '(.dependencies // {}) + (.devDependencies // {}) | keys[]' \
      frontend/package.json | while read pkg; do
      HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
        "https://registry.npmjs.org/$pkg")
      if [ "$HTTP_STATUS" != "200" ]; then
        echo "❌ HALLUCINATED: $pkg does not exist on npm"
        exit 1
      fi
    done

- name: Verify NuGet packages exist
  run: |
    grep -roh 'Include="[^"]*"' backend/ --include="*.csproj" | \
      sed 's/Include="//;s/"//' | sort -u | while read pkg; do
      RESULT=$(curl -s "https://api.nuget.org/v3-flatcontainer/\
        $(echo $pkg | tr '[:upper:]' '[:lower:]')/index.json")
      if echo "$RESULT" | grep -q "BlobNotFound"; then
        echo "❌ HALLUCINATED: $pkg not found on NuGet"
        exit 1
      fi
    done
```

### Static analysis rules that matter most for AI code

AI consistently generates dead code, falls back to `any` types, creates unnecessary abstraction layers, leaves debug logs, and gets React hook dependency arrays wrong. The highest-value rules:

- **ESLint**: `no-unused-vars`, `@typescript-eslint/no-explicit-any`, `react-hooks/exhaustive-deps`, `no-console`
- **Roslyn**: `CA2100` (SQL injection), `CA1062` (validate arguments), `CA1822` (mark static), and **`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`** in CI builds
- **Semgrep** (free tier): `p/owasp-top-ten`, `p/typescript`, `p/csharp` rulesets provide SAST coverage that Trivy cannot

Add **jscpd** for duplicate detection (AI recreates existing functionality instead of reusing it) and **depcheck** for unused dependencies.

---

## 6. Docker Compose + Tilt: the local development stack

### Colima performance tuning

The performance gap between configurations is dramatic. **VirtioFS with the vz virtualization framework delivers 70–90% of native filesystem speed** for reads, while qemu with sshfs can be 16× slower.

```bash
colima start \
  --vm-type vz \
  --mount-type virtiofs \
  --cpu 4 \
  --memory 8 \
  --disk 100 \
  --vz-rosetta  # Rosetta for x86 images on Apple Silicon
```

### Hot reload strategy

For .NET, run `dotnet watch` inside the container and let Tilt sync source files via `live_update`. For React, Vite's HMR in Docker requires `usePolling: true` (essential with Colima) and explicit `host: '0.0.0.0'` configuration. Both approaches avoid full container rebuilds.

### Docker Compose with profiles and health checks

Use profiles for optional services and `condition: service_healthy` to replace `wait-for-it` scripts:

```yaml
services:
  postgres:
    image: postgres:17-alpine
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      timeout: 5s
      retries: 5
      start_period: 10s

  api:
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy

  pgadmin:
    profiles: ["tools"]
    # Only starts with: docker compose --profile tools up

  aspire-dashboard:
    profiles: ["observability"]
```

### Skip .NET Aspire for now

Aspire's `AddProject<T>()` only works for .NET projects — React must still run as a Docker container via `AddDockerfile()`, losing most Aspire benefits. Tilt already solves the developer experience problem, Docker Compose is portable to any cloud, and Aspire adds AppHost + ServiceDefaults project overhead. Revisit if you move to a pure .NET full-stack or deploy to Azure.

---

## 7. The $4/month question: GitHub Pro is the answer

### Why workarounds don't work

Without branch protection (paywalled behind GitHub Pro for private repos), every quality gate is advisory. Pre-push hooks can be bypassed with `--no-verify`. CI comment bots provide visual signals but don't prevent merging. Mergeable and merge-me are free GitHub Apps that add soft protection, but admins can always override them. Even GitHub's newer rulesets require Pro for private repos.

### What $4/month buys

| Feature | Impact |
|---------|--------|
| **Branch protection** | Require status checks, block force-push, require linear history |
| **Rulesets** | More flexible than branch protection, stackable |
| **Required status checks** | CI must pass before merge — the critical gate |
| **Draft PRs** | Signal work-in-progress |
| **Code owners** | Automatic review routing (useful when adding collaborators) |
| **3,000 CI minutes** | 50% more than free tier |
| **GitHub Pages (private)** | Host internal docs |
| **Repository insights** | Traffic, commit frequency, code frequency graphs |

**At $48/year, this is the single most impactful investment for CI/CD quality.** It eliminates the entire workaround problem and is the only way to make the `ci-gate` job actually block merges.

### If staying on free plan

Stack these defenses: Lefthook pre-push hook blocking direct pushes to main, CI comment bot posting ❌/✅ on every PR, self-discipline to always use PRs, and the gate job for aggregated pass/fail visibility. This is adequate for a solo developer during early development but inadequate for production.

---

## 8. Dependabot: grouped updates with auto-merge

### The complete configuration

```yaml
# .github/dependabot.yml
version: 2
updates:
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
      day: "monday"
    groups:
      microsoft:
        patterns:
          - "Microsoft.Extensions.*"
          - "Microsoft.AspNetCore.*"
          - "Microsoft.EntityFrameworkCore.*"
        update-types: ["minor", "patch"]
      testing:
        patterns: ["xunit*", "Microsoft.NET.Test.*", "coverlet.*",
                    "FluentAssertions*", "NSubstitute*", "Bogus*"]
      nuget-minor-patch:
        patterns: ["*"]
        update-types: ["minor", "patch"]
    ignore:
      - dependency-name: "Microsoft.Extensions.*"
        update-types: ["version-update:semver-major"]
    commit-message:
      prefix: "deps(nuget):"

  - package-ecosystem: "npm"
    directory: "/frontend"
    schedule:
      interval: "weekly"
      day: "monday"
    groups:
      react:
        patterns: ["react", "react-dom", "@types/react*"]
        update-types: ["minor", "patch"]
      vite-tooling:
        patterns: ["vite", "@vitejs/*", "typescript", "eslint*", "prettier*"]
        dependency-type: "development"
      testing:
        patterns: ["vitest*", "@testing-library/*", "playwright*"]
        dependency-type: "development"
    commit-message:
      prefix: "deps(npm):"

  - package-ecosystem: "github-actions"
    directory: "/"
    schedule:
      interval: "weekly"
    groups:
      actions-all:
        patterns: ["*"]
    commit-message:
      prefix: "deps(actions):"

  - package-ecosystem: "docker"
    directory: "/backend"
    schedule:
      interval: "weekly"
    commit-message:
      prefix: "deps(docker):"
```

Auto-merge patches and minor updates with a workflow that checks `dependabot/fetch-metadata` output and runs `gh pr merge --auto --squash`. This requires "Allow auto-merge" enabled in repo settings and (with GitHub Pro) branch protection requiring CI to pass — ensuring auto-merged updates are tested.

---

## 9. Trunk-based development: CI that adapts to context

### PR vs. main: different depths of validation

On pull requests, run the fast feedback loop: build, unit tests, lint, format check. On push to main, add integration tests and produce deployment artifacts. The key is conditional steps:

```yaml
- name: Run unit tests
  run: dotnet test --no-build --filter "Category!=Integration"

- name: Run integration tests (main only)
  if: github.event_name == 'push'
  run: dotnet test --no-build --filter "Category=Integration"
```

Keep PRs running the full unit test suite until tests exceed 5 minutes — premature optimization here reduces confidence without meaningful time savings.

### Release automation phases

**Now**: No automation — manual releases during active development. **MVP**: Tag-based releases with `softprops/action-gh-release` generating release notes from conventional commits. **Public release**: Date-based versioning (`v2026.03.25.1`) or semantic versioning with automated release on merge to main.

### Rollback is just a revert

With trunk-based development and small PRs, `git revert HEAD --no-edit && git push` is the rollback strategy. CI runs on the revert commit, validating it automatically. For deployment, keep previous deployment artifacts for instant rollback without waiting for CI.

---

## 10. Future-proofing: what's hard to retrofit

### Set up now

These decisions compound — changing them later requires touching every file:

- **Structured logging** (Serilog with JSON output) — switching from `Console.WriteLine` later requires rewriting every log statement
- **API versioning strategy** — adding `/api/v2/` after public release means breaking changes
- **EF Core migration bundles** for deployment, not runtime `Database.Migrate()` — the deployment pattern is hard to change later
- **Conventional commits** — changing commit format breaks changelog generation
- **Coverage tracking** — establishing the ratchet floor early prevents technical debt accumulation

### Add at MVP

**Testcontainers** works out of the box on GitHub Actions (ubuntu-latest has Docker pre-installed, no configuration needed). Start with database integration tests when you have real data access logic worth testing. **Playwright** for E2E testing of critical user flows — cache browsers at `~/.cache/ms-playwright` and use the `webServer` config to auto-start the React dev server. **EF Core migration testing** in CI validates that migrations apply cleanly to a real database.

### Performance benchmarks: not yet

GitHub Actions runner variance is **10–20%** due to different CPU models (Intel Xeon Platinum 8370C variants with different cache configurations). This makes benchmark-based CI gates unreliable. Use BenchmarkDotNet locally for performance-critical code, and only add CI benchmarks with very generous thresholds (200%+) when you have self-hosted runners for consistent hardware.

---

## The REVIEW.md quality contract

Commit this to the repo root as the authoritative reference for all PR reviews:

```markdown
# REVIEW.md — Quality Gates & Standards

## CI/CD Requirements (Every PR)
- [ ] All CI checks pass (build, test, lint, security scan)
- [ ] No decrease in code coverage (target: auto)
- [ ] Patch coverage ≥ 80%
- [ ] No new HIGH/CRITICAL vulnerabilities (Trivy)
- [ ] PR title follows conventional commits
- [ ] Changes include tests for new functionality

## AI-Generated Code Review Focus
- [ ] Verify all referenced packages exist (CI checks this)
- [ ] Check for unnecessary abstraction layers
- [ ] Validate error handling covers edge cases
- [ ] Confirm no hardcoded secrets or insecure defaults
- [ ] Review authorization on new endpoints
- [ ] Ensure consistent patterns with existing code

## Dependency Rules
- No wildcard version ranges
- Major version bumps require manual review
- License check on new dependencies (no AGPL/SSPL/BUSL)
- All GitHub Actions SHA-pinned with version comments

## Architecture Boundaries
- Domain layer: no infrastructure dependencies
- API layer: thin controllers, logic in services
- Frontend: components under 200 lines, hooks for shared logic
```

---

## Consolidated cost analysis and priority matrix

| Investment | Cost | Phase | Impact |
|-----------|------|-------|--------|
| **GitHub Pro** | $4/mo | **Now** | Branch protection, required status checks — eliminates all workarounds |
| Codecov | Free (1 user) | Now | Coverage tracking with flags and components |
| Trivy + Gitleaks | Free | Now | Vulnerability, IaC, secret, and git history scanning |
| Semgrep | Free | Now | SAST coverage for AI-generated code |
| Dependabot | Free | Now | Automated dependency updates across 4 ecosystems |
| Lefthook + commitlint | Free | Now | Local quality gates with <100ms overhead |
| Colima (vz+virtiofs) | Free | Now | Docker runtime at 70–90% native filesystem speed |
| Testcontainers | Free | MVP | Integration testing with real databases |
| Playwright | Free | MVP | E2E testing of critical flows |
| GitHub Code Security | $30/mo | Public release | SARIF upload, CodeQL SAST, Copilot Autofix |
| Codecov Pro | $12/mo | Team growth | Advanced components, unlimited uploads |

The total investment for a production-ready CI/CD pipeline is **$4/month** through public release. Every other tool in this stack is free for a solo developer on a private repo. The five-layer quality pipeline — Lefthook, AI review, CI gates, build-time analysis, human review — is the right architecture. The configurations above close the gaps.