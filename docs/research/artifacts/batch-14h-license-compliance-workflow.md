# License-compliance CI for a .NET 10 + React 19 monorepo in 2026

**Use `actions/dependency-review-action` v4.9.0 as the PR gate and `anchore/sbom-action` v0.22.0 for weekly SBOM generation.** These two tools, both free for public repos with zero SaaS dependencies, cover the full license-compliance lifecycle when combined. The critical caveat: .NET projects using Central Package Management (`Directory.Packages.props`) require enabling GitHub's Automatic Dependency Submission — launched for NuGet in July 2025 — because static analysis alone reports all CPM package versions as `>= 0` and misses transitive dependencies entirely. npm's `package-lock.json` v3 works flawlessly with no special configuration. Below are the complete findings, ready-to-commit workflows, and license deny lists for all three target repo licenses.

---

## 1. SBOM and license tooling compared for 2026

Four tools were evaluated for a public .NET 10 + React 19 monorepo. They serve fundamentally different purposes, and only one — **`anchore/sbom-action`** — is purpose-built for CI SBOM generation.

| Capability | `actions/dependency-review-action` | `anchore/sbom-action` (Syft) | `fossas/fossa-action` | `trufflesecurity/trufflehog` |
|---|---|---|---|---|
| **Primary function** | PR license + vulnerability gate | SBOM generation | SCA + license compliance (SaaS) | Secret/credential scanning |
| **Generates SBOM files** | No | **Yes** | Via cloud platform only | No |
| **SPDX 2.3 support** | No | **Yes** (JSON + tag-value) | Via cloud only | No |
| **CycloneDX 1.4–1.6** | No | **Yes** (JSON + XML) | Via cloud only | No |
| **Free for public repos** | **Unlimited** | **Unlimited** (Apache-2.0 OSS) | 5 projects, 25 devs | **Unlimited** (AGPL-3.0 OSS) |
| **Requires API key / SaaS** | No | No | Yes | No |
| **Dependency Graph upload** | Reads only | **Yes** (`dependency-snapshot: true`) | No | No |
| **Scheduled job support** | No (PR events only) | **Yes** | Yes | Yes |
| **.NET / NuGet quality** | Good (via dependency graph) | Good (catalogers for packages.lock.json, .deps.json) | Good | N/A |
| **npm quality** | Excellent | **Excellent** | Good | N/A |
| **Latest version** | v4.9.0 (Mar 2025) | v0.22.0 (Jan 2026) | v1.8.0 (Feb 2025) | v3.x (ongoing) |

**Recommendation: Use `anchore/sbom-action`** for SBOM generation. It is the only tool that generates standard-format SBOM documents locally in CI, uploads to GitHub's Dependency Submission API, runs on scheduled triggers, and has zero cost or SaaS dependency. Pair it with `dependency-review-action` as the PR-time license/vulnerability gate. TruffleHog addresses an orthogonal concern (secrets) and should be added separately. FOSSA offers deeper license analysis but its free tier caps at 5 projects and requires cloud connectivity — unnecessary for most public repos.

## 2. .NET transitive dependency detection has a critical gap

**GitHub's static dependency graph does not correctly parse `Directory.Packages.props`.** When a .NET repo uses Central Package Management (CPM), `PackageReference` elements in `.csproj` files lack version attributes. The static analyzer sees them but reports all versions as `>= 0`, making vulnerability and license checks unreliable. Furthermore, **`packages.lock.json` is not a supported manifest** for the dependency graph — the supported .NET manifests are `.sln`, `.csproj`, `.vbproj`, `.vcxproj`, `.fsproj`, and `packages.config`.

The fix arrived in **July 2025**: GitHub launched **Automatic Dependency Submission for NuGet**, which runs `dotnet restore` internally and reads `project.assets.json` via Microsoft's `component-detection` library. This correctly resolves CPM versions and captures both direct and transitive dependencies with proper `runtime`/`development` scope labels.

