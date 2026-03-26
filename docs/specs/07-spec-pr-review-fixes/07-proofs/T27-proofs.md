# T27 Proof Summary: Remove duplicate XML doc sentences in ClaudeCoachingLlm constructors

## Task

Remove duplicate "Initializes a new instance" opening sentences from both constructor XML doc blocks in `ClaudeCoachingLlm.cs`.

## Root Cause

Both constructors had two sentences starting with "Initializes a new instance" -- one generic (SA1642-required) and one specific. The StyleCop SA1642 analyzer requires constructor summaries to begin with "Initializes a new instance of the <see cref="ClassName"/> class." and `dotnet format` auto-inserts this line if missing, so simply deleting it causes the hook to re-add it.

## Fix

Merged the duplicate lines into a single sentence that satisfies SA1642 while incorporating the specific description. Changed the period after "class" to a line-continuation, flowing the specific info into the same sentence.

## Changes

- **File modified:** `backend/src/RunCoach.Api/Modules/Coaching/ClaudeCoachingLlm.cs`
- Constructor 1: Merged into "Initializes a new instance of the <see cref="ClaudeCoachingLlm"/> class / using dependency-injected settings and logger."
- Constructor 2: Merged into "Initializes a new instance of the <see cref="ClaudeCoachingLlm"/> class / with an externally provided client for testing with a mock/substitute."

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | file | T27-01-file.txt | PASS |
| 2 | cli  | T27-02-cli.txt  | PASS |

## Verification

- `dotnet format` reports "Formatted 0 of 91 files" (SA1642 satisfied)
- `dotnet build` succeeds with 0 errors, 0 warnings
- `dotnet test` passes all tests

## Result: PASS
