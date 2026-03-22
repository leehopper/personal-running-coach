# 03-spec-eval-cache-ci

## Introduction/Overview

Make the POC 1 eval test suite fully deterministic in CI by committing cached LLM responses as golden test fixtures and implementing a Record/Replay/Auto cache mode system. This also includes minor code quality cleanup items that were identified during PR #18 review and gate the merge.

## Goals

1. **xUnit v3 migration** — Upgrade from xUnit v2 (2.9.3) to v3, enabling `TestContext.Current.CancellationToken` and modern test infrastructure
2. **CI determinism** — Eval tests pass in CI without an API key, using committed cache files in Replay mode
3. **Developer ergonomics** — Clear error messages when cache is stale, telling developers exactly which scenario needs re-recording
4. **Code quality cleanup** — Fix 4 minor issues identified in PR #18 review (SplitMessages, ConvertSchema, CancellationToken, dead code)
5. **SM6 completion** — Install `dotnet aieval` CLI tool and generate HTML eval report
6. **Merge readiness** — All items complete so PR #18 can merge into `feature/poc1-context-injection-v2`

## User Stories

- As a developer pushing prompt changes, I want CI to tell me exactly which eval scenario caches need re-recording, so I don't waste time debugging opaque failures.
- As a CI pipeline, I want to run all 17 eval tests deterministically from committed cache files with zero API calls, so builds are fast, free, and reproducible.
- As a developer iterating locally, I want the default (Auto) mode to transparently cache responses so repeat runs are instant.

## Demoable Units of Work

### Unit 1: Upgrade to xUnit v3

**Purpose:** Migrate from xUnit v2 (2.9.3) to xUnit v3 to enable `TestContext.Current.CancellationToken` and modernize the test infrastructure. This is a prerequisite for the CancellationToken work in Unit 4.

**Functional Requirements:**

- `Directory.Packages.props` shall replace `xunit` (2.9.3) with `xunit.v3` (latest stable).
- `Directory.Packages.props` shall remove `xunit.runner.visualstudio` — xUnit v3 has a built-in runner and does not use this package.
- The test project `.csproj` shall replace `<PackageReference Include="xunit" />` with `<PackageReference Include="xunit.v3" />` and remove the `xunit.runner.visualstudio` reference.
- `Microsoft.NET.Test.Sdk` shall be removed from both `.csproj` and `Directory.Packages.props` — xUnit v3 generates its own entry point and does not use the VSTest hosting model.
- The global using `<Using Include="Xunit" />` shall remain (namespace is unchanged in v3).
- `IClassFixture<T>` usage in `SmokeTests.cs` shall continue to work without modification (API-compatible in v3).
- `[Fact]`, `[Theory]`, `[Trait]`, `[Fact(Skip = "...")]` attributes shall continue to work without modification.
- `FluentAssertions`, `NSubstitute`, and `coverlet.collector` shall remain compatible — these are framework-agnostic.
- All existing tests shall pass after migration with zero code changes beyond the project file updates.

**Proof Artifacts:**
- File: `Directory.Packages.props` contains `xunit.v3` and does not contain `xunit`, `xunit.runner.visualstudio`, or `Microsoft.NET.Test.Sdk`
- CLI: `dotnet build` passes with zero errors in test project
- CLI: `dotnet test` passes — all existing tests green
- Test: `TestContext.Current` is accessible from a test method (verified by Unit 4 implementation)

### Unit 2: EVAL_CACHE_MODE Implementation

**Purpose:** Add Record/Replay/Auto cache mode support to `EvalTestBase` so CI can run in Replay mode (cache-only, no API calls) while local dev defaults to Auto. Depends on Unit 1 (xUnit v3) for test output via `TestContext`.

**Functional Requirements:**

- The system shall read `EVAL_CACHE_MODE` from environment variables, accepting values `Record`, `Replay`, and `Auto` (case-insensitive), defaulting to `Auto` when unset or empty.
- In `Replay` mode, the system shall use a no-op `IChatClient` as the inner client that throws a descriptive exception on any call, including the scenario name in the error message so developers know exactly which cache needs re-recording.
- In `Record` mode, the system shall use a real Anthropic `IChatClient` as the inner client, with response caching enabled, requiring a valid API key.
- In `Auto` mode (default), the system shall behave as `Record` when an API key is available, and as `Replay` when no API key is configured — preserving current behavior.
- The no-op client exception message shall follow the pattern: `"Cache miss for scenario '{scenarioName}'. Run eval tests locally with EVAL_CACHE_MODE=Record and a valid API key to regenerate the cache, then commit the updated cache files."`
- The system shall expose the current cache mode in test output (e.g., via `TestContext.Current.TestOutputHelper` in xUnit v3) so developers can confirm which mode is active.