To enable it, navigate to **Settings → Code security → Dependency graph → Automatic dependency submission** and enable for NuGet. Alternatively, add the `advanced-security/component-detection-dependency-submission-action` to your workflow. This is a **mandatory prerequisite** for the `dependency-review-action` to work reliably with your .NET 10 project. The SBOM workflow below includes a `dotnet restore` step to ensure `project.assets.json` exists for Syft's catalogers.

**Known limitation**: Automatic Dependency Submission currently fails to authenticate to private NuGet registries (including GitHub Packages). If your project uses private feeds, you will need a custom workflow step with explicit `dotnet restore --configfile nuget.config` and manual submission to the dependency API. For public NuGet.org dependencies, the feature works without configuration.

## 3. npm package-lock.json v3 works without issues

**`package-lock.json` v3 has been fully supported since March 2023.** GitHub's dependency graph parser handles all three lockfile versions: v1 (npm 5–6), v2 (npm 7–8), and v3 (npm 9+). The v3 format drops the legacy `dependencies` property and uses only the `packages` property — GitHub's parser reads both correctly.

The `dependency-review-action` correctly distinguishes **dev vs production dependencies** for npm. The `fail-on-scopes` parameter defaults to `runtime` only, meaning dev-dependency issues do not fail the build unless you explicitly set `fail-on-scopes: runtime, development`. The dependency graph extracts the `dev` boolean from `package-lock.json` v3 entries and maps them to the `development` scope.

One edge-case note: if you use the `advanced-security/component-detection-dependency-submission-action` (for .NET), its npm lockfile v3 detector is marked **experimental** and requires explicit opt-in via `detectorArgs: NpmLockfile3=EnableIfDefaultOff`. This only matters if you use that action — GitHub's built-in static parsing handles v3 natively and is what `dependency-review-action` relies upon.

## 4. License deny lists for three repo license scenarios

License compatibility flows in one direction: permissive code can enter copyleft projects, but copyleft code cannot enter permissive projects. The lists below use SPDX identifiers with the current `-only` / `-or-later` suffixes (bare `GPL-2.0` / `GPL-3.0` are deprecated since SPDX 3.0).

**If repo license = MIT** (most restrictive deny list — copyleft of any kind forces relicensing):

```
GPL-2.0-only, GPL-2.0-or-later, GPL-3.0-only, GPL-3.0-or-later,
AGPL-3.0-only, AGPL-3.0-or-later,
LGPL-2.0-only, LGPL-2.0-or-later, LGPL-2.1-only, LGPL-2.1-or-later,
LGPL-3.0-only, LGPL-3.0-or-later,
EUPL-1.1, EUPL-1.2, CDDL-1.0, CDDL-1.1, EPL-1.0, EPL-2.0, OSL-3.0,
SSPL-1.0, BUSL-1.1,
CC-BY-SA-4.0, CC-BY-NC-4.0, CC-BY-NC-SA-4.0, CC-BY-ND-4.0, CC-BY-NC-ND-4.0
```

**If repo license = Apache-2.0** (similar to MIT but adds BSD-4-Clause and CPL-1.0):

```
GPL-2.0-only, GPL-2.0-or-later, GPL-3.0-only, GPL-3.0-or-later,
AGPL-3.0-only, AGPL-3.0-or-later,
LGPL-2.0-only, LGPL-2.0-or-later, LGPL-2.1-only, LGPL-2.1-or-later,
LGPL-3.0-only, LGPL-3.0-or-later,
EUPL-1.1, EUPL-1.2, CDDL-1.0, CDDL-1.1, EPL-1.0, EPL-2.0, OSL-3.0, CPL-1.0,
BSD-4-Clause, SSPL-1.0, BUSL-1.1,
CC-BY-SA-4.0, CC-BY-NC-4.0, CC-BY-NC-SA-4.0, CC-BY-ND-4.0, CC-BY-NC-ND-4.0
```

**If repo license = AGPL-3.0** (shortest deny list — AGPL absorbs permissive code and is compatible with GPL-3.0):

