# T02 Proof Summary

## Task
TEST: Add missing required field deserialization test for GenerateStructuredAsync

## Description
Added a test verifying that valid JSON missing required fields (goal_description, phases, rationale, warnings) throws `JsonException` during deserialization in `GenerateStructuredAsync<MacroPlanOutput>`. The test sends `{"total_weeks": 12}` which is syntactically valid JSON but missing the `required` properties defined on the `MacroPlanOutput` record.

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | T02-01-test.txt | PASS |
| 2 | cli  | T02-02-cli.txt  | PASS |

## Files Modified
- `backend/tests/RunCoach.Api.Tests/Modules/Coaching/ClaudeCoachingLlmTests.cs` — added 1 new test

## Verification
- Build: 0 errors, 0 warnings
- Tests: 349 passed, 9 failed (all pre-existing Eval cache-miss failures)
- New test passes and confirms JsonException on missing required fields
