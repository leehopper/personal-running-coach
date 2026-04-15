# SonarQube Cloud CI integration for a .NET 10 + React monorepo

**SonarQube Cloud (the 2024 rebrand of SonarCloud) requires CI-based analysis for your monorepo**, not automatic analysis. The recommended 2026 pattern uses two separate SonarQube Cloud projects under one organization — one for the .NET backend scanned via `dotnet-sonarscanner`, one for the React frontend scanned via the unified `SonarSource/sonarqube-scan-action@v7`. Your existing Cobertura coverage **will not work directly** for the .NET side; SonarQube Cloud has no `sonar.cs.cobertura.reportsPaths` property, so you must add OpenCover output from Coverlet alongside Cobertura. The frontend LCOV coverage works as-is. Below is everything needed for a ready-to-commit integration.

---

## 1. Branding, action name, and SHA-pinned references

The product is officially **"SonarQube Cloud"** across all documentation since October 2024. The URL remains `sonarcloud.io` and the docs live at `docs.sonarsource.com/sonarqube-cloud/`. The old `SonarSource/sonarcloud-github-action` repository was **archived on October 22, 2025**; its final v5.0.0 release is a redirect stub pointing to the unified action.

The **only action to use** is `SonarSource/sonarqube-scan-action`, which now handles both SonarQube Server and SonarQube Cloud. Version 7 ships SonarScanner CLI v8 with an **embedded Java 21 JRE**, runs as a composite action (Linux, macOS, Windows), and eliminates the old Docker-only limitation.

| Action | Tag | SHA | Released |
|---|---|---|---|
| `SonarSource/sonarqube-scan-action` | **v7.0.0** | `a31c9398be7ace6bbfaf30c0bd5d415f843d45e9` | Dec 9, 2025 |
| `actions/checkout` | **v6.0.2** | `de0fac2e4500dabe0009e67214ff5f5447ce83dd` | Jan 9, 2026 |
| `actions/setup-java` | **v5.2.0** | `be666c2fcd27ec809703dec50e508c2fdc7f6654` | Jan 22, 2026 |
| `actions/setup-dotnet` | **v5.2.0** | `c2fa09f4bde5ebb9d1777cf28262a3eb3db3ced7` | Mar 5, 2025 |
| `actions/setup-node` | **v6.3.0** | `53b83947a5a98c8d113130e565377fae1a50d02f` | Mar 4, 2025 |

The `dotnet-sonarscanner` global tool is at **version 11.2.1** (April 2, 2026 on NuGet). Scanner versions 7.0+ auto-provision a JRE from the SonarQube server, so `actions/setup-java` is **not required** for the .NET job. The `sonarqube-scan-action@v7` embeds its own JRE as well, so Java setup is also unnecessary for the frontend job. You can omit `actions/setup-java` entirely.

A critical September 2025 security advisory from SonarSource urges all users to run at minimum v5.3.1 of the scan action. Using v7.0.0 satisfies this.

---

## 2. Why CI-based analysis is the only viable mode

Your prior analysis is **correct on all counts**. Automatic analysis is disqualified for three independent reasons:

- **Monorepos are explicitly unsupported.** The docs state "Not compatible with monorepos" — automatic analysis processes the entire repo as one project and cannot split into sub-projects.
- **Coverage is not available** under automatic analysis. Only CI-based analysis can ingest coverage reports.
- **SonarScanner for .NET cannot run** in automatic mode. The `begin → build → end` lifecycle requires CI orchestration.
- Automatic analysis requires ≥20% of lines in a supported language. A mixed .NET + TypeScript repo may not hit this threshold consistently for either language in isolation.

You must **disable automatic analysis** per-project in SonarQube Cloud (Administration → Analysis Method) after creating the projects. The two modes are mutually exclusive — having both enabled causes conflicts.

---

## 3. Complete workflow file

