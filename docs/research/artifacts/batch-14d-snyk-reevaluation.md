# Snyk free tier vs. Dependabot + Trivy + CodeQL in 2026

**For a public GitHub repository, Snyk's free tier imposes effectively zero limits** — all scan types (SCA, SAST, Container, IaC) are unlimited for open-source projects, making it a genuine zero-cost addition to an existing Dependabot + Trivy + CodeQL stack. The strongest case for adding Snyk is its unique ability to patch transitive npm vulnerabilities where no upgrade path exists and its proprietary vulnerability database that discovers CVEs roughly 47 days faster than public sources. However, it does not replace Dependabot's version-update PRs, Trivy's free SBOM generation, or CodeQL's superior taint analysis depth. The optimal strategy is to layer Snyk on top of the existing stack rather than substitute it.

---

## 1. Snyk free tier limits are irrelevant for public repos

The official pricing page at snyk.io/plans and docs.snyk.io both confirm: **"We do not count contributions to public (open source) repos"** and **"you may run unlimited tests for public repositories."** This means the .NET 10 + React 19 + TypeScript monorepo on a public GitHub repo gets unlimited scanning on the free tier across all products.

For reference, the private-repo limits on the free plan are:

| Product | Private Repo Limit | Public Repo Limit |
|---|---|---|
| Snyk Open Source (SCA) | **200 tests/month** (pricing page) or 400 (docs — discrepancy) | **Unlimited** |
| Snyk Code (SAST) | 100 tests/month | **Unlimited** |
| Snyk Container | 100 tests/month | **Unlimited** |
| Snyk IaC | 300 tests/month | **Unlimited** |

Additional free-tier parameters: up to **5 Organizations** per tenant, up to **10,000 projects** per Organization, unlimited contributing developers (no seat cap), and **weekly** recurring scans (daily requires a paid plan). Fix PRs have no count limit — only upgrade PRs are capped at 5 simultaneously open per project (configurable 1–10).

**Language support confirmed**: Snyk Code supports **C# (including C# 14 / .NET 10** as of early 2026) and **TypeScript** as first-class languages. Snyk Open Source supports NuGet (.csproj, packages.config, packages.lock.json) and npm/Yarn/pnpm.

**Notable exclusions on free tier**: no Jira integration, no license compliance policies (basic detection only), no reports dashboard, no SSO/RBAC, no private package registries, no daily scheduled scans, no service accounts, and no Snyk Broker. SBOM generation via API requires a paid plan, though the CLI `snyk sbom` command may work with restrictions.

### Has Snyk reduced the free tier recently?

There is a discrepancy between the pricing page (**200** SCA tests for private repos) and the docs (**400**), suggesting a recent reduction. A third-party monitor (Oligo Security) notes "Snyk has reduced the scope of its free tier several times." A **credit-based licensing model** launched January 1, 2026 for new paid licenses, but the free tier currently remains test-count-based. None of these changes affect public repositories.

---

## 2. Fix-PR behavior differs in important ways

### What Snyk automates that Dependabot cannot

Snyk's distinctive capability is **patch PRs via `@snyk/protect`** — when no upgrade path exists for a vulnerable transitive dependency, Snyk can apply source-level patches to the installed package. This is **JavaScript/Node.js only** and requires Snyk's security team to have created a patch for that specific vulnerability (limited to high-impact CVEs in popular packages). Dependabot has no patching mechanism whatsoever.

Other Snyk advantages over Dependabot include broader SCM support (GitLab, Bitbucket, Azure DevOps), a proprietary vulnerability database, **priority scoring** combining EPSS, exploit maturity, and fix availability (Risk Score 0–1000), and a 21-day waiting period before recommending new package versions to avoid unstable releases.

Conversely, **Dependabot does things Snyk cannot**: version-update PRs that keep all dependencies current (not just vulnerable ones), grouped multi-ecosystem PRs (GA as of July 2025), and it is completely free with no limits on any repo type.

### Transitive dependency fix PRs

Snyk's official documentation states plainly: **"You cannot automatically fix transitive dependencies or open a Fix PR."** However, Snyk *will* upgrade the direct dependency to a version that pulls in a safe transitive dependency. If no such upgrade path exists — if the direct dependency has no version bringing in a vulnerability-free transitive — Snyk shows "No supported fix" and cannot generate a PR.

Dependabot's transitive handling is ecosystem-dependent. For **npm**, Dependabot can unlock and bump parent + child dependencies together in a single PR (since September 2022). For **NuGet**, the rewritten C# updater (2025) supports transitive deps via Central Package Management, but has **known bugs** — it sometimes pins transitive dependencies directly instead of upgrading the parent (GitHub issue #13804, December 2025).

