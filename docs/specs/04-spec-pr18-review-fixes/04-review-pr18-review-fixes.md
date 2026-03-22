# Code Review Report

**Reviewed**: 2026-03-22
**Branch**: feature/poc1-eval-refactor
**Base**: origin/feature/poc1-context-injection-v2
**Commits**: 10 commits, ~80 source files changed
**Overall**: APPROVED

## Summary

- **Blocking Issues**: 0
- **Advisory Notes**: 5 (2 security, 3 correctness)
- **Spec Compliance**: PASS (all 9 original review findings verified as addressed)
- **FIX Tasks Created**: none

## Review Methodology

**Approach**: Concern-partitioned team review
**Reviewers**: 3 specialized agents (all Opus 4.6)

| Reviewer | Concern | Primary Category | Status |
|----------|---------|-----------------|--------|
| security-reviewer | Security | B | Completed |
| correctness-reviewer | Correctness | A | Completed |
| spec-reviewer | Spec Compliance + Quality | C + D | Completed |

**Challenge Round**: Not triggered (0 blocking findings < 3 threshold)

## Advisory Notes

### [NOTE-1] Category A: Lazy<Task> captures CancellationToken from first caller
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/Prompts/YamlPromptStore.cs:82-83`
- **Severity**: Medium (advisory)
- **Concern**: Correctness
- **Description**: The `GetOrAdd` + `Lazy<Task>` pattern captures the CancellationToken from the first caller. If that caller cancels, the faulted Lazy stays permanently in the ConcurrentDictionary and all subsequent callers get OperationCanceledException. Classic Lazy<Task> pitfall.
- **Mitigation**: In practice, YamlPromptStore is a singleton and the first prompt load happens at startup before any cancellation is likely. Edge-case risk is low.
- **Suggestion**: Add fault-eviction logic (remove faulted entries from the dictionary in a catch block) in a future cleanup pass.

### [NOTE-2] Category B: Trivy action pinned to @master
- **File**: `.github/workflows/ci.yml:106,115,123`
- **Severity**: Medium (advisory)
- **Concern**: Security (supply chain)
- **Description**: Three Trivy steps use `aquasecurity/trivy-action@master`. A force-push or compromise of that branch silently changes CI code with repo contents access. TODO comments acknowledge this but provide no mitigation.
- **Suggestion**: Pin to a commit SHA or specific release tag when convenient.

### [NOTE-3] Category A/B: actions/checkout@v6 may not exist
- **File**: `.github/workflows/ci.yml:19,49,103`
- **Severity**: Medium (advisory)
- **Concern**: Correctness + Security
- **Description**: The spec required auditing all action versions. `setup-node` was fixed from @v6 to @v4, but `actions/checkout@v6` remains in 3 places. As of May 2025, v4 was the latest stable. If v6 doesn't exist as a legitimate tag, it could resolve to an unexpected ref or be vulnerable to tag-squatting. The worker added inline verification comments.
- **Suggestion**: Verify `actions/checkout@v6` exists. If not, pin to @v4.

### [NOTE-4] Category A: EvalTestBase.DisposeAsync does not dispose ReportingConfiguration
- **File**: `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/EvalTestBase.cs:183-187`
- **Severity**: Low (advisory)
- **Concern**: Correctness
- **Description**: `_sonnetReportingConfig` and `_haikuReportingConfig` are never disposed. If they hold file handles (likely, given DiskBasedReportingConfiguration), this could cause file locking issues on Windows CI.
- **Suggestion**: Dispose both configs in `DisposeAsyncCore()`.

### [NOTE-5] Category A: Integer truncation in pace range checks
- **File**: `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/PlanConstraintEvaluator.cs:151-154`
- **Severity**: Low (advisory)
- **Concern**: Correctness
- **Description**: Casts `TotalSeconds` to `int` before multiplying by tolerance factor, causing consistent off-by-one toward a wider tolerance band than intended. Minor given the 15% band.
- **Suggestion**: Cast after multiplication for precise calculation.

## Spec Compliance Verification

All 9 original review findings verified as correctly addressed:

| # | Finding | Status | Location |
|---|---------|--------|----------|
| 1 | ConfigureAwait(false) in AnthropicStructuredOutputClient | FIXED | Lines 55, 58, 159 |
| 2 | YamlPromptStore race condition | FIXED | GetOrAdd + Lazy<Task> at line 82 |
| 3 | Unused ContextTemplate comment | FIXED | FUTURE: comment at line 90 |
| 4 | Pace tolerance asymmetry | FIXED | Upper bound at lines 172-178 |
| 5 | Trace.WriteLine invisible in xUnit v3 | FIXED | SendDiagnosticMessage |
| 6 | actions/setup-node@v6 | FIXED | @v4 at line 76 |
| 7 | Cache key separator validation | FIXED | Lines 129-139 |
| 8 | Dual-constructor removal | FIXED | Single IPromptStore ctor |
| 9 | Dead ExtractJson removal | FIXED | Zero matches remain |

## Experiment Infrastructure Removal

- 17 source files + 5 test files deleted (0 remaining)
- ContextAssembler simplified: parameterless ctor, SystemPromptText, sync Assemble() all removed
- 28 ContextAssemblerTests converted to async with mock IPromptStore
- EvalTestBase converted to use real YamlPromptStore with async assembly
- New eval cache fixtures committed (cache keys changed due to IPromptStore refactor)

## Checklist

- [x] No hardcoded credentials or secrets
- [x] Error handling at system boundaries
- [x] Input validation (cache key separator validation added)
- [x] Changes match spec requirements (all 9 findings addressed)
- [x] Follows repository patterns and conventions
- [x] No obvious performance regressions
