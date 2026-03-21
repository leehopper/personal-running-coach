# .NET tooling for LLM testing, caching, and structured outputs with Claude

**The .NET ecosystem for LLM testing has matured rapidly — Microsoft's first-party `Microsoft.Extensions.AI.Evaluation` suite is the standout discovery, providing LLM-as-judge scoring, response caching, and HTML reporting that works with any `IChatClient` implementation including Anthropic's.** Combined with EasyVCR for HTTP recording/replay, Verify for snapshot testing, and .NET 9's built-in `JsonSchemaExporter` for structured output schemas, a complete eval test suite can be assembled entirely from well-maintained packages. The Anthropic Batch API (50% discount) stacks with prompt caching (90% savings on repeated system prompts) to make large-scale eval runs remarkably cost-effective — potentially **95% cheaper** than naïve per-request pricing.

---

## HTTP recording and replay: three viable options

All Anthropic API calls hit a single endpoint (`POST /v1/messages`) with different JSON bodies, making **body-based matching** the critical differentiator among VCR libraries. Three actively maintained options exist, each with a distinct architecture.

**EasyVCR** (NuGet: `EasyVCR` v0.13.0) is the top recommendation for this use case. It explicitly targets **.NET 10**, supports `.ByBody()` match rules to distinguish requests by JSON content, includes built-in censoring for API keys (`Censors.DefaultSensitive()`), and returns a standard `HttpClient` that plugs directly into the Anthropic.SDK constructor. It offers `Mode.Auto` (replay if cassette exists, record otherwise), `Mode.Bypass` for forced refresh, and configurable cassette expiration via `ValidTimeFrame`. Its main limitation is the lack of an environment variable toggle — mode must be set in code.

**Vcr.HttpRecorder** (NuGet: `Vcr.HttpRecorder`, fork by GeorgopoulosGiannis) takes a `DelegatingHandler` approach, making it more composable in DI pipelines. It supports `.ByContent()` matching, the `HTTP_RECORDER_MODE` environment variable for CI/CD mode switching, and concurrent test execution via `HttpRecorderConcurrentContext`. Cassettes use the human-readable HAR format. The trade-off is a smaller community (13 GitHub stars) and binary content matching rather than semantic JSON comparison.

**WireMock.Net** (NuGet: `WireMock.Net` v2.0.0) is the most powerful option but the heaviest. It runs as an in-process HTTP server, offers `JsonMatcher` with semantic JSON comparison (ignoring key order, extra elements), and has dedicated xUnit (`WireMock.Net.xUnit`) and FluentAssertions packages. The downside: it requires redirecting the SDK's `BaseUrl` to `http://localhost:{port}` rather than transparent handler injection.

### Wiring EasyVCR into xUnit tests with the Anthropic SDK

```csharp
using EasyVCR;
using Anthropic.SDK;
using Xunit;
using FluentAssertions;

public class ClaudeEvalTests : IDisposable
{
    private readonly VCR _vcr;
    private readonly AnthropicClient _client;

    public ClaudeEvalTests()
    {
        var cassettePath = Path.Combine("TestCassettes", GetType().Name);
        var settings = new AdvancedSettings
        {
            MatchRules = new MatchRules().ByBody().ByFullUrl(),
            Censors = new Censors()
                .CensorHeaders(new List<KeyCensorElement>
                {
                    new("x-api-key", caseSensitive: false),
                    new("authorization", caseSensitive: false)
                })
        };

        _vcr = new VCR(settings);
        var cassette = new Cassette(cassettePath, "claude_responses");
        _vcr.Insert(cassette);

        // Mode.Auto: replay from cassette if exists, record otherwise
        var mode = Environment.GetEnvironmentVariable("VCR_MODE") switch
        {
            "record" => Mode.Record,
            "replay" => Mode.Replay,
            "bypass" => Mode.Bypass,
            _ => Mode.Auto
        };
        if (mode == Mode.Auto) _vcr.Auto(); else if (mode == Mode.Record) _vcr.Record();
        else _vcr.Replay();

        // Inject VCR's HttpClient into the Anthropic SDK
        _client = new AnthropicClient(httpClient: _vcr.Client);
    }

    [Fact]
    public async Task Should_extract_structured_contact_info()
    {
        var response = await _client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = AnthropicModels.Claude35Sonnet,
            MaxTokens = 1024,
            Messages = new List<Message>
            {
                new(RoleType.User, "Extract: John Smith, [email protected], Enterprise plan")
            }
        });

        response.Content.Should().NotBeEmpty();
        response.StopReason.Should().Be("end_turn");
    }

    public void Dispose() => _vcr.Eject();
}
```

