# T13 Proof Summary: Standardize edition citations

## Task
FIX conv-3: Standardize edition citations to 4th edition across VdotCalculator and PaceCalculator.

## Change
Updated VdotCalculator.cs XML doc comment from "3rd edition" to "4th edition" to match PaceCalculator.cs which already cited "4th edition".

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | cli  | T13-01-cli.txt  | PASS |
| 2 | test | T13-02-test.txt | PASS |

## Verification
- All three edition references in the codebase now consistently cite "4th edition"
- Build passes with zero warnings
- All tests pass