```
GPL-2.0-only, LGPL-2.0-only,
CDDL-1.0, CDDL-1.1, EPL-1.0, EPL-2.0, CPL-1.0, OSL-3.0, EUPL-1.1,
BSD-4-Clause, SSPL-1.0, BUSL-1.1,
CC-BY-NC-4.0, CC-BY-NC-SA-4.0, CC-BY-ND-4.0, CC-BY-NC-ND-4.0
```

Key rationale for each category: **Strong copyleft** (GPL/AGPL) forces the combined work under its own license, making it incompatible with permissive outbound licenses. **GPL-2.0-only** is specifically incompatible with Apache-2.0 due to patent-termination clause conflicts (confirmed by both FSF and ASF). **LGPL** is denied for MIT/Apache projects because static linking or bundling (the norm in .NET and JavaScript) triggers full copyleft obligations. **SSPL-1.0 and BUSL-1.1** are not OSI-approved and should always be denied. **AGPL-3.0 as repo license** can accept MIT, BSD, Apache-2.0, ISC, GPL-3.0-only/or-later, LGPL-2.1+, MPL-2.0, EUPL-1.2, and CC-BY-SA-4.0 — hence the shorter deny list.

Note on **MPL-2.0**: It is file-level copyleft with an explicit GPL-3.0 compatibility clause (Section 3.3). It is excluded from the deny lists as a pragmatic choice, since modifications to MPL files stay MPL while the rest of your code remains under your license. Conservative policies may choose to add it.

## 5. Handling intentional license exceptions

The `dependency-review-action` provides the **`allow-dependencies-licenses`** parameter, which exempts specific packages from license checks entirely using PURL (Package URL) format. Packages listed here bypass all license validation, even if their license is undetectable.

**Inline syntax** (comma-separated in workflow YAML):
```yaml
allow-dependencies-licenses: >-
  pkg:nuget/Newtonsoft.Json,
  pkg:npm/@dual-licensed/package,
  pkg:npm/specific-exception
```

**External config file syntax** (in `.github/dependency-review-config.yml`):
```yaml
allow-dependencies-licenses:
  - 'pkg:nuget/Newtonsoft.Json'
  - 'pkg:npm/@dual-licensed/package'
  - 'pkg:npm/specific-exception'
```

PURL format by ecosystem: **NuGet** uses `pkg:nuget/PackageName`, **npm unscoped** uses `pkg:npm/package-name`, and **npm scoped** uses `pkg:npm/@scope/package-name`. Omitting the version acts as a wildcard matching all versions; appending `@1.0.0` pins the exception to a specific version. As of v4.9.0, **PURL comparisons are case-insensitive** and handle URL-encoded namespaces correctly.

Best practice for managing exceptions: maintain an external config file (`config-file` parameter) in a central org-level repository so exceptions are tracked via pull requests and auditable. Inline parameters override config file settings, allowing per-repo customization atop org-wide defaults. Document each exception with a comment explaining the justification (dual licensing, commercial license held, etc.).

## 6. Workflow file: `.github/workflows/license-review.yml`