```yaml
# .github/workflows/sonarqube.yml
name: SonarQube Cloud

on:
  push:
    branches: [main]
  pull_request:
    types: [opened, synchronize, reopened]

permissions:
  contents: read

jobs:
  # ──────────────────────────────────────────────
  # Backend: .NET 10 via dotnet-sonarscanner
  # ──────────────────────────────────────────────
  backend:
    name: Backend analysis
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2
        with:
          fetch-depth: 0 # full history required for new-code detection

      - uses: actions/setup-dotnet@c2fa09f4bde5ebb9d1777cf28262a3eb3db3ced7 # v5.2.0
        with:
          dotnet-version: "10.0.x"
          dotnet-quality: preview

      # Scanner v11.2+ auto-provisions a JRE — no setup-java needed
      - name: Install dotnet-sonarscanner
        run: dotnet tool install --global dotnet-sonarscanner --version 11.2.1

      - name: SonarScanner begin
        env:
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN_BACKEND }}
        run: |
          dotnet sonarscanner begin \
            /k:"<org>_runcoach-backend" \
            /o:"<org>" \
            /d:sonar.host.url="https://sonarcloud.io" \
            /d:sonar.token="$SONAR_TOKEN" \
            /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" \
            /d:sonar.exclusions="docs/**,.claude/**,**/wwwroot/lib/**" \
            /d:sonar.test.inclusions="**/*.Tests/**,**/*.Test/**" \
            /d:sonar.cs.roslyn.ignoreIssues=false

      - name: Build
        run: dotnet build backend/RunCoach.slnx --no-incremental

      - name: Test with coverage
        run: |
          dotnet test backend/RunCoach.slnx --no-build \
            --collect "XPlat Code Coverage" \
            -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover,cobertura

      - name: SonarScanner end
        env:
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN_BACKEND }}
        run: dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"

      # Existing Codecov upload still works — cobertura file is produced alongside opencover
      # Add your existing Codecov upload step here if this workflow replaces the old one

  # ──────────────────────────────────────────────
  # Frontend: React 19 + TypeScript via scan action
  # ──────────────────────────────────────────────
  frontend:
    name: Frontend analysis
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2
        with:
          fetch-depth: 0

      - uses: actions/setup-node@53b83947a5a98c8d113130e565377fae1a50d02f # v6.3.0
        with:
          node-version: "22"
          cache: npm
          cache-dependency-path: frontend/package-lock.json

      - name: Install dependencies
        working-directory: frontend
        run: npm ci

      - name: Run tests with coverage
        working-directory: frontend
        run: npx vitest run --coverage

      - name: SonarQube scan
        uses: SonarSource/sonarqube-scan-action@a31c9398be7ace6bbfaf30c0bd5d415f843d45e9 # v7.0.0
        env:
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN_FRONTEND }}
        with:
          projectBaseDir: frontend/
```

### Key design decisions in the workflow

**No `actions/setup-java` step.** The `dotnet-sonarscanner` v11.2+ auto-provisions a JRE from the SonarQube server, and the `sonarqube-scan-action@v7` embeds Java 21 in its Scanner CLI v8. This eliminates a step and ~15 seconds of CI time.

**Coverage dual-format output.** The `--collect "XPlat Code Coverage"` flag with `Format=opencover,cobertura` produces both files in each test project's `TestResults/` directory. The OpenCover file (`coverage.opencover.xml`) feeds SonarQube via the glob `**/coverage.opencover.xml`; the Cobertura file continues feeding Codecov. This requires `coverlet.collector` (not `coverlet.msbuild`) as a package reference in test projects. If you're using `coverlet.msbuild`, switch to `/p:CoverletOutputFormat=\"opencover,cobertura\"` instead.

**The `sonar.login` parameter is deprecated.** Use `sonar.token` in all begin/end invocations.

**Two parallel jobs.** Backend and frontend run concurrently. Each uses its own `SONAR_TOKEN_*` secret (or a single org-scoped token assigned to both).

---

## 4. Monorepo structure: two projects, not modules

The **2026 recommended pattern** is **two separate SonarQube Cloud projects** under a single organization — `<org>_runcoach-backend` and `<org>_runcoach-frontend`. This is the approach documented in the official monorepo guide.

Do **not** use `sonar.modules=backend,frontend`. The `sonar.modules` mechanism is a legacy SonarScanner feature that splits a single project into modules. It is poorly supported on SonarQube Cloud, and critically, **it cannot work when one module requires `dotnet-sonarscanner` (begin/end lifecycle) and the other uses the generic SonarScanner CLI**. These are fundamentally different scanner types with incompatible execution models.

Two separate projects give you independent quality gates, independent PR decoration, independent new-code definitions, and clean separation of language-specific configuration. The monorepo docs explicitly recommend unique keys following the pattern `myorg_mymonorepo_myproject`.

