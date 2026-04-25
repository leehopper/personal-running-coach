# Batch 17b — Anthropic SDK Choice for RunCoach (.NET 10 + M.E.AI)

**Artifact:** `docs/research/artifacts/batch-17b-anthropic-sdk-choice.md`
**Date:** 2026-04-24
**Author:** Research agent (solo)
**Status:** Decision-grade research; the executive verdict is load-bearing for Slice 1.
**Related decisions:** DEC-022 (provider lock-in), DEC-037 (the bridge), DEC-039 (eval cache), DEC-047 (onboarding schema), R-048, R-051, R-053.

---

## 1. Executive verdict

**Migrate now, before Slice 1 lands.** Replace the DEC-037 `AnthropicStructuredOutputClient` bridge with the first-party `Anthropic` NuGet (currently 12.17.0 on NuGet.org as of April 2026), consumed through its built-in `AsIChatClient("model-id")` entry point into `Microsoft.Extensions.AI` [[NuGet: Anthropic 12.17.0]](https://www.nuget.org/packages/Anthropic/) [[Claude Docs — C# SDK]](https://platform.claude.com/docs/en/api/sdks/csharp). The original DEC-037 problem (community SDK silently dropping `ChatResponseFormat.ForJsonSchema()`) is no longer the right problem to solve, because (a) Anthropic shipped **native, GA structured outputs via `output_config.format`** in 2025 — schema compliance is now an API contract, not a tool-use hack [[Claude Docs — Structured outputs]](https://platform.claude.com/docs/en/build-with-claude/structured-outputs); (b) the first-party SDK's `AsIChatClient` adapter now explicitly maps M.E.AI's `ChatResponseFormat` into that preconfigured `output_config`, merged in PR #166 "client: merge response format into preconfigured output config" shipped in Anthropic-v12.12.0 [[anthropic-sdk-csharp releases]](https://github.com/anthropics/anthropic-sdk-csharp/releases); and (c) the "keep the bridge" path accrues a compounding 12-month cost — every Anthropic feature shipped (vision, batches, `cache_control` TTL changes, extended thinking signature semantics, Claude Mythos/Opus 4.7 model IDs) must be manually reflected in a DelegatingChatClient that exists *solely* to work around a bug that was upstream-fixed a quarter ago. Keep the bridge *only* if integration testing in a throwaway spike proves the first-party `AsIChatClient` still drops schema on Claude Sonnet 4.6/Haiku 4.5, and even then keep it as a **temporary** shim with an expiry date. The hybrid is the worst option for a solo side project: it doubles the surface area you must reason about on every new call site.

---

## 2. Capability comparison matrix

The three paths:

- **(A) First-party `Anthropic` 12.x via `AsIChatClient`** — the `anthropics/anthropic-sdk-csharp` Stainless-generated package, MIT, in beta but GA-quality on core surface [[NuGet]](https://www.nuget.org/packages/Anthropic/) [[GitHub]](https://github.com/anthropics/anthropic-sdk-csharp).
- **(B) `AnthropicStructuredOutputClient` bridge (DEC-037)** — custom `DelegatingChatClient` over either `Anthropic.SDK` (tghamm, 5.10.0) or the first-party raw client, intercepting `ForJsonSchema` and translating by hand.
- **(C) Hybrid** — DI-register (A) for chat + streaming, (B) for any call site that carries `ChatResponseFormat.ForJsonSchema`.

| Capability | (A) First-party `AsIChatClient` | (B) DEC-037 bridge | (C) Hybrid |
|---|---|---|---|
| **`ChatResponseFormat.ForJsonSchema()`** | ✅ End-to-end. `AsIChatClient` maps M.E.AI's `ChatResponseFormatJson.Schema` into Anthropic's native `output_config.format: {type: "json_schema", schema: ...}` (PR #166 "merge response format into preconfigured output config", Anthropic-v12.12.0, April 2026) [[releases]](https://github.com/anthropics/anthropic-sdk-csharp/releases). Anthropic's API has **GA** structured-output grammar-constrained decoding for Sonnet 4.5+, Opus 4.1+, Haiku 4.5 — guaranteed schema compliance, not best-effort [[structured outputs docs]](https://platform.claude.com/docs/en/build-with-claude/structured-outputs). | ✅ Works today on the models you originally tested it with. **But** it encodes the old "tool-use-as-JSON-mode" pattern per the Anthropic cookbook (synthetic tool, forced `tool_choice`, fake `tool_result` echoes) [[cookbook: extracting_structured_json]](https://github.com/anthropics/anthropic-cookbook/blob/main/tool_use/extracting_structured_json.ipynb) [[sdk-python issue #1034]](https://github.com/anthropics/anthropic-sdk-python/issues/1034). That path is now **inferior** to native `output_config` — weaker guarantees, extra tokens, interacts poorly with extended thinking and real tool use per Anthropic's own docs. | ✅ Works, but you maintain two code paths for the same feature forever. |
| **Prompt caching `cache_control` + `ttl: "1h"`** | ⚠️ **Raw-SDK drop-down required.** M.E.AI's `ChatOptions` has no first-class `cache_control` surface, so `AsIChatClient` either (i) relies on Anthropic's server-side **automatic caching** (enabled via top-level `cache_control: {type: "ephemeral"}` on the raw params) or (ii) you bypass `AsIChatClient` and call `client.Messages.Create(parameters)` directly for cache-critical paths, building `MessageCreateParams` with block-level `cache_control`. Anthropic silently dropped the default TTL from 1h to 5min in March 2026 — **you must explicitly set `"ttl": 3600`** [[dev.to: TTL regression]](https://dev.to/whoffagents/anthropic-silently-dropped-prompt-cache-ttl-from-1-hour-to-5-minutes-16ao). Cache metrics (`cache_creation_input_tokens`, `cache_read_input_tokens`) are on `Message.Usage`, reachable via `ChatResponse.RawRepresentation` or `AdditionalProperties`. | ⚠️ Same raw-SDK drop-down; the bridge already does this today for DEC-037, so the muscle memory exists. | ⚠️ Same constraint regardless. |
| **Tool use (`tool_use` / `tool_result` round-trip)** | ✅ Clean. `AsIChatClient(...).AsBuilder().UseFunctionInvocation().Build()` is the documented pattern in Anthropic's own docs and is demonstrated with the MCP C# SDK [[Claude Docs — C# SDK]](https://platform.claude.com/docs/en/api/sdks/csharp). `AIFunctionFactory.Create(...)` maps to Anthropic tool schemas; typed `tool_use.input` and verbatim `tool_use_id` round-tripping are handled by the bridge. | ⚠️ Works, but every new tool-use feature (parallel tools, `disable_parallel_tool_use`, `strict: true` tools, computer-use tools, `tool_choice` variants) has to be plumbed through your delegating client by hand. | ⚠️ Doubled maintenance. |
| **Extended thinking (`thinking` / `redacted_thinking`, verbatim `signature`)** | ⚠️ **Gap.** As of the April 2026 docs, Anthropic explicitly notes: *"No SDK currently includes `display` in its type definitions… The C#, Go, Java, PHP, and Ruby SDKs require a direct HTTP request until native support lands."* for `display: "omitted"` [[extended thinking docs]](https://docs.anthropic.com/en/docs/build-with-claude/extended-thinking). The first-party SDK returns `thinking`/`redacted_thinking` blocks in `Message.Content` and preserves `signature`, but M.E.AI's `TextReasoningContent` abstraction may not surface the signature through `AsIChatClient` without drop-down. **For full verbatim round-trip today** (required for multi-turn tool use with thinking enabled), you store the raw `Message` in the event log and replay by serializing `MessageCreateParams`. The community `Anthropic.SDK` currently has richer M.E.AI-side plumbing here (`ThinkingContent`/`RedactedThinkingContent`, `Delta.Signature` streaming) [[DeepWiki: tghamm/Anthropic.SDK extended thinking]](https://deepwiki.com/tghamm/Anthropic.SDK/3.6-extended-thinking), but the first-party SDK lets you reach the signature via `RawRepresentation` on the `ChatResponse`. | ⚠️ If your bridge is built on tghamm's `Anthropic.SDK`, thinking support is arguably *more complete* today. If it's built on the raw first-party SDK, it's equivalent. | ⚠️ Equivalent to (A) for future slices unless the bridge is specifically upgraded. |
| **Streaming (`IAsyncEnumerable<ChatResponseUpdate>`)** | ✅ `AsIChatClient` implements `GetStreamingResponseAsync` over the first-party SDK's native `client.Messages.CreateStreaming(...)` which returns `IAsyncEnumerable` over SSE events [[Claude Docs — C# SDK]](https://platform.claude.com/docs/en/api/sdks/csharp). Structured outputs also stream (text deltas form the JSON, accumulate and parse at end) [[Vercel: Anthropic structured outputs]](https://vercel.com/docs/ai-gateway/sdks-and-apis/anthropic-messages-api/structured-outputs). | ⚠️ Works if the bridge was written for it. If the bridge was only built for non-streaming structured-output paths, Slice 4's streaming will require extending it. | ⚠️ Have to pick which client handles streaming. |
| **Eval-cache compatibility (DEC-039, `Microsoft.Extensions.AI.Evaluation`)** | ⚠️ **Invalidation risk.** `DiskBasedResponseCache` (in `Microsoft.Extensions.AI.Evaluation.Reporting.Storage`) is an `IDistributedCache` that caches by content-hash of the serialized `ChatMessage[]` + `ChatOptions` [[MS Learn: DiskBasedResponseCache]](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.evaluation.reporting.storage.diskbasedresponsecache). M.E.AI's `DistributedCachingChatClient` docs explicitly warn: *"It is not guaranteed that the object models used by `ChatMessage`, `ChatOptions`, `ChatResponse`, … will roundtrip through JSON serialization with full fidelity. For example, `RawRepresentation` will be ignored, and `Object` values in `AdditionalProperties` will deserialize as `JsonElement`"* [[MS Learn: DistributedCachingChatClient]](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.distributedcachingchatclient). **Key insight:** because the cache key is hashed from the *M.E.AI-shaped* `ChatOptions` and messages — not from the underlying Anthropic wire payload — swapping the bottom `IChatClient` implementation **does not change the cache key** as long as your `ClaudeCoachingLlm` still constructs identical `ChatMessage`/`ChatOptions`. Fixtures recorded against the bridge *should* replay against the first-party client, provided you don't switch from `ResponseFormat = ChatResponseFormat.ForJsonSchema(...)` to some provider-specific `AdditionalProperties` trick. Recommendation: ensure `CacheKeyAdditionalValues` includes a pinned "schema version" so you can invalidate intentionally without hash drift [[MS Learn: DistributedCachingChatClient.CacheKeyAdditionalValues]](https://github.com/dotnet/dotnet-api-docs/blob/main/xml/Microsoft.Extensions.AI/DistributedCachingChatClient.xml). | ✅ Same cache, same keys — that's the whole point of the `IChatClient` layer. Stability is the bridge's main virtue. | ✅ Same. |
| **Observability hooks (OTel, R-051)** | ✅ M.E.AI's `OpenTelemetryChatClient` wraps *any* `IChatClient` via `.UseOpenTelemetry(loggerFactory, sourceName, cfg => cfg.EnableSensitiveData = true)` and emits GenAI OTel semantic-convention spans (`chat <model>`, `gen_ai.operation.name`, `gen_ai.request.model`, etc.) regardless of underlying implementation [[MS Learn: UseOpenTelemetry]](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.opentelemetrychatclientbuilderextensions.useopentelemetry) [[Agent Framework observability]](https://learn.microsoft.com/en-us/agent-framework/tutorials/agents/enable-observability). Source name is `Experimental.Microsoft.Extensions.AI`. | ✅ Same — the bridge sits inside the `IChatClient` pipeline so OTel wraps it identically. | ✅ Same. |
| **License / supply chain** | MIT; published by NuGet owners `Anthropic`, `felix-anthropic`, `packy-anthropic` [[NuGet]](https://www.nuget.org/packages/Anthropic/); ~737K total downloads, 246 GitHub stars, 5 open issues, 8 PRs, last commit **Apr 23 2026** [[GitHub]](https://github.com/anthropics). Dependencies: `System.Text.Json (>= 10.0.2)` + `Microsoft.Extensions.AI.Abstractions` (implicit). Works on .NET Standard 2.0+. Stainless-generated from the OpenAPI spec — auto-regenerated as the API evolves, which is both a pro (freshness) and a con (less hand-polished C# ergonomics). | MIT (tghamm/`Anthropic.SDK` 5.10.0), unofficial. 696K+ downloads, actively maintained, but the README explicitly disclaims affiliation with Anthropic [[NuGet: Anthropic.SDK 5.10.0]](https://www.nuget.org/packages/Anthropic.SDK). You inherit whatever cadence tghamm chooses to ship. | Both SDKs as deps. |
| **Future-feature lead time** | ✅ **Canonical.** New Anthropic API surface ships in the OpenAPI spec the same day as the Python/TS SDKs — recent releases added `claude-mythos-preview`, `claude-opus-4-7`, web fetch tool, etc. within days [[releases]](https://github.com/anthropics/anthropic-sdk-csharp/releases). Anthropic docs now treat the C# SDK as a first-class citizen in examples alongside Python/TS [[Claude Docs — Client SDKs]](https://docs.anthropic.com/en/api/client-sdks). | ⚠️ **Trailing.** Community SDK updates depend on tghamm noticing + porting. Historically it has shipped features late (multi-month lag on Messages API breaking changes, months on extended-thinking semantics). Over a 12-month horizon with Anthropic's cadence (Claude 4.5/4.6/4.7 in <12 months, structured outputs GA in Q1 2026, TTL regression in March 2026, computer use, batches, files API, skills API), cadence-asymmetry is the single most decisive factor. | ⚠️ You keep the community SDK as a required transitive dep just to preserve the bridge. |
| **Real-world adoption signal** | 737K downloads of the `Anthropic` NuGet; ~105K downloads of v12.x alone since its mid-February 2026 release; Microsoft's MCP partnership with Anthropic [[MS Dev Blog: Microsoft partners with Anthropic on MCP]](https://developer.microsoft.com/blog/microsoft-partners-with-anthropic-to-create-official-c-sdk-for-model-context-protocol) aligns incentives for first-party + M.E.AI to co-evolve. Microsoft Foundry public-preview hosts Claude Sonnet 4.5 / Opus 4.1 / Haiku 4.5, making "Anthropic via M.E.AI" a supported production path [[elbruno.com]](https://elbruno.com/2025/12/04/claude-in-azure-with-net-anthropic-claude-microsoft-extensions-ai-meai-%F0%9F%92%A5/). Microsoft Agent Framework has a native `AsAIAgent` extension on `IAnthropicClient` [[MS Learn: AnthropicClientExtensions.AsAIAgent]](https://learn.microsoft.com/en-us/dotnet/api/anthropic.anthropicclientextensions.asaiagent). | tghamm's SDK still has healthy download velocity but its .NET 10 migration lagged the first-party SDK by months; its `README.md` already shows the `Microsoft.SemanticKernel` + `AsBuilder().UseFunctionInvocation().Build()` pattern [[GitHub: tghamm/Anthropic.SDK]](https://github.com/tghamm/Anthropic.SDK) but the sample code paths emphasize its native types, not M.E.AI. | N/A. |

---

## 3. Explicit verdict on the original DEC-037 problem

**The bug that motivated DEC-037 is fixed twice over, in two different places, for two different reasons.**

**Fix #1 — Upstream API redesign (Anthropic's side, not a C# problem anymore).** In Q4 2025 Anthropic shipped a beta for native structured outputs under the `structured-outputs-2025-11-13` beta header with `output_format`, and in Q1 2026 promoted it to GA under `output_config.format` (no beta header required) [[Claude Docs — Structured outputs]](https://platform.claude.com/docs/en/build-with-claude/structured-outputs). This is grammar-constrained decoding that compiles your JSON Schema into a grammar the model is physically constrained to emit — not a tool-use hack and not prompt-level hoping. Anthropic's docs state it's generally available on Claude Mythos Preview, Opus 4.7/4.6/4.5, Sonnet 4.6/4.5, and Haiku 4.5. Refusal messages take precedence over schema constraints (a separate design choice), but schema compliance itself is contractual [[Claude Docs — Structured outputs]](https://platform.claude.com/docs/en/build-with-claude/structured-outputs). **This means the entire DEC-037 problem class — "the SDK silently dropped schema so we hand-rolled a tool-use fallback" — is replaced by a cleaner, more powerful primitive.** Even if your DEC-037 bridge works, it is solving the 2025 problem, not the 2026 problem.

**Fix #2 — First-party SDK explicit schema mapping.** The specific code path: `AsIChatClient` in the `Anthropic` 12.x NuGet reads `ChatOptions.ResponseFormat`, and when it's a `ChatResponseFormatJson` with a non-null schema, it sets `MessageCreateParams.OutputConfig.Format` to the corresponding `json_schema` object. This behavior was added in PR #166 **"client: merge response format into preconfigured output config"** and shipped in Anthropic-v12.12.0 [[releases]](https://github.com/anthropics/anthropic-sdk-csharp/releases). Unlike the 2025 community-SDK bug where `ResponseFormat` was silently dropped, the first-party SDK now explicitly wires it through. Microsoft's own structured-outputs quickstart for Anthropic-hosted models in Foundry uses `GetResponseAsync<T>(...)` end-to-end with Claude deployments [[MS Learn: structured-output quickstart]](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/structured-output).

**Caveat — verify before you delete the bridge.** The Anthropic docs' "SDK helper syntax" section notes that unlike Python (Pydantic), TS (Zod), Java (POJO), Ruby and PHP, the **"CLI, C#, Go: Raw JSON schemas passed via output_config"** [[Claude Docs — Structured outputs]](https://platform.claude.com/docs/en/build-with-claude/structured-outputs). This means the C# SDK doesn't yet have a Pydantic-style helper that derives a schema from a C# type — you pass the raw JSON schema dictionary. This is fine for RunCoach: M.E.AI has `AIJsonUtilities.CreateJsonSchema(typeof(T))` and `ChatResponseFormat.ForJsonSchema<T>()` which produce an M.E.AI-shaped `ChatResponseFormatJson.Schema` as a `JsonElement` [[MS Learn: ChatResponseFormat]](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatresponseformat) [[MS Learn: GetResponseAsync<T>]](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/structured-output). `AsIChatClient` then forwards that `JsonElement` into `output_config.format.schema`. **Required verification step (30 min spike):** run an integration test against `claude-sonnet-4-6` and `claude-haiku-4-5` with a 4-field schema matching Slice 1's onboarding DTO (`{ extracted, reply, confidence, needs_clarification }`), assert (a) response parses, (b) `response.Message.RawRepresentation` shows no `tool_use` blocks (confirming native path not fallback), (c) a deliberately ambiguous prompt still produces schema-valid JSON. If that spike passes, DEC-037's bridge is provably redundant. If it fails, file an issue on `anthropics/anthropic-sdk-csharp` — given the repo's 5-day commit cadence the fix turnaround will likely be shorter than the bridge maintenance cost.

**Remaining known issues in v12.x** that do NOT block migration: Issue #47 reports that setting `APIKey` in code still adds `Authorization: Bearer` header from env var — workaround is to unset `ANTHROPIC_AUTH_TOKEN` [[issue #47]](https://github.com/anthropics/anthropic-sdk-csharp/issues/47). For a solo side project reading `ANTHROPIC_API_KEY` from environment, this is a non-issue.

---

## 4. Migration scope estimate

RunCoach is pre-Slice-1, which means the migration surface is intentionally small *today* and grows every slice you defer. File-by-file impact for a migration executed **now**:

| File | Change | Effort |
|---|---|---|
| `*.csproj` (API project) | Add `<PackageReference Include="Anthropic" Version="12.17.0" />`. Remove reference to `Anthropic.SDK` (tghamm) if the bridge was built on it. | 5 min |
| `Infrastructure/Ai/AnthropicStructuredOutputClient.cs` (DEC-037 bridge) | **Delete.** | 2 min |
| `Infrastructure/Ai/ClaudeCoachingLlm.cs` | Unchanged in shape — it still depends on `IChatClient`. Confirm it calls `GetResponseAsync(messages, new ChatOptions { ResponseFormat = ChatResponseFormat.ForJsonSchema<OnboardingTurnOutput>(...), ModelId = ... })` as-is. | 0 min (if already M.E.AI-shaped) |
| `Program.cs` / DI composition root | Change the `IChatClient` registration from `services.AddSingleton<IChatClient>(new AnthropicStructuredOutputClient(...))` to `services.AddChatClient(b => b.Use(new AnthropicClient().AsIChatClient("claude-sonnet-4-6")))` wrapped with the desired middleware (see §5). | 15 min |
| `Infrastructure/Ai/PromptCachingDecorator.cs` (if you have one, or add now) | A small `DelegatingChatClient` that for messages flagged as cache-eligible, drops down to `client.GetService<AnthropicClient>()` and calls `Messages.Create` with explicit `cache_control: { type: "ephemeral", ttl: "1h" }` on system-prompt blocks. This is **new work** not caused by the migration — you need it regardless of SDK choice because M.E.AI has no cache abstraction. | 45–90 min |
| `Tests/Integration/LlmIntegration.cs` | Re-record a small number of golden fixtures against the new client. Because the eval-cache key is hashed from the M.E.AI-shaped request (§2), existing fixtures for non-structured paths should replay; only structured-output fixtures recorded via the bridge's raw-SDK detour may have been keyed on options that no longer match. Bump `CacheKeyAdditionalValues` with `"sdk=anthropic-first-party-12.17"` so a full re-record is forced, done once, and future swaps are explicit. | 30–60 min |
| `docs/decisions/DEC-037-anthropic-structured-output-bridge.md` | Supersede. Link to this artifact and a new DEC-0xx "Migrate to first-party Anthropic SDK". | 20 min |
| `docs/decisions/DEC-047-onboarding-turn-schema.md` | No change to the schema; add a note that it's now enforced via `output_config.format` natively. | 5 min |

**Total estimate for a solo developer on 30–45 min capped sessions: 3–4 sessions (~2 hours of focused work) plus one integration-test spike of 30 minutes to verify the v12.x structured-output path.** This is less than the effort to extend the bridge for streaming (Slice 4), and dramatically less than the rolling cost of keeping the bridge in sync with Anthropic's feature cadence.

**Do the migration before Slice 1 starts.** If you write 6–12 new LLM call sites under the bridge in Slice 1 and then migrate, you rewrite each call site's test fixtures. Migrating now costs 2 hours; migrating after Slice 1 costs 6–10 hours.

---

## 5. Wiring sketch for the recommended path

```csharp
// Program.cs (ASP.NET Core 10, .NET 10)
using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage; // DEC-039
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ---- 1) The raw first-party Anthropic client (singleton). ----
// Reads ANTHROPIC_API_KEY from env. Configure retries/timeouts here.
builder.Services.AddSingleton<AnthropicClient>(sp => new AnthropicClient
{
    MaxRetries = 3,                       // Anthropic docs: default is 2, exponential backoff
    Timeout    = TimeSpan.FromSeconds(60) // 10-min default is too generous for a coaching UX
});

// ---- 2) The IChatClient pipeline: bottom = first-party, then middleware. ----
builder.Services.AddChatClient(sp =>
{
    var raw      = sp.GetRequiredService<AnthropicClient>();
    var logger   = sp.GetRequiredService<ILoggerFactory>();

    // AsIChatClient binds a default model; ClaudeCoachingLlm can override per-call via ChatOptions.ModelId.
    IChatClient chat = raw.AsIChatClient(modelId: "claude-sonnet-4-6");

    return new ChatClientBuilder(chat)
        // R-051: OTel spans using GenAI semantic conventions.
        // Source name matches Microsoft Agent Framework convention so Aspire/Foundry picks it up.
        .UseOpenTelemetry(
            loggerFactory: logger,
            sourceName:    "RunCoach.Llm",
            configure:     cfg => cfg.EnableSensitiveData =
                              builder.Environment.IsDevelopment())
        // Function invocation loop — needed when Slice 4 introduces tools.
        .UseFunctionInvocation()
        // DEC-039: eval replay via M.E.AI.Evaluation's DiskBasedResponseCache.
        // Only active when the EvalHarness sets the ambient cache; production passes through.
        .UseDistributedCache(sp.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>())
        .Build();
});

// ---- 3) Domain-level interface remains unchanged. ----
builder.Services.AddSingleton<ICoachingLlm, ClaudeCoachingLlm>();

var app = builder.Build();
app.Run();
```

```csharp
// Infrastructure/Ai/ClaudeCoachingLlm.cs — unchanged shape, lean implementation.
public sealed class ClaudeCoachingLlm(IChatClient chat, YamlPromptStore prompts) : ICoachingLlm
{
    public async Task<OnboardingTurnOutput> NextTurnAsync(
        OnboardingContext ctx, CancellationToken ct)
    {
        var system = await prompts.RenderAsync("coaching-v1", ctx);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, system),
            // Conversation history from the Marten event stream...
            .. ctx.History.Select(turn => new ChatMessage(turn.Role, turn.Text)),
            new(ChatRole.User, ctx.LatestUserText),
        };

        var options = new ChatOptions
        {
            ModelId        = "claude-sonnet-4-6",
            MaxOutputTokens = 1024,
            ResponseFormat = ChatResponseFormat.ForJsonSchema<OnboardingTurnOutput>(),
            // AdditionalProperties used to carry cache_control semantics to a downstream decorator
            // that drops into the raw AnthropicClient when needed (see below).
            AdditionalProperties = new() { ["runcoach.cache_system"] = true },
        };

        var resp = await chat.GetResponseAsync<OnboardingTurnOutput>(messages, options, ct);
        return resp.Result!;
    }
}
```

```csharp
// Infrastructure/Ai/AnthropicCacheControlDecorator.cs
// Why this exists: M.E.AI has no cache_control abstraction. For non-structured long-system-prompt
// calls you want prompt caching, you need to drop through to the raw SDK. This decorator ONLY does
// that for requests flagged via AdditionalProperties; everything else passes through unchanged.
public sealed class AnthropicCacheControlDecorator(IChatClient inner, AnthropicClient raw)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
    {
        if (options?.AdditionalProperties?.ContainsKey("runcoach.cache_system") != true)
            return await base.GetResponseAsync(messages, options, ct);

        // Build MessageCreateParams by hand so we can set cache_control with ttl=1h per
        // https://dev.to/whoffagents/anthropic-silently-dropped-prompt-cache-ttl-from-1-hour-to-5-minutes-16ao
        var parameters = BuildParamsWithCacheControl(messages, options);
        var msg = await raw.Messages.Create(parameters, cancellationToken: ct);
        return ChatResponseFromAnthropicMessage(msg); // small mapper; preserves tool_use, thinking blocks
    }
}
```

Register the decorator *inside* the pipeline below `UseOpenTelemetry` so spans still capture the caching path. You do not need this decorator on day one of Slice 1 if you're fine with Anthropic's server-side automatic-caching behaviour (enabled by `cache_control: {type: "ephemeral"}` at the top level of the request, which the first-party SDK accepts via `MessageCreateParams`). Add the decorator when you measure a cache-read-tokens ratio that is unacceptably low.

---

## 6. Keep-bridge-but-prepare-for-future-migration plan

*Provided for completeness. Only adopt this if your structured-output spike in §3 fails against the live Anthropic API.*

**Monitor for:**

1. New releases on `anthropics/anthropic-sdk-csharp` [[releases]](https://github.com/anthropics/anthropic-sdk-csharp/releases) that mention `output_config`, `ResponseFormat`, `ChatResponseFormat`, `json_schema`, or close GitHub issues that reference structured outputs. PR #166 already landed; watch for any regression.
2. Removal of the `currently in beta` disclaimer on [platform.claude.com/docs/en/api/sdks/csharp](https://platform.claude.com/docs/en/api/sdks/csharp). That promotion-to-stable is the canonical "migrate now" signal.
3. New Anthropic features that RunCoach needs (extended thinking display modes, 1h `ttl` nuances, Mythos/Opus 4.7 model IDs) shipping to the first-party SDK before the community SDK — widening cadence gap is itself evidence.

**Invariants to preserve in current code to keep migration cheap:**

- **Keep `ICoachingLlm` a project-internal interface.** Do not leak any Anthropic or `Anthropic.SDK` types into your domain (no `MessageParameters`, no `RoleType.User`, no `CacheControl` types) [[DEC-022]]. Your `ClaudeCoachingLlm` adapter is the only place that knows which SDK is underneath.
- **Ensure `ClaudeCoachingLlm` depends only on `IChatClient` + `ChatOptions` + `ChatMessage`** — the M.E.AI abstractions. The bridge's job is to make `ForJsonSchema` work through that surface, not to expose a new surface.
- **Tag your prompt YAML with a schema version string.** When you migrate, bumping the version guarantees clean eval-cache invalidation rather than silent drift.
- **Record in a comment on `AnthropicStructuredOutputClient.cs` the exact Anthropic SDK version and commit hash it was written against.** Future-you will thank present-you.
- **Write the eval-cache `CacheKeyAdditionalValues` to include SDK identity** (e.g. `"sdk=bridge-v1"`). When you eventually migrate, changing this value forces a clean re-record.
- **Do not bolt streaming onto the bridge.** If Slice 4 needs streaming, that's your forcing function to migrate — do not extend the bridge to streaming just to defer the decision, because that triples the migration cost.

**Re-evaluate:**

- Before starting Slice 4 (streaming) — unconditional re-eval.
- Any time a new Claude model ships and isn't in the SDK you're on.
- Quarterly, as a calendar item.

---

## 7. Interaction note with R-051 (LLM observability)

The recommended SDK composes with M.E.AI's `OpenTelemetryChatClient` via `.UseOpenTelemetry(...)` in the `ChatClientBuilder` pipeline. The `OpenTelemetryChatClient` emits spans per the OpenTelemetry GenAI semantic conventions (`gen_ai.operation.name=chat`, `gen_ai.request.model`, `gen_ai.system=anthropic`, `gen_ai.response.finish_reasons`) [[MS Learn: UseOpenTelemetry]](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.opentelemetrychatclientbuilderextensions.useopentelemetry) [[Agent Framework observability]](https://learn.microsoft.com/en-us/agent-framework/tutorials/agents/enable-observability). The `ActivitySource` to `AddSource(...)` in your OTel registration is `"Experimental.Microsoft.Extensions.AI"` (use a wildcard `"*Microsoft.Extensions.AI"` to catch version bumps). **Hard constraint — function-invocation trace stitching:** M.E.AI issue #5767 documents that function-calling spans (the parent LLM span and child tool-invocation spans) were not correlated under a single trace in earlier releases [[issue #5767]](https://github.com/dotnet/extensions/issues/5767); verify against your M.E.AI version (10.3.0 GA or later recommended) before relying on correlated traces in Slice 4. **Additionally:** the first-party `AnthropicClient` uses a standard `HttpClient` and `System.Net.Http` instrumentation will emit HTTP-level spans automatically on .NET 10 [[MS Learn: Networking tracing]](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/telemetry/tracing), giving you two layers of visibility without extra code.

Either SDK choice is fine for observability. **No observability-driven reason to keep the bridge.**

---

## 8. Interaction note with R-053 (multi-turn eval pattern)

Multi-turn evals run a full conversation through `ICoachingLlm` with the `DiskBasedResponseCache` in front via `UseDistributedCache(...)`. The cache is content-addressed on the JSON serialization of `ChatMessage[] + ChatOptions + CacheKeyAdditionalValues` [[MS Learn: DistributedCachingChatClient]](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.distributedcachingchatclient) [[MS Learn: DiskBasedResponseCache]](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.evaluation.reporting.storage.diskbasedresponsecache). Three concrete implications for the migration:

1. **`RawRepresentation` is explicitly excluded from serialization.** That's good: provider-specific payloads (Anthropic's `Message.Usage.CacheCreationInputTokens`, `tool_use.id` strings, thinking `signature` values) are not baked into the cache key, so swapping SDKs does not trivially invalidate fixtures.
2. **`AdditionalProperties` serialize as `JsonElement` on replay**, so custom markers like `runcoach.cache_system = true` round-trip as opaque JSON. Use this for cross-SDK-stable request tagging.
3. **Pin `CacheKeyAdditionalValues` to `["sdk=anthropic-first-party-12.17", "prompt-version=coaching-v1"]`** in your `ReportingConfiguration`. When either axis changes intentionally, re-record; when it doesn't, replay is deterministic. This makes the multi-turn eval harness cache-hermetic across the SDK swap.

For multi-turn runs with extended thinking (future slices), the verbatim-signature requirement still applies: serialize the *complete* assistant `ChatMessage` (including the `RawRepresentation` backing `Message`) as a domain event in your Marten event store, not just the text. The M.E.AI eval cache is for replaying the LLM black box; your Marten stream is the source of truth for multi-turn state and must store signatures verbatim per Anthropic's docs [[extended thinking docs]](https://docs.anthropic.com/en/docs/build-with-claude/extended-thinking) [[stepcodex case study]](https://www.stepcodex.com/en/issue/anthropic-thinking-block-signature-field-lost-during-session).

The multi-turn eval pattern composes cleanly with the recommended SDK.

---

## 9. Citations

**First-party Anthropic C# SDK**
- NuGet package listing, `Anthropic` 12.17.0 (latest at time of writing, last updated 2026-04-01 for 12.11.0 and subsequently 12.17.0 noted in NuGet listings; 737K+ total downloads). https://www.nuget.org/packages/Anthropic/
- GitHub repo, `anthropics/anthropic-sdk-csharp`, MIT, last commit 2026-04-23/24. https://github.com/anthropics/anthropic-sdk-csharp
- GitHub releases page (PR #166 "client: merge response format into preconfigured output config" in Anthropic-v12.12.0). https://github.com/anthropics/anthropic-sdk-csharp/releases
- Anthropic's official C# SDK reference docs (documents `AsIChatClient`, streaming, retries, error types). https://platform.claude.com/docs/en/api/sdks/csharp
- Issue #47 "Both X-Api-Key and Authorization headers are sent" — known non-blocking issue. https://github.com/anthropics/anthropic-sdk-csharp/issues/47

**Anthropic API structured outputs (GA)**
- Structured outputs docs — `output_config.format` is GA, grammar-constrained decoding, schema guarantee. https://platform.claude.com/docs/en/build-with-claude/structured-outputs
- Vercel AI Gateway docs covering the GA `output_config.format` shape + streaming behaviour. https://vercel.com/docs/ai-gateway/sdks-and-apis/anthropic-messages-api/structured-outputs
- Anthropic cookbook — the older "tool as JSON schema" pattern, which is what DEC-037 emulated. https://github.com/anthropics/anthropic-cookbook/blob/main/tool_use/extracting_structured_json.ipynb
- Original pain-point issue documenting the tool-use-as-JSON-mode awkwardness. https://github.com/anthropics/anthropic-sdk-python/issues/1034

**Anthropic prompt caching**
- Prompt caching docs (cache hierarchy tools→system→messages, `cache_creation_input_tokens`/`cache_read_input_tokens`, pricing). https://platform.claude.com/docs/en/build-with-claude/prompt-caching
- March-2026 TTL regression write-up. https://dev.to/whoffagents/anthropic-silently-dropped-prompt-cache-ttl-from-1-hour-to-5-minutes-16ao

**Anthropic extended thinking**
- Building with extended thinking (`signature` verbatim requirement, `redacted_thinking`, C# SDK noted as requiring raw HTTP for `display`). https://docs.anthropic.com/en/docs/build-with-claude/extended-thinking
- Field report on signature-field loss during session serialization. https://www.stepcodex.com/en/issue/anthropic-thinking-block-signature-field-lost-during-session

**Microsoft.Extensions.AI (2025–2026)**
- M.E.AI libraries overview (IChatClient, middleware, OTel). https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai
- `ChatResponseFormat` / `ForJsonSchema` API reference. https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatresponseformat
- `UseOpenTelemetry` extension for ChatClientBuilder. https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.opentelemetrychatclientbuilderextensions.useopentelemetry
- `dotnet/extensions` releases — 10.3.0 first stable, M.E.AI.OpenAI 10.5.0 current. https://github.com/dotnet/extensions/releases
- M.E.AI.Evaluation libraries overview + `DiskBasedResponseCache`. https://learn.microsoft.com/en-us/dotnet/ai/evaluation/libraries ; https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.evaluation.reporting.storage.diskbasedresponsecache
- `DistributedCachingChatClient` roundtrip caveats (RawRepresentation ignored; cache key hashes messages + options + additional values). https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.distributedcachingchatclient ; https://github.com/dotnet/dotnet-api-docs/blob/main/xml/Microsoft.Extensions.AI/DistributedCachingChatClient.xml
- .NET 10 Quickstart — Structured Outputs with `GetResponseAsync<T>`. https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/structured-output

**Microsoft/Anthropic partnership and Foundry adoption signal**
- Microsoft partners with Anthropic to create official MCP C# SDK. https://developer.microsoft.com/blog/microsoft-partners-with-anthropic-to-create-official-c-sdk-for-model-context-protocol
- Claude in Azure via Foundry (public preview, December 2025). https://elbruno.com/2025/12/04/claude-in-azure-with-net-anthropic-claude-microsoft-extensions-ai-meai-%F0%9F%92%A5/
- Agent Framework `AsAIAgent` extension on `IAnthropicClient` (Microsoft Learn). https://learn.microsoft.com/en-us/dotnet/api/anthropic.anthropicclientextensions.asaiagent

**Community alternative (context)**
- tghamm/`Anthropic.SDK` 5.10.0 NuGet + README (richer `ThinkingContent`/`RedactedThinkingContent` support via M.E.AI, `PromptCacheType.FineGrained`, 696K+ downloads). https://www.nuget.org/packages/Anthropic.SDK ; https://github.com/tghamm/Anthropic.SDK
- DeepWiki dump of community SDK internals (structured view of `MessagesEndpoint.ChatClient.cs`, `ChatClientHelper.cs`, extended-thinking handling). https://deepwiki.com/tghamm/Anthropic.SDK ; https://deepwiki.com/tghamm/Anthropic.SDK/3.6-extended-thinking

---

*End of research artifact.*