```yaml
# .github/workflows/license-review.yml
# PR gate: license compliance + vulnerability check for .NET 10 + React 19 monorepo
# Requires: GitHub Automatic Dependency Submission enabled for NuGet (Settings > Code security)

name: License & Vulnerability Review

on:
  pull_request:
    types: [opened, synchronize, reopened]
  merge_group:

permissions:
  contents: read
  pull-requests: write  # Required for PR comment

jobs:
  dependency-review:
    name: Dependency review
    runs-on: ubuntu-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5 # v4.3.1

      - name: Dependency review
        uses: actions/dependency-review-action@2031cfc080254a8a887f58cffee85186f0e49e48 # v4.9.0
        with:
          # --- Vulnerability policy ---
          fail-on-severity: high           # Fail on CRITICAL and HIGH CVEs
          fail-on-scopes: runtime          # Ignore dev-dependency vulnerabilities
          show-patched-versions: true      # Show available fix versions

          # --- License policy ---
          # NOTE: deny-licenses is deprecated in v4; migrate to allow-licenses
          # before v5 release. Using deny-licenses here for clarity with the
          # deny list approach. Choose ONE of the three blocks below based on
          # your repo's outbound license, and delete the others.

          ## >>> If repo license = Apache-2.0 (DEFAULT — uncomment this block):
          deny-licenses: >-
            GPL-2.0-only, GPL-2.0-or-later, GPL-3.0-only, GPL-3.0-or-later,
            AGPL-3.0-only, AGPL-3.0-or-later,
            LGPL-2.0-only, LGPL-2.0-or-later, LGPL-2.1-only, LGPL-2.1-or-later,
            LGPL-3.0-only, LGPL-3.0-or-later,
            EUPL-1.1, EUPL-1.2, CDDL-1.0, CDDL-1.1, EPL-1.0, EPL-2.0,
            OSL-3.0, CPL-1.0, BSD-4-Clause,
            SSPL-1.0, BUSL-1.1,
            CC-BY-SA-4.0, CC-BY-NC-4.0, CC-BY-NC-SA-4.0, CC-BY-ND-4.0, CC-BY-NC-ND-4.0

          ## >>> If repo license = MIT, use the same list but remove BSD-4-Clause and CPL-1.0.
          ## >>> If repo license = AGPL-3.0, use the shorter list from the documentation.

          # --- Per-package license exceptions (dual-licensed deps, etc.) ---
          # Add approved exceptions here in PURL format:
          # allow-dependencies-licenses: >-
          #   pkg:nuget/Example.DualLicensed.Pkg,
          #   pkg:npm/@example/dual-licensed

          # --- PR interaction ---
          comment-summary-in-pr: always    # Post/update summary comment on every run

          # --- Snapshot reliability for .NET auto-submission ---
          retry-on-snapshot-warnings: true
          retry-on-snapshot-warnings-timeout: 180

          # --- External config (optional, for org-wide policy) ---
          # config-file: 'my-org/.github/dependency-review-config.yml@main'
          # external-repo-token: ${{ secrets.CONFIG_REPO_TOKEN }}
```

### Alternative: allow-licenses approach (recommended for v5 migration)

Replace the `deny-licenses` block above with the following `allow-licenses` block to adopt the recommended allowlist model. This is the fail-closed approach — any license not explicitly allowed will cause a failure:

```yaml
          # Allow-list approach (recommended, survives v5 migration)
          allow-licenses: >-
            MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, 0BSD,
            Unlicense, CC0-1.0, CC-BY-4.0, Zlib, BSL-1.0,
            Python-2.0, PSF-2.0, Artistic-2.0, MPL-2.0, WTFPL
```

## 7. Workflow file: `.github/workflows/license-sbom.yml`

```yaml
# .github/workflows/license-sbom.yml
# Weekly SBOM generation for .NET 10 + React 19 monorepo
# Generates SPDX 2.3 JSON SBOM, uploads as workflow artifact,
# and submits to GitHub Dependency Graph for Dependabot alerting.

name: Weekly SBOM Generation

on:
  schedule:
    - cron: '0 6 * * 1'  # Monday 06:00 UTC
  workflow_dispatch:       # Allow manual trigger

permissions:
  contents: write          # For artifact upload and dependency snapshot

jobs:
  sbom:
    name: Generate SBOM
    runs-on: ubuntu-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5 # v4.3.1

      # --- .NET restore (required for accurate NuGet dependency detection) ---
      - name: Setup .NET 10
        uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4.3.1
        with:
          dotnet-version: '10.0.x'

      - name: Restore .NET packages
        run: dotnet restore --locked-mode
        # Generates project.assets.json which Syft reads for transitive deps.
        # --locked-mode ensures packages.lock.json is used as-is.

      # --- npm install (required for accurate npm dependency detection) ---
      - name: Setup Node.js
        uses: actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020 # v4.4.0
        with:
          node-version: '22'
          cache: 'npm'

      - name: Install npm dependencies
        run: npm ci
        # Ensures node_modules and resolved package-lock.json v3 are present.

      # --- SBOM generation ---
      - name: Generate SBOM (SPDX 2.3 JSON)
        uses: anchore/sbom-action@62ad528 # v0.22.0 — VERIFY FULL SHA before committing
        # To get the full 40-char SHA, run:
        #   git ls-remote https://github.com/anchore/sbom-action.git v0.22.0
        # Then replace the abbreviated SHA above with the full hash.
        id: sbom
        with:
          path: .
          format: spdx-json
          artifact-name: sbom-spdx.json
          dependency-snapshot: true  # Upload to GitHub Dependency Graph
          upload-artifact: true
          upload-artifact-retention: 90

      # --- Optional: generate CycloneDX as a second format ---
      - name: Generate SBOM (CycloneDX 1.5 JSON)
        uses: anchore/sbom-action@62ad528 # v0.22.0
        with:
          path: .
          format: cyclonedx-json
          artifact-name: sbom-cyclonedx.json
          dependency-snapshot: false  # Already submitted above
          upload-artifact: true
          upload-artifact-retention: 90
```

