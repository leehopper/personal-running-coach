# CW-Research: POC 1 Eval Cache & Next Actions

## Summary

POC 1 eval refactor is **implementation-complete** — all 17 eval tests pass, cached re-runs complete in <1 second. The remaining work is **operational**, not architectural. Six concrete items need to be done before PR #18 can merge, with the eval cache commit being the critical-path item.

**Key insight from research synthesis:** The `DiskBasedReportingConfiguration` cache already exists on disk at `backend/poc1-eval-cache/{sonnet,haiku}/cache/` with 16 cached scenario entries. The `.gitignore` currently excludes these. The commit strategy is: remove the gitignore exclusion, commit the cache files, and set `EVAL_CACHE_MODE=Replay` in CI. However, `EVAL_CACHE_MODE` is a pattern from R-015 research that has **not yet been implemented** in `EvalTestBase.cs` — the current code always enables caching but has no replay-only mode that throws on cache miss.

---

## 1. Eval Cache Commit Strategy (Critical Path)

### Current State

- **Cache location:** `backend/poc1-eval-cache/sonnet/cache/` (11 scenario dirs) and `backend/poc1-eval-cache/haiku/cache/` (5 scenario dirs)
- **Cache format:** Each scenario has `entry.json` (metadata with 14-day TTL) + `contents.data` (full LLM response)
- **Gitignore:** Lines 35-36 of `.gitignore` exclude both `poc1-eval-results/` and `poc1-eval-cache/`
- **EvalTestBase.cs:** Lines 74-92 create `DiskBasedReportingConfiguration` with `enableResponseCaching: true` but no replay-only mode

### What Needs to Happen

1. **Remove `poc1-eval-cache/` from `.gitignore`** (keep `poc1-eval-results/` ignored — those are generated outputs)
2. **Implement `EVAL_CACHE_MODE` environment variable** in `EvalTestBase.cs`:
   - `Record` — real API calls, responses cached (local dev with API key)
   - `Replay` — cache-only, throw `InvalidOperationException` on miss (CI)
   - `Auto` (default) — cache if available, call API otherwise (current behavior)
3. **Commit existing cache files** as golden test fixtures
4. **Document the workflow:** Change prompts → run locally with Record → commit updated cache alongside prompt changes

### Research Backing (R-015 / batch-7a)

From `batch-7a-ichatclient-structured-output-bridge.md`:
- Cache key = SHA-256 of full `IEnumerable<ChatMessage>` + `ChatOptions` (including `ResponseFormat`)
- Any prompt/schema/model change = different cache key = automatic cache miss
- For git-committed cache: set `EVAL_CACHE_MODE=Replay` in CI → guarantees no API key needed, fully deterministic
- 14-day default TTL in `DiskBasedReportingConfiguration` — cache entries have expiration timestamps

### Open Question: Cache TTL in CI

Cache entries have a 14-day expiration in `entry.json`. In Replay mode, should TTL be honored or ignored? Options:
- **Ignore TTL in Replay** — cache files are version-controlled golden fixtures; expiration is meaningless
- **Honor TTL** — forces periodic re-record; catches prompt/model drift

**Recommendation:** Ignore TTL in Replay mode. Version control is the change-tracking mechanism, not TTL.

---

## 2. Install `dotnet aieval` CLI Tool

### Requirement
Success Metric 6 (SM6) from the spec requires HTML report generation. The tool isn't installed yet.

### Action
```bash
dotnet tool install --local Microsoft.Extensions.AI.Evaluation.Console
```

This adds to `.config/dotnet-tools.json` (which should already exist from the project scaffold).

---

## 3. Code Cleanup Items (4 Items)

### 3a. Clean Up `GenerateExperimentResults.cs`

**File:** `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Experiments/GenerateExperimentResults.cs`
**Current:** Permanently skipped `[Fact(Skip = "One-off artifact generator - run manually")]`
**Action:** Delete the file. The experiment results it generates are in `poc1-eval-results/experiments/` which is already gitignored. If needed again, the logic is trivial to recreate.

### 3b. Fix `AnthropicStructuredOutputClient.SplitMessages`

**File:** `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/AnthropicStructuredOutputClient.cs`, line 76
**Current:** Silently overwrites `systemPrompt` on each system message — drops all but last
**Action:** Concatenate multiple system messages with newline separator, or throw `InvalidOperationException` if >1 system message exists. Concatenation is safer since M.E.AI caching can inject system-level messages.

### 3c. Fix `AnthropicStructuredOutputClient.ConvertSchema`

**File:** Same file, line 98
**Current:** `JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(schemaElement.GetRawText())!`
**Action:** Replace `!` with explicit null check: `?? throw new InvalidOperationException("Schema deserialization returned null")`

### 3d. Add `CancellationToken` to Eval Test API Calls

**Missing in 4 locations:**
1. `PlanGenerationEvalTests.GenerateStructuredAsync()` line 397
2. `PlanGenerationEvalTests.GetCoachingNarrativeAsync()` line 427
3. `SafetyBoundaryEvalTests.GetCoachingResponseAsync()` line 259
4. `IChatClientCachingSpikeTests` lines 84, 102