### @snyk/protect patch status

The old `snyk protect` CLI command was **removed on March 31, 2022**. Its replacement, the **`@snyk/protect` npm package, is still actively maintained** and is integrated into Snyk's Fix PR service. When Snyk determines a patch is available, the Fix PR adds a patch entry to the `.snyk` policy file, adds `@snyk/protect` as a dependency, and adds a `prepare` script. On `npm install`, patches are downloaded from Snyk's database and applied to `node_modules`. Patches must be re-applied after every install.

**Critical limitation**: patching is **Node.js/npm only**. There is no patch mechanism for NuGet/.NET transitive vulnerabilities. For the .NET side of the monorepo, the only remediation path is upgrading the direct dependency.

---

## 3. SCA overlap matrix: Snyk OSS vs. Dependabot + Trivy

| Dimension | Snyk OSS | Dependabot | Trivy | Winner |
|---|---|---|---|---|
| **Direct dep CVE detection** | Proprietary DB + NVD; reachability analysis; Risk Score | GitHub Advisory DB; no prioritization | Aqua trivy-db; NVD + distro DBs | **Snyk** (prioritization) |
| **Transitive dep CVE detection** | Full tree; shows dependency path | npm: full; NuGet: buggy; others: limited | Requires lockfiles (packages.lock.json for .NET) | **Snyk** (consistency) |
| **Automated upgrade PRs** | Security-only; min safe version | Security + version updates; grouped PRs | None (scanner only) | **Dependabot** (version updates) |
| **Patch PRs (no upgrade path)** | ✅ JS/Node.js only via @snyk/protect | ❌ None | ❌ None | **Snyk** (unique) |
| **License scanning** | Basic detection free; full policies paid | ❌ None | ✅ SPDX classification; needs node_modules for npm | **Snyk** (ease) / **Trivy** (free) |
| **.NET NuGet quality** | Excellent: .csproj, packages.config, .sln | Good: native C# updater; CPM support | Good but requires packages.lock.json or .deps.json; does NOT parse .csproj | **Snyk** |
| **npm quality** | Excellent: full tree from lockfile | Excellent: transitive support, grouped updates | Very good: lockfile parsing | Tie (all strong) |
| **SBOM generation** | CLI available; full API export paid-only | ❌ None (GitHub Dependency Graph separate) | ✅ Excellent: CycloneDX 1.6, SPDX 2.3, free | **Trivy** |

