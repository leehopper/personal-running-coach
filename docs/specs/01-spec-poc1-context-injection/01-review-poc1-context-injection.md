# Code Review Report

**Reviewed**: 2026-03-22
**Branch**: feature/poc1-context-injection-v2
**Base**: main
**Commits**: 22 commits, 437 files changed
**Overall**: CHANGES REQUESTED

## Summary

- **Blocking Issues**: 2 (A: 1 correctness, B: 1 security)
- **Advisory Notes**: 13
- **Files Reviewed**: 24 / 73 non-test source files (core production code prioritized)
- **FIX Tasks Created**: #10, #11

## Blocking Issues

### [ISSUE-1] Category A (Correctness): VDOT 49→50 pace table data anomaly
- **File**: `backend/src/RunCoach.Api/Modules/Training/Computations/PaceCalculator.cs:42-43`
- **Severity**: Blocking
- **Description**: The pace table has an anomalous jump at the VDOT 49→50 boundary. Step sizes are 2-3x larger than all surrounding entries (10-13 s/km vs typical 3-5 s/km across EasyMin, EasyMax, Threshold, Interval, and Repetition columns). This produces incorrect training paces for runners at VDOT 49-50, a common intermediate fitness level. Interpolated values would be affected in the 49.x range.
- **Fix**: Cross-reference against Daniels' Running Formula 4th edition. Correct if transcription error.
- **Task**: FIX-REVIEW #10

### [ISSUE-2] Category B (Security): Hardcoded database password in committed config
- **File**: `backend/src/RunCoach.Api/appsettings.Development.json:9`
- **Severity**: Blocking
- **Description**: PostgreSQL connection string with `Password=runcoach_dev` is committed to source control. Both root and backend CLAUDE.md explicitly state: "All secrets via environment variables or .NET user-secrets, never in config files."
- **Fix**: Remove from config file. Use Docker Compose env vars, .NET user-secrets, or environment variable override.
- **Task**: FIX-REVIEW #11

## Advisory Notes

### [NOTE-1] Category A: ContextAssembler conditional formatting edge case
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/ContextAssembler.cs:393`
- **Description**: When `HeightCm` is set but `WeightKg` is null, the height line gets an orphaned ` | ` separator. Same issue at line 405 for `MaxHeartRate` when `RestingHeartRateAvg` is null.
- **Suggestion**: Guard the separator on the preceding field being non-null.

### [NOTE-2] Category B: Misleading XML doc on API key masking
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/ClaudeCoachingLlm.cs:23-24`
- **Description**: Comment says "Only the first 8 characters are included in diagnostic messages (masked)" but no such code exists. The API key is never logged.
- **Suggestion**: Simplify to "The API key is never logged."

### [NOTE-3] Category C: VdotCalculator vs PaceCalculator edition mismatch
- **File**: `backend/src/RunCoach.Api/Modules/Training/Computations/VdotCalculator.cs:16` vs `PaceCalculator.cs:16`
- **Description**: VdotCalculator references "3rd edition" while PaceCalculator references "4th edition" of Daniels' Running Formula. Different editions for VDOT calculation vs pace lookup could introduce subtle mismatches.
- **Suggestion**: Verify both use consistent source data. Update reference to match.

### [NOTE-4] Category C: AsIChatClient() not on ICoachingLlm interface
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/ClaudeCoachingLlm.cs:194`
- **Description**: `AsIChatClient()` is public but not declared on `ICoachingLlm`. Consumers holding the interface cannot access it without downcasting.
- **Suggestion**: Add to interface if intended for DI consumers, or mark internal if eval-only.

### [NOTE-5] Category D: Missing ILogger<T> on services
- **Files**: `ContextAssembler.cs:75`, `PaceCalculator.cs`, `VdotCalculator.cs`
- **Description**: Backend CLAUDE.md requires "All controllers, services, and repositories inject `ILogger<T>`." Three services lack loggers.
- **Suggestion**: Add ILogger injection to match the convention.

### [NOTE-6] Category D: TestProfiles.All re-creates on every access
- **File**: `backend/src/RunCoach.Api/Modules/Training/Profiles/TestProfiles.cs:22`
- **Description**: Expression-bodied property creates a new dictionary and invokes all 5 profile factories (including VDOT/pace calculations) on every access. Called 15+ times across codebase.
- **Suggestion**: Cache in a static readonly field or Lazy<T>.

### [NOTE-7] Category D: TestProfiles in src/ instead of tests/
- **File**: `backend/src/RunCoach.Api/Modules/Training/Profiles/TestProfiles.cs`
- **Description**: Test fixture data ships in the production assembly. The console app references it, which may justify the placement, but backend CLAUDE.md says test fixtures belong in test projects.
- **Suggestion**: Consider a shared project or move to test infrastructure.

### [NOTE-8] Category D: WeekGroup nested record uses mutable List
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/ContextAssembler.cs:594`
- **Description**: `WeekGroup` contains `List<WorkoutSummary>` rather than `IReadOnlyList` or `ImmutableArray`, inconsistent with the project's immutability patterns.

