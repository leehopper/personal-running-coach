# CodeQL SAST for a .NET 10 + React 19 monorepo

**CodeQL Action v4 is now the current major version** — v3 still functions but is deprecated for December 2026. Your workflow should target v4, use the combined `javascript-typescript` language identifier, and opt for `build-mode: manual` for C# despite .slnx now being supported by autobuild since CodeQL 2.24.0. Below is a complete, ready-to-commit configuration with SHA-pinned actions, a scoped query config, and operational guidance for branch protection, storage, local debugging, and rollback.

---

## 1. v4 replaced v3 as the current major in October 2025

GitHub released **CodeQL Action v4 on October 7, 2025**, running on Node.js 24. v3 (Node.js 20) continues to work but logs deprecation warnings and will be officially retired in **December 2026** alongside GHES 3.19. The v3 deprecation was announced in the GitHub Changelog on October 28, 2025. Brownout periods may be scheduled later in 2026 if migration lags.

All three sub-actions (`init`, `autobuild`, `analyze`) share a single repo and therefore a single commit SHA per release. The `autobuild` action still exists but is de-emphasized — the README now recommends using `build-mode: autobuild` as an input to `init` instead.

**Verified SHAs for SHA-pinning** (all 40-character, confirmed from the GitHub releases page):

| Action | Tag | Full SHA |
|---|---|---|
| `github/codeql-action/*` | **v4.35.1** | `c10b8064de6f491fea524254123dbe5e09572f13` |
| `actions/checkout` | **v6.0.2** | `de0fac2e4500dabe0009e67214ff5f5447ce83dd` |
| `actions/setup-dotnet` | **v5.2.0** | `c2fa09f4bde5ebb9d1777cf28262a3eb3db3ced7` |

A newer patch **v4.35.2** exists (released ~April 13, 2026, bundling CodeQL CLI 2.25.2) but its abbreviated SHA (`95e58e9`) could not be expanded to 40 characters from public pages. To retrieve it yourself, run: `curl -s https://api.github.com/repos/github/codeql-action/git/ref/tags/v4.35.2 | jq '.object.sha'`. Configure **Dependabot for the `github-actions` ecosystem** to automate future SHA bumps.

---

## 2. Autobuild supports .slnx since January 2026, but manual build gives more control

The **CodeQL 2.24.0 changelog** (January 26, 2026) explicitly states: *"Added autobuilder and build-mode: none support for .slnx solution files."* The supported-languages page now lists `.slnx` alongside `.sln` in the C# extensions column. Both `autobuild` and `build-mode: none` will correctly detect and process `backend/RunCoach.slnx`.

**Despite this, `build-mode: manual` remains the right choice here** for three reasons. First, your existing `ci.yml` already runs `dotnet restore` then `dotnet build --no-restore` with specific SDK and NuGet configurations — duplicating that sequence in CodeQL ensures the analysis database matches your real build artifacts exactly. Second, autobuild's heuristic picks the solution file "closest to the root," which could misbehave if repository layout changes. Third, `build-mode: none` for C# creates the database without compilation, which can miss some inter-procedural data-flow paths that a traced build captures.

C# in CodeQL 2.25.x supports all three build modes: **`none`** (extraction without building — fast but reduced coverage), **`autobuild`** (heuristic build detection), and **`manual`** (explicit build commands between `init` and `analyze`).

---

## 3. The combined `javascript-typescript` identifier is now standard

CodeQL merged JavaScript and TypeScript analysis into a single language identifier. The docs state: *"Use `javascript-typescript` to analyze code written in JavaScript, TypeScript or both."* The legacy identifiers `javascript` and `typescript` still work as aliases but internally resolve to the same combined extractor. **`build-mode: none`** is correct for JS/TS — no compilation required.

React 19 with TypeScript strict mode needs **zero additional configuration**. The CodeQL supported-frameworks page lists `react` and `react native` as recognized frameworks. The JavaScript extractor handles TypeScript analysis natively; Node.js 14+ must be available on the runner (the `ubuntu-latest` image includes Node.js 20+, so this is satisfied automatically). No custom extractor environment variables or `tsconfig.json` path hints are needed.

---

## 4. Ready-to-commit `.github/workflows/codeql.yml`