> **Critical SDK note:** The official `Anthropic` NuGet package (v12.9.0, Stainless-generated) does **not** publicly document `HttpClient` injection — it exposes `BaseUrl`, `ApiKey`, `MaxRetries`, and `Timeout` properties. For transparent handler-based VCR integration, use the unofficial `Anthropic.SDK` (v5.10.0 by tghamm), which accepts `HttpClient` in its constructor and targets .NET 10. Alternatively, the official SDK's `BaseUrl` can be pointed at a WireMock.Net server. Both SDKs implement `IChatClient` from `Microsoft.Extensions.AI.Abstractions`.

---

## Microsoft.Extensions.AI.Evaluation is the .NET eval framework

The single most important finding in this research is Microsoft's first-party evaluation library suite. Released as part of the broader `Microsoft.Extensions.AI` ecosystem and **tested on GitHub Copilot experiences**, it provides everything from LLM-as-judge scoring to built-in response caching — without requiring Semantic Kernel orchestration.

The suite consists of five packages. `Microsoft.Extensions.AI.Evaluation` (v10.4.0) defines core abstractions like `IEvaluator` and `EvaluationMetric`. `Microsoft.Extensions.AI.Evaluation.Quality` (v10.0.0) provides **11 LLM-as-judge evaluators** — Relevance, Truth, Completeness, Fluency, Coherence, Groundedness, Equivalence, Retrieval, ToolCallAccuracy, TaskAdherence, and IntentResolution — each powered by any `IChatClient` implementation. `Microsoft.Extensions.AI.Evaluation.NLP` (preview) offers algorithmic metrics — **BLEU, GLEU, and F1** — requiring no LLM at all. `Microsoft.Extensions.AI.Evaluation.Reporting` (v10.4.0) adds **disk-based response caching** (keyed by full request parameters, 14-day default TTL) and HTML report generation. A CLI tool (`dotnet aieval`) generates reports from test runs.

The response caching in the Reporting package deserves special attention. It wraps any `IChatClient` with a caching decorator, storing responses on disk. On subsequent runs, **only prompts that changed since the last run trigger new LLM calls** — unchanged prompts serve cached responses instantly. This alone can eliminate the majority of LLM costs during iterative eval development. The cache key includes the model endpoint, all prompt content, and request parameters.

No .NET ports of DeepEval, Promptfoo, or RAGAS exist. Microsoft.Extensions.AI.Evaluation is the closest equivalent and covers most of the same metrics. Braintrust offers a C# SDK (`Braintrust.Sdk`, beta, .NET 8+) with experiment tracking and OpenTelemetry-based tracing, but its evaluator library (`autoevals`) remains Python/JS-only.

---

## Structured outputs: from C# record to guaranteed-valid JSON

Anthropic's structured outputs use **constrained decoding** — grammar-based token generation that guarantees schema-compliant JSON. The API parameter is `output_config.format` with type `json_schema`. The most elegant .NET approach combines three built-in capabilities: C# records for type definition, .NET 9's `JsonSchemaExporter` for schema generation, and `System.Text.Json` for deserialization.

### Complete record-to-structured-output pipeline

