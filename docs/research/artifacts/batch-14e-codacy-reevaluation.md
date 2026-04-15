# Codacy adds no unique signal to this stack

**For a .NET 10 + React 19 monorepo already planning SonarQube Cloud and CodeQL, Codacy's free tier provides effectively zero residual value.** Every analyzer Codacy would run on this codebase is either identical to something already in the stack (it literally ships the same SonarAnalyzer.CSharp NuGet and the same ESLint + eslint-plugin-sonarjs bundle) or is an open-source tool you can run standalone in CI for free. The unified dashboard — Codacy's main differentiator — is fully replicated by SonarQube Cloud with deeper metrics. Adding Codacy would introduce duplicate findings, broader GitHub permissions, and a second dashboard to maintain, with no net new insight.

---

## 1. What the Codacy open-source tier actually provides in 2026

Codacy's pricing page now shows three tiers — **Developer** ($0, IDE-only), **Team** ($18–21/dev/month), and **Business** (custom) — but public repositories receive the full Team tier at no cost, still branded "Open Source" on the GitHub Marketplace and in the FAQ. The naming is mid-transition; the GitHub Marketplace listing (73,868 installs) labels the free plan "Open Source" while the pricing cards say "Developer."

The free open-source tier includes **49 languages, 30+ bundled analysis tools, and over 12,000 configurable scan rules**. Unlimited public repositories are permitted. PR integration includes status checks (pass/fail), inline issue annotations, and issue summaries on GitHub PRs — but **PR summary comments, suggested fixes, and the AI Reviewer are locked to paid plans**. Coverage upload via `codacy-coverage-reporter` is supported, as are customizable quality gates (issue count, complexity, duplication, coverage thresholds, and diff-coverage gates). Organization-wide gate policies can template these across repos. The Security & Risk Management dashboard, SAST, hardcoded-secrets detection, SCA for new code, and IaC misconfiguration scanning (via Checkov) are all included. Features gated behind Business tier include DAST, license scanning, SBOM exports, and daily SCA rescans.

No explicit user-count cap is documented for the open-source plan, though the paid Team plan caps at 30 developers. One third-party source (Vendr) references a 2-committer limit on a "Starter" tier, but this does not appear on Codacy's own pricing page and likely refers to an older or private-repo construct.

---

## 2. Codacy's tools mapped against the existing stack, language by language

The critical finding is that **Codacy's C# analyzer is SonarAnalyzer.CSharp itself** — the `codacy-sonar-csharp` GitHub repository shows Dependabot bumps tracking versions 10.5–10.6 as of early 2026. It does not bundle StyleCop or any general Roslyn analyzers. For TypeScript and JavaScript, Codacy runs ESLint v8/v9 with approximately 100 bundled plugins, explicitly including `eslint-plugin-sonarjs` and `@typescript-eslint/eslint-plugin`. The overlap with the project's own ESLint configuration is essentially **100%**.

| Language | Codacy tools | Already covered by your stack? |
|---|---|---|
| **C#** | SonarC# (= SonarAnalyzer.CSharp), Opengrep, PMD CPD, Lizard, Trivy | SonarC# duplicates SonarAnalyzer.CSharp + SonarQube Cloud. Opengrep overlaps with CodeQL SAST. StyleCop/Roslyn not covered by Codacy at all. |
| **TypeScript** | ESLint + eslint-plugin-sonarjs, BiomeJS, Opengrep, jscpd, Lizard, Trivy | ESLint bundle is identical. SonarQube Cloud adds SonarJS (deeper dataflow). CodeQL covers security. BiomeJS is the only non-duplicate — a formatter/linter you can run locally for free. |
| **JavaScript** | ESLint, PMD, BiomeJS, Opengrep, jscpd, Lizard, Trivy | Same overlap as TypeScript. PMD's JS support uses the legacy Rhino parser and adds minimal value over ESLint. |
| **CSS** | Stylelint, BiomeJS | Neither is in your stack, but both are trivially added to CI. SonarQube Cloud does analyze CSS. |
| **Dockerfile** | Hadolint, Opengrep, Trivy | Hadolint is a standalone binary you can add in one CI step. Opengrep/Trivy overlap with CodeQL and GitHub Dependabot. |
| **YAML** | No static analysis linter (only Trivy for secrets) | Codacy provides **zero YAML linting** — no yamllint, no syntax checking. |
| **JSON** | Jackson Linter (syntax-only), BiomeJS | Jackson Linter validates JSON structure only. BiomeJS available standalone. SonarQube Cloud analyzes JSON. |
| **Markdown** | remark-lint, markdownlint | These are genuinely not in the stack, but both are trivial standalone CI additions (one `npx` command each). |
| **HTML** | **Not supported** | Codacy does not analyze HTML at all. SonarQube Cloud does. |

