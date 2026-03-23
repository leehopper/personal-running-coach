# 05-spec-pr18-review-round2

## Introduction/Overview

Address all 15 findings (7 warnings + 8 advisories) from the PR #18 code review before merging into `feature/poc1-context-injection-v2`. Fixes span CI security hardening, eval cache reliability, test hygiene, code conventions, and a pre-existing configuration issue.

## Goals

1. Eliminate the 14-day cache TTL time bomb that would silently break CI on April 5, 2026
2. Harden CI pipeline against supply chain attacks by SHA-pinning all GitHub Actions
3. Improve eval test isolation with framework-level filtering and process-safe env var tests
4. Align all code with project conventions (sealed classes, async I/O, named constants)
5. Remove the pre-existing DB password from committed config files

## User Stories

- As a developer, I want CI eval tests to replay indefinitely from committed fixtures so that I don't have to re-record cache files every 14 days.
- As a developer, I want GitHub Actions pinned to commit SHAs so that a supply chain compromise of mutable tags cannot inject malicious code into my CI pipeline.
- As a developer, I want eval tests explicitly excluded from the main CI test step so that a forgotten `CanRunEvals` guard cannot accidentally attempt live API calls.
- As a developer, I want all code to follow project conventions so that the codebase is consistent and reviewable.

## Demoable Units of Work

### Unit 1: CI Security and Reliability

**Purpose:** Harden CI pipeline — SHA-pin all actions, add eval test filtering, move DB password out of committed config.

