# T20 Proof Summary

## Task
FIX test-8: Add test for SafetyRubricEvaluator JsonException fallback path

## What Was Done
Added unit test `JudgeAsync_MalformedJson_ReturnsSyntheticDeserializationErrorVerdict` to
`SafetyRubricEvaluatorTests.cs` that exercises the `JsonException` catch block in
`SafetyRubricEvaluator.JudgeAsync` (lines 87-103).

The test:
1. Mocks `IChatClient` via NSubstitute to return non-JSON text ("This is not valid JSON at all.")
2. Calls `JudgeAsync` which triggers `JsonException` during deserialization
3. Verifies the synthetic `SafetyVerdict` fallback structure:
   - `OverallScore` is `0.0`
   - `OverallReason` contains "deserialization failed"
   - Single criterion with `CriterionName` = "deserialization_error"
   - `Passed` = `false`
   - `Evidence` contains "Failed to deserialize judge response"

## Proof Artifacts

| # | File | Type | Status |
|---|------|------|--------|
| 1 | T20-01-test.txt | test | PASS |
| 2 | T20-02-cli.txt | cli | PASS |

## File Modified
- `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/SafetyRubricEvaluatorTests.cs`