---

## 5. Configuration properties per module

### Backend — passed via `dotnet sonarscanner begin` arguments

All backend properties are set as `/d:` flags in the begin step (shown in the workflow above). The dotnet-sonarscanner does not read `sonar-project.properties` — it uses MSBuild integration. Key properties:

```
/k:"<org>_runcoach-backend"
/o:"<org>"
/d:sonar.host.url="https://sonarcloud.io"
/d:sonar.token="$SONAR_TOKEN"
/d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml"
/d:sonar.exclusions="docs/**,.claude/**,**/wwwroot/lib/**"
/d:sonar.test.inclusions="**/*.Tests/**,**/*.Test/**"
/d:sonar.cs.roslyn.ignoreIssues=false
```

The `sonar.sources` and `sonar.tests` properties are **auto-detected** by the .NET scanner based on the project SDK type (`Microsoft.NET.Sdk` vs `Microsoft.NET.Sdk.Web`) and test framework references. You do not need to set them manually for .NET projects.

### Frontend — `frontend/sonar-project.properties`

```properties
sonar.projectKey=<org>_runcoach-frontend
sonar.organization=<org>
sonar.host.url=https://sonarcloud.io

# Source configuration
sonar.sources=src
sonar.tests=src
sonar.test.inclusions=**/*.test.ts,**/*.test.tsx,**/*.spec.ts,**/*.spec.tsx

# Coverage
sonar.javascript.lcov.reportPaths=coverage/lcov.info

# Exclusions
sonar.exclusions=dist/**,node_modules/**,coverage/**,.claude/**,docs/**,**/*.config.ts,**/*.config.js
sonar.javascript.exclusions=node_modules/**,dist/**,coverage/**

# TypeScript
sonar.typescript.tsconfigPaths=tsconfig.json,tsconfig.app.json

# Encoding
sonar.sourceEncoding=UTF-8
```

Note: `sonar.typescript.lcov.reportPaths` is **deprecated**. Use `sonar.javascript.lcov.reportPaths` for both JavaScript and TypeScript coverage.

---

## 6. Quality gate customization

### The "Sonar Way" default gate in 2026

The built-in **Sonar Way** gate enforces six conditions on new code:

| Condition | Threshold |
|---|---|
| Reliability rating | A (no new bugs) |
| Security rating | A (no new vulnerabilities) |
| Maintainability rating | A |
| Security hotspots reviewed | 100% |
| Coverage on new code | ≥ 80% |
| Duplicated lines on new code | ≤ 3% |

There is also a **"Sonar way for AI Code"** gate optimized for AI-generated code. A small-project "fudge factor" (enabled by default) skips the duplication and coverage conditions when the changeset has fewer than **20 new lines**.

### Removing the duplication condition

Custom quality gates require a **Team or Enterprise plan**. On the Free plan, the Sonar Way gate is read-only and cannot be modified. If you're on a paid plan:

1. Navigate to **Organization Settings → Quality Gates**.
2. Click **Create** to create a new gate (you cannot edit the built-in Sonar Way).
3. Name it something like "RunCoach Way".
4. Add all Sonar Way conditions **except** the duplication condition.
5. Keep Reliability ≥ A and Security ≥ A strict.
6. Set this gate as the default for your organization, or assign it per-project via **Project Settings → Quality Gate**.

On the **Free plan**, your alternative is to accept the fudge factor (which ignores duplication on small changesets) and tolerate occasional duplication failures on larger PRs. Duplication gate failures do not block merges unless you explicitly add the SonarQube quality gate as a required status check in GitHub branch protection.

---

## 7. PR decoration and GitHub App setup

### One-time installation in 2026

PR decoration on SonarQube Cloud is **automatic** for GitHub repositories. When you import a repository through the SonarQube Cloud UI (+ → Analyze new project → select your GitHub org and repo), SonarQube Cloud uses the GitHub App installed during your initial organization binding. No separate GitHub App installation is needed per-repo.

If your GitHub organization doesn't yet have the SonarQube Cloud GitHub App:

1. Go to `sonarcloud.io` → **Create Organization** → **GitHub**.
2. Authorize the **SonarCloud** GitHub App (it still uses the legacy app name).
3. Grant it access to your repository (or all repositories).
4. This app provides PR decoration, commit status checks, and repository access.

