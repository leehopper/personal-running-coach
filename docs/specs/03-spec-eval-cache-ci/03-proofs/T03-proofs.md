# T03: Commit Cache Files & CI Configuration — Proof Summary

## Results

| # | Type | Description | Status |
|---|------|-------------|--------|
| 1 | file | `.gitignore` no longer excludes `poc1-eval-cache/` | PASS |
| 2 | file | 22 cache scenario directories committed (17 sonnet + 5 haiku) | PASS |
| 3 | file | CI: `pull_request` has no branches filter; `EVAL_CACHE_MODE: Replay` set | PASS |

## Changes

- `.gitignore`: Removed `poc1-eval-cache/` exclusion, kept `poc1-eval-results/`
- `ci.yml`: Removed `branches: [main]` from `pull_request` trigger (all PRs get CI)
- `ci.yml`: Added `EVAL_CACHE_MODE: Replay` env var to backend test step
- `ci.yml`: Removed VSTest-specific `--collect:"XPlat Code Coverage"` (incompatible with MTP)
- Committed 22 cache scenario directories as golden test fixtures
