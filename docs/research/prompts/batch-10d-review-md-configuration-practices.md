# Research Prompt: Batch 10d — R-024
# REVIEW.md Configuration Best Practices for AI Code Review

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: How to write effective REVIEW.md configuration files for AI-powered code review systems

Context: I'm configuring a hierarchical REVIEW.md system for an AI code review tool (deep-review) that dispatches specialized AI agents to review PRs across 7 dimensions: bugs, security, cross-file-impact, tests, conventions, types, and simplification. The system supports:

- **Root REVIEW.md** — applies to all files in the repository
- **Subdirectory REVIEW.md** — overrides/extends root config for specific directories (co-located with CLAUDE.md files)
- **Hierarchical inheritance:** settings (confidence threshold, severity threshold, model tier) override; rules, skip patterns, and ignore patterns accumulate

Configuration sections available:
```
## Focus          — restrict to specific review dimensions (list)
## Skip           — glob patterns for files to exclude
## Rules          — natural-language rules applied to all review agents
## Severity Threshold — minimum severity to include (critical/high/medium/low)
## Confidence Threshold — post-validation confidence minimum (0-100, default 80)
## Max Findings   — cap on total findings (0 = unlimited)
## Model Tier     — optimized (Sonnet default) or frontier (Opus for reasoning-heavy agents)
## Ignore         — suppress known false positives (dimension:"pattern" format)
```

My project: RunCoach — AI-powered running coach. .NET 10 backend + React 19 frontend monorepo. Three REVIEW.md files planned:
1. **Root** — cross-cutting: security, architecture principles, git conventions, CI standards
2. **backend/** — .NET/C#/EF Core/xUnit/eval infrastructure rules
3. **frontend/** — React/TypeScript/RTK Query/Tailwind/Zod rules

I want to maximize review quality (frontier model tier, low severity threshold, unlimited findings, all dimensions enabled). The question is how to write the best possible rules.

What I need to learn:

### 1. Writing Effective Natural-Language Review Rules
- How should rules be phrased for AI agents? Specific and prescriptive ("All async methods must accept CancellationToken") vs. directional ("Prefer immutable types")?
- What makes a rule actionable vs. noise? How do you write rules that reduce false positives?
- Should rules include the "why" (rationale) or just the "what" (the rule itself)?
- Should rules include severity hints? (e.g., "CRITICAL: Never expose secrets in config files")
- How long should individual rules be? One sentence? A paragraph with examples?
- What's the optimal number of rules per file before signal-to-noise degrades? 10? 25? 50? 100?
- Should rules be organized by category (security, performance, style) or flat list?

### 2. Hierarchical Configuration Design
- When should a rule go in root vs subdirectory REVIEW.md?
  - Root: security rules, architecture principles, git/CI conventions
  - Subdirectory: technology-specific patterns, library-specific anti-patterns
- How to avoid rule conflicts between root and subdirectory? (since rules accumulate, not override)
- Should subdirectory rules reference root rules? ("In addition to root security rules, also check...")
- How granular should subdirectories go? `backend/` level is clear, but what about `backend/Modules/Coaching/`?
- What's the maintenance burden of hierarchical configs? When does it become more trouble than it's worth?

### 3. Skip Patterns Strategy
- What files should always be skipped? (generated code, lock files, migration files, binary fixtures)
- Should test files be skipped or reviewed with different rules?
- How to handle "review this file but only for security" vs "skip this file entirely"?
- Common skip pattern mistakes: too broad (skipping all tests), too narrow (missing generated files)

### 4. Confidence and Severity Tuning
- Default confidence threshold is 80. Security auto-floors at 70. When should you lower these?
- Is it better to start strict (high threshold) and lower as you trust the system, or start loose and tighten?
- How do different severity thresholds affect review quality? Is `low` (report everything) actually useful, or does it create noise?
- Should backend and frontend have different thresholds?

### 5. Ignore Pattern Best Practices
- Format: `dimension:"pattern"` — how specific should patterns be?
- Should you pre-populate ignore patterns for known framework patterns? (e.g., `conventions:"file naming"` for generated files)
- How to manage ignore pattern growth over time? When to audit and clean up?
- Date-stamped comments — useful for tracking when dismissals were made?

### 6. Cross-Reference with Other Config Systems
- How do successful teams configure CodeRabbit's `path_instructions`? What rules work well?
- ESLint rule design: what makes ESLint rules effective? (specificity, auto-fixability, clear error messages)
- Greptile's review config: what patterns emerged from their hierarchical approach?
- SonarQube quality profiles: how do they categorize rules (reliability, security, maintainability)?
- What can we learn from code review checklists used by Google, Microsoft, Meta?

### 7. Monorepo-Specific Considerations
- Backend (.NET) and frontend (React/TypeScript) have very different review needs
- Should the root REVIEW.md avoid technology-specific rules entirely? Or include the most critical ones from both?
- How to handle shared concerns (API contracts, types that cross the boundary)?
- Cross-file impact: how to configure the reviewer to understand backend-frontend dependencies?

### 8. Evolving REVIEW.md Over Time
- How should REVIEW.md files evolve as the project matures?
  - Early stage: focus on architecture consistency and security
  - Mid stage: add performance rules, test quality rules
  - Late stage: add backwards compatibility rules, API stability rules
- Should you version REVIEW.md changes in the decision log?
- How to measure REVIEW.md effectiveness? (false positive rate, missed bugs in production, developer satisfaction)

### 9. Anti-Patterns in Review Configuration
- Over-specification: so many rules that every PR gets 50 findings and developers ignore them all
- Under-specification: rules so vague ("write good code") that they produce inconsistent results
- Stale rules: rules that applied to an older version of the codebase but no longer match
- Contradictory rules: root says "prefer immutable" but subdirectory says "use mutable for performance"
- Copy-paste rules: duplicating the same rule across multiple REVIEW.md files instead of using root

### 10. Example REVIEW.md Files from the Wild
- Are there any open-source repositories with well-crafted REVIEW.md (or equivalent) files?
- What do the best CodeRabbit configurations look like?
- How do companies like Vercel, Supabase, or other developer-tool companies configure their AI review tools?
- Any case studies on AI code review configuration effectiveness?

Output I need:
- A framework for deciding where each rule belongs (root vs subdirectory)
- Rule writing guidelines: templates, length, specificity, severity hints
- Recommended structure for each REVIEW.md file (section ordering, categorization)
- A "rule audit checklist" for periodically reviewing REVIEW.md quality
- Concrete examples of well-written vs poorly-written review rules
- Recommended starting configuration for a new project (what to include day 1 vs add later)
- Signal-to-noise analysis: how many rules per file is optimal for AI reviewers
