# Quality tooling for an AI-generated .NET + React monorepo

**When virtually all code is AI-generated, your quality pipeline is your product.** The tooling landscape as of March 2026 offers a remarkably capable — and mostly free — stack for open-source solo developers, but the "AI-reviewing-AI" problem demands a layered strategy where no single tool is trusted as sufficient. CodeRabbit's analysis of 470 PRs found AI-authored code produces **1.7× more issues per PR** than human-written code, with logic errors up 75% and security vulnerabilities 1.5–2× higher. Yet IBM research shows LLM-as-judge alone detects only **~45% of errors** in AI-generated code. The path forward: automated tools for breadth, human review for depth, and architectural guardrails baked into every layer.

---

## 1. AI PR review tools are free, imperfect, and essential

The market has exploded since mid-2025, but only a few tools combine free open-source access, genuine .NET + TypeScript support, and meaningful AI-generated code awareness.

### CodeRabbit is the clear primary choice

CodeRabbit is the most-installed AI review app on GitHub (**2M+ repos, 13M+ PRs reviewed**). It combines AST analysis, 40+ SAST tools, and generative AI to provide line-level review, PR summaries, security scanning, and agentic workflows. For open-source repos, the full feature set — identical to paid plans — costs nothing. It installs in two clicks as a GitHub App and automatically reviews every PR across both C# and TypeScript files.

Practitioner experience is consistently "useful but noisy." An independent audit found **28% of comments were noise or incorrect**, but the system learns from dismissed comments over time. The PR summary feature alone is invaluable for a solo dev reviewing AI-generated diffs — it explains what changed in plain language before you read a single line of code. CodeRabbit explicitly researches AI-generated code patterns and catches the mechanical issues AI introduces (missing null checks, edge cases, security patterns) well. It is weaker on architectural drift, which requires whole-codebase reasoning.

Configure via `.coderabbit.yaml` in the repo root. Teach it your patterns by resolving unhelpful comments — the Learnings system suppresses repeated false positives.

### Claude Code GitHub Action as targeted second reviewer

Anthropic's open-source GitHub Action (`anthropics/claude-code-action@v1`) provides a fully customizable PR reviewer powered by Claude. You pay only for API tokens — typically **$0.50–$3 per review** depending on PR size. The key advantage over CodeRabbit is extreme configurability: you control the review prompt, can reference `CLAUDE.md` for architectural standards, and can focus the review on specific concerns (architectural consistency, unnecessary complexity, pattern drift).

Using a *different* AI system for review than for generation partially breaks the correlated blind-spot problem documented in the literature. Configure it to run on-demand via `@claude` mentions rather than every PR to control costs. The companion `anthropics/claude-code-security-review` action adds dedicated security analysis.

Anthropic also launched a premium **Claude Code Review** product on March 9, 2026 — a multi-agent system dispatching specialized reviewers in parallel. At an estimated **$15–$25 per review** and restricted to Team/Enterprise plans, it's overkill for a solo open-source project.

### What about the other tools?

**GitHub Copilot Code Review** requires a paid Copilot subscription ($10–39/month) and consistently disappoints practitioners. A Cotera.co evaluation found **31 of 47 suggestions would have been caught by ESLint**, and 7 were factually wrong. It's diff-only with no cross-file context — precisely the wrong architecture for catching AI-generated code problems. Skip unless you already pay for Copilot Pro.

**Qodo Merge** (formerly CodiumAI/PR-Agent) is the strongest alternative to CodeRabbit. The hosted free tier provides **75 PR reviews/month** — generous for a solo dev. Its differentiator is test generation via `/improve` and compliance checking. The self-hosted PR-Agent option gives full control with your own API key. Less noisy than CodeRabbit by default due to its `focus_only_on_problems` mode. Consider it if CodeRabbit's noise proves unmanageable.

**Codacy** (free for open source) offers quality gates that can block merges and traditional SAST scanning for 49 languages. It's more rigorous on security than CodeRabbit but less conversational. Worth adding as a complementary static analysis layer if you want merge-blocking quality gates.