```csharp
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

// 1. Define the output shape as a C# record
[Description("Contact information extracted from an email")]
record ContactInfo(
    [property: Description("Full name")] string Name,
    [property: Description("Email address")] string Email,
    [property: Description("Plan tier")] string PlanInterest,
    [property: Description("Whether a demo was requested")] bool DemoRequested,
    [property: Description("Preferred demo time if mentioned")] string? PreferredTime = null
);

// 2. Generate JSON Schema from the record type
var exporterOptions = new JsonSchemaExporterOptions
{
    TreatNullObliviousAsNonNullable = true,
    TransformSchemaNode = (context, schema) =>
    {
        // Inject [Description] attributes into the schema
        var attr = (context.PropertyInfo?.AttributeProvider ?? context.TypeInfo.Type)
            ?.GetCustomAttributes(true).OfType<DescriptionAttribute>().FirstOrDefault();
        if (attr is not null && schema is JsonObject obj)
            obj.Insert(0, "description", attr.Description);

        // Anthropic requires additionalProperties: false on all objects
        if (schema is JsonObject o && o.ContainsKey("properties"))
            o["additionalProperties"] = false;

        return schema;
    }
};

var jsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Default)
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
};
JsonNode schema = jsonOptions.GetJsonSchemaAsNode(typeof(ContactInfo), exporterOptions);

// 3. Pass schema to Anthropic API via output_config
var parameters = new MessageCreateParams
{
    MaxTokens = 1024,
    Model = "claude-sonnet-4-5-20250929",
    Messages = [new() {
        Role = Role.User,
        Content = "Extract info: John Smith ([email protected]) wants Enterprise, demo Tuesday 2pm."
    }],
    OutputConfig = new()
    {
        Format = new() { Type = "json_schema", Schema = schema }
    }
};

var message = await client.Messages.Create(parameters);

// 4. Deserialize — guaranteed schema-compliant
var contact = JsonSerializer.Deserialize<ContactInfo>(
    message.Content[0].ToString(), jsonOptions);
```

**Schema complexity limits** constrain what records can express: a maximum of **20 strict tools per request**, **24 optional parameters** across all schemas, and **16 union-type parameters**. Recursive schemas, numerical constraints (`minimum`/`maximum`), and string length constraints are not supported. First-request latency includes ~100–300ms for grammar compilation, with **compiled grammars cached server-side for 24 hours**.

For schema validation in tests, two libraries stand out. **NJsonSchema** (v11.5.2, 296M+ downloads) generates schemas from types via `JsonSchema.FromType<T>()` and uses Newtonsoft.Json. **JsonSchema.Net** (v9.1.3) plus its `JsonSchema.Net.Generation` companion does the same with System.Text.Json. For .NET 9+ projects, the built-in `JsonSchemaExporter` eliminates the need for either third-party package entirely.

---

## Prompt caching and batch processing slash eval costs

Two Anthropic features combine to make large-scale eval suites dramatically cheaper. **Prompt caching** stores previously processed prompt prefixes server-side so subsequent requests reuse them. Cache reads cost **0.1× the base input price** — a 90% savings. The 5-minute default TTL refreshes on each use; a 1-hour TTL option costs 2× on writes but is better for long eval runs. Critically, **cached tokens don't count toward input-tokens-per-minute rate limits**, effectively multiplying throughput 5–10× for eval suites that share a system prompt.

The **Batch API** processes requests asynchronously (within 24 hours, typically under 1 hour) at a **50% discount** on all tokens. Up to 10,000 requests per batch, with separate rate limits from the real-time API. Both the official SDK (`client.Beta.Messages.Batches`) and unofficial `Anthropic.SDK` support batch operations.

**Stacking both:** a shared system prompt cached + batch processing yields up to **95% savings** on input tokens compared to individual uncached requests. For an eval suite of 500 test cases sharing a 2,000-token system prompt, a naïve approach costs ~$X; batch + caching reduces that to ~$0.05X. The pattern is straightforward: mark the system prompt with `cache_control`, submit all test cases as a batch, and poll for completion.

One interaction to note: changing `output_config.format` (the structured output schema) **invalidates the prompt cache** for that conversation thread. Keep schemas identical across test cases to maintain cache hits.