```yaml
# .github/workflows/codeql.yml
# CodeQL SAST — first-party code only (C# + TypeScript/JavaScript)
# Trivy handles dependency, secret, and IaC scanning separately.
name: "CodeQL"

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  schedule:
    # Weekly Monday 07:25 UTC — offset from the hour to avoid Actions queue spikes
    - cron: "25 7 * * 1"

permissions:
  security-events: write   # Required to upload SARIF results
  actions: read             # Required for workflow run context
  contents: read            # Required to checkout code

concurrency:
  group: codeql-${{ github.ref }}-${{ matrix.language }}
  cancel-in-progress: true

jobs:
  analyze:
    name: Analyze (${{ matrix.language }})
    runs-on: ubuntu-latest
    timeout-minutes: 30

    strategy:
      fail-fast: false
      matrix:
        include:
          - language: csharp
            build-mode: manual
          - language: javascript-typescript
            build-mode: none

    steps:
      - name: Checkout repository
        uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2

      - name: Initialize CodeQL
        uses: github/codeql-action/init@c10b8064de6f491fea524254123dbe5e09572f13 # v4.35.1
        with:
          languages: ${{ matrix.language }}
          build-mode: ${{ matrix.build-mode }}
          config-file: ./.github/codeql/codeql-config.yml

      # ── C# manual build (runs only for the csharp matrix leg) ──
      - name: Set up .NET 10 preview SDK
        if: matrix.language == 'csharp'
        uses: actions/setup-dotnet@c2fa09f4bde5ebb9d1777cf28262a3eb3db3ced7 # v5.2.0
        with:
          dotnet-version: "10.0.x"
          dotnet-quality: "preview"

      - name: Restore NuGet packages
        if: matrix.language == 'csharp'
        run: dotnet restore backend/RunCoach.slnx

      - name: Build backend
        if: matrix.language == 'csharp'
        run: dotnet build backend/RunCoach.slnx --no-restore
      # ── End C# manual build ──

      - name: Perform CodeQL analysis
        uses: github/codeql-action/analyze@c10b8064de6f491fea524254123dbe5e09572f13 # v4.35.1
        with:
          category: "/language:${{ matrix.language }}"
```

**Design notes on this workflow.** The `concurrency` block deduplicates runs when multiple pushes arrive on the same branch. The `timeout-minutes: 30` prevents runaway builds from consuming unlimited Actions minutes. The `category` output in `analyze` ensures each language uploads to a distinct SARIF category so results never overwrite each other. The `autobuild` action is intentionally absent — `build-mode: manual` in `init` tells CodeQL to trace whichever commands you run between `init` and `analyze`.

---

## 5. Ready-to-commit `.github/codeql/codeql-config.yml`

```yaml
# .github/codeql/codeql-config.yml
# Scoped CodeQL config for RunCoach — security-extended queries only.
# Quality/style rules are handled by SonarAnalyzer.CSharp + eslint-plugin-sonarjs
# to avoid double-reporting.
name: "RunCoach CodeQL config"

queries:
  - uses: security-extended

paths:
  - "backend/src"
  - "frontend/src"

paths-ignore:
  # Test files
  - "backend/tests"
  - "backend/tests/eval-cache"
  - "frontend/src/**/*.test.ts"
  - "frontend/src/**/*.test.tsx"
  # Generated / build artifacts
  - "**/obj"
  - "**/bin"
  - "**/node_modules"
  - "**/*.generated.cs"
  - "**/*.Designer.cs"
  - "**/wwwroot/lib"

# ── Query-level filters ──
# cs/hardcoded-credentials was REMOVED from CodeQL in CLI 2.21.4 (May 2025).
# GitHub now recommends Secret Scanning for credential detection.
# The exclusion below is kept as defense-in-depth in case an older CLI is pinned.
query-filters:
  - exclude:
      id: cs/hardcoded-credentials
  - exclude:
      id: cs/hardcoded-connection-string-credentials
```

**Why `security-extended` and not `security-and-quality`.** The `security-extended` suite adds ~50 additional security queries beyond the default `code-scanning` suite (covering CWEs like unsafe deserialization, SSRF, and path traversal) without including the hundreds of code-quality rules that overlap with your existing SonarAnalyzer.CSharp and eslint-plugin-sonarjs configurations. This eliminates duplicate alerts and keeps the Security tab focused exclusively on vulnerabilities.

**On `cs/hardcoded-credentials` suppression.** GitHub deprecated and removed hardcoded-secrets detection from CodeQL as of **CLI 2.21.4 (May 30, 2025)**, recommending Secret Scanning instead. Since the current bundled CLI is 2.25.2, **you will not see these alerts at all**. The `query-filters` block above is defense-in-depth that has no runtime cost. If you need per-file suppression for any future query, you have two options: inline comments (`// codeql[cs/some-query-id]` on the line above the flagged code), or the `advanced-security/filter-sarif@v1` action inserted between `analyze` (with `upload: failure-only`) and a separate `upload-sarif` step — the filter-sarif approach supports glob patterns like `-backend/src/Constants/**:cs/hardcoded-credentials`.

---

## 6. Branch protection without blocking the first run

The **modern approach uses repository rulesets**, which avoid the chicken-and-egg problem entirely. Traditional required status checks demand that a check name (e.g., `CodeQL / Analyze (csharp)`) has run successfully within the past 7 days before it can be selected. Rulesets offer a dedicated "Require code scanning results" rule that is tool-aware.

**Step-by-step with rulesets (recommended):**

1. Navigate to **Settings → Rules → Rulesets → New branch ruleset**.
2. Set the target branch pattern to `main`.
3. Under **Branch protections**, enable **"Require code scanning results"**.
4. Click **Add tool**, select **CodeQL**, then configure alert thresholds — for example, set Security alerts to "High or higher" and Alerts to "Errors."
5. Set enforcement status to **Evaluate** initially (logs violations without blocking merges), then switch to **Active** once the first scan on `main` completes.