### Soft launch without blocking PRs

PR decoration (inline annotations + quality gate summary comment) is enabled **by default** and does not block anything. The quality gate status appears as a GitHub Check but is **not a required check** unless you explicitly add it to branch protection:

- To add as required: Repository Settings → Branches → Branch protection rules → Require status checks → add "SonarQube Cloud Code Analysis".
- To keep soft: Simply don't add it as a required check. Developers see the quality gate status on each PR but can merge regardless.

This gives you a soft-launch window to tune thresholds and exclusions before making the gate mandatory.

---

## 8. MCP server configuration for Claude Code

The **SonarQube MCP Server** at `github.com/SonarSource/sonarqube-mcp-server` is still the current repository as of April 2026. The latest release is **v1.9.0.1909** (February 5, 2026). Additionally, SonarQube Cloud introduced a **native embedded MCP server** on March 18, 2026 that requires no local Docker setup.

### Option A: Docker-based MCP (standalone)

Add to your Claude Code user-level config via the CLI:

```bash
claude mcp add sonarqube \
  --env SONARQUBE_TOKEN=$SONAR_TOKEN \
  --env SONARQUBE_ORG=<org> \
  -- docker run -i --rm -e SONARQUBE_TOKEN -e SONARQUBE_ORG mcp/sonarqube
```

Or add manually to `~/.claude/mcp_config.json`:

```json
{
  "mcpServers": {
    "sonarqube": {
      "command": "docker",
      "args": [
        "run", "-i", "--rm",
        "-e", "SONARQUBE_TOKEN",
        "-e", "SONARQUBE_ORG",
        "mcp/sonarqube"
      ],
      "env": {
        "SONARQUBE_TOKEN": "<your-user-token>",
        "SONARQUBE_ORG": "<org>"
      }
    }
  }
}
```

### Option B: Embedded MCP in SonarQube Cloud (March 2026+)

The embedded server requires no Docker. Configure it by pointing to the SonarQube Cloud remote MCP endpoint. See `docs.sonarsource.com/sonarqube-cloud/ai-capabilities/sonarqube-mcp-server#mcp-server-in-sonarqube-cloud` for the remote server URL format.

### Key environment variables

| Variable | Required | Purpose |
|---|---|---|
| `SONARQUBE_TOKEN` | Yes | Must be a **User** token (not project/global) |
| `SONARQUBE_ORG` | Yes (Cloud) | Your organization key |
| `SONARQUBE_URL` | No | Defaults to `https://sonarcloud.io`; set to `https://sonarqube.us` for US region |
| `SONARQUBE_READ_ONLY` | No | Set `true` for read-only mode |

---

## 9. SonarAnalyzer.CSharp overlap is by design

The SonarScanner for .NET **injects its own copy of SonarAnalyzer.CSharp into the build** during the `begin` step. It replaces the active code analysis ruleset with one matching your SonarQube Cloud quality profile. This means:

- **SonarQube Cloud re-runs analysis** — it doesn't just read your build's existing Roslyn output. The scanner injects its own version of the analyzer (potentially different from the NuGet version you reference).
- If you also have `SonarAnalyzer.CSharp` as a NuGet package reference (for IDE feedback in Rider/VS), that's fine. The scanner's injected version takes precedence during CI analysis. **No duplicated findings** will appear in the SonarQube dashboard because the scanner controls which rules fire.
- **Third-party Roslyn analyzers** (e.g., `Microsoft.CodeAnalysis.NetAnalyzers`, `StyleCop.Analyzers`) are imported as **external issues** by default. They show up separately from Sonar-native issues. To disable this import, set `sonar.cs.roslyn.ignoreIssues=true`.

### The recommended setup matches your preference

**Keep build-time analyzers as the hard gate** (fail the build on Roslyn warnings via `TreatWarningsAsErrors` or `WarningsAsErrors`). **Use SonarQube Cloud as the dashboard** for trend analysis, tech debt tracking, and PR decoration. There is no conflict because:

- Build-time Roslyn analyzers catch issues at compile time (fast, blocking).
- SonarQube Cloud provides the broader view: coverage tracking, duplication, security hotspots, historical trends, and PR-level quality gates.

The `sonar.cs.roslyn.reportFilePaths` property is an **internal/undocumented** parameter auto-managed by the scanner between `begin` and `end`. You do not need to set it manually.

