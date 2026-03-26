# T01 Proof Summary: Add stop_reason check in ClaudeCoachingLlm

## Task
FIX bug-1: Add stop_reason check in ClaudeCoachingLlm

## Changes
- Added truncation detection in both `GenerateAsync` and `GenerateStructuredAsync`
  that throws `InvalidOperationException` when `stop_reason` is `max_tokens`
- Uses SDK's `StopReason.MaxTokens` enum for type-safe comparison (avoids
  string comparison issues with the Anthropic SDK's `ApiEnum` type)
- Added 3 new unit tests verifying the behavior

## Files Modified
- `backend/src/RunCoach.Api/Modules/Coaching/ClaudeCoachingLlm.cs` (implementation)
- `backend/tests/RunCoach.Api.Tests/Modules/Coaching/ClaudeCoachingLlmTests.cs` (tests)

## Proof Artifacts
| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | T01-01-test.txt | PASS |
| 2 | cli  | T01-02-cli.txt  | PASS |

## Verification
- All 302 tests pass (3 new + 299 existing)
- Build succeeds with 0 warnings