**Proof Artifacts:**
- Test: New unit tests in `EvalTestBase` test file verify mode parsing (Record/Replay/Auto/default), case-insensitivity, and that Replay mode throws descriptive exception on cache miss
- CLI: `EVAL_CACHE_MODE=Replay dotnet test --filter "Category=Eval"` passes with committed cache files
- CLI: `EVAL_CACHE_MODE=Replay dotnet test --filter "FullyQualifiedName~SomeNewUncachedTest"` fails with descriptive cache miss message

### Unit 3: Commit Cache Files & CI Configuration

**Purpose:** Remove the gitignore exclusion for `poc1-eval-cache/`, commit existing cache files, configure `EVAL_CACHE_MODE=Replay` in CI, and fix CI triggers so checks run on all PRs (not just PRs targeting `main`).

**Functional Requirements:**

- The `.gitignore` shall no longer exclude `poc1-eval-cache/` (the `poc1-eval-results/` exclusion shall remain — those are generated outputs).
- All existing cache files under `backend/poc1-eval-cache/sonnet/cache/` (11 scenario directories) and `backend/poc1-eval-cache/haiku/cache/` (5 scenario directories) shall be committed as golden test fixtures.
- The CI workflow (`.github/workflows/ci.yml`) shall set `EVAL_CACHE_MODE: Replay` in the backend test step environment.
- The CI workflow `pull_request` trigger shall run on all PRs regardless of target branch — remove `branches: [main]` from the `pull_request` trigger. The `push` trigger shall remain restricted to `main` only. Currently, PRs targeting feature branches (e.g., PR #18 targeting `feature/poc1-context-injection-v2`) skip all CI checks.
- Cache entry TTL (14-day expiration in `entry.json`) shall be ignored in Replay mode — version control is the change-tracking mechanism, not TTL.

**Proof Artifacts:**
- File: `.gitignore` no longer contains `poc1-eval-cache/` line
- File: `backend/poc1-eval-cache/sonnet/cache/` contains 11 scenario directories with `entry.json` + `contents.data` in each
- File: `backend/poc1-eval-cache/haiku/cache/` contains 5 scenario directories
- File: `.github/workflows/ci.yml` contains `EVAL_CACHE_MODE: Replay` in backend test env
- File: `.github/workflows/ci.yml` `pull_request` trigger has no `branches` filter

### Unit 4: Code Quality Cleanup

**Purpose:** Fix 4 minor code quality issues from PR #18 review and delete dead code. Depends on Unit 1 (xUnit v3) for `TestContext.Current.CancellationToken`.

**Functional Requirements:**

- `AnthropicStructuredOutputClient.SplitMessages` shall concatenate multiple system messages with `\n\n` (double newline) separator instead of silently dropping all but the last. This keeps sections visually distinct in Anthropic's system prompt.
- `AnthropicStructuredOutputClient.ConvertSchema` shall replace the `!` null-forgiving operator with an explicit null check that throws `InvalidOperationException` with message `"Schema deserialization returned null for the provided JSON schema element."`.
- All `GetResponseAsync` calls in eval test helper methods shall accept and pass through a `CancellationToken`, using `TestContext.Current.CancellationToken` (xUnit v3) at call sites to prevent hanging CI builds.
- `GenerateExperimentResults.cs` shall be deleted — it is a permanently-skipped one-off utility (`[Fact(Skip = "...")]`) whose outputs are gitignored.

**Proof Artifacts:**
- Test: Existing unit tests in `AnthropicStructuredOutputClient` tests (if any) still pass, or new tests verify concatenation behavior for multi-system-message input
- CLI: `dotnet build` passes with zero warnings in test project
- CLI: `dotnet test --filter "Category=Eval"` passes (all 17 tests)
- File: `GenerateExperimentResults.cs` no longer exists

### Unit 5: HTML Report Generation (SM6)

**Purpose:** Install the `dotnet aieval` CLI tool and verify HTML report generation, completing Success Metric 6 from the eval refactor spec.

**Functional Requirements:**

- The `dotnet aieval` CLI tool shall be installed as a local tool via `dotnet tool install --local Microsoft.Extensions.AI.Evaluation.Console`, recorded in `.config/dotnet-tools.json`.
- After running the eval suite, `dotnet aieval report` shall generate an HTML report from the cached eval data.
- The generated report shall contain results for all eval scenarios.

**Proof Artifacts:**
- File: `.config/dotnet-tools.json` contains `Microsoft.Extensions.AI.Evaluation.Console` entry
- CLI: `dotnet aieval report` generates an HTML file without errors
- File: Generated HTML report exists and contains scenario results

## Non-Goals (Out of Scope)

- **NLI entailment checking** — Research identified this as valuable but it's a post-MVP-0 optimization
- **Batch API integration** — Cost optimization deferred to DEC-038 / post-MVP-0
- **Prompt caching (Anthropic server-side)** — Separate optimization, not needed for CI determinism
- **Additional eval scenarios** — No new test scenarios; this spec is about infrastructure only
- **CI pipeline restructuring** — Only the `EVAL_CACHE_MODE` env var and PR trigger fix; no workflow refactoring beyond that
- **Cache TTL management** — Replay mode ignores TTL; no custom TTL configuration needed

## Design Considerations

No UI/UX requirements — this is backend infrastructure and CI configuration.

## Repository Standards

- **Commit messages:** Conventional Commits (`feat:`, `fix:`, `chore:`, `refactor:`)
- **Code style:** Sealed classes, immutable records, `TreatWarningsAsErrors` enabled, no `!` null-forgiving operators without justification
- **Testing:** xUnit v3 with FluentAssertions, `[Trait("Category", "Eval")]` for eval tests
- **Build verification:** `dotnet build` + `dotnet test` after every change

## Technical Considerations

- **xUnit v3 migration:** The migration from v2 → v3 changes package names (`xunit` → `xunit.v3`) and removes the need for `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`. Core test APIs (`[Fact]`, `[Theory]`, `[Trait]`, `IClassFixture<T>`) are compatible. The primary benefit for this spec is `TestContext.Current.CancellationToken`. Key risk: `coverlet.collector` compatibility — verify during implementation.
- **DiskBasedReportingConfiguration TTL:** The M.E.AI cache stores expiration timestamps in `entry.json`. In Replay mode, the no-op inner client approach sidesteps TTL entirely — if the cache entry exists and is readable, it's served regardless of expiration, because the real client is never consulted.
- **Cache key stability:** Cache keys are SHA-256 hashes of full `ChatMessage[]` + `ChatOptions` including `ResponseFormat`. Changing a prompt, model ID, temperature, or schema automatically invalidates the relevant cache entry. Schema objects must be generated once at startup and reused to avoid non-deterministic property ordering.
- **xUnit v3 CancellationToken:** `TestContext.Current.CancellationToken` is the idiomatic xUnit v3 pattern. It's automatically cancelled when a test exceeds its timeout. No manual `CancellationTokenSource` needed.
- **No-op client design:** The throwing client should implement `IChatClient` minimally — just the `GetResponseAsync` method that throws. It does not need streaming support since eval tests don't use streaming.

## Security Considerations

- Cache files (`contents.data`) contain full LLM responses — these are coaching plan text and safety evaluation results, not secrets. Safe to commit.
- The no-op client in Replay mode ensures CI never makes outbound API calls, even if an API key were accidentally available.
- API keys remain in user-secrets (`runcoach-api-tests`), never in committed files.

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| SM1 | xUnit v3 migration complete | `dotnet test` passes with `xunit.v3` package, no v2 packages remain |
| SM2 | All 17 eval tests pass in Replay mode | `EVAL_CACHE_MODE=Replay dotnet test --filter "Category=Eval"` exits 0 |
| SM3 | Zero API calls in Replay mode | No-op client is never bypassed; no Anthropic API errors in output |
| SM4 | Cache miss produces actionable error | Error message includes scenario name and re-recording instructions |
| SM5 | HTML eval report generates | `dotnet aieval report` produces viewable HTML file |
| SM6 | Zero compiler warnings | `dotnet build` exits 0 with TreatWarningsAsErrors |
| SM7 | Dead code removed | `GenerateExperimentResults.cs` absent from tree |

## Open Questions

No open questions at this time. All design decisions resolved via research (R-013 through R-016) and clarifying questions above.
