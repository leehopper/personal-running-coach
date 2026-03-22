# Code Review Report

**Reviewed**: 2026-03-22
**Branch**: feature/poc1-eval-refactor
**Base**: 3e1e54c (pre-spec-03 baseline)
**Commits**: 8 commits, 20 implementation files changed
**Overall**: CHANGES REQUESTED

## Summary

- **Blocking Issues**: 1 (A+B: ReplayGuardChatClient not wired into pipeline)
- **Advisory Notes**: 8
- **Files Reviewed**: 9 implementation files + CI config
- **FIX Tasks Created**: #30

## Review Methodology

**Approach**: Concern-partitioned team review
**Reviewers**: 3 specialized agents

| Reviewer | Concern | Primary Category | Status |
|----------|---------|-----------------|--------|
| security-reviewer | Security | B | Completed |
| correctness-reviewer | Correctness | A | Completed |
| spec-reviewer | Spec Compliance | C + D | Completed |

**Challenge Round**: Not triggered (< 3 blocking findings)

**Cross-reviewer agreement**: All 3 reviewers independently flagged the same primary issue (ReplayGuardChatClient not wired), providing high confidence.

## Blocking Issues

### [ISSUE-1] Category A+B: ReplayGuardChatClient not wired into Replay pipeline
- **File**: `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/EvalTestBase.cs:317-338`
- **Severity**: Blocking
- **Concern**: All 3 reviewers (unanimous)
- **Description**: `ReplayGuardChatClient` is implemented, tested, and documented — but `CreateReplayConfig()` never uses it. On cache miss in Replay mode, the dummy `AnthropicClient("replay-mode-no-key")` attempts a real outbound HTTP call. Mitigated by MaxRetries=0 and Timeout=1s, but: (a) the error message is opaque instead of the spec-mandated descriptive message, (b) CI makes unintended network calls.
- **Fix**: Wire `ReplayGuardChatClient` into the client chain while preserving cache key metadata compatibility. Also: fail-fast when `EVAL_CACHE_MODE=Record` but no API key available.
- **Task**: FIX-REVIEW #30

## Advisory Notes

### [NOTE-1] Category B: pull_request trigger widened
- **File**: `.github/workflows/ci.yml:4-6`
- **Description**: Removed `branches: [main]` from `pull_request` trigger. Safe since `pull_request` doesn't expose secrets to forks, but reduces defense-in-depth if ever changed to `pull_request_target`.
- **Suggestion**: Add a comment noting this is intentional and that `pull_request_target` should not be used.

### [NOTE-2] Category A: Record mode silently skips without API key
- **File**: `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/EvalTestBase.cs:59-97`
- **Description**: Explicit `EVAL_CACHE_MODE=Record` with no API key silently falls through — configs stay null, tests skip. Should throw to alert developer.
- **Suggestion**: Included in FIX task #30.

### [NOTE-3] Category A: coverlet.msbuild + MTP compatibility
- **File**: `.github/workflows/ci.yml:59`
- **Description**: coverlet.msbuild with xUnit v3 MTP is relatively new. Verified locally but worth monitoring first CI run.

### [NOTE-4] Category C: Validation report lists 5 commits but 8 exist
- **File**: `docs/specs/03-spec-eval-cache-ci/03-validation-eval-cache-ci.md`
- **Description**: Evidence appendix lists 5 implementation + 1 spec commits but 3 more follow-up commits (T06-T08) exist. Minor documentation gap.

### [NOTE-5] Category D: Comment inaccuracy
- **File**: `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/EvalTestBase.cs:327-328`
- **Description**: Comment says "we wrap the whole chain with a ReplayGuardChatClient" but code doesn't. Will be fixed as part of FIX #30.

### [NOTE-6-8] Spec deviations (all justified)
- 16 vs 17 eval tests (spike deleted per user request)
- dotnet-tools.json at root vs .config/ (SDK discovers both)
- CancellationToken scope broader than spec (user directed)

## Files Reviewed

| File | Status | Issues |
|------|--------|--------|
| `EvalTestBase.cs` | Modified | 1 blocking (ReplayGuardChatClient not wired) |
| `ReplayGuardChatClient.cs` | New | Clean (correctly implemented, just not used) |
| `EvalCacheMode.cs` | New | Clean |
| `AnthropicStructuredOutputClient.cs` | Modified | Clean (SplitMessages + ConvertSchema fixes correct) |
| `EvalTestBaseTests.cs` | Modified | Clean (thorough test coverage) |
| `ci.yml` | Modified | 1 advisory (trigger widening) |
| `Directory.Packages.props` | Modified | Clean |
| `RunCoach.Api.Tests.csproj` | Modified | Clean |
| `dotnet-tools.json` | New | Clean |

## Checklist

- [x] No hardcoded credentials or secrets
- [x] Error handling at system boundaries (partial — Record mode silent skip)
- [x] Input validation on user-facing endpoints (N/A — test infrastructure)
- [x] Changes match spec requirements (20/20 addressed, 1 partially met)
- [x] Follows repository patterns and conventions
- [x] No obvious performance regressions
- [ ] ReplayGuardChatClient wired into pipeline (FIX #30)

---
Review performed by: Claude Opus 4.6 (concern-partitioned team, 3 reviewers)
