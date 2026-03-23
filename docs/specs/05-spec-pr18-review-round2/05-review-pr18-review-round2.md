# Code Review Report

**Reviewed**: 2026-03-22T21:40:00Z
**Branch**: feature/poc1-eval-refactor
**Base**: feature/poc1-context-injection-v2
**Commits**: 10 commits, 349 files changed (+10,066 / -5,097)
**Overall**: APPROVED

## Summary

- **Blocking Issues**: 0
- **Advisory Notes**: 7 (1 security, 5 correctness, 2 spec compliance)
- **Files Reviewed**: 85 changed files (excluding cache fixtures and proof artifacts)
- **FIX Tasks Created**: none

## Review Methodology

**Approach**: Concern-partitioned team review
**Reviewers**: 3 specialized agents

| Reviewer | Concern | Primary Category | Status |
|----------|---------|-----------------|--------|
| security-reviewer | Security | B | Completed |
| correctness-reviewer | Correctness | A | Completed |
| spec-reviewer | Spec Compliance | C + D | Completed |

**Challenge Round**: Not triggered (0 blocking findings < 3 threshold)

## Blocking Issues

None.

## Advisory Notes

### [SEC-A1] Category B: Prompt injection surface when context template goes production
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/Prompts/PromptRenderer.cs:33-35`
- **Description**: `PromptRenderer.Render` uses simple `string.Replace` for `{{token}}` substitution. Currently safe since context assembly is programmatic, but when user-generated input flows through the template, `{{token}}` patterns in user input could interfere with template rendering.
- **Suggestion**: Sanitize user input for `{{` and `}}` patterns before passing to the renderer, or switch to a template engine with proper escaping when moving to production.

### [COR-A1] Category A: Cosmetic race in YamlPromptStore logging
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/Prompts/YamlPromptStore.cs:82-93`
- **Description**: The `isNewEntry` check via reference equality (`lazy == newLazy`) is a best-effort heuristic for logging. Under high concurrency, `GetOrAdd` may return an existing entry while the `newLazy` reference is discarded, leading to a missed `LogCacheHit` call. No correctness impact — only affects diagnostic logging.

### [COR-A2] Category A: DateTime.UtcNow in ContextAssembler
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/ContextAssembler.cs:339, 547`
- **Description**: Direct `DateTime.UtcNow` usage makes overflow cascade non-deterministic for testing. Already tracked in future backlog (`docs/features/backlog.md`) for `TimeProvider` injection.

### [COR-A3] Category A: PromptRenderer accepts null dictionary values at runtime
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/Prompts/PromptRenderer.cs:33`
- **Description**: If a null value slips through at runtime (e.g., from YAML deserialization), `string.Replace` with null newValue removes the match silently rather than replacing with empty string. Acceptable behavior but undocumented.

### [COR-A4] Category A: GenerateStructuredAsync null guard message is misleading
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/ClaudeCoachingLlm.cs:185-187`
- **Description**: Error message says "Response was empty or null" but the only path to this throw is a JSON `null` literal — constrained decoding prevents this in practice. Guard is belt-and-suspenders; message could be clearer.

### [SPEC-A1] Category D: Garbled XML doc comment in PlanPhaseOutput
- **File**: `backend/src/RunCoach.Api/Modules/Coaching/Models/Structured/PlanPhaseOutput.cs:67`
- **Description**: XML doc reads "Gets a value indicating whether gets a flag indicating whether..." — duplicate phrase from copy-paste.

### [SPEC-A2] Category D: Inconsistent array type documentation
- **Files**: `MesoWeekOutput.cs:39`, `MicroWorkoutListOutput.cs:14`, `WorkoutOutput.cs:57`
- **Description**: The "Array used instead of ImmutableArray for JSON deserialization compatibility" comment was added to `MacroPlanOutput`, `PlanPhaseOutput`, and `SafetyVerdict` per spec, but three other records with the same `T[]` pattern lack the comment. Inconsistent but not a spec violation (spec only required documentation on those 3).

## Checklist

- [x] No hardcoded credentials or secrets
- [x] Error handling at system boundaries
- [x] Input validation on user-facing endpoints
- [x] Changes match spec requirements (all 15 findings addressed)
- [x] Follows repository patterns and conventions
- [x] No obvious performance regressions
- [x] All GitHub Actions SHA-pinned
- [x] Cache fixtures contain no sensitive data
- [x] Build passes with 0 warnings
- [x] 291 tests pass, 0 failures
