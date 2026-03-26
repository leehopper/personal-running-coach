# T24 Proof Summary: Add CancellationToken parameter to WriteEvalResultAsync

## Task
FIX conv-2: Add CancellationToken parameter to WriteEvalResultAsync

## Changes Made
- Added `CancellationToken ct = default` parameter to `EvalTestBase.WriteEvalResultAsync`
- Forwarded `ct` to `File.WriteAllTextAsync` call
- Updated all 11 call sites to pass `TestContext.Current.CancellationToken`

## Files Modified
1. `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/EvalTestBase.cs` - Method signature + body
2. `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/SafetyBoundaryEvalTests.cs` - 5 call sites
3. `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/PlanGenerationEvalTests.cs` - 5 call sites
4. `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/EvalTestBaseTests.cs` - 1 call site

## Proof Artifacts
| # | Type | Status | File |
|---|------|--------|------|
| 1 | file | PASS | T24-01-file.txt |
| 2 | cli | PASS | T24-02-cli.txt |

## Result
All proofs PASS. Build succeeds with 0 errors, all 326 tests pass.
