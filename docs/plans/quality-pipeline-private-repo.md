# Quality Pipeline: Private Repo Redesign

Handoff document for implementing the revised quality pipeline. See DEC-034 amendment for the decision rationale and R-012 research (batch-5-ai-pr-review-quality-tool.md) for the underlying research.

**Context:** The original pipeline (DEC-034) assumed a public/OSS repo where CodeRabbit, CodeQL, SonarCloud, and Claude Code GitHub Action are free. The repo is private to protect coaching prompt IP. This plan implements the revised five-layer pipeline using tools that are free regardless of repo visibility.

**Core principle preserved:** IBM Research showed LLM-as-judge alone detects ~45% of errors in AI-generated code. Supplemented with deterministic static analysis, coverage rises to ~94%. Every layer below maintains uncorrelated deterministic analysis.

**Read before starting:** CLAUDE.md (root), ROADMAP.md, DEC-034 (including amendment), the batch 5 research artifact.

---

## Step 1: Fix Lefthook config (Layer 1)

Align `lefthook.yml` with the research recommendation. Current config has issues discovered during initial scaffolding.

**Update `lefthook.yml`:**

```yaml
pre-commit:
  parallel: true
  commands:
    dotnet-format:
      root: "backend/"
      glob: "*.cs"
      run: dotnet format RunCoach.slnx --include {staged_files} --no-restore
      stage_fixed: true
    eslint:
      root: "frontend/"
      glob: "*.{ts,tsx}"
      run: npx eslint --fix {staged_files}
      stage_fixed: true
    prettier:
      root: "frontend/"
      glob: "*.{ts,tsx,css,json}"
      run: npx prettier --write {staged_files}
      stage_fixed: true

commit-msg:
  commands:
    commitlint:
      run: npx commitlint --edit {1}

pre-push:
  parallel: true
  commands:
    dotnet-test:
      root: "backend/"
      glob: "*.cs"
      run: dotnet test RunCoach.slnx --no-restore --filter "Category!=Integration"
    typescript-check:
      root: "frontend/"
      glob: "*.{ts,tsx}"
      run: npx tsc --noEmit
```

Key changes from current config:
- `dotnet format --include {staged_files}` (fix mode) replaces `--verify-no-changes` (reject mode)
- `stage_fixed: true` on all formatters — auto-restage fixed files
- ESLint runs with `--fix` (auto-fix) instead of lint-only
- Prettier runs with `--write` (auto-fix) instead of `--check`
- All commands use `root:` for proper path scoping

**Verify:** Make a commit with an intentionally unformatted .ts file — Lefthook should auto-fix and restage it. Make a commit with a bad message — commitlint should reject it.

---

## Step 2: Install eslint-plugin-sonarjs (Layer 4 — frontend)

```bash
cd frontend
npm install -D eslint-plugin-sonarjs
```

**Update `frontend/eslint.config.js`** to add the sonarjs plugin:

```javascript
import js from '@eslint/js'
import tseslint from 'typescript-eslint'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import sonarjs from 'eslint-plugin-sonarjs'
import prettier from 'eslint-config-prettier'

export default [
  { ignores: ['dist'] },
  js.configs.recommended,
  ...tseslint.configs.strict,
  sonarjs.configs.recommended,
  {
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
    },
  },
  prettier,
]
```

**Verify:** `cd frontend && npx eslint src/` — should pass with no new errors on existing code. If sonarjs flags existing code, evaluate whether to fix or suppress on a case-by-case basis.

---

## Step 3: Replace CodeQL with Trivy in CI (Layer 3)

**Update `.github/workflows/ci.yml`:**

Replace the `security` job entirely. Remove CodeQL. Add Trivy.

```yaml
  security:
    runs-on: ubuntu-latest
    permissions:
      contents: read
    steps:
      - uses: actions/checkout@v4

      - name: Trivy filesystem scan
        uses: aquasecurity/trivy-action@master
        with:
          scan-type: 'fs'
          scan-ref: '.'
          severity: 'CRITICAL,HIGH'
          exit-code: '1'
          ignore-unfixed: true

      - name: Trivy IaC scan
        uses: aquasecurity/trivy-action@master
        with:
          scan-type: 'config'
          scan-ref: '.'
          severity: 'CRITICAL,HIGH'
          exit-code: '1'

      - name: Trivy secrets scan
        uses: aquasecurity/trivy-action@master
        with:
          scan-type: 'fs'
          scan-ref: '.'
          scanners: 'secret'
          exit-code: '1'
```

This gives three scans:
1. **Filesystem** — NuGet + npm dependency vulnerabilities (CRITICAL/HIGH fail the build)
2. **IaC** — Dockerfile and docker-compose.yml misconfigurations
3. **Secrets** — catches committed secrets that .gitignore missed

**Verify:** CI pipeline passes with Trivy instead of CodeQL. No false positives on the current codebase.

---

## Step 4: Add Codecov configuration (Layer 3)

Create `codecov.yml` at repo root:

```yaml
coverage:
  status:
    project:
      default:
        target: 60%
    patch:
      default:
        target: 70%

flags:
  backend:
    paths:
      - backend/
    carryforward: true
  frontend:
    paths:
      - frontend/
    carryforward: true

comment:
  layout: "reach,diff,flags,files"
  behavior: default
```

Coverage thresholds per research recommendation:
- 60% project target (overall)
- 70% patch coverage (new code — ensures AI writes tests for new features)
- Carryforward flags so path-filtered CI runs don't reset coverage for unchanged paths

**Verify:** Codecov PR comment shows correct flags and thresholds on next push.

---

## Step 5: Verify Dependabot is working

Check that Dependabot is actually enabled and running:

```bash
# Check if Dependabot has created any PRs or alerts
gh api repos/{owner}/{repo}/dependabot/alerts --jq length
gh api repos/{owner}/{repo}/vulnerability-alerts -i  # Check if enabled

# If vulnerability alerts aren't enabled:
gh api repos/{owner}/{repo}/vulnerability-alerts -X PUT
```

Verify the `dependabot.yml` covers all four ecosystems: nuget, npm, github-actions, docker.

**Verify:** Dependabot is enabled and has run at least one check. If there are existing vulnerability alerts, triage them.

---

## Step 6: Set up branch protection

Configure branch protection on `main` via GitHub CLI:

```bash
gh api repos/{owner}/{repo}/branches/main/protection -X PUT -f '{
  "required_status_checks": {
    "strict": false,
    "contexts": ["gate"]
  },
  "enforce_admins": false,
  "required_pull_request_reviews": {
    "required_approving_review_count": 0
  },
  "restrictions": null
}'
```

This enforces:
- The `gate` CI status check must pass before merging (which means build, test, Trivy, and coverage all passed)
- PRs are required (no direct push to main)
- No approval required (solo dev — you approve your own PRs after `/review-pr`)

**Verify:** Try to merge the current PR without CI passing — GitHub should block it.

---

## Step 7: Commit, push, and verify CI

After all changes:

1. `dotnet build backend/RunCoach.slnx` — zero warnings
2. `cd frontend && npm run build` — passes
3. `cd frontend && npx eslint src/` — passes (including sonarjs rules)
4. Commit with conventional commit message
5. Push and verify CI pipeline: changes → backend → frontend → security (Trivy) → gate all pass
6. Verify Codecov comment appears on PR with correct flags

---

## After this plan

Update ROADMAP.md:
- Mark quality pipeline finalization as complete
- Update current phase to "Development-Ready"
- Confirm next step is POC 1

The repo is then fully development-ready. Next session implements POC 1 following `docs/plans/poc-1-context-injection-plan-quality.md`.
