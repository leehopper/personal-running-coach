# CodeRabbit integration plan for a .NET 10 + React 19 monorepo

**CodeRabbit remains free-forever at Pro tier for public GitHub repos in 2026, installs as a native GitHub App (not an Action), and requires zero CI changes.** The configuration below is ready to commit and addresses every constraint: protecting prompt IP from AI rewording, silencing noise on Dependabot bumps, avoiding Roslyn double-reporting, and keeping ADR docs free of line-level nitpicks. This plan was verified against docs.coderabbit.ai content fetched April 15, 2026, the Marketplace listing (193K+ installs), and real-world configs from Crossplane and other large OSS projects.

---

## (a) Justified `.coderabbit.yaml` — ready to commit

```yaml
# yaml-language-server: $schema=https://coderabbit.ai/integrations/schema.v2.json
# ─────────────────────────────────────────────────────────────────────
# CodeRabbit configuration for [repo-name]
# Schema v2 is still current as of 2026-04-15 (last schema update: 2026-04-13).
# Docs: https://docs.coderabbit.ai/getting-started/yaml-configuration
# ─────────────────────────────────────────────────────────────────────

# Review language — matches our docs and comments standard.
language: "en-US"

# No early-access features; we want stable behavior only.
early_access: false

reviews:
  # ── Tone & workflow ────────────────────────────────────────────────
  # "chill" focuses on bugs, security, and logic errors. "assertive"
  # adds style/naming/perf nits — start chill, re-evaluate after 4 weeks.
  profile: "chill"

  # Do NOT let CodeRabbit submit "Request Changes" reviews.
  # Humans own the merge gate; CodeRabbit is advisory only.
  request_changes_workflow: false

  # ── PR summary controls ───────────────────────────────────────────
  # Generate a high-level summary in each PR's walkthrough comment.
  high_level_summary: true

  # No poem — keeps walkthrough professional for external contributors.
  poem: false

  # Show review status messages so authors know CodeRabbit is working.
  review_status: true

  # Collapse walkthrough behind a <details> toggle to reduce scroll.
  collapse_walkthrough: true

  # Sequence diagrams are useful for backend module interactions.
  sequence_diagrams: true

  # ── Path filters ──────────────────────────────────────────────────
  # Exclude paths that should never receive review comments.
  # CodeRabbit already ignores node_modules, lockfiles (package-lock.json,
  # yarn.lock, pnpm-lock.yaml, *.lock), .dll, .exe, .min.js, images,
  # and many generated patterns by default. We add repo-specific exclusions.
  path_filters:
    - "!node_modules/**"                      # redundant but explicit
    - "!**/package-lock.json"                 # redundant but explicit
    - "!**/yarn.lock"                         # redundant but explicit
    - "!**/pnpm-lock.yaml"                    # redundant but explicit
    - "!backend/tests/eval-cache/**"          # committed eval fixture cache — never review
    - "!**/*.generated.cs"                    # EF Core scaffolded / source-gen output
    - "!**/*.g.cs"                            # .NET incremental source generators
    - "!**/*.Designer.cs"                     # WinForms / resource designers
    - "!frontend/src/**/*.generated.ts"       # any codegen TS files
    - "!**/dist/**"                           # build output if ever committed
    - "!**/*.min.js"                          # redundant but explicit
    - "!**/*.min.css"                         # redundant but explicit

  # ── Path instructions ─────────────────────────────────────────────
  # Scoped review guidance per module. CodeRabbit's LLM reads these
  # before reviewing files matching each glob pattern.
  path_instructions:
    # Backend module boundaries & deterministic/LLM split
    - path: "backend/Modules/**/*.cs"
      instructions: |
        This is a modular .NET 10 backend. Each Module/ folder is a vertical
        slice with its own DI registration, domain, and API surface.
        When reviewing:
        - Enforce module boundary isolation: a module must NOT reference
          another module's internal types directly. Cross-module calls go
          through contracts/interfaces in a shared Contracts project.
        - Flag any code that mixes deterministic business logic with LLM
          orchestration. Deterministic logic belongs in pure service classes;
          LLM calls belong in dedicated *Agent or *Orchestrator classes.
        - Verify that new public APIs have corresponding integration tests.
        - Check for correct use of C# 14 features (primary constructors,
          field keyword, extension types) consistent with the rest of the
          codebase.

    # Frontend — React 19 + RTK Query conventions
    - path: "frontend/src/modules/**/*.{ts,tsx}"
      instructions: |
        This is a React 19 + Vite + TypeScript strict frontend using RTK
        Query for data fetching. When reviewing:
        - Ensure components use React 19 patterns (use() hook for async,
          server component compatibility where applicable).
        - Data fetching must use RTK Query endpoints, NOT raw fetch/axios
          calls inside components.
        - Verify TypeScript strict mode compliance (no `any`, no `as`
          escape hatches without justification).
        - Check that new modules register their RTK Query API slices via
          the central store configuration.

    # PROTECTED: coaching prompt IP — do NOT suggest rewording
    - path: "backend/**/Prompts/*.yaml"
      instructions: |
        These YAML files contain proprietary coaching prompt intellectual
        property. Do NOT suggest prompt wording changes, rephrasing,
        restructuring of prompt text, or tone adjustments. Only flag:
        - Broken YAML syntax
        - Missing required YAML keys (if a schema is evident)
        - Obvious security issues (e.g., prompt injection vectors)
        All other commentary on these files should be suppressed.

    # PROTECTED: ADR files — append-only historical record
    - path: "docs/decisions/*.md"
      instructions: |
        Architecture Decision Records are append-only historical documents.
        Do NOT suggest rewording, restructuring, or stylistic changes.
        Do NOT post individual line-level review comments on these files.
        Only include them in the high-level PR summary walkthrough.
        Flag only: broken Markdown links or obviously incorrect dates.

    # PROTECTED: plan files — similar treatment to ADRs
    - path: "docs/plans/**"
      instructions: |
        Plan documents are living project artifacts authored by humans.
        Do NOT suggest rewording or restructuring. Do NOT post line-level
        review comments. Only include in the high-level PR summary.
        Flag only: broken Markdown links or factual inconsistencies
        with linked ADRs.

  # ── Auto-review controls ──────────────────────────────────────────
  auto_review:
    enabled: true
    drafts: false                            # skip draft PRs

    # Dependabot & Renovate: skip automatically — prevents noise on
    # version-bump PRs. Exact usernames, case-sensitive, [bot] suffix.
    ignore_usernames:
      - "dependabot[bot]"
      - "renovate[bot]"
      - "github-actions[bot]"

    # Skip WIP PRs by title keyword
    ignore_title_keywords:
      - "WIP"
      - "[skip review]"
      - "do not merge"

    # Re-review on each push, but pause after 5 commits to avoid
    # flooding long-lived PRs.
    auto_incremental_review: true
    auto_pause_after_reviewed_commits: 5

  # ── Tools configuration ───────────────────────────────────────────
  tools:
    # Read GitHub Checks annotations from our existing CI (Trivy, Codecov,
    # dotnet build warnings). CodeRabbit can suggest fixes for CI failures.
    github-checks:
      enabled: true
      timeout_ms: 120000                     # 2 min — our CI can be slow

    # DISABLE CodeRabbit's built-in ESLint — we already run
    # eslint-plugin-sonarjs in CI via Lefthook pre-commit and GitHub
    # Actions. Enabling this would double-report.
    eslint:
      enabled: false

    # CodeRabbit has no built-in Roslyn/C# analyzer, so there is no
    # setting to disable. It will not duplicate SonarAnalyzer.CSharp
    # findings directly. The LLM may independently flag overlapping
    # issues, but profile: "chill" minimizes this.

    # Keep secret scanning on — Betterleaks replaced Gitleaks in Mar 2026.
    gitleaks:
      enabled: true

    # Keep Semgrep enabled for security rules the LLM might miss.
    semgrep:
      enabled: true

    # YAML lint — useful for our Prompts/*.yaml files (syntax only).
    yamllint:
      enabled: true

    # Markdown lint for docs — light touch.
    markdownlint:
      enabled: true

    # Shell check for any scripts.
    shellcheck:
      enabled: true

    # Hadolint if we add Dockerfiles.
    hadolint:
      enabled: true

    # Disable tools we don't need.
    ruff:
      enabled: false                         # no Python in this repo
    biome:
      enabled: false                         # we use ESLint, not Biome
    golangci-lint:
      enabled: false                         # no Go in this repo

  # ── Finishing touches ─────────────────────────────────────────────
  finishing_touches:
    docstrings:
      enabled: false                         # we manage XML docs manually
    unit_tests:
      enabled: false                         # we write our own tests

# ── Chat settings ─────────────────────────────────────────────────
chat:
  auto_reply: true                           # respond to @coderabbitai mentions

# ── Knowledge base ────────────────────────────────────────────────
knowledge_base:
  opt_out: false                             # allow learning from our repo
  learnings:
    scope: "auto"                            # learn from dismissed comments
  code_guidelines:
    enabled: true
    filePatterns:
      - "**/CLAUDE.md"                       # if we add AI coding guidelines
      - "**/CONTRIBUTING.md"
```

