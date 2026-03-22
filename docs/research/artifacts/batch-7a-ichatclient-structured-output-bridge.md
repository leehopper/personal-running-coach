# Bridging Anthropic structured outputs with .NET eval caching

**A custom `DelegatingChatClient` wrapper is the cleanest path.** The Anthropic C# SDK's `AsIChatClient()` adapter silently drops `ChatResponseFormat.ForJsonSchema()` — it never maps to Anthropic's native `output_config.format` with `json_schema` type, meaning you get free-form JSON instead of constrained decoding. By writing a thin `DelegatingChatClient` subclass that intercepts structured output requests and delegates to the native SDK, you keep a single `IChatClient` pipeline. This means M.E.AI.Evaluation's caching middleware, eval reporting, and your entire test harness work unchanged — no dual-path complexity, no manual cache injection, no separate file stores to maintain.

## The bridge gap is real and unfiled

Both the official Anthropic C# SDK (`anthropics/anthropic-sdk-csharp`, the `Anthropic` NuGet package you're using at v12.9.0) and the unofficial SDK (`tghamm/Anthropic.SDK`) fail to translate `ChatResponseFormat.ForJsonSchema()` to Anthropic's native constrained decoding. The official SDK's `AsIChatClient()` adapter handles text chat, streaming, tool calling, and MCP, but **no documentation, tests, or source code references show any `ResponseFormat` → `OutputConfig` mapping**. The unofficial SDK's `ChatClientHelper.cs` does read `ResponseFormat`, but only to apply strict tool schemas — the older workaround, not native `output_config.format`.

No GitHub issues have been filed about this gap in either repository. Microsoft's own documentation for `ChatOptions.ResponseFormat` explicitly states: "It is up to the client implementation if or how to honor the request. If the client implementation doesn't recognize the specific kind of ChatResponseFormat, it can be ignored." So the Anthropic adapter is technically compliant — but functionally incomplete. The reference OpenAI adapter in `dotnet/extensions` *does* map `ChatResponseFormatJson` with a schema to `CreateJsonSchemaFormat()`, establishing the expected pattern that Anthropic's adapter simply hasn't implemented. This is a known class of problem: `dotnet/extensions#5808` documented the OpenAI adapter initially missing the `strict` parameter, and `vercel/ai#12298` shows the TypeScript SDK still using the deprecated `output_format` instead of `output_config.format`.

## The custom DelegatingChatClient approach

Microsoft's official extensibility pattern for IChatClient is `DelegatingChatClient` — a middleware base class that wraps an inner client and intercepts calls. Here's the concrete design for your wrapper:

```csharp
public sealed class AnthropicStructuredOutputClient : DelegatingChatClient
{
    private readonly AnthropicClient _nativeClient;
    private readonly string _defaultModel;

    public AnthropicStructuredOutputClient(
        IChatClient inner,
        AnthropicClient nativeClient,
        string defaultModel) : base(inner)
    {
        _nativeClient = nativeClient;
        _defaultModel = defaultModel;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Only intercept when ForJsonSchema() is set
        if (options?.ResponseFormat is ChatResponseFormatJson { Schema: not null } jsonFormat)
        {
            return await CallNativeStructuredAsync(
                messages, options, jsonFormat, cancellationToken);
        }

        // Everything else flows through the normal adapter
        return await base.GetResponseAsync(messages, options, cancellationToken);
    }

    private async Task<ChatResponse> CallNativeStructuredAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions options,
        ChatResponseFormatJson jsonFormat,
        CancellationToken cancellationToken)
    {
        // Build native Anthropic request with OutputConfig
        var request = new MessageCreateParams
        {
            Model = options.ModelId ?? _defaultModel,
            MaxTokens = options.MaxOutputTokens ?? 4096,
            Messages = MapMessages(messages),
            Temperature = options.Temperature,
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat
                {
                    Schema = JsonDocument.Parse(jsonFormat.Schema).RootElement
                }
            }
        };

        var nativeResponse = await _nativeClient.Messages.CreateAsync(
            request, cancellationToken);

        // Map back to M.E.AI ChatResponse
        return MapToChatResponse(nativeResponse);
    }

    private static ChatResponse MapToChatResponse(Message msg)
    {
        var text = string.Join("", msg.Content
            .OfType<TextBlock>()
            .Select(b => b.Text));

        var response = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, text));

        response.AdditionalProperties ??= new();
        response.AdditionalProperties["stop_reason"] = msg.StopReason;
        response.ModelId = msg.Model;

        if (msg.Usage is { } usage)
        {
            response.Usage = new UsageDetails
            {
                InputTokenCount = usage.InputTokens,
                OutputTokenCount = usage.OutputTokens
            };
        }
        return response;
    }
}
```

