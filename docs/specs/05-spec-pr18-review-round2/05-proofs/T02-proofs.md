# T02 Proof Summary: Eval Cache TTL Fix

## Task
Extend cache fixture expiration to prevent silent CI breakage (14-day TTL time bomb).

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | cli  | T02-01-cli.txt  | PASS |
| 2 | file | T02-02-file.txt | PASS |
| 3 | file | T02-03-file.txt | PASS |

## Summary

- **T02-01-cli**: All 44 `entry.json` files now have `"expiration": "9999-12-31T23:59:59Z"`. Zero files retain the old April 2026 expiration dates.
- **T02-02-file**: `.gitattributes` created at repo root with `backend/poc1-eval-cache/**/*.data binary` entry.
- **T02-03-file**: `EvalTestBase.cs` contains a detailed re-recording workflow comment documenting when to re-record, how to run, and how to extend TTL.

## Build Verification

- `dotnet build`: 0 warnings, 0 errors (verified before and after changes)