**Action:** Add `CancellationToken cancellationToken = default` parameter to helper methods and pass `TestContext.Current.CancellationToken` (xUnit v3) or a timeout-based token from test methods.

---

## 4. Implementation Requirements for EVAL_CACHE_MODE

### EvalTestBase.cs Changes Needed

The current `EvalTestBase` (lines 57-100) creates `DiskBasedReportingConfiguration` unconditionally. Needs:

```csharp
// New: read mode from environment
private static EvalCacheMode GetCacheMode() =>
    Environment.GetEnvironmentVariable("EVAL_CACHE_MODE") switch
    {
        "Record" => EvalCacheMode.Record,
        "Replay" => EvalCacheMode.Replay,
        _ => EvalCacheMode.Auto  // default
    };

enum EvalCacheMode { Auto, Record, Replay }
```

**In Replay mode:** The `DiskBasedReportingConfiguration` should still be created with `enableResponseCaching: true` (so it reads from cache), but a wrapper or configuration should ensure cache misses throw rather than calling the API. Options:
- Wrap the `IChatClient` with a `DelegatingChatClient` that throws on uncached calls
- Set the Anthropic API key to empty/invalid so any real call fails fast
- Use a custom `HttpMessageHandler` that rejects outbound requests

**Simplest approach:** In Replay mode, don't create a real Anthropic client — pass a no-op `IChatClient` as the inner client. The caching layer sits above it, serves from cache, and if it misses, the no-op client throws.

### CI Pipeline Changes

Add to the test step in `.github/workflows/ci.yml`:
```yaml
env:
  EVAL_CACHE_MODE: Replay
```

No API key needed. Cache miss = test failure = signal that cache needs re-recording.

---

## 5. Merge Sequence

After all items complete:
1. Commit cache files + code changes on `feature/poc1-eval-refactor`
2. Merge PR #18 (`feature/poc1-eval-refactor` → `feature/poc1-context-injection-v2`)
3. Full POC 1 review of PR #17 (`feature/poc1-context-injection-v2` → `main`)

---

## Architecture & Pattern References

| Topic | Source Document | Key Finding |
|-------|----------------|-------------|
| VCR record/replay | `batch-7a-ichatclient-structured-output-bridge.md` | EVAL_CACHE_MODE env var pattern with Record/Replay/Auto modes |
| Cache key mechanics | Same | SHA-256 of messages + ChatOptions; schema changes auto-invalidate |
| Tiered assertion | `batch-6a-llm-eval-strategies.md` | Deterministic → NLI → LLM-judge; 95% cost reduction |
| M.E.AI caching | `batch-6b-dotnet-llm-testing-tooling.md` | DiskBasedReportingConfiguration with 14-day TTL, keyed by full request hash |
| Structured output bridge | DEC-037 | AnthropicStructuredOutputClient wraps IChatClient for constrained decoding |
| Model IDs | `batch-7b-anthropic-model-ids-versioning.md` | Floating aliases (claude-sonnet-4-6, claude-haiku-4-5) for development |
| Cost optimization | DEC-038 | Deferred to post-MVP-0; Haiku judge current, Opus judge future |

---

## Meta-Prompt for /cw-spec

---

**Feature:** POC 1 Eval Cache Commit & Cleanup

**Problem:** Eval tests pass locally with cached responses but CI has no cache — tests either need an API key (expensive, flaky) or get skipped (zero coverage). Additionally, 4 minor code quality items need cleanup before PR merge.

**Key Components:**
- `EvalTestBase.cs` — caching infrastructure, needs EVAL_CACHE_MODE support
- `AnthropicStructuredOutputClient.cs` — two minor fixes (SplitMessages, ConvertSchema)
- `PlanGenerationEvalTests.cs`, `SafetyBoundaryEvalTests.cs` — need CancellationToken
- `GenerateExperimentResults.cs` — delete permanently-skipped utility
- `.gitignore` — remove cache exclusion
- `.github/workflows/ci.yml` — add EVAL_CACHE_MODE=Replay
- `.config/dotnet-tools.json` — add dotnet aieval tool

**Architectural Constraints:**
- Must preserve existing cache key mechanics (SHA-256 of messages + ChatOptions)
- Replay mode must throw on cache miss, never make API calls
- Cache TTL should be ignored in Replay mode
- No breaking changes to existing test infrastructure

**Patterns to Follow:**
- VCR-style record/replay (R-015 research)
- DelegatingChatClient for no-op inner client in Replay mode
- xUnit CancellationToken via TestContext or timeout-based tokens

**Suggested Demoable Units:**
1. EVAL_CACHE_MODE implementation in EvalTestBase
2. Cache files committed (remove from .gitignore)
3. Code cleanup (4 items)
4. dotnet aieval tool installation + HTML report generation

**Code References:**
- `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/EvalTestBase.cs:57-100`
- `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/AnthropicStructuredOutputClient.cs:66-99`
- `backend/poc1-eval-cache/` (16 cached entries)
- `.gitignore:35-36`

---
