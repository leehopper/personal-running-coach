# T28 Proof Summary: Document prompt injection sanitization plan for user-controlled fields

## Task

Add FUTURE documentation comment to ContextAssembler.cs identifying all user-controlled free-text fields that flow unsanitized into LLM prompt sections, noting sanitization requirements before wiring user-facing endpoints.

## Root Cause

User-controlled free-text fields (Notes, Description, Constraints, Name, Conditions, UserMessage, etc.) flow directly into assembled prompt sections without sanitization. Currently safe because POC has no user-facing input endpoints -- all data is programmatic test fixtures. The risk needs to be documented so sanitization is implemented before user input is wired in.

## Fix

Added a `<remarks>` XML doc block to the `ContextAssembler` class enumerating all 8 user-controlled fields by type and section, with guidance on sanitization approach (dedicated `IPromptSanitizer` at section boundaries). Cross-references the existing FUTURE comment in `PromptRenderer` about template-token interference.

## Changes

- **File modified:** `backend/src/RunCoach.Api/Modules/Coaching/ContextAssembler.cs`
- Added `<remarks>` block with FUTURE comment listing fields: UserProfile.Name, InjuryNote.Description, RaceTime.Conditions, UserPreferences.Constraints, RaceGoal.RaceName, WorkoutSummary.Notes, ConversationTurn.UserMessage, ContextAssemblerInput.CurrentUserMessage

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | file | T28-01-file.txt | PASS |
| 2 | cli  | T28-02-cli.txt  | PASS |

## Verification

- `dotnet build` (source project) succeeds with 0 errors, 0 warnings
- FUTURE comment present in class-level XML doc remarks
- All 8 user-controlled fields enumerated with their target sections
- Cross-reference to PromptRenderer remarks included

## Result: PASS
