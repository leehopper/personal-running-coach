# T07 Proof Summary: Add test for null literal deserialization path

## Task
FIX test-2: Consider test for null literal deserialization path (contested)

## Changes
- Added test `GenerateStructuredAsync_ThrowsInvalidOperationException_WhenJsonIsNullLiteral`
  that mocks the API returning the JSON literal `"null"`, verifying the `??` throw guard
  at `ClaudeCoachingLlm.cs:198-200` raises `InvalidOperationException`
- Test comment documents that constrained decoding makes this structurally unreachable
  in production, but the guard prevents silent null propagation if the invariant breaks

## Files Modified
- `backend/tests/RunCoach.Api.Tests/Modules/Coaching/ClaudeCoachingLlmTests.cs` (test only)

## Proof Artifacts
| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | T07-01-test.txt | PASS |
| 2 | cli  | T07-02-cli.txt  | PASS |

## Verification
- All 306 tests pass (1 new + 305 existing)
- Build succeeds with 0 warnings
