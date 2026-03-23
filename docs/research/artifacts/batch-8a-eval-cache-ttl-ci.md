# LLM eval cache management for CI pipelines in M.E.AI

**The 14-day default TTL in Microsoft.Extensions.AI.Evaluation's disk-based caching silently breaks CI pipelines that replay committed fixtures without API keys, and the library currently exposes no public API to change it.** This is a real architectural gap: the caching layer was designed for developer workstations where expired entries transparently refresh from the LLM, not for git-committed fixtures replayed in keyless CI. The most robust workaround is wrapping `DiskBasedResponseCache`'s `IDistributedCache` implementation with a decorator that strips expiration metadata, combined with a scheduled re-recording workflow. The broader industry has converged on similar patterns — VCR-style cassette files with no expiration and strict replay modes — that M.E.AI's architecture doesn't natively support.

## The 14-day TTL is hardcoded with no public override

Multiple official Microsoft Learn tutorials and the dotnet/ai-samples README confirm a **14-day absolute expiration** as the default. The `DiskBasedReportingConfiguration.Create()` factory method exposes five parameters: `storageRootPath`, `evaluators`, `chatConfiguration`, `enableResponseCaching` (bool), and `executionName`. None of these accept a `TimeSpan` or TTL value. The class name `DiskBasedCacheOptions` — referenced in the user's question — **does not appear in any public API documentation, NuGet package metadata, or GitHub search results**. It may be an internal/unreleased class, or the TTL may be set directly inside `ResponseCachingChatClient` when it calls `IDistributedCache.SetAsync()` with a `DistributedCacheEntryOptions` containing `AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14)`.

The expiration type is **absolute, not sliding**. An entry cached on day 1 expires on day 15 regardless of access frequency. When expiration fires, the caching layer returns `null` from `IDistributedCache.GetAsync()`, causing `CachingChatClient` to forward the request to the inner `IChatClient`. On a developer workstation with an API key, this transparently re-fetches and re-caches. In the user's CI environment — where the inner client is a `ReplayGuardChatClient` — this fallthrough throws `InvalidOperationException`, silently breaking the test suite. The `enableResponseCaching` parameter only toggles caching entirely on or off; setting it to `false` doesn't help because that disables cache reads too.

