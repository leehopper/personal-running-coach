# T19 Proof Summary

## Task
FIX test-2: Add test for unrecoverable token budget overflow in ContextAssembler

## Description
Added a test that documents the behavior when start sections (user_profile, goal_state,
fitness_estimate, training_paces) alone exceed the 15K token budget. The overflow cascade
only reduces middle/end sections and cannot recover from pathologically large start data.

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | T19-01-test.txt | PASS |
| 2 | cli  | T19-02-cli.txt  | PASS |

## Changes Made
- **Modified**: `backend/tests/RunCoach.Api.Tests/Modules/Coaching/ContextAssemblerTests.cs`
  - Added test: `AssembleAsync_StartSectionsAloneExceedBudget_ReturnsOverBudgetWithoutError`
  - Added helper: `BuildPathologicallyLargeProfileInput()` (250 injuries, 130 races, 50 constraints)

## Key Finding
The overflow cascade in `ContextAssembler.ApplyOverflowCascade` exhausts all 5 steps but
cannot reduce start sections. This is by design -- start sections contain critical athlete
context. The test documents this known limitation for future consideration (e.g., adding
profile summarization or start section truncation in a future iteration).

## Verification
- Build: 0 errors, 0 warnings
- Tests: 318 passed, 0 failed, 0 skipped