**Important SHA-pinning note**: The `anchore/sbom-action` SHA `62ad528` shown above is abbreviated. Before committing, resolve the full 40-character SHA by running:
```bash
git ls-remote --tags https://github.com/anchore/sbom-action.git 'v0.22.0'
```
Then replace `62ad528` with the complete hash. Similarly, verify all other action SHAs against the releases pages for `actions/checkout`, `actions/setup-dotnet`, and `actions/setup-node`. The SHAs for `actions/checkout` (v4.3.1 → `34e114876b0b11c390a56381ad16ebd13914f8d5`) and `actions/dependency-review-action` (v4.9.0 → `2031cfc080254a8a887f58cffee85186f0e49e48`) were confirmed directly from their GitHub releases pages.

## 8. What to do before these workflows work

The workflows above assume three prerequisites that must be configured before the first run:

- **Enable Automatic Dependency Submission for NuGet.** Go to your repository's Settings → Code security → Dependency graph → Automatic dependency submission, and enable the NuGet ecosystem. Without this, `dependency-review-action` will see .NET dependencies with incorrect versions or miss transitive dependencies entirely. This feature uses `component-detection` with `dotnet restore` internally and supports .NET 8.x, 9.x, and 10.x.

- **Commit both lockfiles.** Ensure `packages.lock.json` (generated by setting `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in `Directory.Build.props`) and `package-lock.json` are committed. The dependency graph identifies transitive dependencies only from lockfiles or via the dependency submission API.

- **Verify and update SHAs.** Run `git ls-remote` for each action to confirm the full 40-character commit SHAs match the version tags shown in the comments. Dependabot can automate future SHA updates if configured with `package-ecosystem: github-actions` in your `.github/dependabot.yml`.

The **REUSE specification** (reuse.software, v3.3) from FSFE is complementary to this setup. While `dependency-review-action` checks *dependency* license compatibility, REUSE ensures your *own* project's files have proper SPDX headers and license texts in a `LICENSES/` directory. Adding `fsfe/reuse-action` as a third workflow provides complete bidirectional license hygiene — though it is not strictly required for the dependency compliance goal described here.

## Conclusion

The two-workflow architecture — `dependency-review-action` as a fail-fast PR gate plus `anchore/sbom-action` on a weekly schedule — covers license compliance, vulnerability detection, and SBOM generation with **zero cost and no SaaS dependency** for a public repo. The most important operational insight is that **`.NET Central Package Management requires Automatic Dependency Submission`** to function correctly with GitHub's dependency graph. Without it, the entire .NET side of the license check is unreliable. npm, by contrast, works out of the box with `package-lock.json` v3. The `allow-dependencies-licenses` parameter with PURL notation (`pkg:nuget/...`, `pkg:npm/...`) provides surgical exception handling for dual-licensed or pre-approved packages, keeping the license gate strict without creating false-positive friction. Plan to migrate from `deny-licenses` to `allow-licenses` before `dependency-review-action` v5 ships, as `deny-licenses` was officially deprecated in v4.7.2 (August 2024) and will be removed in the next major release.