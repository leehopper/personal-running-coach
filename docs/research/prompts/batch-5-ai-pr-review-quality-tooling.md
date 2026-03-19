# Research Prompt: Batch 5 — R-012
# AI-Powered PR Review and Code Quality Tooling for AI-Assisted Development

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

I'm a solo developer building an AI-powered running coach application. My primary development tool is Claude Code (Anthropic's CLI agent) — virtually all code in the project will be AI-generated. I need to design a comprehensive code quality pipeline that enforces standards deterministically at every stage, because the core finding from my prior research is that text-based rules alone don't prevent AI agents from producing inconsistent or low-quality code under pressure.

### About the project and tech stack

- **Backend:** .NET 10 / ASP.NET Core / C# 14, EF Core, Marten (event sourcing), Wolverine (message processing), PostgreSQL
- **Frontend:** React 19 + TypeScript (strict mode), Vite, Redux Toolkit + RTK Query, Tailwind CSS + shadcn/ui
- **Testing:** xUnit + FluentAssertions + NSubstitute (backend), Vitest + React Testing Library (frontend), Playwright (E2E)
- **Infra:** Docker Compose + Tilt (local dev), Colima (container runtime), GitHub Actions (CI/CD)
- **Repo:** Monorepo, open source (to access free tiers of quality tools)
- **Development pattern:** Plan-first cycle per feature. Claude Code writes all code. Human reviews diffs and runs the pipeline. Sessions capped at 30-45 minutes.

### Quality layers already decided

I've already committed to these at the build/lint level:
- **Backend:** EditorConfig + .NET Analyzers + StyleCop Analyzers + Central Package Management
- **Frontend:** ESLint + Prettier + Biome lint rules
- **Claude Code hooks:** PostToolUse hooks that run formatters and linters after every file edit; PreToolUse hooks that block dangerous bash commands

What I need researched is everything ABOVE these — the PR review, pre-commit, and CI layers.

### What I need researched

**1. AI-powered PR review tools — comprehensive comparison**

Research every significant AI PR review tool available as of March 2026. I know about at least these, but there may be others:

- **CodeRabbit** — AI-powered code review bot for GitHub/GitLab
- **Anthropic's Claude Code Review** — Anthropic's official GitHub Action for PR review (announced early 2026)
- **Codacy** — automated code review with AI features
- **Qodo (formerly CodiumAI)** — AI code review and test generation
- **GitHub Copilot code review** — GitHub's native AI review feature
- **Sourcery** — AI code review focused on Python (may not apply)
- **Ellipsis** — AI PR reviewer
- **Bito** — AI code review
- Any others that have emerged

For each tool, I need:
- What it actually does (line-level review, security scanning, style enforcement, architectural feedback, test suggestions?)
- Pricing — especially free tier for open source vs. private repos
- How it integrates (GitHub Action, bot, app?)
- Quality of reviews — are they useful or noisy? What do practitioners say?
- Configuration depth — can you customize review rules, ignore patterns, focus areas?
- **Critical question:** How well does it work when reviewing AI-generated code specifically? AI-reviewing-AI is a different use case than AI-reviewing-human. The failure modes differ — AI code tends to be syntactically correct but may have subtle architectural drift, unnecessary complexity, missed edge cases, or gradual inconsistency across sessions. Does the tool catch these patterns?

I want a clear recommendation for which tool (or combination) is best for a solo developer whose code is AI-generated, working on a .NET + React monorepo.

**2. Pre-commit hook ecosystem and best practices**

Research the current state of pre-commit hooks for a .NET + React monorepo:

- **Husky** — is it still the standard for monorepo git hooks? Alternatives?
- **lint-staged** — how does it work with both .NET and JavaScript/TypeScript files in the same repo?
- **commitlint / conventional commits** — what's the current best practice for enforcing commit message standards? Is this valuable when most commits are AI-generated?
- **dotnet format** and **dotnet build** as pre-commit checks — how to configure for speed (only changed files)?
- **.NET-specific pre-commit tools** — are there tools like `dotnet-outdated`, security scanners, or analyzers that should run pre-commit vs. in CI?
- **Cross-stack coordination** — what's the pattern for a monorepo where pre-commit needs to run different tools depending on which files changed (.cs files → .NET tools, .tsx files → JS tools)?

**3. CI/CD quality gates beyond testing**

Research what should run in the GitHub Actions CI pipeline beyond the test suite:

- **Security scanning:** CodeQL (GitHub native) vs. Snyk vs. Dependabot vs. others for both .NET and npm dependency vulnerability scanning. What's free for open source? What catches real issues vs. noise?
- **Code coverage:** What's the current best tool for combined .NET + React coverage reporting in CI? Is there a threshold that makes sense for a new project?
- **License compliance:** Any lightweight tools for checking dependency licenses in both ecosystems?
- **Container scanning:** Should Docker images be scanned? What's lightweight and free?
- **Performance regression:** Any tools that catch performance regressions in CI for .NET APIs?

**4. SonarQube / SonarCloud — honest assessment**

I'm specifically considering whether SonarQube (self-hosted) or SonarCloud (hosted) adds enough value over the built-in .NET analyzers + StyleCop + ESLint I already have. Research:

- What does SonarQube/SonarCloud catch that the built-in tools don't?
- What's the actual setup and maintenance cost (self-hosted vs. cloud)?
- Is there a meaningful difference for a new project vs. a legacy codebase?
- Are there lighter alternatives that provide the "dashboard + trends" value without the full SonarQube overhead?
- Honest practitioner opinions — do solo developers and small teams actually find it valuable, or is it enterprise theater?

**5. The "AI-reviewing-AI" problem**

This is the meta-question underlying everything above. When Claude Code writes code and then another AI tool reviews it:

- What are the documented failure modes? Do AI reviewers miss the same things AI authors miss?
- Are there patterns where AI-generated code consistently fools AI reviewers?
- What's the practitioner consensus on the value of AI PR review when the code is AI-authored?
- Is human review of AI diffs still the irreplaceable layer, with AI review as supplement?
- Are there any studies, blog posts, or practitioner reports on this specific dynamic?

### What "good" looks like for this research

Actionable recommendations, not a catalog. For each tool or approach:
- Does it add real value for my specific setup (solo dev, AI-generated code, .NET + React monorepo, open source)?
- What's the cost (money, setup time, ongoing maintenance, CI minutes)?
- How noisy is it? (A tool that generates 50 low-value comments per PR is worse than no tool.)
- Would you actually recommend it, or is it technically interesting but not worth the overhead?

Prioritize honest practitioner experience over marketing claims. If a tool sounds great in docs but practitioners report it's noisy or doesn't catch real issues, I want to know that.

### Output format

Structure as a single research document with sections matching the five topics above. End with a **"Recommended Quality Pipeline"** section that synthesizes everything into a concrete, layered recommendation:

1. **Pre-commit layer** — exactly what runs, how it's configured
2. **PR review layer** — which tool(s), how configured, what to expect
3. **CI quality gates** — what checks run in GitHub Actions, in what order
4. **Dashboard / trends** (if warranted) — whether SonarCloud or alternatives are worth it
5. **The human layer** — what the human reviewer should focus on given all the automation above

For each layer, note the setup effort (hours), ongoing cost ($), and expected noise level (low/medium/high).
