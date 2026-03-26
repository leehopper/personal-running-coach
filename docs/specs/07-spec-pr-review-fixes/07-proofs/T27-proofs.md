# T27 Proof Summary: Remove duplicate XML doc sentences in ClaudeCoachingLlm constructors

## Task

Remove duplicate "Initializes a new instance" opening sentences from both constructor XML doc blocks in `ClaudeCoachingLlm.cs`.

## Changes

- **File modified:** `backend/src/RunCoach.Api/Modules/Coaching/ClaudeCoachingLlm.cs`
- Removed the generic `Initializes a new instance of the <see cref="ClaudeCoachingLlm"/> class.` line from both constructors' `<summary>` blocks
- Each constructor retains its specific description (dependency-injected vs externally-provided client)

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | file | T27-01-file.txt | PASS |
| 2 | cli  | T27-02-cli.txt  | PASS |

## Verification

- `grep -c "Initializes a new instance of the"` returns 0 matches (duplicate removed)
- `dotnet build` succeeds with 0 errors, 0 warnings
- `dotnet test` passes all tests

## Result: PASS