Wire it into your pipeline like this:

```csharp
var anthropicClient = new AnthropicClient();
IChatClient evalClient = new AnthropicStructuredOutputClient(
        inner: anthropicClient.AsIChatClient("claude-sonnet-4-5"),
        nativeClient: anthropicClient,
        defaultModel: "claude-sonnet-4-5")
    .AsBuilder()
    .UseDistributedCache(diskCache) // M.E.AI caching middleware
    .Build();
```

This design has several important properties. The caching middleware sits **above** your wrapper in the pipeline, so cached responses are returned before your code ever runs — no wasted API calls on replay. The `ChatOptions.ResponseFormat` is included in the cache key hash (messages + options are JSON-serialized, then SHA-256 hashed), so a structured call with a schema and an unstructured call with the same prompt produce **different cache keys** automatically. And the `ChatResponse` you return contains plain text content (the JSON string), which survives `DistributedCachingChatClient`'s JSON round-trip without loss — `RawRepresentation` is excluded from serialization, but you don't need it.

## How M.E.AI.Evaluation caching actually works

The eval caching system has two layers you need to understand. The **response cache** (`DistributedCachingChatClient` backed by `DiskBasedResponseCache`) prevents re-calling the LLM. The **result store** (separate disk-based JSON files) records evaluation metrics for the HTML report. They are independent.

The cache key is computed by `DistributedCachingChatClient.GetCacheKey()`: it JSON-serializes the full `IEnumerable<ChatMessage>` and `ChatOptions` object, then produces a **SHA-256 hash**. Any change to the prompt text, system message, temperature, model ID, response format, tools, or additional values produces a different key. The serialized `ChatResponse` is stored as UTF-8 JSON bytes through the `IDistributedCache` interface, with a **14-day default expiration**.

For your git-committed cache strategy, the `DiskBasedResponseCache` writes files to `{storageRootPath}`. You can commit this entire directory. In CI, set `enableResponseCaching: true` in `DiskBasedReportingConfiguration.Create()` — the middleware will read from the cache without needing an API key. If a cache miss occurs, the test will fail (the native Anthropic call will throw without `ANTHROPIC_API_KEY`), which is actually the behavior you want: it forces you to record responses locally before pushing.

A critical nuance: `EvaluateAsync(messages, modelResponse)` doesn't care where the `ChatResponse` came from. Even if you bypassed IChatClient entirely, you could manually construct a `ChatResponse` and pass it to the evaluator. The HTML report reads from the result store, not the response cache. This means your custom wrapper's responses flow into eval reports identically to standard IChatClient responses.

## Why not dual-path instead

The dual-path architecture — native SDK for structured calls, IChatClient for unstructured — is the pattern used by `tghamm/Anthropic.SDK` and recommended in several community discussions. It works, but for a solo developer building an eval suite, it creates two problems.

First, your structured calls would bypass the caching middleware entirely. You'd need a parallel cache: compute your own hash key (model + messages + schema + temperature), serialize responses to JSON files, manage expiration and directory structure. Instructor (Python) and Promptfoo both do this — SHA-256 key over the full request, stored as `{cacheDir}/{key}.json` — and it works fine, but it's **redundant infrastructure** when M.E.AI already provides it. Second, the eval report wouldn't automatically include structured call results unless you manually called `scenarioRun.EvaluateAsync()` with hand-constructed `ChatResponse` objects. That's workable but adds ceremony to every test.

The custom `DelegatingChatClient` avoids both problems. Structured and unstructured calls share one pipeline, one cache, one report. The only cost is ~100 lines of wrapper code and message-mapping logic.

## Pitfalls to watch for and mitigations

**Schema serialization in cache keys.** `ChatResponseFormatJson.Schema` is a `string` (the raw JSON schema). The cache key includes this string via `ChatOptions` serialization. If your JSON schema has non-deterministic property ordering between runs (unlikely with `System.Text.Json` but possible with some schema generators), you'll get spurious cache misses. Mitigate by generating schemas once at startup using `AIJsonUtilities.CreateJsonSchema<T>()` and reusing the same instance. LiteLLM had a production bug (`#8706`) where `response_format` was excluded from cache keys entirely — be glad M.E.AI includes it by default.

**Streaming structured calls.** Anthropic supports streaming with structured outputs, but the `DelegatingChatClient` also needs to override `GetStreamingResponseAsync()`. For eval testing, you likely don't need streaming at all — use non-streaming for determinism and simpler caching. No major caching framework (Instructor, Promptfoo, Braintrust) caches streaming responses well.