**Sourcery** has a significant noise problem (~50% of comments are noise or bikeshedding per one team's evaluation) and lacks deep C# rules. **Ellipsis** has excellent signal-to-noise ratio and can implement fixes, but charges $20/month with no free OSS tier. **Bito** builds a knowledge graph for deeper context but starts at $15/month. Neither is justified for a free-tier solo dev workflow.

### Recommended combination

Install **CodeRabbit** (free, always-on, every PR) as the primary automated reviewer. Add the **Claude Code GitHub Action** (API costs only, on-demand) for important PRs where you want architectural review against your `CLAUDE.md` standards. Together they provide cross-model review with uncorrelated blind spots, catching **meaningfully more** than either alone.

| Tool | Cost | Setup | Noise | Catches |
|------|------|-------|-------|---------|
| CodeRabbit | $0 (OSS) | 5 min | Medium-high, improves over time | Security, null checks, edge cases, style, PR summaries |
| Claude Action | ~$2–8/mo API | 30 min | Low (you control the prompt) | Architectural drift, complexity, pattern consistency |
| Codacy | $0 (OSS) | 45 min | Medium | SAST, quality gates, merge blocking |

---

## 2. Lefthook wins the pre-commit battle for polyglot monorepos

### Why Lefthook over Husky

Husky v9 remains the default choice in JavaScript-only projects (~5M weekly npm downloads), but for a polyglot .NET + React monorepo, **Lefthook is strictly superior**. Written in Go, it runs as a single binary with no Node.js startup overhead, executes hooks in parallel by default, includes built-in staged-file support via `{staged_files}`, and routes commands by glob pattern — eliminating the need for lint-staged entirely.

Where Husky requires Husky + lint-staged + shell scripts (three moving parts), Lefthook consolidates everything into a single `lefthook.yml`. It adds zero entries to `node_modules` versus ~1,500 for the Husky + lint-staged combination. Setup is one command: `npm install -D lefthook && npx lefthook install`.

### The recommended configuration

```yaml
# lefthook.yml
pre-commit:
  parallel: true
  commands:
    dotnet-format:
      glob: "*.cs"
      root: "backend/"
      run: dotnet format backend/RunCoach.sln --include {staged_files} --no-restore
      stage_fixed: true

    eslint:
      glob: "*.{ts,tsx}"
      root: "frontend/"
      run: npx eslint --fix {staged_files}
      stage_fixed: true

    prettier:
      glob: "*.{ts,tsx,js,json,css,md,yml}"
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
      glob: "*.cs"
      root: "backend/"
      run: dotnet test --no-restore --filter "Category=Unit"
    frontend-typecheck:
      glob: "*.{ts,tsx}"
      root: "frontend/"
      run: npx tsc --noEmit
```

The `glob` directive filters by file extension, `root` scopes to subdirectories, `parallel: true` runs .NET and frontend checks simultaneously, and `stage_fixed: true` re-stages auto-fixed files. The `--no-restore` flag on `dotnet format` skips NuGet restore, saving 1–2 seconds.

### Timing and commit message conventions

With parallel execution, the total pre-commit time is **~2–3 seconds** — dominated by `dotnet format` on staged files (~2s) while ESLint (~1s) and Prettier (~0.5s) run concurrently. This stays well under the 5-second comfort threshold.

**commitlint** adds ~300ms as a lightweight safety net. Claude Code already follows conventional commits when instructed via `CLAUDE.md`, making commitlint a backstop rather than a primary enforcement mechanism. Its real value emerges later when you want automated changelog generation via `standard-version` or `semantic-release`. Configure it with `@commitlint/config-conventional` and the standard type enum (feat, fix, docs, refactor, test, chore, ci).

### What stays in CI, not pre-commit

Roslyn analyzers and StyleCop run automatically during `dotnet build` — they don't need a separate pre-commit step. Configure severity in `.editorconfig` and set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in CI. Security Code Scan explicitly warns it "will slow down IDE" and is designed for CI pipelines. Full test suites, `dotnet format --verify-no-changes` (the verification-only mode), and any security scanning belong in CI where time pressure is lower.

---

## 3. Three free tools cover 95% of CI security and quality needs

### The free trinity: CodeQL + Dependabot + Trivy

**CodeQL** is the highest-value security tool for this stack. Completely free for public repositories with unlimited scans, it performs semantic code analysis for both C# and JavaScript/TypeScript in a single workflow using a matrix strategy. It now supports `build-mode: none` for C# (faster database creation) and dependency caching. Setup is one-click via repository Settings → Security, or a 15-minute workflow file for advanced configuration. Noise is low — designed for high-fidelity security results. As of April 2025, CodeQL also scans GitHub Actions workflow files themselves, having found **800K+ vulnerabilities** across 158K repos.

**Dependabot** handles automated dependency update PRs and security alerts for both NuGet and npm ecosystems, plus GitHub Actions versions and Docker base images. Completely free for all repos. The main management task is noise control: configure `open-pull-requests-limit`, group minor/patch updates, and set weekly schedules. A `dependabot.yml` file covering all four ecosystems (nuget, npm, github-actions, docker) takes 10 minutes to write.

**Trivy** is the Swiss army knife — one open-source tool scanning filesystem dependencies (NuGet + npm), Docker images, IaC files (Dockerfiles, Compose), secrets, *and* licenses. Apache 2.0 licensed with no tiers or feature restrictions. Use `--ignore-unfixed` and `--severity CRITICAL,HIGH` to keep noise manageable. Upload results as SARIF to integrate with GitHub's Security tab. This single tool replaces the need for separate container scanning, partially replaces license checking tools, and supplements CodeQL's dependency scanning.

**Snyk** offers superior fix-PR automation and reachability analysis but adds friction (account management, token rotation) and has free-tier limits that could matter if the repo goes private. Skip initially; consider later if you need automated fix PRs.

### Code coverage with Codecov

The cleanest path for combined .NET + React coverage: **Coverlet** (already included with new xUnit projects) generates Cobertura XML from `dotnet test --collect:"XPlat Code Coverage"`, and **Vitest's v8 provider** generates LCOV from `npx vitest run --coverage`. Upload both to **Codecov** (free for open source, unlimited) with separate `flags` (backend, frontend) — Codecov natively merges them into a single PR comment and dashboard.

For a new project, start with a **60% project target** and **70% patch coverage** (new code) threshold. Patch coverage enforcement is especially important for AI-generated code — it ensures Claude Code actually writes tests for new features. Use Codecov's Carryforward Flags so path-filtered CI runs (only backend changed) don't reset frontend coverage.

### License compliance and container scanning

For licenses, avoid heavyweight solutions like FOSSA. Two lightweight CLI tools handle both ecosystems: `npx license-checker --onlyAllow "MIT;ISC;BSD-2-Clause;BSD-3-Clause;Apache-2.0;0BSD"` for npm and `nuget-license -i MyApp.sln -a "MIT;Apache-2.0;BSD-2-Clause;BSD-3-Clause"` for .NET. Trivy also checks licenses via `--scanners license`. Run license checks **weekly via scheduled workflow**, not on every PR — license changes are rare.

Container scanning is handled by the same Trivy action you already use for filesystem scanning. Add a post-build `trivy image` step with `--ignore-unfixed` and `--severity CRITICAL,HIGH`. Start with `exit-code: 0` (warn only) and tighten to `exit-code: 1` once initial findings are triaged.

### Performance regression testing: defer it

BenchmarkDotNet + `github-action-benchmark` can track .NET performance over time, but **GitHub-hosted runners have 5–20% variance** from noisy neighbors, making regression detection unreliable without statistical significance testing. A small benchmark suite takes 5–15 minutes per run. For a solo dev on a greenfield project, this is premature optimization. Revisit when you have specific hot paths to monitor. A lightweight alternative for Phase 2: k6 smoke tests asserting API response times stay under a threshold — catches catastrophic regressions without microbenchmark overhead.

### Workflow optimization with path filtering

Use `dorny/paths-filter` (or its security-hardened fork `step-security/paths-filter`) for job-level conditional execution. A single CI workflow file with a `changes` job determines which paths were modified, then conditionally runs `dotnet` and `frontend` jobs only when relevant files change. A final `ci-gate` job that always runs solves the "skipped required check blocks merge" problem by checking upstream job results.

Public repositories get **free and unlimited** GitHub Actions minutes on standard runners. There are no cost constraints on CI for this open-source project.

---

## 4. SonarCloud earns its place — but only because of AI-generated code

### The traditional case is weak for a solo developer

SonarCloud's primary value proposition — enforcing standards across teams, manager dashboards, quality gates as team agreements — is designed for teams of five or more. Roughly **85% of SonarQube's rules focus on code quality** (readability, formatting, refactoring) that your existing StyleCop + ESLint + Biome stack already covers. G2 reviewers consistently list false positives as a top concern, and multiple practitioners note that tuning rules to reduce noise takes significant time. For a solo developer who already enforces standards via `.editorconfig` and automated formatters, this is partially enterprise theater.

### The AI-generated code angle changes the calculus

Academic research paints a stark picture: **29–44% of AI-generated code contains security vulnerabilities** across multiple studies (Veracode, Pearce et al.). GitClear found a **4× increase in code cloning** with AI assistants. SonarSource has responded with features specifically targeting this: "AI Code Assurance" that detects AI-generated code patterns, enhanced analysis pipelines, and critically, an **MCP server that integrates directly with Claude Code** — allowing your AI coding assistant to pull SonarCloud issues, quality gate status, and security hotspots into its working context.

Where SonarCloud adds genuine value beyond your existing tools: **cross-file taint analysis** (tracking user input from HTTP endpoint through service layers to SQL/file operations — something ESLint cannot do), **cognitive complexity scoring**, **duplication detection** (AI loves duplicating code), and the **feedback loop** potential. Research shows feeding static analysis warnings back to AI coding tools fixed **up to 55.5% of security issues** — meaning SonarCloud → Claude Code creates a systematic improvement cycle.

### The pragmatic lightweight alternative

If SonarCloud's setup cost (2–3 hours) feels excessive, install the **`SonarAnalyzer.CSharp` NuGet package** and **`eslint-plugin-sonarjs`** into your existing build pipeline for free. You get Sonar's rules as build warnings without the dashboard, trends, or PR decoration. This captures roughly **90% of the analysis value at 0% of the platform overhead**. Add SonarCloud later when the codebase grows large enough for trends and duplication tracking to matter.

### Recommendation

**Use SonarCloud's free tier** — your open-source project qualifies. The setup cost is a one-time 2–3 hour investment. Don't obsess over code smell counts (many overlap with existing tools). Focus on the security hotspot detection, coverage tracking, and duplication metrics. Enable the MCP server integration to close the feedback loop with Claude Code. Skip self-hosted SonarQube entirely — SonarCloud's free tier with branch analysis and PR decoration is strictly superior.

---

## 5. The AI-reviewing-AI problem is real, structural, and partially solvable

### Correlated blind spots are the core risk

When the same model family generates and reviews code, **confirmation bias is architectural, not incidental**. Qodo's analysis documents the mechanism: the generator creates an anchor that constrains subsequent analysis. The same training data biases that cause the generator to produce a pattern cause the reviewer to accept it. GitClear data shows an **8× increase in duplicated code blocks** when AI reviews its own output, with a **39.9% decrease in refactored code** and a **37.6% increase in critical vulnerabilities** after multiple AI "improvement" cycles.

IBM Research confirmed this quantitatively: LLM-as-judge alone detects only **~45% of errors** in AI-generated code across all four production-level judge models tested. When supplemented with analytic hints from a separate, non-AI tool (static analysis, type checking), coverage rises to **94%**. This is the strongest argument for combining AI review with deterministic analysis tools like CodeQL, Roslyn analyzers, and SonarCloud.

### What AI review catches versus what it misses

AI review tools reliably catch **mechanical defects**: missing null checks, off-by-one errors, unused imports, simple security patterns, formatting inconsistencies, and basic edge cases. These represent roughly 40–46% of real bugs in AI-generated code — a meaningful class that would otherwise consume your human review attention.

AI review tools consistently miss the **highest-severity defects**: missing authorization rules, business logic gaps, architectural drift across files, workflow logic errors, and "silent failures" where code avoids crashing by producing plausible but incorrect output. ProjectDiscovery's benchmark of 3 AI-generated applications found **70 exploitable vulnerabilities including 18 Critical/High** — and traditional code-only review (including AI review) missed all of them. The most dangerous issues required running the application to discover.

A particularly insidious pattern documented by IEEE Spectrum (March 2026): newer AI models increasingly produce code that **removes safety checks, creates fake output matching expected formats, or uses techniques to avoid crashing**. This code passes automated review precisely because it avoids triggering error conditions.

### The architectural drift accumulates silently

LLMs are autoregressive — they optimize for locally plausible tokens, not global consistency. A Stanford/Meta study found instruction adherence averages only **43.7% across models over time**. For a solo dev on a monorepo with 30–45 minute sessions, this manifests as: inconsistent solutions to similar problems, naming drift toward generic defaults, layer boundary violations, duplicated logic reimplementing existing utilities, and formatting inconsistencies that accumulate over weeks.

AI reviewers don't catch this drift because they share the same generic baseline. Your primary defense is a well-maintained `CLAUDE.md` loaded every session, documenting architectural decisions, naming conventions, forbidden patterns, and existing utilities. Treat this file as living infrastructure, not documentation.

### What the human reviewer must focus on

With AI handling mechanical checks, your limited human attention should target six areas where AI review is architecturally incapable of substituting:

- **Business logic correctness** — does the code actually solve the right problem with correct domain rules? For a running coach, are the training recommendations physiologically sound?
- **Architectural consistency** — does new code follow your established patterns, or did Claude invent a new approach?
- **Test quality** — would these tests actually fail if the feature broke? Watch for tests that mock the very thing being tested, or assert nothing meaningful
- **Security threat modeling** — any code touching auth, payments, user input, or secrets gets mandatory human review
- **Scope creep** — did Claude change things beyond what you asked? AI frequently makes speculative changes: reorganizing imports, adding docstrings, touching unrelated files
- **Dependency verification** — do all referenced packages, imports, and APIs actually exist?

Budget **5–10 minutes per function** of deliberate slow reading on new code. The "explain every line" standard catches 60% of AI bugs that automated tools miss.

---

## Recommended quality pipeline: the complete stack

### Layer 1 — Pre-commit hooks

**What runs:** Lefthook with parallel `dotnet format` (staged .cs files), ESLint + Prettier (staged .ts/.tsx files), and commitlint on commit messages. Pre-push adds unit tests and TypeScript type checking.

| Metric | Value |
|--------|-------|
| Setup effort | 1–2 hours |
| Ongoing cost | $0 |
| Execution time | ~2–3 seconds (parallel) |
| Noise level | Low (formatters are deterministic) |

### Layer 2 — PR review automation

**What runs:** CodeRabbit (automatic on every PR) + Claude Code GitHub Action (on-demand for important PRs via `@claude` mention). Optionally, Codacy for SAST quality gates.

| Metric | Value |
|--------|-------|
| Setup effort | CodeRabbit: 5 min; Claude Action: 30 min; Codacy: 45 min |
| Ongoing cost | CodeRabbit: $0; Claude Action: ~$2–8/month API; Codacy: $0 |
| Noise level | Medium — improves as CodeRabbit learns your preferences |

**Configuration priorities:** Teach CodeRabbit your patterns by resolving noisy comments. Write a focused Claude Action prompt targeting architectural consistency and unnecessary complexity. Document everything in `CLAUDE.md` — this file is both your coding assistant's context and your review assistant's standard.

### Layer 3 — CI quality gates (GitHub Actions)

**Phase 1 (Day 1, ~2 hours):**
- Path-filtered CI via `dorny/paths-filter` — .NET jobs run only when backend changes, frontend jobs only when frontend changes
- `dotnet build` with `TreatWarningsAsErrors` (Roslyn analyzers + StyleCop enforce at build time)
- `dotnet test` with Coverlet coverage → Codecov upload (backend flag)
- `npm ci && vitest run --coverage` → Codecov upload (frontend flag)
- CodeQL for C# + JavaScript/TypeScript (security scanning)
- Dependabot configured for NuGet, npm, GitHub Actions, Docker

**Phase 2 (Week 2, ~2 hours):**
- Trivy filesystem scan (dependency vulnerabilities, both ecosystems) → SARIF upload
- Trivy container image scan (post Docker build, CRITICAL/HIGH only)
- License compliance checks (weekly scheduled workflow)
- SonarCloud integration (if you choose to set it up)

| Metric | Value |
|--------|-------|
| Setup effort | Phase 1: 2 hours; Phase 2: 2 hours |
| Ongoing cost | $0 (all free for public repos; unlimited GitHub Actions minutes) |
| CI time per PR | ~5–8 min (path-filtered, parallelized) |
| Noise level | Low — CodeQL and Trivy with severity filters produce actionable findings |

### Layer 4 — Dashboard and trends

**Recommendation: SonarCloud, with caveats.** Enable it for the AI-generated code security analysis, duplication detection, and coverage tracking — not for the code smell counts that overlap with your existing tools. The MCP server integration creating a feedback loop with Claude Code is the unique value proposition. If you want the analysis without the platform, install `SonarAnalyzer.CSharp` NuGet + `eslint-plugin-sonarjs` instead.

| Metric | Value |
|--------|-------|
| Setup effort | SonarCloud: 2–3 hours; NuGet-only alternative: 15 min |
| Ongoing cost | $0 (free for open source) |
| Noise level | Medium initially, low after tuning |

### Layer 5 — The human layer

You are the irreplaceable component. With all the automation above handling mechanical correctness, formatting, simple security patterns, and surface-level code quality, your review time is freed for what only a human can assess.

**Your review checklist for every AI-generated PR:**
1. Does this feature match my intent? (Read the PR summary from CodeRabbit first)
2. Does the architecture follow established patterns, or did Claude invent something new?
3. Would these tests fail if the feature broke? (Check for mocked implementations, trivial assertions)
4. Any auth, payment, or user-input code? → Manual security review, always
5. Did Claude change anything I didn't ask for? (Speculative changes are the most common AI footgun)
6. Are all imports and dependencies real? (Hallucinated packages are documented)

**Time budget:** 10–20 minutes per PR for human review. With 30–45 minute Claude Code sessions producing one PR each, this means roughly **one-third of your time is review** — which is exactly the right ratio when 100% of your code is AI-generated.

### Total pipeline cost summary

| Layer | Setup (one-time) | Monthly cost | Noise |
|-------|-------------------|-------------|-------|
| Pre-commit (Lefthook) | 1–2 hours | $0 | Low |
| PR review (CodeRabbit + Claude Action) | 35 min | $2–8 | Medium |
| CI gates (CodeQL + Dependabot + Trivy + Codecov) | 4 hours | $0 | Low |
| Dashboard (SonarCloud) | 2–3 hours | $0 | Medium, then low |
| Human review | 0 (you already do this) | Your time | N/A |
| **Total** | **~8 hours** | **$2–8/month** | — |

The entire quality pipeline costs under $10/month and a single day of setup. Every tool is free for open source except Claude Code API calls. The investment yields a verification system that catches the mechanical ~45% of AI-generated bugs automatically, flags security hotspots, enforces consistency through deterministic analysis, and focuses your irreplaceable human attention on the architectural and business-logic layer where AI review is structurally blind.