**Key takeaway**: Dependabot + Trivy together cover direct/transitive CVE detection, automated PRs, license scanning, and SBOM generation. Adding Snyk provides **patch PRs for npm transitive deps**, a faster proprietary vulnerability database, and reachability-based prioritization. For NuGet, note that Trivy requires generating `packages.lock.json` (set `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in your .csproj) — without this, Trivy cannot scan .NET transitive dependencies.

---

## 4. SAST overlap: Snyk Code vs. CodeQL

| Dimension | Snyk Code | CodeQL | Assessment |
|---|---|---|---|
| **C# rule depth** | OWASP Top 10; caught SQLi + XSS but missed SSRF, IDOR in DryRun benchmark (2/6) | Interprocedural analysis; caught SQLi only in same benchmark (1/6); EF Core + Dapper ORM models | Both weak on C# auth/logic flaws; **Snyk Code edges on breadth**, CodeQL on depth |
| **TypeScript rule depth** | First-class support; 19+ languages; no compilation needed | TS 5.9 supported; built-in models for React, Express, 40+ npm libraries; deep data flow | **CodeQL wins** — explicit React framework support and more library models |
| **Taint analysis** | ML-based (DeepCode engine, 25M+ data flow cases); inter-file but moderate depth | Full interprocedural, inter-file via semantic database; custom sources/sinks/sanitizers via QL | **CodeQL wins** — architecturally superior; can trace arbitrarily complex paths |
| **False-positive rate** | ~8% FP (85% accuracy) per SAST Evaluation Study 2024 | ~5% FP (88% accuracy) per same study; errs toward precision | **CodeQL wins** — fewer false positives |
| **Cost on public repos** | Free (unlimited for public repos) | Free (GitHub Advanced Security free for public repos) | **Tie** — both zero-cost |
| **Scan speed** | Seconds; no build step needed | Minutes (10–30 min for large repos); requires database creation | **Snyk Code wins** — dramatically faster |

**Recommendation for a .NET + TypeScript monorepo**: Run both. Use **Snyk Code for fast PR-level feedback** (seconds, no compilation, AI fix suggestions) and **CodeQL for deep scheduled analysis** (nightly or on merges to main). CodeQL's QL query language also allows writing custom security rules specific to your codebase — Snyk Code's rules are a managed black box. For TypeScript/React specifically, CodeQL has explicit framework models that Snyk Code lacks. Since both are free on public repos, there is no cost reason to choose only one.

---

## 5. GitHub Actions workflow and SHA-pinnable versions

### Current action status

The `snyk/actions` repository released **v1.0.0 on October 3, 2025**. The full SHA-pinnable commit hash is:

```
snyk/actions@9adf32b1121593767fc3c057af55b55db032dc04  # v1.0.0
```

**`snyk/actions/dotnet` is officially deprecated.** It is listed under "Deprecated Actions" in the repository README. It still functions but is no longer maintained. The recommended replacement is `snyk/actions/setup`, which installs only the Snyk CLI and lets you bring your own .NET SDK via `actions/setup-dotnet`.

`snyk/actions/node` remains **fully supported**.

### Recommended monorepo workflow

```yaml
name: Snyk Security Scans
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

permissions:
  security-events: write  # Required for SARIF upload

jobs:
  snyk-dotnet:
    name: Snyk Open Source (.NET)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@4
        with:
          dotnet-version: '10.0.x'
      - uses: snyk/actions/setup@9adf32b1121593767fc3c057af55b55db032dc04 # v1.0.0
      - run: dotnet restore ./src/Backend/Backend.sln
      - name: Snyk test .NET
        continue-on-error: true
        run: snyk test --file=./src/Backend/Backend.sln --severity-threshold=high --sarif-file-output=snyk-dotnet.sarif
        env:
          SNYK_TOKEN: ${{ secrets.SNYK_TOKEN }}
      - uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: snyk-dotnet.sarif
          category: snyk-dotnet

  snyk-node:
    name: Snyk Open Source (Node.js)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Snyk test Node
        uses: snyk/actions/node@9adf32b1121593767fc3c057af55b55db032dc04 # v1.0.0
        continue-on-error: true
        env:
          SNYK_TOKEN: ${{ secrets.SNYK_TOKEN }}
        with:
          args: --file=./src/Web/package-lock.json --severity-threshold=high --sarif-file-output=snyk-node.sarif
      - uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: snyk-node.sarif
          category: snyk-node

  snyk-code:
    name: Snyk Code (SAST)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Snyk Code scan
        uses: snyk/actions/node@9adf32b1121593767fc3c057af55b55db032dc04 # v1.0.0
        continue-on-error: true
        env:
          SNYK_TOKEN: ${{ secrets.SNYK_TOKEN }}
        with:
          command: code test
          args: --severity-threshold=high --sarif-file-output=snyk-code.sarif
      - uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: snyk-code.sarif
          category: snyk-code
```

**Critical configuration notes**: `continue-on-error: true` is mandatory before the SARIF upload step — Snyk exits with a non-zero code when vulnerabilities are found, which would skip the upload. The `SNYK_TOKEN` is set as a repository secret under Settings → Secrets and variables → Actions. The `category` parameter in `upload-sarif` distinguishes multiple SARIF uploads in the GitHub Security tab. The `permissions: security-events: write` block is required for SARIF upload.

An alternative to `snyk/actions/setup` is direct binary installation with SHA verification:

```yaml
- run: |
    curl https://downloads.snyk.io/cli/stable/snyk-linux -o snyk-linux
    curl https://downloads.snyk.io/cli/stable/snyk-linux.sha256 -o snyk.sha256
    sha256sum -c snyk.sha256
    chmod +x snyk-linux
    sudo mv snyk-linux /usr/local/bin/snyk
```

---

## 6. Account friction and integration scoping

### Setup steps

1. Navigate to **app.snyk.io** and sign up (GitHub OAuth, Google, Bitbucket, or email)
2. Get your API token from **Settings → General → API Token**
3. Store it as a GitHub Actions secret named `SNYK_TOKEN`
4. Either connect the repo through the web UI *or* use CLI-only via GitHub Actions

### GitHub App scoping: single-repo access is supported

The **GitHub Cloud App** (recommended integration, replacing the legacy OAuth method) explicitly asks during installation whether to grant access to **all repositories or only selected repositories**. You can scope it to a single repo and modify this later from GitHub's Installed Apps settings. This addresses the "sees ALL repos" concern.

The **legacy OAuth integration** uses a personal access token with broad `repo (all)` and `admin:read:org` scopes, granting visibility across all accessible repos. Snyk recommends migrating to the GitHub Cloud App for least-privilege access.

The GitHub Cloud App requests these permissions (non-customizable): repository metadata (read), contents (read+write for fix PRs), pull requests (read+write), commit statuses (read+write), checks (read+write), repository hooks (read+write), and organization members (read).

### Privacy and data handling

For **Snyk Open Source**, only dependency metadata (package names, versions, licenses) is sent to Snyk servers — no source code. For **Snyk Code (SAST)**, source code is uploaded for analysis but **cached for only 24–48 hours and then deleted**. Snyk explicitly states: "Your code is removed and is not stored in the Snyk network or logs." Customer code is not used for AI model training. Snyk holds SOC 2 Type 2 and ISO 27001 certifications.

One historical concern: a researcher discovered a broken access control vulnerability in Snyk Code that could leak files from private repositories if an attacker knew the repo name and file path. **Snyk fixed this within 72 hours of the report.** For a public repo, this specific concern is less relevant since the code is already public.

### CLI-only usage without the GitHub App

**Yes, this is fully supported.** The Snyk CLI works independently of any SCM integration — you need only a `SNYK_TOKEN` environment variable. The docs state explicitly: "You do not need to connect to an SCM integration such as GitHub or GitLab for this to work."

What you **keep** with CLI-only: on-demand scanning (`snyk test`, `snyk code test`), SARIF output for GitHub Code Scanning, and optional dashboard uploads via `snyk monitor` or `--report`.

What you **lose** with CLI-only:
- Automatic fix/upgrade PRs from Snyk
- Automatic PR status checks from Snyk
- Recurring scheduled scans (daily/weekly) managed by Snyk
- Auto-detection of new manifest files

For a privacy-conscious setup that already uses Dependabot for automated PRs, **CLI-only is the ideal approach** — you get Snyk's vulnerability database and SAST analysis in CI without granting Snyk any repository access.

---

## 7. Snyk business and product changes in 2025–2026

**Snyk has not been acquired** as of April 2026. At least three private equity firms explored acquisition but could not agree on price (Snyk's high-water valuation was **$8.5B** in 2021). Snyk plans to IPO in 2026 if no satisfactory offer materializes. Revenue growth slowed to **12% year-over-year** in H1 2025, with approximately **$278M in 2024 revenue** and roughly $400M in remaining cash.

Key product and business developments:

- **Credit-based licensing** launched January 1, 2026 for new paid contracts; free tier unaffected
- **Ignite tier** introduced at $1,260/year per contributing developer (up to 50 devs), positioned between Team ($25/month/dev) and Enterprise
- **Team plan capped at 10 licenses** — larger organizations must move to Ignite or Enterprise
- **Snyk Code now supports C# 14 and .NET 10** (added in early 2026 release cycle)
- **Snyk Code PR Checks** reached General Availability
- **Acquisitions**: Invariant Labs (June 2025, agentic AI security) and Probely (November 2024, DAST/API security integrated as "Snyk API & Web")
- **"Evo by Snyk"** launched October 2025 — agentic security orchestration for AI-native applications
- **Docker Desktop Extension** end-of-support effective June 20, 2025
- New language support: Ruby 4.0, PHP 8.5, Yarn 4, pnpm, Gradle 9, Rust (Code analysis Early Access)
- OWASP Top 10 mappings updated from 2021 to 2025 revision

No core free-tier features have been paywalled or removed. The free tier still provides access to all four products (SCA, SAST, Container, IaC) with unlimited scanning for public repositories. Snyk continues to state: "Snyk remains free for Open Source projects, and always will be."

---

## Conclusion: a layered strategy beats any single tool

The existing Dependabot + Trivy + CodeQL stack already covers the critical security surface well. Adding Snyk's free tier fills three specific gaps: **npm transitive-dependency patching** (unique to Snyk), a **faster proprietary vulnerability database** that catches CVEs before they hit the NVD, and **quick-feedback SAST** via Snyk Code complementing CodeQL's deeper but slower analysis. The CLI-only integration path avoids all GitHub App permission concerns while still surfacing results in the GitHub Security tab via SARIF upload. For the .NET side, the deprecated `snyk/actions/dotnet` should be avoided in favor of `snyk/actions/setup` paired with `actions/setup-dotnet`. The fact that public repos face zero test limits makes Snyk a low-friction, zero-cost addition — but it should augment the existing stack, not replace it. Dependabot's version-update PRs and grouped updates, Trivy's free SBOM generation and license scanning, and CodeQL's deep taint analysis each provide capabilities that Snyk's free tier does not match.