---

## Snapshot testing and golden file management

**Verify** (NuGet: `Verify.XunitV3` v31.13.2 for xUnit v3, `Verify.Xunit` v31.12.5 for v2) is the clear choice for snapshot testing LLM responses. Maintained by Simon Cropp with daily commits, it serializes objects to JSON `.verified.txt` files, compares against `.received.txt` on each run, and provides `DiffEngineTray` for bulk approval. Built-in scrubbers auto-replace GUIDs and DateTimes with stable placeholders.

For LLM outputs, the recommended pattern is to **snapshot structured/parsed output rather than raw prose**. Combine Verify with HTTP recording: record Claude's response once via EasyVCR, then snapshot-test the deterministic replay. Scrub volatile fields (`id`, `created`, `usage`, token counts) via Verify's fluent API:

```csharp
[Fact]
public Task Verify_structured_extraction() =>
    Verify(parsedContact)
        .ScrubLinesContaining("request_id", "created")
        .ScrubLinesWithReplace(line =>
            line.Contains("model") ? "  model: [SCRUBBED]" : line);
```

**Cache invalidation when prompts change** is handled differently depending on the layer. EasyVCR cassettes must be manually deleted or expired via `ValidTimeFrame` + `WhenExpired = ExpirationActions.RecordAgain`. The Microsoft.Extensions.AI.Evaluation.Reporting cache automatically detects changed prompts and makes fresh LLM calls — only unchanged prompts serve from cache. The most robust workflow: store cassettes in source control, delete them in CI when prompt files change (detectable via git diff), and re-record.

ApprovalTests.Net is officially deprecated in favor of Verify. Snapshooter (`Snapshooter.Xunit` v0.12.2) is an alternative but less actively maintained.

---

## Resilience, parallelization, and additional tooling

**Polly v8** no longer includes a caching strategy (the v7 `Polly.Caching.MemoryCache` was not ported). The recommended replacement is `Microsoft.Extensions.Http.Resilience` (v10.4.0), which integrates Polly v8 with `IHttpClientFactory` and provides `AddStandardResilienceHandler()` — a pre-configured pipeline of rate limiter → total timeout → retry (with `Retry-After` header support) → circuit breaker → attempt timeout. For LLM response caching, build a custom `DelegatingHandler` that hashes request bodies with SHA256 and checks `IMemoryCache`.

**Test parallelization** with LLM rate limits requires careful handling. xUnit's `MaxParallelThreads` doesn't limit concurrent async tests. The proven pattern uses a shared `SemaphoreSlim` via `ICollectionFixture`:

```csharp
[CollectionDefinition("Claude API")]
public class ClaudeCollection : ICollectionFixture<ClaudeFixture> { }

public class ClaudeFixture
{
    public SemaphoreSlim Throttle { get; } = new(5, 5); // 5 concurrent calls
}

[Collection("Claude API")]
public class MyTests(ClaudeFixture fixture)
{
    [Fact]
    public async Task Eval_test()
    {
        await fixture.Throttle.WaitAsync();
        try { /* call Claude */ }
        finally { fixture.Throttle.Release(); }
    }
}
```

