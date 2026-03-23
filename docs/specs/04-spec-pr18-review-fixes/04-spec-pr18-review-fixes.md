# 04-spec-pr18-review-fixes

## Introduction/Overview

Address 9 code review findings from PR #18 (eval refactor) before merging into PR #17, plus remove the now-obsolete experiment infrastructure from POC 1 Unit 4. The goal is to ship a clean, CI-passing PR with no known blockers, consistent async patterns, and no dead code.

## Goals

1. Unblock CI by fixing all invalid GitHub Action version references
2. Achieve async pattern consistency (`.ConfigureAwait(false)`) across all library-layer code
3. Remove ~16 files / ~800 LOC of one-time experiment infrastructure that the eval suite supersedes
4. Simplify `ContextAssembler` to a single constructor and code path (YAML-based)
5. Fix all easy-to-moderate code quality issues identified in the review (race condition, dead code, logging, tolerance gaps, cache key safety)

## User Stories

- As the developer, I want CI to pass on the first push so that PR #18 can be merged without manual intervention.
- As a future contributor, I want consistent async patterns so that the codebase doesn't have subtle deadlock risks.
- As the developer, I want dead experiment code removed so that the codebase reflects only current production and eval paths.

## Demoable Units of Work

### Unit 1: CI & Build Fixes

**Purpose:** Eliminate the hard CI blocker and pin all GitHub Actions to verified versions.

**Functional Requirements:**
- The system shall use `actions/setup-node@v4` (latest stable as of 2025) instead of the non-existent `@v6`
- The system shall audit and verify all GitHub Action version references in `ci.yml` (`actions/checkout`, `actions/setup-dotnet`, `codecov/codecov-action`, `aquasecurity/trivy-action`, `dorny/paths-filter`) and pin each to the latest stable major version
- The CI workflow shall pass validation via `act --list` or equivalent dry-run check

**Proof Artifacts:**
- File: `.github/workflows/ci.yml` contains only verified action versions
- CLI: `grep -n 'uses:' .github/workflows/ci.yml` shows all pinned versions with no `@v6` for setup-node

### Unit 2: Experiment Infrastructure Removal

**Purpose:** Remove the one-time POC 1 experiment framework (Unit 4) that is superseded by the eval suite, and simplify `ContextAssembler` to a single YAML-based code path.