---

## (b) Install steps in order

### Step 1 — Sign up via the GitHub Marketplace

Navigate to **https://github.com/marketplace/coderabbitai** and click **"Set up a plan."** Select the **Open Source (Free)** tier. CodeRabbit's Pro plan is **free forever for public repositories** with no seat limits, no credit card, and no application process. The Marketplace install triggers the GitHub App authorization flow.

Alternatively, go to **https://app.coderabbit.ai/login** and sign in with your GitHub account. Both paths install the same native GitHub App. The Marketplace route is preferred because it keeps the app visible in your org's "Installed GitHub Apps" list alongside Trivy and Codecov.

### Step 2 — Grant repository access

During app installation, GitHub asks which repos to authorize. Select **"Only select repositories"** and choose your monorepo. This limits CodeRabbit's access scope. The app requests read access to code, PRs, issues, and checks, plus write access to PR comments and commit statuses.

### Step 3 — Commit `.coderabbit.yaml`

Copy the YAML from section (a) above into the repository root. CodeRabbit reads the config from **the feature branch under review** — not just `main`. This means you can PR the config file itself and CodeRabbit will use it during that PR's review.

```bash
git checkout -b chore/add-coderabbit-config
cp .coderabbit.yaml .   # place in repo root
git add .coderabbit.yaml
git commit -m "chore: add CodeRabbit AI review configuration"
git push origin chore/add-coderabbit-config
```