`Meziantou.Xunit.ParallelTestFramework` extends xUnit to run individual test cases within a class in parallel (xUnit's default is sequential within a class), with `[DisableParallelization]` for selective opt-out.

For **semantic assertions without LLM calls**, `Microsoft.Extensions.AI.Evaluation.NLP` provides BLEU, GLEU, and F1 scores algorithmically. For true semantic similarity, `Microsoft.ML.OnnxRuntime` can run ONNX-exported sentence-transformer or NLI models locally, though setup is significant. The `EquivalenceEvaluator` in the Quality package provides LLM-as-judge semantic comparison with minimal code. No dedicated .NET Roslyn analyzers or source generators for LLM eval exist today.

---

## Comparison of all recommended libraries

| Library | NuGet package | Version | Solves | Maintenance | .NET support | LLM-specific |
|---|---|---|---|---|---|---|
| **MS AI Evaluation** | `Microsoft.Extensions.AI.Evaluation.*` | 10.4.0 | LLM-as-judge scoring, NLP metrics, response caching, reporting | ★★★★★ Microsoft first-party | .NET 8+, netstandard2.0 | Yes — purpose-built |
| **EasyVCR** | `EasyVCR` | 0.13.0 | HTTP recording/replay with body matching | ★★★★ Active (EasyPost) | .NET 6–10 | Body matching for single-endpoint APIs |
| **Verify** | `Verify.XunitV3` | 31.13.2 | Snapshot/approval testing | ★★★★★ Daily commits | .NET 8+ | Scrubbers for volatile LLM fields |
| **WireMock.Net** | `WireMock.Net` | 2.0.0 | HTTP mocking/recording with JSON matching | ★★★★★ Very active | .NET 6+ | JsonMatcher for request body |
| **Anthropic.SDK** | `Anthropic.SDK` | 5.10.0 | Claude API client with HttpClient injection | ★★★★ Active | .NET 8/10 | Prompt caching, batch API, structured output |
| **Anthropic (official)** | `Anthropic` | 12.9.0 | Official Claude API client | ★★★★ Stainless-generated | netstandard2.0+ | IChatClient, auto-retry on 429 |
| **NJsonSchema** | `NJsonSchema` | 11.5.2 | Schema generation/validation from C# types | ★★★★★ 296M downloads | .NET 6+ | Schema gen for structured outputs |
| **JsonSchema.Net** | `JsonSchema.Net` + `Generation` | 9.1.3 / 7.1.3 | Schema gen/validation (System.Text.Json) | ★★★★ Active | .NET 8+ | Modern STJ-based schemas |
| **MS HTTP Resilience** | `Microsoft.Extensions.Http.Resilience` | 10.4.0 | Polly v8 retry/circuit-breaker for HttpClient | ★★★★★ Microsoft | .NET 8+ | Retry-After header support |
| **Vcr.HttpRecorder** | `Vcr.HttpRecorder` | latest | DelegatingHandler-based HTTP VCR | ★★★ Small community | .NET 6–8 | Env var mode control |
| **Braintrust** | `Braintrust.Sdk` | beta | Experiment tracking, tracing | ★★★ Beta | .NET 8+ | OpenTelemetry LLM tracing |
| **Snapshooter** | `Snapshooter.Xunit` | 0.12.2 | JSON snapshot testing | ★★★ Moderate | .NET 8 | Strict mode for CI |
| **Meziantou Parallel** | `Meziantou.Xunit.ParallelTestFramework` | latest | Intra-class test parallelism | ★★★★ Active | .NET 8+ | — |
| **ONNX Runtime** | `Microsoft.ML.OnnxRuntime` | 1.16+ | Local model inference (NLI, embeddings) | ★★★★★ Microsoft | .NET 6+ | Semantic similarity without API calls |

## Conclusion

The .NET LLM testing stack has a clear layered architecture. **Microsoft.Extensions.AI.Evaluation** is the foundation — use it for scoring, response caching, and reporting regardless of which other libraries you adopt. **EasyVCR** fills the HTTP recording gap that the official Anthropic SDK's lack of `HttpClient` injection creates (the unofficial `Anthropic.SDK` by tghamm is the pragmatic choice if you need handler-level control). **Verify** handles snapshot assertions with scrubbers tuned for LLM output volatility. .NET 9's built-in `JsonSchemaExporter` eliminates the need for third-party schema libraries when generating structured output schemas from C# records.

The cost optimization story is the unexpected highlight. Stacking Anthropic's prompt caching (0.1× on repeated system prompts), batch processing (0.5×), and client-side response caching (0× on unchanged prompts) means a 500-test eval suite can run for pennies. The `Microsoft.Extensions.AI.Evaluation.Reporting` cache — which automatically detects prompt changes and only re-invokes the LLM for modified tests — is perhaps the single most impactful tool for teams iterating on prompts daily.