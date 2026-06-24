<!--
PROVENANCE: R-083 (batch-30b). Prompt: docs/research/prompts/batch-30b-streaming-llm-eval-harness.md.
Surfaced in the Slice 4B conversation-core brainstorm (2026-06-24). Integration target:
docs/plans/mvp-0-cycle/slice-4b-conversation-core.md § Unit 7 + research-triggers; research-queue R-083.

VERIFICATION PASS (2026-06-24, against the installed packages + EvalTestBase):
- CONFIRMED: the streaming-cache-by-coalescing mechanism (CachingChatClient.CoalesceStreamingUpdates default true,
  ResponseCachingChatClient : DistributedCachingChatClient, DefaultTimeToLiveForCacheEntries) is present in the
  installed M.E.AI / Evaluation.Reporting packages. Buffer-then-assert is the right architecture.

ERRATA (corrected during integration — do not apply the artifact's claims verbatim where flagged):
1. § Version pins names the COMMUNITY `Anthropic.SDK` (tghamm) 5.10.0 as the SDK. WRONG for this repo.
   This repo pins the FIRST-PARTY `Anthropic` 12.29.1 (NuGet id `Anthropic`, Stainless-generated) — verified in
   backend/Directory.Packages.props and the NuGet cache. See R-084 (batch-30c) for the authoritative SDK facts.
2. § Version pins hedges the Evaluation family at "10.4.0-10.6.0". The repo PINS `Microsoft.Extensions.AI.Evaluation`
   (+ .Quality / .Reporting) at 10.7.0 (CPM-pinned; all of 10.4-10.7 are in the local cache). Core
   `Microsoft.Extensions.AI[.Abstractions]` resolves transitively; 10.7.0 is available.
3. The "14-day TTL - the big gotcha" is ALREADY SOLVED in this repo: EvalTestBase + the re-record workflow document a
   TTL-extension step (the batch-8a / DEC-039 resolution). Streaming fixtures INHERIT it - run the existing
   TTL-extension on the new streaming fixtures; this is not novel unsolved work. The artifact's mechanism description
   is still correct and useful context.
-->

# Recording, Replaying, and Asserting Over Streamed LLM Output in Microsoft.Extensions.AI.Evaluation (RunCoach Slice 4B)

## TL;DR
- **Streaming IS cached, byte-stably, with zero new infrastructure.** The eval reporting cache's `ResponseCachingChatClient` derives from `DistributedCachingChatClient`, whose `CachingChatClient` base intercepts `GetStreamingResponseAsync`, coalesces the `IAsyncEnumerable<ChatResponseUpdate>` into one `ChatResponse` via `.ToChatResponse()` on record, and re-splits it via `.ToChatResponseUpdates()` on replay (`CoalesceStreamingUpdates = true` by default). A streamed coaching turn therefore replays from a committed fixture exactly like your plan-gen / adaptation / onboarding evals.
- **Buffer-then-assert is the correct architecture.** Drive `ICoachingLlm.StreamAsync`, concatenate updates to full text with `.ToChatResponse().Text`, and run the existing `VoiceProseGuard` / `TrademarkProseGuard` / Haiku `Restraint` judge over the assembled text — identical to your non-streaming voice evals. Chunk boundaries/timing are an integration/E2E (Playwright) concern, never an eval-layer assertion, because they are non-deterministic on replay and would break Replay determinism.
- **One gotcha that genuinely bites you: the 14-day cache TTL.** `DiskBasedResponseCache` expires entries after `Defaults.DefaultTimeToLiveForCacheEntries = TimeSpan.FromDays(14)`. For committed fixtures this means CI silently breaks two weeks after recording. Neutralize it by passing a large finite `timeToLiveForCacheEntries` TimeSpan to your reporting-configuration factory. Also note the streaming cache key differs from the non-streaming key for the same prompt.

## Key Findings

### 1. Does the caching layer cache streaming at all? Yes — by coalescing.
The Microsoft.Extensions.AI.Evaluation.Reporting cache that backs Replay is `ResponseCachingChatClient`, declared (in `src/Libraries/Microsoft.Extensions.AI.Evaluation.Reporting/CSharp/ResponseCachingChatClient.cs`, confirmed at dotnet/extensions commit `4a53db27`) as:

```csharp
internal sealed class ResponseCachingChatClient : DistributedCachingChatClient
```

It only overrides `ReadCacheAsync`, `ReadCacheStreamingAsync`, `WriteCacheAsync`, and `WriteCacheStreamingAsync` to add latency/cache-hit telemetry; it delegates actual cache I/O to `base`. It does **not** override `GetCacheKey`, `CoalesceStreamingUpdates`, or the streaming logic. So streaming caching behaviour is exactly that of `CachingChatClient` → `DistributedCachingChatClient`.

In `CachingChatClient` (`src/Libraries/Microsoft.Extensions.AI/ChatCompletion/CachingChatClient.cs`), `CoalesceStreamingUpdates` is `public bool … = true`. The summary reads: *"Gets or sets a value indicating whether streaming updates are coalesced. … true if the client attempts to coalesce contiguous streaming updates into a single update, to reduce the number of individual items that are yielded on subsequent enumerations of the cached data … The default is true."* (The coalescing-on-write / re-split-on-read round trip and `true` default are corroborated by the Microsoft Learn `DistributedCachingChatClient` (net-10.0) documentation.) With it `true`, the streaming round-trip is:

- **Record (cache miss):** stream from the inner client, yield each `ChatResponseUpdate` to the caller, accumulate them, then `WriteCacheAsync(cacheKey, capturedItems.ToChatResponse(), …)` — i.e. it stores the **coalesced non-streaming `ChatResponse`**, the same shape as a non-streaming response. (Verbatim source comment: *"When coalescing updates, we cache non-streaming results coalesced from streaming ones … When we get a cache hit, we yield the non-streaming result as a streaming one."*)
- **Replay (cache hit):** `ReadCacheAsync` returns the stored `ChatResponse`, and the client re-emits it as a stream via `chatResponse.ToChatResponseUpdates()`.

So your committed fixture for a streamed turn is a single JSON `ChatResponse`, identical in form to your existing non-streaming fixtures, and replays deterministically as a stream. CI makes zero live Anthropic calls.

(If you ever set `CoalesceStreamingUpdates = false`, it instead caches the raw `IReadOnlyList<ChatResponseUpdate>` via `WriteCacheStreamingAsync` and replays them verbatim. You do **not** want this for byte-stable fixtures — keep the default `true`.)

### 2. Cache-key + manifest stability
`DistributedCachingChatClient.GetCacheKey(messages, options, additionalValues)` JSON-serializes and hashes, in order: a boxed cache-format version (`_cacheVersion = 2`), `messages`, `options`, the caller-supplied `additionalValues`, then the instance's `CacheKeyAdditionalValues`, using `AIJsonUtilities.HashDataToString`. Key facts for RunCoach:

- **A streamed request and a non-streamed request with identical messages+options get DIFFERENT keys.** `CachingChatClient` passes a boxed streaming discriminator — `_boxedFalse` on the non-streaming path, `_boxedTrue` on the streaming path — as `additionalValues`. So you cannot record a fixture via `GetResponseAsync` and replay it via `GetStreamingResponseAsync` (or vice-versa); each surface needs its own recorded fixture. (Note: both streaming sub-branches — coalesce on/off — use the same `_boxedTrue`, so the *key* doesn't change with coalescing, but the cached *payload format* does.)
- **The key is otherwise byte-stable** for a streamed request exactly as it is for `GenerateStructuredAsync`: it derives purely from messages + `ChatOptions` (+ version + additional values). A streaming-only `ChatOptions` difference *would* change the key, so keep `ChatOptions` identical between record and replay. Drive streaming with the same options object you would for the non-streaming path; do not toggle streaming-only options.
- The remark *"The generated cache key is not guaranteed to be stable across releases of the library"* plus the `_cacheVersion` field means a M.E.AI **major-version bump can invalidate every committed fixture**. Pin versions (see below) and re-record deliberately.

**What changes in your harness:** essentially nothing structural. Your `SanitizationAuditChatClient : DelegatingChatClient` already implements `GetStreamingResponseAsync`, so the streamed call routes through the same delegating chain → caching/replay layer → Anthropic SDK as your structured/plain-text calls. Your `rerecord-eval-cache.sh` just needs to invoke the new streaming eval test(s) so their fixtures get written during the Record pass (targeted re-record, not blanket wipe). The `EvalTestBase` Record/Replay switch needs no new code path — a streamed `ScenarioRun` records and replays through the identical `ReportingConfiguration`.

**Prompt-hash (DEC-074):** no flow change. The `.prompt-hashes.sha256` manifest hashes the **prompt bytes**, which are the system/user message templates — orthogonal to whether the response is streamed. Add the streaming system prompt(s) and the Pattern-B classifier prompt to the manifest, regenerate the manifest *before* the Record run (as you already do), and the lefthook hook + `EvalTestBase` static-ctor backstop guard them identically.

### 3. Assertion architecture for streamed coaching text
Buffer the full stream, then assert on the assembled text. This is the supported and correct pattern:

```csharp
List<ChatResponseUpdate> updates = [];
await foreach (var u in coachingLlm.StreamAsync(messages, options, ct))
    updates.Add(u);
ChatResponse response = updates.ToChatResponse();   // safe coalescing
string text = response.Text;
```

`ToChatResponse()` (in `ChatResponseExtensions.cs`) reconstructs `ChatMessage`s using `ChatResponseUpdate.MessageId` to find message boundaries and coalesces contiguous `TextContent` items. Run your existing deterministic guards (`VoiceProseGuard` em-dash/exclamation/banned-phrase, `TrademarkProseGuard`) and the advisory Haiku `VoiceRubrics.Restraint` judge over `text` — byte-identical discipline to the non-streaming voice evals once buffered. Because the fixture is a coalesced `ChatResponse`, the assembled text is deterministic across runs.

**Pitfalls to guard against when reassembling:**
- **Non-text updates:** updates may carry `FunctionCallContent` (tool calls), `UsageContent`, reasoning/`TextReasoningContent`, or annotations rather than display text. Don't `string`-concatenate raw updates; use `.ToChatResponse()` and then read `.Text` (which extracts only text content), or explicitly filter `response.Messages[].Contents` by `TextContent`. For Slice 4B's confirm-then-commit classifier, separate the structured/tool content from the prose before the prose guards run.
- **Ordering / multiple messages:** rely on `MessageId` grouping (what `ToChatResponse` does) rather than raw arrival order. The lossy-conversion caveat (only one `ModelId`/`RawRepresentation` survives) is irrelevant for prose assertions.
- **Do NOT assert chunk count, chunk boundaries, or inter-chunk timing in the eval layer.** On replay the chunking is whatever `ToChatResponseUpdates()` produces from the coalesced response, not the original Anthropic SSE framing — so any chunk-boundary assertion tests the library, not your coach, and is non-deterministic. Incremental-delivery UX (first-token latency, SSE-over-fetch render) belongs in Playwright E2E only.

### 4. Evaluating the classifier + answer quality
The Slice 4B classify-then-extract call is a Pattern-B structured call (`{Question | WorkoutLog}` + `StructuredLogDraft`) and **evals exactly like your existing structured evals** — its position before streaming adds nothing to the eval mechanics; it's a separate cached `GetResponseAsync`/`GenerateStructuredAsync` fixture with its own cache key.

**Classifier accuracy (deterministic, in Replay):**
- Build a ground-truth fixture set: canonical inputs (status / injury / schedule / intensity questions, plus workout-log utterances and deliberately ambiguous ones) each labelled with the expected intent. Record one cached structured response per input.
- In Replay, deserialize each cached classification and assert label == expected. Aggregate into a confusion-matrix-style check (per-class precision/recall, or simply a count of misclassifications) and gate on a threshold (e.g. "0 regressions on the canonical set" or "≥ N/M correct"). Because responses are cached and `Temperature` is fixed at 0, this is fully deterministic — it tests *your prompt*, not model variance.
- Treat "ambiguous" as its own expected class so the eval can assert the classifier asks for confirmation rather than guessing (this aligns with confirm-then-commit).

**Answer quality across question shapes:**
- For deterministic, no-LLM checks, use the `Microsoft.Extensions.AI.Evaluation.NLP` evaluators (`BLEUEvaluator`, `GLEUEvaluator`, `F1Evaluator`) against curated reference answers via `BLEUEvaluatorContext`/`F1EvaluatorContext`. These need no LLM and are perfectly reproducible.
- For semantic quality, use the `Microsoft.Extensions.AI.Evaluation.Quality` LLM-judge evaluators (`RelevanceEvaluator`, `CoherenceEvaluator`, `CompletenessEvaluator`, `GroundednessEvaluator`, `EquivalenceEvaluator`) — but run the judge through the **same cached `IChatClient`** so the judge's own responses are also cached/replayed, keeping CI offline. Call `scenarioRun.EvaluateAsync(messages, modelResponse)`; the buffered streamed response coalesces to the `ChatResponse` you pass in. Gate via `result.Get<NumericMetric>(...).Interpretation.Failed`.
- Set thresholds conservatively from a first recorded baseline; treat the advisory Haiku judge as non-gating (warn) and the deterministic guards as hard-gating, mirroring your existing voice/safety split.

### 5. First-party support to adopt instead of hand-rolling
Adopt the M.E.AI.Evaluation reporting stack rather than hand-rolling a buffer-then-assert adapter for caching:
- Use `DiskBasedReportingConfiguration.Create(storageRootPath, evaluators, chatConfiguration, …)` with response caching enabled (the default). The returned `ReportingConfiguration` exposes a cached `IChatClient` via `ScenarioRun.ChatConfiguration.ChatClient`; **always drive your coach through that client** so both the primary streamed response and the evaluator judge calls are cached.
- `ScenarioRun` + `CreateScenarioRunAsync(scenarioName, iterationName, …)` + `EvaluateAsync(messages, response)` give you per-scenario/iteration result storage and the `dotnet aieval report` HTML report for free — valuable for a solo dev.
- The `DiskBasedResponseCache` is the disk cache that handles streaming (via the coalescing path above) since it inherits the `DistributedCachingChatClient` behaviour. There is **no dedicated "streamed-conversation" or multi-turn scenario runner** in the library today — multi-turn is modelled simply as a longer `messages` list passed to `EvaluateAsync`. So the only thing you "hand-roll" is the tiny buffer loop in §3, which is unavoidable and trivial. Everything else (cache, key, replay, report) is first-party.

## Details

### Concrete recommendation a solo dev can implement
1. **Reuse the existing pipeline.** Route `ICoachingLlm.StreamAsync` through `SanitizationAuditChatClient.GetStreamingResponseAsync` → the eval `ResponseCachingChatClient` (from `ReportingConfiguration`) → Anthropic SDK. No new caching client.
2. **Record:** in the Record pass (`rerecord-eval-cache.sh` with the funded key), run the streaming eval test. The caching layer coalesces and writes one `ChatResponse` fixture under `tests/eval-cache/`. Regenerate `.prompt-hashes.sha256` *before* this run.
3. **Replay (CI):** `EvalTestBase` in Replay mode serves the coalesced fixture and re-streams it. Buffer with `await foreach … updates.Add(u); var text = updates.ToChatResponse().Text;`.
4. **Assert:** run `VoiceProseGuard` + `TrademarkProseGuard` (hard-gate) and `VoiceRubrics.Restraint` Haiku judge (advisory) over `text`; run safety evals over the same fixture.
5. **Classifier:** separate Pattern-B fixtures with expected-label ground truth; confusion-matrix assertion with a zero-regression threshold.
6. **Neutralize TTL:** pass a large finite `timeToLiveForCacheEntries` (e.g. `TimeSpan.FromDays(36500)`) when creating the reporting configuration so committed fixtures don't expire in CI.

### Alternatives considered and why rejected
- **Native streaming-cache that stores raw chunks (`CoalesceStreamingUpdates = false`):** rejected. It caches the raw update list, which is larger, noisier, and tied to the original provider's chunk framing — worse for byte-stable committed fixtures and offers no eval benefit since you assert on assembled text anyway.
- **Buffer-then-assert (chosen):** minimal code, deterministic, reuses 100% of existing voice/safety discipline.
- **Split streaming from eval (eval the prompt only via the cached non-streaming `GetResponseAsync`, assert streaming delivery in Playwright only):** partially adopted — Playwright *should* own incremental-delivery UX. But evaluating the prompt via the non-streaming path produces a **different cache key** and does not exercise the actual streamed `ICoachingLlm.StreamAsync` surface or `SanitizationAuditChatClient.GetStreamingResponseAsync`. Recommendation: record the fixture via the *streaming* surface (so the real code path is covered), and additionally keep Playwright for delivery. Do not substitute the non-streaming path for the content eval.

### Version pins (as of mid-2026 — verify on each NuGet page before pinning)
- **Microsoft.Extensions.AI / .Abstractions:** 10.7.0 was observed as the current NuGet listing during research, but this could not be independently re-confirmed by a second check — verify against the Microsoft.Extensions.AI NuGet page before pinning. Functionally, `CoalesceStreamingUpdates` and the coalescing streaming-cache round-trip require a reasonably recent build; any 9.5+/10.x is safe, but pin to the 10.x line for .NET 10.
- **Microsoft.Extensions.AI.Evaluation, .Quality, .Reporting:** these ship in lockstep, but observed listings during research spanned **10.4.0–10.6.0** across the family (e.g. core Evaluation/.Reporting seen at 10.4.0, Quality at 10.5.0, Reporting also seen at 10.6.0). **Treat the exact number as unverified and confirm on each NuGet page**, then pin all eval packages to one identical 10.x version. The `timeToLiveForCacheEntries` parameter and `Defaults.DefaultTimeToLiveForCacheEntries` are present throughout current 10.x.
- **Anthropic .NET SDK (`Anthropic.SDK`, tghamm):** 5.10.0, released 2026-02-20 (GitHub Releases marks "v5.10.0 Latest"; NuGet release note: "Opus/Sonnet 4.6 Series, Adaptive Thinking, Costing, Web Search/Fetch, Bug Fixes, Latest M.E.AI"; targets NetStandard 2.0, .NET 8.0, .NET 10.0). Supports `IChatClient` streaming (`GetStreamingResponseAsync` → `ChatResponseUpdate`), constrained/structured decoding, and Claude 4.x models including Haiku for the advisory judge. This is the SDK that exposes native constrained decoding for Pattern-B.
- **xUnit v3:** `xunit.v3` 3.2.2 (latest stable on NuGet; installing it pulls `xunit.v3.mtp-v1`; "Supports .NET Framework 4.7.2 or later and .NET 8 or later"). Works with `dotnet test` / Microsoft Testing Platform on .NET 10.

### Gotchas
- **14-day TTL (the big one):** `DiskBasedResponseCache` sets each entry's expiration to creation + `timeToLiveForCacheEntries`, defaulting to `Defaults.DefaultTimeToLiveForCacheEntries = TimeSpan.FromDays(14)` (confirmed in source and in the Microsoft Learn safety/quality tutorials, which note "the cached entry has expired (14 days, by default)"). Entries are evicted lazily on read once expired — so committed fixtures silently start missing ~2 weeks after recording, breaking offline CI. Fix: pass a large finite TTL through the reporting-configuration factory (avoid `TimeSpan.MaxValue`, which overflows `DateTime.Add`).
- **Streaming vs non-streaming cache-key divergence:** record each surface through the surface it will replay through.
- **Cache-version / library-version invalidation:** `_cacheVersion` and the "not stable across releases" remark mean a M.E.AI upgrade can invalidate fixtures; pin versions and re-record on deliberate bumps.
- **Replay determinism:** never assert chunk ordering/count/timing; assert only on coalesced text. The replay chunking is synthetic.
- **Non-text updates:** filter tool-call/usage/reasoning content out before prose guards.
- **`ChatOptions` drift:** keep options byte-identical between record and replay; a streaming-only option flip changes the key.

## Recommendations
1. **Now:** Add the streaming eval as a `ScenarioRun` driven through the cached `IChatClient` from your `ReportingConfiguration`; buffer with `.ToChatResponse().Text`; reuse `VoiceProseGuard`/`TrademarkProseGuard`/Haiku judge. Record once with the funded key; commit the single coalesced `ChatResponse` fixture.
2. **Now:** Set a large finite `timeToLiveForCacheEntries` in your reporting-config factory so committed fixtures never expire in CI. Benchmark/trigger to change: if CI ever shows a cache miss in Replay, the TTL or a key change is the cause.
3. **Now:** Add the streaming system prompt + the Pattern-B classifier prompt to `.prompt-hashes.sha256`; regenerate before Record.
4. **Next:** Build the labelled classifier ground-truth set (status/injury/schedule/intensity + workout-log + ambiguous); assert zero confusion-matrix regressions. Threshold to change: tighten from "no regressions" to per-class recall ≥ target once you have enough canonical cases.
5. **Next:** Layer Quality LLM-judge evaluators (Relevance/Coherence/Completeness) over the assembled text, judge running through the cached client; keep them advisory until baseline scores stabilize, then promote to gating.
6. **Keep separate:** Playwright owns SSE-over-fetch incremental-delivery and first-token latency; the eval layer never touches chunk timing.

## Caveats
- The internal type/method names (`ResponseCachingChatClient`, `CoalesceStreamingUpdates`, `_boxedTrue`/`_boxedFalse`, `GetCacheKey`, `Defaults.DefaultTimeToLiveForCacheEntries`) are confirmed from dotnet/extensions source at commit `4a53db27` (early 2026). `ResponseCachingChatClient` and `DiskBasedResponseCache` are `internal`; rely on the public `ReportingConfiguration` / `DiskBasedReportingConfiguration.Create` factory surface rather than these types directly, since internals can change between releases.
- The exact public parameter list of `DiskBasedReportingConfiguration.Create` (specifically the `timeToLiveForCacheEntries` argument name on the public factory) should be verified against the Microsoft Learn API page for your pinned package version before wiring it up — the parameter name and 14-day default are confirmed at the `DiskBasedResponseCache` constructor level, but the public factory signature was not quoted verbatim.
- All NuGet version numbers above carry some uncertainty (the eval family was observed spanning 10.4.0–10.6.0 and M.E.AI at 10.7.0 in research, not all re-confirmed); confirm each on its NuGet page when pinning, and pin the whole eval family to one identical version.
- Cache keys and fixture validity are tied to the M.E.AI library version; a major bump is a deliberate re-record event, not a silent upgrade.