Codacy has **no proprietary analysis engine**. The term "Codacy Patterns" in its UI refers to configurable rules from the bundled open-source tools, not a unique engine. The only semi-proprietary tool is Codacy Scalameta Pro for Scala, which is irrelevant here.

---

## 3. The overlap matrix

This table shows whether each capability is provided (Yes), absent (No), or incompletely covered (Partial) by each configuration layer.

| Capability | Current stack (ESLint + Roslyn + StyleCop + SonarAnalyzer + Codecov) | + CodeQL | + SonarQube Cloud | + Codacy |
|---|---|---|---|---|
| **ESLint / JS-TS linting rules** | Yes | No | Yes (SonarJS adds deeper dataflow rules) | Yes (runs same ESLint + sonarjs plugin) |
| **Roslyn / StyleCop rules** | Yes | No | No | No |
| **First-party SAST (security vulns)** | Partial (SonarAnalyzer security rules at build) | **Yes** (deep taint-tracking for C#, TS, JS) | Yes (SAST + Security Hotspots) | Yes (Opengrep/Semgrep patterns) |
| **Coverage dashboard** | Yes (Codecov) | No | Yes | Yes |
| **Duplication detection dashboard** | No | No | **Yes** (built-in CPD + trend tracking) | Yes (PMD CPD / jscpd) |
| **Cognitive complexity trend tracking** | Partial (SonarAnalyzer flags at build, no trend) | No | **Yes** (Sonar invented the metric; full trend graphs) | Partial (Lizard computes cyclomatic, not cognitive) |
| **Multi-tool consolidated dashboard** | No | Partial (GitHub Security tab) | **Yes** (single pane for quality + security + coverage) | Yes (aggregates 30+ tools) |
| **Quality gates blocking PR merge** | Yes (TreatWarningsAsErrors fails build) | Yes (GitHub status check) | **Yes** (highly configurable gate with ratings A–E) | Yes (threshold-based gate) |

The key column is **+ SonarQube Cloud**, which fills every gap the current stack has: duplication dashboard, cognitive complexity trends, consolidated view, and configurable quality gates with rating-based conditions. Adding Codacy after SonarQube Cloud produces **no cell that flips from No to Yes**.

---

## 4. GitHub App permissions are scoped per-repo since 2020

Codacy switched from OAuth to a GitHub App in **February 2020**, which means you can install it on specific repositories rather than granting access to everything. Organization owners control which repos the app sees.

The current Cloud GitHub App requests these permissions:

- **Repository-level**: Checks (read/write), Issues (read/write), Pull Requests (read/write), Commit Statuses (read/write), Webhooks (read/write), Contents (read-only, added September 2023), Metadata (read-only)
- **Organization-level**: Members (read-only), Webhooks (read/write)
- **User-level**: Email addresses (read-only)

The formerly controversial **Administration: read/write** scope was dropped in January 2024 when Codacy moved from SSH keys to installation access tokens (which expire hourly). The permission surface is reasonable but **broader than SonarQube Cloud's** — notably, Codacy requests write access to Issues (it can create GitHub issues from findings) and read access to organization members. SonarQube Cloud's GitHub App does not request Issues write or org-member read permissions. For a security-conscious open-source project, this is a minor but real consideration.

---

## 5. SonarQube Cloud's quality gate is more mature than Codacy's

SonarQube Cloud's quality gate system is **the industry benchmark** for this feature. It offers conditions on both new code and overall code, uses a nuanced **A-through-E rating system** for reliability, security, and maintainability (not just raw counts), and enforces the "Clean as You Code" methodology by default — the Sonar Way gate requires zero new issues, all security hotspots reviewed, and minimum coverage thresholds. Custom gates support coverage percentage, duplicated-lines density, issue counts by severity, and both cyclomatic and cognitive complexity. Full API access enables programmatic gate management.

Codacy's quality gate uses simpler **numeric thresholds**: issues-over-X, complexity-over-X, duplication-over-X%, coverage-under-X%. It uniquely offers a "diff coverage" gate (minimum coverage on changed lines specifically) and organization-wide gate policies. However, it lacks rating-based conditions and the new-code/overall-code distinction that makes SonarQube Cloud's gates more precise.

Community reports from 2025 indicate that both platforms occasionally exhibit flaky behavior — SonarQube Cloud users report quality gates stuck in "Waiting" on Azure DevOps (less common on GitHub), while Codacy users report inconsistencies between issue counts in metrics views versus the Issues tab. **Neither is flawless, but SonarQube Cloud's deeper metric granularity and longer track record give it a reliability edge**, particularly on GitHub where its integration is most mature.

On false positives, **Codacy's multi-engine approach generates more initial noise** because it wraps ESLint, PMD, Opengrep, and other tools whose rule sets can conflict. Codacy acknowledged this by shipping "Smart False Positive Triage" (AI-powered, Business tier only) in October 2025. SonarQube Cloud's single deterministic rule engine produces fewer false positives out of the box, though C/C++ users have historically reported issues with build-environment mismatches.

---

## 6. Recommendation: do not add Codacy

Given the planned stack of **SonarQube Cloud + CodeQL + build-time analyzers (ESLint, StyleCop, Roslyn, SonarAnalyzer.CSharp) + Codecov**, Codacy provides no unique analytical signal:

- **C# analysis**: Codacy runs the identical SonarAnalyzer.CSharp engine. You already run it at build time with TreatWarningsAsErrors *and* will get it again via SonarQube Cloud with trend tracking. Codacy adds a third copy.
- **TypeScript/JavaScript analysis**: Codacy runs the same ESLint with the same eslint-plugin-sonarjs. SonarQube Cloud adds SonarJS with deeper dataflow analysis that ESLint cannot replicate. Codacy adds nothing beyond what you already have.
- **SAST/security**: CodeQL provides deep taint-tracking for C#, TypeScript, and JavaScript. SonarQube Cloud adds Security Hotspot review. Codacy's Opengrep (formerly Semgrep) uses pattern-matching that is less precise than CodeQL's dataflow analysis and largely overlaps with SonarQube Cloud's security rules.
- **SCA/dependency scanning**: GitHub Dependabot already covers this for public repos at no cost, as does Trivy (which you can run standalone without Codacy).
- **Dashboard**: SonarQube Cloud provides the consolidated dashboard with duplication, cognitive complexity trends, and quality gates. Codacy's dashboard would be a second, redundant pane.
- **Coverage**: Codecov is already best-in-class for coverage dashboards. SonarQube Cloud ingests coverage reports too. Codacy's coverage feature would be a third redundant view.
- **Unique tools**: Hadolint (Dockerfile), markdownlint, remark-lint, and Stylelint are genuine additions, but each is a single `npx` or `docker run` command in CI — no platform needed.

The cost of adding Codacy is not financial (it's free) but **operational**: a second dashboard to check, a second set of findings to triage (many duplicated), broader GitHub App permissions, and confusion about which platform is the source of truth for quality gates. Community consensus from 2025–2026 is clear: **pick one aggregation platform and complement with specialized tools**, not two overlapping platforms.

---

## 7. When to reconsider Codacy

Four future scenarios would warrant re-evaluation:

- **SonarQube Cloud changes its free OSS tier.** If SonarSource introduces LOC caps, feature restrictions, or eliminates the free plan for public repos, Codacy becomes the strongest fallback — it offers a comparable free tier with broader language support (49 vs 30 languages).
- **You add a language Codacy covers but SonarQube Cloud does not.** SonarQube Cloud supports 30+ languages; Codacy supports 49. If the monorepo adds Scala, Elixir, Dart, Perl, Crystal, or another language outside SonarQube Cloud's list, Codacy's bundled tools for those languages could fill a gap.
- **Codacy ships a genuinely proprietary capability.** Codacy's AI Reviewer (currently paid-only) and AI Guardrails (IDE extension) are newer features. If these mature, become free for open-source, and demonstrate unique value beyond GitHub Copilot code review and SonarQube Cloud's AI CodeFix, they could justify the integration.
- **You need integrated DAST or SBOM exports on the free tier.** Codacy's Business tier includes DAST (via OWASP ZAP) and SBOM generation. If these features migrate to the free open-source tier, they would represent capabilities neither SonarQube Cloud nor CodeQL provide.
- **GitHub Code Quality exits preview and replaces SonarQube Cloud's quality role.** GitHub launched Code Quality (public preview, October 2025) using CodeQL for maintainability and reliability analysis of C#, JavaScript, and other languages. If this matures to include coverage, duplication, and cognitive complexity tracking, it could eliminate the need for SonarQube Cloud entirely — at which point Codacy's dashboard role might warrant a fresh look, though GitHub's own tooling would likely still be the better fit for a GitHub-hosted repo.

None of these triggers appear imminent. The recommendation stands: **run SonarQube Cloud for dashboarding and quality gates, CodeQL for SAST, your existing build-time analyzers for enforcement, and Codecov for coverage.** Skip Codacy.