**Functional Requirements:**
- The system shall pin all GitHub Actions in `ci.yml` to full commit SHAs with version comments (checkout, setup-dotnet, setup-node, paths-filter, trivy-action, codecov)
- The system shall add `--filter "Category!=Eval"` to the main `dotnet test` step in CI
- The system shall set `EVAL_CACHE_MODE: Replay` as an environment variable on the backend test step
- The system shall move the PostgreSQL connection string from `appsettings.json` to `appsettings.Development.json` (git-tracked) with a placeholder comment in `appsettings.json` explaining the pattern
- The system shall ensure `appsettings.Development.json` is not gitignored (it's a template, not a secret — the password is a local-only dev credential matching docker-compose)

**Proof Artifacts:**
- File: `ci.yml` contains SHA-pinned actions with `# vX.Y.Z` comments on every `uses:` line
- File: `ci.yml` dotnet test step contains `--filter "Category!=Eval"`
- File: `appsettings.json` contains no password values
- CLI: `dotnet build` passes with no warnings

### Unit 2: Eval Cache TTL Fix

**Purpose:** Extend cache fixture expiration to prevent silent CI breakage per DEC-039.

**Functional Requirements:**
- The system shall provide a script or tool that rewrites all `entry.json` files in `poc1-eval-cache/` to set `"expiration": "9999-12-31T23:59:59Z"` while preserving other fields
- The system shall add a `.gitattributes` entry marking `backend/poc1-eval-cache/**/*.data` as binary
- The system shall commit the updated `entry.json` files with extended expiration
- The system shall add a comment in `EvalTestBase` documenting the re-recording workflow (when to re-record, how to run, how to extend TTL)

**Proof Artifacts:**
- CLI: `grep -r '"expiration"' backend/poc1-eval-cache/ | head -5` shows `9999-12-31` dates
- File: `.gitattributes` contains binary marker for `.data` files
- File: `EvalTestBase.cs` contains re-recording workflow comment

### Unit 3: Test Hygiene

**Purpose:** Fix test isolation issues, remove dead tests, and align with conventions.

**Functional Requirements:**
- The system shall refactor `EvalTestBase.ParseCacheMode()` to accept an optional `string? envValue` parameter so tests can pass values directly without mutating `Environment`
- The system shall update `EvalTestBaseTests` to call `ParseCacheMode(envValue)` instead of `Environment.SetEnvironmentVariable`
- The system shall delete the tautological `IsApiKeyConfigured_WithKey_ReturnsTrue` test from `EvalTestBaseCachingTests`
- The system shall call `LogCacheHit` in `YamlPromptStore.GetPromptAsync` when returning an already-completed cached task
- The system shall seal `EvalTestBaseTests`, `PlanConstraintEvaluatorTests`, and `SafetyRubricEvaluatorTests` classes

**Proof Artifacts:**
- Test: `EvalTestBaseTests` passes without `Environment.SetEnvironmentVariable` calls
- CLI: `dotnet test --filter "FullyQualifiedName~EvalTestBaseTests"` passes
- File: `YamlPromptStore.cs` contains `LogCacheHit` call site
- File: All three test classes use `sealed` modifier

### Unit 4: Code Quality Conventions

**Purpose:** Align remaining code with project conventions — async I/O, named constants, defensive comments.

**Functional Requirements:**
- The system shall change `EvalTestBase.WriteEvalResult` to `WriteEvalResultAsync` using `File.WriteAllTextAsync`, and update all call sites
- The system shall extract the pace tolerance magic number in `PlanConstraintEvaluator` to a named constant (e.g., `const double PaceTolerancePercent = 0.15`)
- The system shall add a comment to `AnthropicStructuredOutputClient.SplitMessages` documenting the text-only limitation (non-text content parts are dropped)
- The system shall use word-boundary regex for crisis hotline number assertions in `SafetyBoundaryEvalTests` (e.g., `MatchRegex(@"\b988\b")` instead of `ContainAny("988", ...)`)
- The system shall add `ImmutableArray<T>` or a comment to structured output records (`MacroPlanOutput`, `PlanPhaseOutput`, `SafetyVerdict`) documenting that `T[]` is intentional for JSON deserialization compatibility

**Proof Artifacts:**
- CLI: `dotnet build` passes with 0 warnings
- CLI: `dotnet test` passes (all categories)
- File: `PlanConstraintEvaluator.cs` contains named constant for tolerance
- File: `AnthropicStructuredOutputClient.cs` contains text-only limitation comment

## Non-Goals (Out of Scope)

- Injecting `TimeProvider` into `ContextAssembler` for `DateTime.UtcNow` (W7 — valid but architectural change, defer to a future refactor when testability of date-dependent overflow cascade becomes a priority)
- Filing a feature request on dotnet/extensions for configurable TTL (noted in DEC-039 as future action)
- Upgrading to an `IDistributedCache` decorator approach for cache TTL (DEC-039 chose the simpler post-process approach)
- Changing structured output records from `T[]` to `ImmutableArray<T>` (document as intentional exception instead)

## Design Considerations

No specific design requirements identified. All changes are internal code quality and CI configuration.

## Repository Standards

- Sealed classes for all leaf types (production and test)
- `ConfigureAwait(false)` on all awaits in library code
- Async throughout for all I/O operations
- Conventional Commits for commit messages
- `TreatWarningsAsErrors` — zero warnings allowed

## Technical Considerations

- `EvalTestBase.ParseCacheMode` refactor must maintain backward compatibility — the parameterless overload should still work for production code paths that read from `Environment`
- The `entry.json` post-processing script should be idempotent (safe to run multiple times)
- SHA pinning requires looking up current commit SHAs for each action version — verify each SHA matches the expected version tag before committing
- Moving the DB password to `appsettings.Development.json` requires verifying that the ASP.NET Core configuration layer loads it correctly in Development environment

## Security Considerations

- SHA-pinning GitHub Actions mitigates supply chain attacks on mutable version tags
- Moving DB credentials out of `appsettings.json` aligns with the project's stated rule: "All secrets via environment variables or .NET user-secrets, never in config files"
- The local dev password (`runcoach_dev`) is acceptable in `appsettings.Development.json` since it matches docker-compose and only applies to local containers
- Verify no API keys or secrets are introduced in any changes

## Success Metrics

- 0 warnings, 0 test failures after all changes
- All GitHub Actions in `ci.yml` pinned to commit SHAs
- No `entry.json` files with expiration dates before 2030
- No `Environment.SetEnvironmentVariable` calls in test code
- No password values in `appsettings.json`

## Open Questions

No open questions at this time. All decisions resolved via Q&A and R-017/DEC-039.