The GitHub issue tracker for dotnet/extensions contains **no issues or feature requests** specifically about making TTL configurable, disabling expiration for CI fixtures, or supporting committed cache files. The evaluation libraries were open-sourced relatively recently (after December 2024, following issue #5724 requesting open-sourcing), and the community discussion on caching configuration is sparse.

## Cache keys are deterministic and change automatically with prompt edits

The cache key computation follows a three-layer inheritance chain: `CachingChatClient` → `DistributedCachingChatClient` → `ResponseCachingChatClient`. The `GetCacheKey()` method serializes three inputs to JSON using `System.Text.Json`, then hashes the result:

- **`messages`**: All `ChatMessage` objects — system prompts, user prompts, message roles, and multimodal content (text, images, audio). A single character change in any prompt text produces a different cache key.
- **`options`** (`ChatOptions`): `Temperature`, `ModelId`, `ResponseFormat`, `TopP`, `TopK`, `MaxOutputTokens`, `StopSequences`, `Tools`/function definitions, and `AdditionalProperties`. Changing the model deployment name or temperature invalidates the cache.
- **`additionalValues`**: `ResponseCachingChatClient` injects the **model endpoint URL** and **model identifier** here, ensuring cached responses are endpoint-specific.

This design means **prompt changes cause clean cache misses, never stale hits**. If a developer changes a system prompt from "You are a safety judge" to "You are a strict safety judge," the cache key changes entirely, and the old cached response is simply ignored. This is the correct behavior for committed fixtures — it means stale fixtures from prompt changes will trigger the `ReplayGuardChatClient` exception immediately (which is detectable), rather than silently serving wrong cached responses.

The `DiskBasedResponseCache` stores entries as files on disk. Based on the user's observation, each cache entry consists of `entry.json` (metadata including expiration timestamp) and `contents.data` (serialized response). The `IDistributedCache` interface stores `byte[]` values, and the `DistributedCachingChatClient` handles JSON serialization of `ChatResponse` objects into these byte arrays. One caveat: JSON serialization **does not preserve full fidelity** — `RawRepresentation` is discarded, and `Object` values in `AdditionalProperties` deserialize as `JsonElement` rather than original types.

## Industry consensus favors indefinite fixtures with strict replay

The broader LLM testing ecosystem has converged on patterns that separate recording from replay with no TTL on committed fixtures. Three approaches dominate.

**VCR-style cassettes** are the most mature pattern. Python's VCR.py, the LangChain-specific `vcr-langchain`, and BAML VCR all record HTTP request-response pairs to YAML/JSON cassette files. These cassettes have **no built-in TTL** — they persist until manually re-recorded. The recommended CI configuration uses `record_mode="none"`, which throws an error on any unrecorded API call, guaranteeing deterministic replay. BAML VCR's documentation explicitly recommends: "Commit cassettes. Refresh periodically. Use `record_mode='none'` in CI."

**Content-addressed caching** is the approach used by Block (formerly Square), described in their influential "Testing Pyramid for AI Agents" blog post (January 2026). Their Rust-based `TestProvider` saves responses as JSON files keyed by a hash of input messages — effectively the same content-addressed approach M.E.AI uses for cache keys. There is no expiration. Their philosophy: "We don't run live LLM tests in CI. It's too expensive, too slow, and too flaky. CI validates the deterministic layers."

**Promptfoo's configurable TTL** is the closest analog to M.E.AI's approach. Promptfoo also defaults to **14 days** but exposes `PROMPTFOO_CACHE_TTL` as an environment variable and supports content-addressed cache invalidation via CI cache keys that include `checksum "prompts/**/*"`. This means prompt file changes automatically bust the CI cache. CopilotKit's `llmock` adds weekly drift detection — a scheduled CI job runs against real APIs to detect when fixtures diverge from actual model behavior.

## Four practical workarounds for the TTL problem

Given that M.E.AI doesn't expose a TTL configuration option, the following approaches are ordered from most architecturally sound to most pragmatic.

**Approach 1: IDistributedCache decorator that strips expiration.** Since `DiskBasedResponseCache` implements `IDistributedCache`, wrap it with a decorator that intercepts `SetAsync()` and passes through `DistributedCacheEntryOptions` with all expiration properties set to `null` (which means "never expire" per the .NET caching contract). This is the cleanest solution because it works within the M.E.AI architecture, preserves cache key computation, and doesn't depend on internal file format details. The challenge is injecting this wrapper — `DiskBasedReportingConfiguration.Create()` constructs the cache internally, so you would need to either use reflection, subclass the configuration, or build a custom `ReportingConfiguration` using the lower-level APIs (`ResponseCachingChatClient` + custom `IDistributedCache`). An environment variable can toggle between normal mode (14-day TTL for local development) and fixture mode (no expiration for CI).

```csharp
public sealed class NoExpirationCache : IDistributedCache
{
    private readonly IDistributedCache _inner;
    public NoExpirationCache(IDistributedCache inner) => _inner = inner;
    
    public byte[]? Get(string key) => _inner.Get(key);
    public Task<byte[]?> GetAsync(string key, CancellationToken ct = default)
        => _inner.GetAsync(key, ct);
    
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        => _inner.Set(key, value, new DistributedCacheEntryOptions());
    public Task SetAsync(string key, byte[] value,
        DistributedCacheEntryOptions options, CancellationToken ct = default)
        => _inner.SetAsync(key, value, new DistributedCacheEntryOptions(), ct);
    
    // Delegate Refresh/Remove unchanged
    public void Refresh(string key) => _inner.Refresh(key);
    public Task RefreshAsync(string key, CancellationToken ct) => _inner.RefreshAsync(key, ct);
    public void Remove(string key) => _inner.Remove(key);
    public Task RemoveAsync(string key, CancellationToken ct) => _inner.RemoveAsync(key, ct);
}
```

**Approach 2: Post-process entry.json before committing to git.** Write a pre-commit hook or CI prep script that scans the cache directory, parses each `entry.json` file, and either removes the expiration timestamp or sets it to a far-future date (e.g., `9999-12-31T23:59:59Z`). This is quick and effective but fragile — it depends on the internal file format of `DiskBasedResponseCache`, which is not a public API contract and could change between package versions.

**Approach 3: CI-side timestamp refresh script.** Run a script in the CI pipeline *before* tests that reads every `entry.json` and rewrites the expiration to be 14 days from now. This effectively resets the clock on every CI run without modifying committed files. The tradeoff is that the cache files on disk diverge from what's in git, and you need to understand the exact expiration field name and format.

**Approach 4: Custom fixture-serving IChatClient.** Bypass M.E.AI's caching entirely for CI. Extract cached responses into plain JSON fixture files during recording, then build a minimal `IChatClient` implementation that serves fixtures by computing the same content-addressed key (hash of messages + options) and looking up responses from a dictionary. This gives full control over fixture management but loses integration with M.E.AI's reporting and evaluation pipeline.

## A re-recording workflow that prevents silent breakage

The recommended workflow combines automated detection with manual-trigger re-recording, adapted from the patterns used by VCR.py, Block, and llmock practitioners.

**Recording runs locally with an API key.** A developer runs `dotnet test` with an environment variable like `EVAL_RECORD=true` and valid Claude API credentials. The M.E.AI caching layer fetches fresh responses and writes them to disk. The developer inspects the diffs (new fixture files or changed responses), then commits the updated cache directory. This should happen whenever prompts change, the model version changes, or on a regular cadence (monthly or quarterly).

**Detection of stale fixtures** can be automated through two complementary signals. First, include prompt file checksums in the CI cache key or a manifest file, similar to promptfoo's `checksum "prompts/**/*"` approach — any prompt change flags that fixtures need re-recording. Second, add a scheduled CI job (weekly or biweekly) that runs with real API keys in `record_mode="all"`, compares outputs against committed fixtures using semantic similarity rather than exact match, and alerts when drift exceeds a threshold. llmock's "three-layer triangulation approach" to drift detection is a strong model here.

**CI runs in strict replay mode.** The test configuration uses `enableResponseCaching: true` with committed fixtures and the `ReplayGuardChatClient` as the fallback. Any cache miss — whether from prompt changes, expired TTL, or missing fixtures — immediately throws `InvalidOperationException`. This is desirable: it makes fixture staleness a loud failure rather than a silent one. The fix is always "re-record locally," never "add an API key to CI."

For the specific 22-scenario setup described (Claude Sonnet for plan generation, Claude Haiku for safety judging), re-recording all fixtures should take under 5 minutes with actual API calls. A quarterly re-recording schedule is reasonable for models that don't change frequently, with ad-hoc re-recording triggered by any prompt modification.

## Conclusion

The core problem — M.E.AI's 14-day TTL breaking committed CI fixtures — stems from the library being designed for iterative developer workflows, not offline replay. The most important takeaway is that **no public API currently exists to configure TTL**, making a workaround necessary. The `IDistributedCache` decorator approach (Approach 1) is the most architecturally sound solution because it works within the caching framework's contract rather than manipulating internal file formats. Filing a feature request on dotnet/extensions asking for a `TimeSpan? cacheEntryLifetime` parameter on `DiskBasedReportingConfiguration.Create()` — or even a `Timeout.InfiniteTimeSpan` sentinel to disable expiration — would benefit the broader community. The cache key computation is already well-designed for the fixture use case: prompt changes automatically produce clean misses rather than stale hits, which is exactly the safety property needed for committed fixtures.