**Functional Requirements:**
- The system shall delete the `Modules/Coaching/Experiments/` directory and all 12+ source files within it
- The system shall delete the corresponding test files in `tests/.../Experiments/` (4 test files)
- The system shall remove the parameterless `ContextAssembler()` constructor
- The system shall remove the `SystemPromptText` hardcoded constant from `ContextAssembler`
- The system shall remove the synchronous `Assemble(ContextAssemblerInput)` method, making `AssembleAsync` the sole entry point
- The system shall update `EvalTestBase` and `PlanGenerationEvalTests` to use the async `AssembleAsync` path with an `IPromptStore` (or appropriate test adapter) instead of the removed synchronous path
- The system shall remove the `ContextAssembler dual-constructor` code smell (review finding #8)
- All remaining tests shall pass after the removal (`dotnet test`)

**Proof Artifacts:**
- CLI: `find backend/src -path '*/Experiments/*' -type f | wc -l` returns 0
- CLI: `find backend/tests -path '*/Experiments/*' -type f | wc -l` returns 0
- CLI: `grep -r 'SystemPromptText' backend/src/` returns no matches
- CLI: `grep -c 'public ContextAssembler()' backend/src/` returns 0
- Test: `dotnet test backend/RunCoach.slnx` passes with 0 failures

### Unit 3: Code Quality & Consistency Fixes

**Purpose:** Address all remaining review findings — async patterns, dead code, logging, tolerance gaps, race condition, and cache key safety.

**Functional Requirements:**
- The system shall add `.ConfigureAwait(false)` to all `await` calls in `AnthropicStructuredOutputClient` (lines 55, 58, 159) to match the production `ClaudeCoachingLlm` pattern (review finding #1)
- The system shall fix the `YamlPromptStore.LoadAndCacheAsync` race condition by using `ConcurrentDictionary.GetOrAdd` with a `Lazy<Task<PromptTemplate>>` pattern for true single-flight loading (review finding #2)
- The system shall add a `// TODO: ContextTemplate loaded but not yet used — wire into PromptRenderer when context injection goes production` comment on the unused `template.ContextTemplate` in `ContextAssembler.AssembleAsync` (review finding #3)
- The system shall add an upper-bound check to `PlanConstraintEvaluator.CheckPaceRanges` for fast pace, making the tolerance symmetric with the easy pace check (review finding #4)
- The system shall replace `Trace.WriteLine` in `EvalTestBase` with `TestContext.Current.TestOutputHelper?.WriteLine` (or equivalent xUnit v3 output mechanism) so cache mode logging is visible in test output (review finding #5)
- The system shall sanitize or validate cache key inputs in `YamlPromptStore.BuildCacheKey` to prevent `::` separator collisions — either by validating inputs don't contain `::` or by using a collision-resistant approach (review finding #7)
- The system shall remove the dead `ExtractJson` method from `PlanGenerationEvalTests` since constrained decoding guarantees bare JSON, and remove the `ExtractJson` call site in `GenerateStructuredAsync` (review finding #9)
- All existing tests shall continue to pass after the changes

**Proof Artifacts:**
- CLI: `grep -n 'ConfigureAwait' backend/tests/.../AnthropicStructuredOutputClient.cs` shows 3 occurrences
- CLI: `grep -n 'GetOrAdd' backend/src/.../YamlPromptStore.cs` shows the Lazy pattern
- CLI: `grep -n 'ExtractJson' backend/tests/.../PlanGenerationEvalTests.cs` returns no matches
- CLI: `grep -n 'Trace.WriteLine' backend/tests/.../EvalTestBase.cs` returns no matches
- Test: `dotnet test backend/RunCoach.slnx` passes with 0 failures

## Non-Goals (Out of Scope)

- Refactoring `ContextAssembler` to wire `PromptRenderer` into the production `AssembleAsync` path (that's a future feature)
- Changing the eval cache format or re-recording cache fixtures
- Upgrading any NuGet packages beyond what's needed for the fixes
- Any frontend changes

## Design Considerations

No specific design requirements identified. All changes are backend code quality.

## Repository Standards

- Conventional Commits for all commit messages
- `dotnet build` + `dotnet test` must pass after every change
- TreatWarningsAsErrors is enabled — no new warnings allowed
- Structured logging with named placeholders (no string interpolation in log calls)
- One type per file, sealed classes where applicable

## Technical Considerations

- **Experiment removal cascading:** `ExperimentContextAssembler` uses `ContextAssembler.EstimateTokens` (public) and internal constants (`CharsPerToken`, `SafetyMarginPercent`). Once experiments are deleted, check that no other code depends on these internals. The public `EstimateTokens` method should remain for the eval path.
- **Eval test path change:** `EvalTestBase.AssembleContext` and `AssembleContextWithConversation` currently call the synchronous `Assemble()` method. After removing the parameterless ctor, these need to switch to `AssembleAsync` with an appropriate `IPromptStore`. A test-only `IPromptStore` implementation or a `YamlPromptStore` with the test project's prompts directory may be needed.
- **Cache key stability:** Changing `YamlPromptStore.BuildCacheKey` format would invalidate existing cached templates. Since the cache is in-memory only (not persisted), this is safe — but the change should be documented.
- **GetOrAdd with Lazy:** The standard pattern is `_cache.GetOrAdd(key, _ => new Lazy<Task<T>>(() => LoadAsync(...)))`. The `Lazy` ensures only one thread loads; callers `await` the shared `Task`. The cache type changes from `ConcurrentDictionary<string, PromptTemplate>` to `ConcurrentDictionary<string, Lazy<Task<PromptTemplate>>>`.

## Security Considerations

No security-sensitive changes. No API keys, tokens, or auth code is modified.

## Success Metrics

- CI workflow passes end-to-end (backend build, test, coverage upload; frontend build, test; security scans)
- All existing tests pass (391+ tests, 0 failures, 0 warnings)
- Net reduction in source files (experiment removal should delete ~16 files)
- Zero instances of the deprecated patterns (`Trace.WriteLine` in tests, missing `ConfigureAwait(false)` in library code, `ExtractJson` dead code)

## Open Questions

No open questions at this time.
