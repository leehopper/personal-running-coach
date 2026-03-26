# T16 Proof Summary

## Task
FIX xfi-2: Call ValidateConfiguredVersions at startup when DI wired (surfaced)

## Changes
- `backend/src/RunCoach.Api/Program.cs`: Added `using` for `RunCoach.Api.Modules.Coaching.Prompts`, resolves `IPromptStore` from DI after `app.Build()`, casts to `YamlPromptStore`, and calls `ValidateConfiguredVersions()` to fail fast if configured YAML prompt files are missing.

## Proof Artifacts

| # | Type | Description | Status |
|---|------|-------------|--------|
| 1 | file | Program.cs contains ValidateConfiguredVersions() call after app build | PASS |
| 2 | cli  | `dotnet build` succeeds with 0 errors | PASS |
| 3 | cli  | `dotnet test` passes all 315 tests with 0 failures | PASS |

## Result: PASS