---

## 10. Rollback procedure

If SonarQube Cloud integration causes noise or incorrectly blocks PRs, roll back in stages:

**Stage 1 — Remove gate enforcement (5 minutes):**
Remove the quality gate from GitHub branch protection. Go to Repository Settings → Branches → edit the branch protection rule → uncheck "SonarQube Cloud Code Analysis" from required status checks. PRs can now merge regardless of SonarQube results.

**Stage 2 — Disable the workflow (1 minute):**
Either delete `.github/workflows/sonarqube.yml` or rename it to `.github/workflows/sonarqube.yml.disabled`. Alternatively, add `if: false` to both jobs to keep the file in version control for later re-enablement.

**Stage 3 — Clean up secrets (optional):**
Remove `SONAR_TOKEN_BACKEND` and `SONAR_TOKEN_FRONTEND` from GitHub repository secrets (Settings → Secrets and variables → Actions).

**Stage 4 — Remove SonarQube Cloud projects (optional):**
In the SonarQube Cloud UI, go to each project → Administration → Deletion → Delete. This removes all historical analysis data. Only do this if you're sure you won't re-enable.

**Stage 5 — Revoke GitHub App access (nuclear option):**
Go to GitHub → Settings → Applications → SonarCloud → Revoke. This removes PR decoration and all SonarQube Cloud access to the repository.

Existing CI, Codecov uploads, build-time Roslyn analyzers — none of these depend on SonarQube Cloud. Removing the SonarQube workflow has **zero impact** on the rest of the pipeline.

---

## One-time setup checklist

### Organization level (do once)
- [ ] Create SonarQube Cloud organization bound to your GitHub org at `sonarcloud.io`
- [ ] Install the SonarCloud GitHub App and grant access to the `RunCoach` repository
- [ ] Generate an organization-scoped token (Team plan) or personal access token (Free plan)
- [ ] Optionally create a custom quality gate without the duplication condition (Team/Enterprise plan only)

### Repository level (do once)
- [ ] In SonarQube Cloud UI: + → Analyze new project → "Setup a monorepo" → create `<org>_runcoach-backend` and `<org>_runcoach-frontend` as separate projects
- [ ] Disable automatic analysis for both projects: each project → Administration → Analysis Method → toggle off
- [ ] Set New Code Definition for each project (e.g., "Previous version" or "Reference branch: main")
- [ ] Add `SONAR_TOKEN_BACKEND` and `SONAR_TOKEN_FRONTEND` as GitHub repository secrets
- [ ] Add `frontend/sonar-project.properties` (see Section 5)
- [ ] Ensure test projects reference `coverlet.collector` NuGet package (for `--collect "XPlat Code Coverage"` to produce OpenCover format)
- [ ] Commit `.github/workflows/sonarqube.yml` (see Section 3)
- [ ] Verify first analysis succeeds on `main` push, then check PR decoration on a test PR

### Optional enhancements
- [ ] Configure MCP server in Claude Code user config (see Section 8)
- [ ] Once stable, add "SonarQube Cloud Code Analysis" as required status check in branch protection
- [ ] Enable the "Sonar way for AI Code" quality gate if using AI-assisted coding extensively

---

## Conclusion

The integration pivots on three decisions that differ from what the defaults might suggest. First, **CI-based analysis is mandatory** — automatic analysis cannot handle .NET scanner lifecycle, monorepo splitting, or coverage ingestion. Second, **two separate projects beat one module-based project** because the backend and frontend use fundamentally different scanners and have independent quality concerns. Third, **you must produce OpenCover-format coverage** alongside Cobertura because SonarQube Cloud has no Cobertura ingestion path for .NET — the `Format=opencover,cobertura` flag on the `--collect` directive solves this cleanly.

The workflow avoids `actions/setup-java` entirely because both the .NET scanner (v11.2+) and the scan action (v7) auto-provision or embed their own JREs. The overlap between build-time `SonarAnalyzer.CSharp` and SonarQube Cloud is intentional and harmless — keep your build-time analyzers as the fast compile-time gate while using Cloud for the dashboard, trends, and PR-level quality visualization. With the quality gate initially configured as a non-required check, you get a risk-free soft launch with a clear three-stage rollback path if the integration proves noisy.