### Step 4 — Open a test PR and verify

Open the PR for the config commit. CodeRabbit will post its first walkthrough comment within **1–3 minutes**. Verify:

- The walkthrough summary appears (not suppressed)
- No poem is generated
- No line comments appear on the `.coderabbit.yaml` file itself (it's config, not code)
- The profile says "chill" in the review metadata

### Step 5 — Validate Dependabot exclusion

Check an existing Dependabot PR or trigger one. CodeRabbit should **silently skip** it — no "review skipped" comment, no walkthrough. If you ever need a one-off review on a bot PR, comment `@coderabbitai review` to override.

### Step 6 — Validate path instructions on a real code PR

Open a small PR touching `backend/Modules/` and `backend/**/Prompts/*.yaml`. Confirm that:
- Module boundary guidance appears in code review comments for `.cs` files
- **No prompt-wording suggestions** appear on `.yaml` prompt files
- ADR/plan files (if touched) get included in the walkthrough summary but receive no line comments

### Step 7 — Tune over 2–4 weeks

CodeRabbit's **learning loop** stores dismissed-comment feedback per repository. Every time a reviewer resolves or dismisses a CodeRabbit comment as unhelpful, the system learns to suppress similar comments in future. Teams consistently report review quality improving significantly after **2–4 weeks** of active use. If after this period the noise level is still too high, switch specific path instructions to more restrictive language or consider toggling `profile` to `assertive` for backend code only (requires splitting configs via inheritance).

---

## (c) Uninstall / revert procedure

Removing CodeRabbit is clean and immediate. No code changes are required beyond deleting the config file.

### Full removal (org-level)

1. Go to **GitHub → Organization Settings → GitHub Apps** (or personal Settings → Applications → Installed GitHub Apps)
2. Find **CodeRabbit** → click **Configure**
3. Scroll to the bottom → click **"Uninstall"**
4. Optionally: go to **personal Settings → Applications → Authorized OAuth Apps** and revoke CodeRabbit's OAuth token
5. Delete `.coderabbit.yaml` from the repo root
6. Optionally: log into **app.coderabbit.ai** and use the **Delete Account** button (admin only; irreversible) to purge all stored learnings and analytics

### Per-repo removal (keep app for other repos)

1. Go to **GitHub → Organization Settings → GitHub Apps → CodeRabbit → Configure**
2. Under "Repository access," switch from "All repositories" to **"Only select repositories"** and deselect the target repo
3. Delete `.coderabbit.yaml` from that repo

### Per-PR suppression (temporary)

Add `@coderabbitai ignore` anywhere in the PR description body. CodeRabbit will skip that PR entirely.

### What happens after uninstall

CodeRabbit's comments remain in PR history as regular GitHub comments — they are **not deleted retroactively**. No webhooks, no Actions workflows, and no secrets need cleanup. The app runs entirely on CodeRabbit's infrastructure, so there are no orphaned containers or CI artifacts.

---

## Key questions answered

### Does CodeRabbit duplicate Roslyn analyzer findings?

**No, and here's why.** CodeRabbit has **no built-in Roslyn or .NET static analyzer**. Its 40+ integrated linters cover ESLint, Biome, Ruff, golangci-lint, Semgrep, and others — but not Roslyn. Your `SonarAnalyzer.CSharp` and `TreatWarningsAsErrors=true` findings flow through `dotnet build` in CI and appear as GitHub Check annotations. CodeRabbit's `github-checks` integration **reads** those annotations and can suggest fixes for failing checks, but it does not independently re-run Roslyn rules.

The only overlap risk is the LLM independently flagging the same issue a Roslyn analyzer also catches (e.g., an unused variable). With `profile: "chill"`, this is rare because chill mode suppresses style and naming comments. The config above also **disables CodeRabbit's built-in ESLint** since you already run `eslint-plugin-sonarjs` via Lefthook and CI, which eliminates the most common source of double-reporting in JS/TS repos.

### Can CodeRabbit post summaries but skip line comments on doc paths?

**There is no first-class "summary-only" toggle per path.** CodeRabbit does not expose a `line_comments: false` option scoped to a glob. The recommended workaround — and the one used in the config above — is to use `path_instructions` with explicit directives: *"Do NOT post individual line-level review comments on these files. Only include them in the high-level PR summary walkthrough."* The LLM respects these instructions reliably, though not with 100% guarantee. If a doc file has a genuinely broken link or syntax error, CodeRabbit may still flag it, which is desirable.

If you need absolute silence on doc files, add them to `path_filters` with `!` prefixes — but this removes them from the walkthrough summary entirely.

### What is the free-tier volume limit for open source?

**Effectively unlimited for public repos.** CodeRabbit's Pro plan is **free forever for all public repositories** — no application, no approval, no seat caps. Rate limits are **200 files/hour** and **4 PR reviews/hour** (3 back-to-back, then throttled). For a typical OSS project doing 5–15 PRs/day, this is never a bottleneck. If your repo ever goes private, you'd fall to the Free tier which has the same rate limits but loses some Pro features like path_instructions and pre-merge custom checks.

### Has the 28% noise rate changed?

The widely cited **28%** figure originates from a single team's one-month study on the Lychee project (28 PRs, 290 findings). It measured 15% "useless" + 13% "wrong assumptions" = 28% noise. More recent data points paint a better picture: a 4-month study in 2026 found **20.6% false positives** (79.4% signal). Another team reported starting at "50-50 useful/useless" but improving significantly as the learning loop calibrated. **The consensus in 2026 is roughly 15–25% noise after tuning**, down from 28–50% on day one, thanks to the dismissed-comment learning system and the `chill` profile introduced in late 2024.

### Is the dismissed-comment learning loop still active?

**Yes.** The `knowledge_base.learnings.scope: "auto"` setting (enabled by default) stores every dismissed or resolved comment as team-specific feedback. Over 2–4 weeks, CodeRabbit progressively suppresses comment patterns your team consistently rejects. This is the single most effective noise-reduction mechanism and requires no manual configuration — just dismiss comments you find unhelpful.

### Is schema v2 still current?

**Yes.** The schema at `https://coderabbit.ai/integrations/schema.v2.json` was last updated **April 13, 2026** (two days before this report). The `# yaml-language-server` directive in the YAML above enables IDE validation. Top-level keys: `language`, `tone_instructions`, `early_access`, `enable_free_tier`, `reviews`, `chat`, `knowledge_base`, `code_generation`, `issue_enrichment`, `inheritance`, `remote_config`.

---

## (d) Citations — 2026-dated documentation

| Claim | Source | URL |
|---|---|---|
| Installation via GitHub App, quickstart flow | CodeRabbit Docs — Quickstart | `docs.coderabbit.ai/getting-started/quickstart` |
| `.coderabbit.yaml` format, all config keys | CodeRabbit Docs — YAML Configuration | `docs.coderabbit.ai/getting-started/yaml-configuration` |
| Full configuration reference (all keys, types, defaults) | CodeRabbit Docs — Configuration Reference | `docs.coderabbit.ai/reference/configuration` |
| Schema v2 JSON (updated 2026-04-13) | CodeRabbit Schema | `coderabbit.ai/integrations/schema.v2.json` |
| `path_instructions` syntax and examples | CodeRabbit Docs — Path-Based Review Instructions | `docs.coderabbit.ai/configuration/path-instructions` |
| `ignore_usernames` for Dependabot exclusion | CodeRabbit Docs — Username-Based PR Review Control | `docs.coderabbit.ai/configuration/username-based-pr-review-control` |
| GitHub Marketplace listing (193K installs, pricing) | GitHub Marketplace | `github.com/marketplace/coderabbitai` |
| Pro free forever for public repos, $24/seat/mo for private | CodeRabbit Pricing Page | `coderabbit.ai/pricing` |
| `github-checks` tool with `timeout_ms` | CodeRabbit Docs — Tools Reference & PHARE config example | `docs.coderabbit.ai/tools` |
| Changelog: Betterleaks, Slop Detection, CLI v0.4 (2026) | CodeRabbit Docs — Changelog | `docs.coderabbit.ai/changelog` |
| Uninstall procedure (GitHub App removal) | CodeRabbit Docs — FAQ | `docs.coderabbit.ai/faq` |
| 28% noise figure origin (Lychee project study) | stvck.dev — One Month with CodeRabbit | `stvck.dev/articles/one-month-with-coderabbit-an-ai-assisted-code-review-experience` |
| 79.4% signal rate (4-month study, 2026) | ohaiknow.com — CodeRabbit Review | `ohaiknow.com/reviews/coderabbit/` |
| Learning loop & tuning period (2–4 weeks) | Flowing Code — Improving Code Reviews with AI | `flowingcode.com/en/improving-code-reviews-with-ai-our-experience-with-coderabbit/` |
| Crossplane `.coderabbit.yaml` (295-line real-world example with `ignore_usernames`) | Crossplane GitHub | `github.com/crossplane/crossplane/blob/main/.coderabbit.yaml` |
| Configuration inheritance (Dec 2025) | CodeRabbit Docs — Configuration Inheritance | `docs.coderabbit.ai/configuration/configuration-inheritance` |
| $600K+ OSS sponsorships in 2025 | CodeRabbit Blog | `coderabbit.ai/blog/we-are-committed-to-supporting-open-source-distributed-600000-to-open-source-maintainers-in-2025` |
| Monorepo path_instructions best practices | AI Code Review Guide | `aicodereview.cc/blog/coderabbit-monorepo/` |
| CodeRabbit vs SonarQube (minimal duplication) | DEV.to — CodeRabbit vs SonarQube | `dev.to/rahulxsingh/coderabbit-vs-sonarqube-ai-review-vs-static-analysis-2026-48if` |

---

## Conclusion

CodeRabbit is a low-friction addition to this stack. It installs in under 5 minutes with no CI pipeline changes, no secrets management, and no new GitHub Actions workflows. The `.coderabbit.yaml` above protects your three sensitive zones (prompt IP, ADRs, plans) through explicit `path_instructions` rather than full exclusion, preserving their inclusion in walkthrough summaries. Dependabot noise is eliminated via `ignore_usernames`, and Roslyn/ESLint double-reporting is avoided by disabling CodeRabbit's built-in ESLint (your CI already runs it) and relying on the fact that CodeRabbit has no Roslyn equivalent. The `github-checks` integration reads your existing CI annotations to suggest fixes rather than duplicate them. Start with `profile: "chill"`, let the learning loop calibrate for a month, and revisit whether `assertive` would add value for backend code once the team has established its dismiss-vs-accept patterns.