**Token usage and metadata.** The `UsageDetails` you set on the mapped `ChatResponse` will be serialized into the cache. Subsequent cache hits will return stale usage data (the original call's token counts), which is fine for eval purposes but misleading if you're tracking costs. Consider adding an `AdditionalProperties` flag like `"cached": true` after a cache hit if you need to distinguish.

**`RawRepresentationFactory` alternative.** Microsoft provides `ChatOptions.RawRepresentationFactory` as the official escape hatch for provider-specific options. In theory, you could set this callback to return a native `MessageCreateParams` with `OutputConfig` populated, and hope the Anthropic adapter reads it. In practice, the current Anthropic adapter **does not inspect `RawRepresentationFactory`** for output config, so this doesn't work today. If a future SDK version adds support, you could simplify your wrapper to just set this property instead of intercepting the full call.

## Record modes for deterministic CI

Borrow the VCR-style record mode pattern from Python's `vcrpy` and adapt it for your setup:

```csharp
public enum CacheMode { Record, Replay, Auto }

// In test setup:
var cacheMode = Environment.GetEnvironmentVariable("EVAL_CACHE_MODE") 
    ?? "Auto";
```

In **Record** mode (local development): real API calls are made, responses cached to disk. In **Replay** mode (CI): cache-only, any cache miss throws `InvalidOperationException("Cache miss in replay mode")`. In **Auto** mode: use cache if available, call API otherwise. Commit the cache directory. Set `EVAL_CACHE_MODE=Replay` in your CI pipeline — this guarantees no API key is needed and tests are fully deterministic.

Promptfoo uses exactly this pattern with `PROMPTFOO_CACHE_ENABLED` and a 14-day TTL. Braintrust takes a different approach with a proxy-based cache on Cloudflare Workers, but that's overkill for a solo project.

## Concrete recommendation and wiring

Your eval test file should look approximately like this:

```csharp
[TestClass]
public class CoachingResponseEvals
{
    private static ReportingConfiguration s_config;
    
    [ClassInitialize]
    public static async Task Setup(TestContext ctx)
    {
        var anthropic = new AnthropicClient();
        
        IChatClient client = new AnthropicStructuredOutputClient(
                inner: anthropic.AsIChatClient("claude-sonnet-4-5"),
                nativeClient: anthropic,
                defaultModel: "claude-sonnet-4-5")
            .AsBuilder()
            .Build();
            
        s_config = DiskBasedReportingConfiguration.Create(
            storageRootPath: ".eval-cache",
            evaluators: [new CoherenceEvaluator(), new RelevanceEvaluator()],
            chatConfiguration: new ChatConfiguration(client),
            enableResponseCaching: true);
    }
    
    [TestMethod]
    public async Task Structured_coaching_plan_is_coherent()
    {
        await using var run = await s_config
            .CreateScenarioRunAsync("CoachingPlan.Structured");
            
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema<CoachingPlan>()
        };
        
        var messages = new[] { new ChatMessage(ChatRole.User, 
            "Create a coaching plan for improving public speaking") };
            
        var response = await run.ChatConfiguration!.ChatClient
            .GetResponseAsync(messages, options);
            
        var plan = JsonSerializer.Deserialize<CoachingPlan>(response.Text);
        Assert.IsNotNull(plan?.Goals);
        
        await run.EvaluateAsync(messages.ToList(), response);
    }
}
```

The entire pipeline — structured output via native constrained decoding, response caching to disk, eval scoring, and HTML report generation — flows through a single `IChatClient`. Cache files go into `.eval-cache/`, which you commit to git. CI runs in replay mode with no API key. The `AnthropicStructuredOutputClient` is the only custom code needed, and it's ~100 lines with clear boundaries: intercept when `ResponseFormat` has a schema, delegate to native SDK, map the response back.

## Conclusion

The gap is real, unfiled, and unlikely to be fixed soon — the official Anthropic C# SDK is still in beta and structured output IChatClient bridging isn't on any public roadmap. **File an issue on `anthropics/anthropic-sdk-csharp`** requesting `ChatResponseFormat.ForJsonSchema()` → `OutputConfig` with `JsonOutputFormat` mapping — you'd be the first. In the meantime, the `DelegatingChatClient` wrapper gives you native constrained decoding within the standard M.E.AI pipeline, costing minimal code and zero architectural complexity. The key insight from cross-ecosystem research is that the cache key must include the schema (M.E.AI does this automatically via `ChatOptions` serialization), and the VCR-style record/replay pattern with committed cache files is the industry standard for deterministic CI — Promptfoo, Instructor, and BAML VCR all converge on this approach. Skip the dual-path; the wrapper is simpler, more maintainable, and keeps your eval reporting unified.