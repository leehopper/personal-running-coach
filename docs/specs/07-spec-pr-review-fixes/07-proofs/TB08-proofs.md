# TB08 Proof Summary: Add SafetyVerdict to JsonSchemaHelper parameterized tests

## Task
TEST task-8: Add [InlineData(typeof(SafetyVerdict))] to GenerateSchema_AllObjectNodes_HaveAdditionalPropertiesFalse and GenerateSchema_NestingDepth tests. Covers decimal and bool schema treatment used by SafetyRubricEvaluator.

## What Changed
Added SafetyVerdict as a new InlineData case to two parameterized Theory tests in `JsonSchemaHelperTests.cs`:

1. `GenerateSchema_AllObjectNodes_HaveAdditionalPropertiesFalse` -- verifies additionalProperties: false on all object nodes (SafetyVerdict root + nested SafetyCriterionResult)
2. `GenerateSchema_NestingDepth_DoesNotExceedLimit` -- verifies nesting depth is exactly 2 (SafetyVerdict -> Criteria array -> SafetyCriterionResult), within the absolute max of 3

This covers decimal (`OverallScore`) and bool (`Passed`) schema treatment that was previously untested.

## Files Modified
- `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Models/Structured/JsonSchemaHelperTests.cs` -- added 2 InlineData entries

## Proof Artifacts
| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | TB08-01-test.txt | PASS |
| 2 | cli  | TB08-02-cli.txt  | PASS |

## Verification
- 329 total tests (320 pass, 9 pre-existing eval cache miss failures)
- 2 new test cases pass (SafetyVerdict in both parameterized tests)
- Build succeeds with 0 warnings, 0 errors