### [NOTE-9] Category D: Nested types violate one-type-per-file
- **Files**: `PaceCalculator.cs:141`, `YamlPromptStore.cs:218`
- **Description**: `PaceTableEntry` and `YamlPromptDocument`/`YamlPromptMetadata` are nested types. Pragmatic since they're private/internal, but deviates from the convention.

### [NOTE-10] Category A: PlanPhaseOutput int vs decimal for distances
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/Models/Structured/PlanPhaseOutput.cs:27,33`
- **Description**: `WeeklyDistanceStartKm`/`EndKm` use `int` but the domain model uses `decimal` for distances elsewhere. Risks truncation of LLM-returned fractional values.

### [NOTE-11] Category B: YamlPromptStore BuildFilePath lacks path traversal check
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/Prompts/YamlPromptStore.cs:179`
- **Description**: `BuildFilePath` combines user-provided prompt ID into a file path without validating for directory traversal characters. Low risk since IDs come from configuration.

### [NOTE-12] Category D: PromptStoreSettings mutable dictionary
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/Prompts/PromptStoreSettings.cs:24`
- **Description**: `ActiveVersions` is `Dictionary<string, string>` rather than an immutable type. Required for IOptions binding but inconsistent with immutability patterns.

### [NOTE-13] Category D: CoachingLlmSettings model ID may not match spec
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/CoachingLlmSettings.cs:25`
- **Description**: Default `ModelId` is `claude-sonnet-4-6` but the original spec references Claude Sonnet 4.5. This is likely an intentional upgrade per DEC-037 (floating aliases), but the spec should be updated.

## Files Reviewed

| File | Status | Issues |
|------|--------|--------|
| `Coaching/ClaudeCoachingLlm.cs` | New | 2 advisory |
| `Coaching/CoachingLlmSettings.cs` | New | 1 advisory |
| `Coaching/ContextAssembler.cs` | New | 3 advisory |
| `Coaching/ICoachingLlm.cs` | New | Clean |
| `Coaching/IContextAssembler.cs` | New | Clean |
| `Coaching/Models/AssembledPrompt.cs` | New | Clean |
| `Coaching/Models/ContextAssemblerInput.cs` | New | Clean |
| `Coaching/Models/ConversationTurn.cs` | New | Clean |
| `Coaching/Models/Structured/JsonSchemaHelper.cs` | New | Clean |
| `Coaching/Models/Structured/MacroPlanOutput.cs` | New | Clean |
| `Coaching/Models/Structured/PlanPhaseOutput.cs` | New | 1 advisory |
| `Coaching/Prompts/IPromptStore.cs` | New | Clean |
| `Coaching/Prompts/PromptRenderer.cs` | New | Clean |
| `Coaching/Prompts/PromptStoreSettings.cs` | New | 1 advisory |
| `Coaching/Prompts/YamlPromptStore.cs` | New | 2 advisory |
| `Training/Computations/IPaceCalculator.cs` | New | Clean |
| `Training/Computations/IVdotCalculator.cs` | New | Clean |
| `Training/Computations/PaceCalculator.cs` | New | 1 blocking |
| `Training/Computations/VdotCalculator.cs` | New | 1 advisory |
| `Training/Profiles/TestProfiles.cs` | New | 2 advisory |
| `RunCoach.Poc1.Console/Program.cs` | New | Clean |
| `.github/workflows/ci.yml` | Modified | Clean |
| `appsettings.json` | Modified | Clean |
| `appsettings.Development.json` | Modified | 1 blocking |

## Checklist

- [x] No hardcoded credentials or secrets — **FAIL** (appsettings.Development.json)
- [x] Error handling at system boundaries — PASS
- [x] Input validation on user-facing endpoints — PASS (guard clauses throughout)
- [x] Changes match spec requirements — PASS (with advisory notes on edition mismatch)
- [x] Follows repository patterns and conventions — PASS (with advisory notes on ILogger)
- [x] No obvious performance regressions — PASS
- [x] SHA-pinned GitHub Actions — PASS
- [x] Trivy security scanning in CI — PASS
