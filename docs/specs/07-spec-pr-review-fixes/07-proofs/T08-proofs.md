# T08 Proof Summary: AssembleAsync null guard test

## Task
FIX test-10: Add test for AssembleAsync null input guard (contested)

## What Changed
Added `AssembleAsync_NullInput_ThrowsArgumentNullException` test to `ContextAssemblerTests.cs`. This test validates the `ArgumentNullException.ThrowIfNull(input)` guard on line 89 of `ContextAssembler.cs`.

While nullable reference types provide compile-time protection, the runtime guard exists as defense-in-depth for callers that suppress nullability warnings (e.g., `null!`). The test documents this contract explicitly.

## Files Modified
- `backend/tests/RunCoach.Api.Tests/Modules/Coaching/ContextAssemblerTests.cs` — added 1 test

## Proof Artifacts
| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | T08-01-test.txt | PASS |
| 2 | cli  | T08-02-cli.txt  | PASS |

## Verification
- All 315 tests pass (0 failures, 0 skipped)
- Build succeeds with 0 warnings, 0 errors