This approach blocks PRs that introduce new CodeQL findings above the threshold without requiring the status check to pre-exist. It also handles the baseline problem because rulesets are tool-aware, not check-name-aware.

**Fallback with traditional status checks:** Merge the `codeql.yml` workflow directly to `main` first (the `on: push` trigger fires immediately). Once both `CodeQL / Analyze (csharp)` and `CodeQL / Analyze (javascript-typescript)` appear as green checks, add them as required status checks under **Settings → Branches → Branch protection rules → Require status checks to pass before merging**.

---

## 7. Storage is free for public repos with generous limits

Code scanning on public repositories is **completely free** — no per-user, per-committer, or per-alert charges. GitHub Actions minutes are also unlimited for public repos. Alerts persist indefinitely (no documented retention/expiration period); closed/dismissed alerts remain accessible for audit purposes. If no push or PR activity occurs for **6 months**, the weekly cron schedule is automatically disabled, but existing alerts are preserved.

**SARIF upload limits to be aware of:**

- **File size**: 10 MB maximum (gzip-compressed)
- **Results per upload**: 5,000 (excess results are silently dropped)
- **Upload API rate limit**: 500 requests/hour per repository (via the `code_scanning_upload` rate-limit bucket)
- **Core API rate limit**: 1,000 requests/hour per `GITHUB_TOKEN` (15,000 for Enterprise Cloud)

For a monorepo with two languages, each push generates exactly 2 SARIF uploads — well within all limits. The weekly cron adds 2 more. Even high-frequency PR branches will not approach the 500/hour ceiling.

---

## 8. Local debugging with the CodeQL CLI

The CodeQL CLI lets you create databases, run queries, and view results entirely offline. Download the CLI bundle from `github.com/github/codeql-cli-binaries/releases` (includes pre-compiled query packs). The full local workflow:

```bash
# 1. Create a database for C# with traced build
codeql database create ./codeql-db-csharp \
  --language=csharp \
  --source-root=. \
  --command="dotnet build backend/RunCoach.slnx"

# 2. Create a database for JS/TS (no build needed)
codeql database create ./codeql-db-js \
  --language=javascript-typescript \
  --source-root=frontend/src

# 3. Run security-extended queries, output as SARIF
codeql database analyze ./codeql-db-csharp \
  --format=sarif-latest \
  --output=csharp-results.sarif \
  --threads=0 \
  codeql/csharp-queries:csharp-security-extended.qls

codeql database analyze ./codeql-db-js \
  --format=sarif-latest \
  --output=js-results.sarif \
  --threads=0 \
  codeql/javascript-queries:javascript-security-extended.qls

# 4. View results locally — no upload to GitHub
# Open .sarif files in VS Code with the "SARIF Viewer" extension
# Or use the CodeQL for VS Code extension (GitHub.vscode-codeql)
```

The **CodeQL for VS Code extension** (`GitHub.vscode-codeql`) provides rich local debugging: load databases, run individual queries, use Quick Evaluation to test predicates, visualize data-flow paths, and compare results across runs. Use `CodeQL: Quick Query` from the command palette to iterate on custom queries without saving `.ql` files. Results stay entirely local unless you explicitly run `codeql github upload-results`.

---

## 9. Rollback procedure

If the CodeQL workflow causes issues (false positives flooding the Security tab, build failures, or excessive Actions minute consumption), follow this sequence:

1. **Disable the workflow immediately**: Push a commit to `main` that either deletes `.github/workflows/codeql.yml` or adds `if: false` to the `analyze` job.
2. **Remove the ruleset enforcement**: In Settings → Rules → Rulesets, switch the code scanning ruleset enforcement to **Disabled**.
3. **Close stale alerts**: Use the code scanning REST API to bulk-dismiss open alerts: `gh api repos/{owner}/{repo}/code-scanning/alerts --paginate -q '.[].number' | xargs -I{} gh api -X PATCH repos/{owner}/{repo}/code-scanning/alerts/{} -f state=dismissed -f dismissed_reason=won\'t\ fix`.
4. **Delete the stale analysis configuration**: If you renamed or removed the workflow, stale alerts may linger. Navigate to **Settings → Code security → Code scanning → Tool status** and delete the old CodeQL configuration entry, or use the API: `DELETE /repos/{owner}/{repo}/code-scanning/analyses/{analysis_id}`.
5. **Re-enable incrementally**: When ready to retry, start with a single language (e.g., only `javascript-typescript` with `build-mode: none`) to validate before adding the C# leg back.

---

## Conclusion

The key operational decisions for this setup are using **CodeQL Action v4** (not v3, which is deprecated), choosing **`build-mode: manual`** for C# to match your existing CI's traced build exactly, and scoping analysis via **`security-extended` queries with explicit `paths` limits** to avoid overlap with SonarAnalyzer and eslint-plugin-sonarjs. The `cs/hardcoded-credentials` query that originally motivated per-file exclusion is already removed from CodeQL 2.21.4+ — the `query-filters` block is pure defense-in-depth. For branch protection, **rulesets with "Require code scanning results"** sidestep the traditional status-check bootstrapping problem entirely. Total cost for a public repo: zero dollars, unlimited Actions minutes, and two SARIF uploads per trigger event.