# T15 Proof Summary

## Task
FIX xfi-1: Wire DI registrations when adding controllers (surfaced)

## Changes
- `backend/src/RunCoach.Api/Infrastructure/ServiceCollectionExtensions.cs`: Added `IConfiguration` parameter, bound `CoachingLlmSettings` and `PromptStoreSettings` from configuration sections, registered `IPromptStore`/`IPaceCalculator`/`IVdotCalculator` as singletons and `ICoachingLlm`/`IContextAssembler` as scoped.
- `backend/src/RunCoach.Api/Program.cs`: Added call to `AddApplicationModules(builder.Configuration)` during startup.

## Proof Artifacts

| # | Type | Description | Status |
|---|------|-------------|--------|
| 1 | file | ServiceCollectionExtensions.cs contains all required DI registrations | PASS |
| 2 | cli  | `dotnet build` succeeds with 0 errors | PASS |
| 3 | cli  | `dotnet test` passes all 315 tests with 0 failures | PASS |

## Result: PASS
