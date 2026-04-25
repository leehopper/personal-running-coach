# R‑051: LLM‑Specific Observability for RunCoach (ASP.NET Core 10 + Anthropic, MVP‑0 @ 10–20 users)

## 1. Executive answer

**Recommendation for Slice 1:** ship a two‑layer stack that inherits from Slice 0's OTel overlay (DEC‑045) and does not add any ClickHouse / Redis / S3 dependencies:

1. **Instrumentation layer (lands in Slice 1, permanent):** a custom `RunCoach.Llm` `ActivitySource` whose attribute schema follows the **OpenTelemetry GenAI Semantic Conventions** — specifically the Anthropic‑flavoured extension which now standardises `gen_ai.usage.cache_read.input_tokens` and `gen_ai.usage.cache_creation.input_tokens` as first‑class attributes ([OTel Anthropic semconv](https://opentelemetry.io/docs/specs/semconv/gen-ai/anthropic/)). Wrap `ICoachingLlm` with the `Microsoft.Extensions.AI` `UseOpenTelemetry()` chat‑client middleware (source name `"Microsoft.Extensions.AI"`) so the out‑of‑the‑box span/meter shape is free ([Microsoft Learn – Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai); [UseOpenTelemetry API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.opentelemetrychatclientbuilderextensions.useopentelemetry?view=net-10.0-pp)), and add RunCoach‑specific attributes (`runcoach.session_id`, `runcoach.turn_id`, `runcoach.prompt.name`, `runcoach.prompt.version`) on the parent `ContextAssembler` activity.

2. **Backend layer (lands in Slice 1, replaceable):** **Arize Phoenix self‑hosted, SQLite mode, single container** ([Phoenix Docker docs](https://arize.com/docs/phoenix/self-hosting/deployment-options/docker); [Phoenix license page](https://arize.com/docs/phoenix/self-hosting/license)) added to the existing `docker-compose.otel.yml`, fed by the OTel Collector you already run. Phoenix is **Elastic License 2.0 (ELv2)** — *self‑hosting for your own app is free and explicitly permitted*; the only restriction is against reselling Phoenix itself as a hosted service ([Phoenix LICENSE](https://github.com/Arize-ai/phoenix/blob/main/LICENSE); [Arize licensing Q&A](https://github.com/Arize-ai/phoenix/discussions/2412)), which does not apply to RunCoach. Phoenix keeps Jaeger as your general APM and adds LLM‑aware trace views, prompt playgrounds, datasets, and experiments that integrate cleanly with OTel GenAI semconv ([Phoenix home](https://phoenix.arize.com/); [Phoenix on AppSec Santa](https://appsecsanta.com/arize-ai)).

**Rationale (the short version).** At 10–20 users × ~30 onboarding turns × Sonnet 4.5, trace volume is measured in thousands of spans per user, not billions. Langfuse self‑hosted v3 now *mandates* ClickHouse + Redis + S3/MinIO in addition to Postgres ([Langfuse architecture](https://langfuse.com/handbook/product-engineering/architecture); [Langfuse self‑hosting](https://langfuse.com/self-hosting); [Langfuse v3 discussion #1902](https://github.com/orgs/langfuse/discussions/1902)), which is four new containers to pay for no user‑visible feature at this volume. Langfuse maintainers have explicitly **declined** to build a Postgres‑only flavour ([Langfuse v2→v3 notes](https://langfuse.com/self-hosting/upgrade/upgrade-guides/upgrade-v2-to-v3)) and a user on the #5785 discussion was told "docker‑compose is the smallest footprint … unfortunately there is no way around [ClickHouse]" ([Discussion #5785](https://github.com/orgs/langfuse/discussions/5785)). Phoenix — one Python container, optional SQLite, optional Postgres (and you already have Postgres) — matches the volume envelope, speaks OTel natively, and gives you 80% of Langfuse's feature set with 20% of the footprint. If usage ever outgrows Phoenix, the wire shape is standard OTel GenAI, so swapping to Langfuse OSS, Opik, or OpenLIT later is a collector‑config change, not a re‑instrumentation.

**What the user doesn't get by choosing Phoenix:** git‑backed prompt version diffing with production labels (a Langfuse strength — [Langfuse prompt versioning](https://langfuse.com/docs/prompt-management/features/prompt-version-control); [Langfuse GitHub integration](https://langfuse.com/docs/prompt-management/features/github-integration)). That's fine because RunCoach already checks `onboarding-v1.yaml` into git — the source of truth is the repo, and the observability tool only needs to *tag* each call with `runcoach.prompt.version` and render a comparison view, which Phoenix's experiments/datasets feature does.

---

## 2. Scope, constraints, and assumptions

- **Volume envelope:** 10–20 users, MVP‑0 → MVP‑1. Using your own figures of ~30 turns × ~8k input tokens × Sonnet, that is **≈ 600 turns per user, ≈ 12,000 LLM calls cumulative across the cohort**, plus ~3–5× as many auxiliary spans (HTTP, ContextAssembler, Marten, Wolverine). Call it **50–100k spans total over MVP‑0/1 lifetime**.
- **User constraints:** no paid SaaS (Langfuse Cloud, LangSmith, Helicone Cloud, Arize AX, Opik hosted, PostHog Cloud all explicitly out); self‑hosted OSS only; same‑Postgres single‑instance; CritterWatch evaluate‑only.
- **Existing infrastructure (DEC‑045):** `docker-compose.otel.yml` with an OpenTelemetry Collector and Jaeger; Marten `OpenTelemetry.TrackConnections` and `TrackEventCounters`; `ActivitySource`/`Meter` registered for `"Marten"`, `"Wolverine"`, `"RunCoach.Llm"` ([Marten OTel docs](https://martendb.io/otel); [Wolverine OTel docs](https://wolverinefx.net/guide/logging)).
- **Anthropic pricing model:** Sonnet 4.5 / Haiku 4.5 emit `input_tokens`, `output_tokens`, `cache_creation_input_tokens` (1.25× base for 5‑min, 2.0× for 1‑hour), and `cache_read_input_tokens` (0.1× base) with a 1h TTL on Sonnet 4.5 / Haiku 4.5 / Opus 4.5 ([Anthropic prompt caching docs](https://platform.claude.com/docs/en/build-with-claude/prompt-caching)). Verifying cache hit rate requires inspecting these fields per call; the MindStudio writeup notes: "if cache_read_input_tokens is consistently zero across a multi‑turn conversation, caching isn't happening" ([MindStudio on cache debugging](https://www.mindstudio.ai/blog/anthropic-prompt-caching-claude-subscription-limits)).

---

## 3. OTel GenAI semantic conventions in 2026 — status and Anthropic specifics

The OpenTelemetry GenAI SIG has published a full set of semantic conventions covering **spans, metrics, events, and agent spans**, with a dedicated **Anthropic** extension ([OTel GenAI systems index](https://opentelemetry.io/docs/specs/semconv/gen-ai/); [OTel Anthropic semconv](https://opentelemetry.io/docs/specs/semconv/gen-ai/anthropic/)). As of the 2026 spec pages, these conventions remain marked **experimental / in‑development**; the spec recommends using `OTEL_SEMCONV_STABILITY_OPT_IN=gen_ai_latest_experimental` to opt into the latest shape and warns: "This transition plan will be updated to include stable version before the GenAI conventions are marked as stable" ([GenAI spans](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-spans/); [GenAI metrics](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-metrics/)). The March 2026 dev.to overview confirms: "As of March 2026, most GenAI semantic conventions are in experimental status" ([dev.to on GenAI semconv](https://dev.to/x4nent/opentelemetry-genai-semantic-conventions-the-standard-for-llm-observability-1o2a)). Treat this as *de‑facto* — every OSS backend surveyed speaks it — but expect additive attribute changes.

Core attributes that RunCoach should emit on every LLM span:

| Attribute | Example | Source |
|---|---|---|
| `gen_ai.operation.name` | `chat` | [GenAI spans spec](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-spans/) |
| `gen_ai.provider.name` | `anthropic` (required for Anthropic spans) | [Anthropic semconv](https://opentelemetry.io/docs/specs/semconv/gen-ai/anthropic/) |
| `gen_ai.request.model` | `claude-sonnet-4-5` | [GenAI registry](https://opentelemetry.io/docs/specs/semconv/registry/attributes/gen-ai/) |
| `gen_ai.response.model` | `claude-sonnet-4-5-20260401` | [GenAI registry](https://opentelemetry.io/docs/specs/semconv/registry/attributes/gen-ai/) |
| `gen_ai.usage.input_tokens` | 8132 | [GenAI registry](https://opentelemetry.io/docs/specs/semconv/registry/attributes/gen-ai/) |
| `gen_ai.usage.output_tokens` | 412 | [GenAI registry](https://opentelemetry.io/docs/specs/semconv/registry/attributes/gen-ai/) |
| `gen_ai.usage.cache_read.input_tokens` | 7904 | [Anthropic semconv note](https://opentelemetry.io/docs/specs/semconv/gen-ai/anthropic/) |
| `gen_ai.usage.cache_creation.input_tokens` | 0 | [Anthropic semconv note](https://opentelemetry.io/docs/specs/semconv/gen-ai/anthropic/) |
| `gen_ai.conversation.id` | `turnId` or `userId` | [GenAI registry](https://opentelemetry.io/docs/specs/semconv/registry/attributes/gen-ai/) |

Critically, the Anthropic semconv explicitly calls out that Anthropic's `input_tokens` *excludes* cached tokens and that instrumentation **MUST** compute `gen_ai.usage.input_tokens = input_tokens + cache_read_input_tokens + cache_creation_input_tokens` ([Anthropic semconv note 11](https://opentelemetry.io/docs/specs/semconv/gen-ai/anthropic/)) — this is the primitive that unlocks the prompt‑cache‑hit‑rate metric R‑048 asked for.

**Content capture is opt‑in:** by default GenAI instrumentations do not record full prompt/completion bodies, which is significant for your HBNR posture. Set `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true` to opt in (development only) ([OTel blog – GenAI](https://opentelemetry.io/blog/2024/otel-generative-ai/)). Equivalent M.E.AI flag: `UseOpenTelemetry(configure: c => c.EnableSensitiveData = true)` ([Microsoft Agent Framework observability](https://learn.microsoft.com/en-us/agent-framework/agents/observability)).

---

## 4. Capability matrix — OSS / self‑hostable candidates (spring 2026)

| Candidate | License | Self‑host dep footprint | .NET ingestion path | Prompt version diff | Anthropic cache‑aware | Eval framework | 2026 activity |
|---|---|---|---|---|---|---|---|
| **Langfuse OSS** | **MIT** (core); commercial EE for SCIM/audit ([Langfuse open source](https://langfuse.com/docs/open-source); [Langfuse handbook](https://langfuse.com/handbook/chapters/open-source)) | Postgres **+ ClickHouse + Redis/Valkey + S3/MinIO + 2 Node containers** ([Langfuse self‑host](https://langfuse.com/self-hosting); [v2→v3 migration](https://langfuse.com/self-hosting/upgrade/upgrade-guides/upgrade-v2-to-v3)) | **OTLP HTTP endpoint** `/api/public/otel/v1/traces` (no gRPC); unofficial .NET SDK (`zborek/Langfuse-dotnet`) ([Langfuse OTel](https://langfuse.com/integrations/native/opentelemetry); [Langfuse-dotnet](https://github.com/lukaszzborek/Langfuse-dotnet); [Langfuse .NET discussion](https://github.com/orgs/langfuse/discussions/9281)) | **First‑class** — labels, production/staging, prompt‑to‑trace linking, GitHub webhook sync ([Prompt versioning](https://langfuse.com/docs/prompt-management/features/prompt-version-control); [GitHub integration](https://langfuse.com/docs/prompt-management/features/github-integration)) | Yes – reads `gen_ai.usage.cache_*` via OTel endpoint | Yes – LLM‑as‑judge, datasets, experiments ([Langfuse overview](https://langfuse.com/)) | Very high; multi‑releases/week, 19k+ stars ([Firecrawl roundup](https://www.firecrawl.dev/blog/best-llm-observability-tools)) |
| **Arize Phoenix** | **Elastic License 2.0** – self‑host free, no feature gates; restricted only against reselling as managed service ([Phoenix license](https://arize.com/docs/phoenix/self-hosting/license); [LICENSE](https://github.com/Arize-ai/phoenix/blob/main/LICENSE)) | **1 container + SQLite** (default) or **+ Postgres** (reuses your existing DB) ([Phoenix Docker](https://arize.com/docs/phoenix/self-hosting/deployment-options/docker)) | OTLP gRPC (4317) and HTTP (6006) — **any** OTel GenAI instrumentation works; TS/Python SDKs available ([phoenix-otel PyPI](https://pypi.org/project/arize-phoenix-otel/); [Arize llms.txt](https://arize.com/llms.txt)) | Prompt playground + experiments/datasets, but no git‑webhook prompt registry like Langfuse ([Arize comparison doc](https://arize.com/llm-evaluation-platforms-top-frameworks/)) | Yes via OTel semconv | Yes – Evals library, experiments, datasets ([Phoenix home](https://phoenix.arize.com/)) | Very high; daily commits, 9.4k+ stars on main repo, 938 contrib on OpenInference ([Arize GitHub](https://github.com/arize-ai)) |
| **Opik (Comet)** | Apache 2.0 (core) ([Opik PyPI](https://pypi.org/project/opik/); [ZenML review](https://www.zenml.io/blog/langsmith-alternatives)) | Multi‑container: Postgres + ClickHouse + Redis + backend + frontend (similar profile to Langfuse) ([Opik README](https://github.com/comet-ml/opik); [Opik self‑host overview](https://www.comet.com/docs/opik/self-host/overview)) | Python/TS SDK + REST API + native OTel support ([Firecrawl roundup](https://www.firecrawl.dev/blog/best-llm-observability-tools)) | Prompt versioning in UI, Playground, agent optimizer | Yes via OTel | Yes – full evaluation suite, guardrails | Active; v2.0 line shipping weekly ([Opik PyPI releases](https://pypi.org/project/opik/)) |
| **OpenLIT** | Apache 2.0 | **3 components: OpenLIT platform + ClickHouse + OTel Collector** ([OpenLIT install](https://docs.openlit.io/latest/openlit/installation)) | OTLP‑native SDK (`openlit.init()` in Python); auto‑instrument several frameworks; no first‑party .NET SDK but the Collector‑path works | Prompt hub / vault feature; lighter than Langfuse | Generic GenAI semconv | Evals module; newer | Moderate/active; growing |
| **OpenLLMetry (Traceloop OSS)** | Apache 2.0 | **Zero new backend**: it's an instrumentation library that exports OTLP to whatever backend you have | Python/TS/Go/Ruby auto‑instrumentation ([OpenLLMetry GitHub](https://github.com/traceloop/openllmetry)) | Delegates to backend | Anthropic Python instrumentor emits prompts/completions/cache tokens ([openllmetry-anthropic pkg](https://github.com/traceloop/openllmetry/tree/main/packages/opentelemetry-instrumentation-anthropic)) | No | Delegates | **⚠️ Traceloop (commercial parent) acquired by ServiceNow March 2026 for ~$60‑80M** ([Morph OpenLLMetry overview](https://www.morphllm.com/openllmetry)); OSS codebase remains Apache 2.0 with 105 contributors but future stewardship uncertain |
| **Helicone self‑host** | Apache 2.0 ([Helicone README](https://github.com/Helicone/helicone/blob/main/README.md)) | Clickhouse + Postgres + MinIO + 3 app containers (was 12, now "just four") ([Helicone self‑host journey](https://www.helicone.ai/blog/self-hosting-journey)) | Proxy‑based – requires changing LLM base URL; not OTel‑native for ingestion | Built‑in prompt management | Yes – cache headers | Experiments/evals in platform | Active |
| **Custom OTel + Grafana (Tempo + Loki + Prometheus)** | Apache 2.0 | Tempo + Loki + Prometheus + Grafana (4 containers) | Pure OTel | No dedicated prompt UI | Raw attrs only | None – BYO | N/A (standard stack) |
| **Jaeger‑only + conventions** | Apache 2.0 | Already running (Slice 0) | Pure OTel | No prompt UI | Raw attrs — filterable but no rollups | None | CNCF, stable ([Uptrace on Jaeger](https://uptrace.dev/glossary/what-is-jaeger)) |

Honourable mentions the user excluded by constraint (kept for completeness): **Langfuse Cloud, LangSmith, Helicone Cloud, Arize AX, Opik Cloud, PostHog Cloud, Datadog, New Relic, Foundry** — all ruled out as paid SaaS. **MLflow** is now a legitimate OSS contender for tracing+eval under Apache 2.0 ([MLflow vs Phoenix](https://mlflow.org/arize-phoenix-alternative)) but its LLM‑observability UI is less mature than Phoenix and the deployment profile is heavier; listed here as a watch‑item for 2026.

**Specifically abandoned / deprecated to flag:** WhyLabs's commercial platform was discontinued after Apple's January 2025 acquisition ([AppSec Santa WhyLabs note](https://appsecsanta.com/arize-ai)) — rule out. Langfuse v2 (Postgres‑only, lightweight) is **no longer supported** ([Langfuse v2→v3 migration](https://langfuse.com/self-hosting/upgrade/upgrade-guides/upgrade-v2-to-v3)) — this is the single biggest change in this space since R‑048 was written.

---

## 5. Why Phoenix (self‑hosted, SQLite) wins at 10–20 users

### 5.1 Deployment footprint

Phoenix ships a single image `arizephoenix/phoenix:latest`. The minimal compose block is eight lines:

```yaml
services:
  phoenix:
    image: arizephoenix/phoenix:latest
    ports: ["6006:6006", "4317:4317"]
    environment:
      - PHOENIX_WORKING_DIR=/mnt/data
    volumes:
      - phoenix_data:/mnt/data
volumes:
  phoenix_data:
```

That's it — SQLite is the default persistence, no Postgres required unless you want it ([Phoenix Docker docs](https://arize.com/docs/phoenix/self-hosting/deployment-options/docker); [Medium deployment guide](https://medium.com/@guptaakshay213/streamlining-llm-observability-arize-phoenix-setup-on-gcp-with-terraform-and-docker-c0c4abf84b3c)). Because your existing Postgres already carries Marten, Wolverine, and app data, using Phoenix's SQLite keeps cross‑component blast radius low; if you later want query parity, setting `PHOENIX_SQL_DATABASE_URL=postgresql://…` points Phoenix at a new schema in the same Postgres instance.

Contrast with Langfuse: minimal v3 deployment needs web + worker + Postgres + ClickHouse + Redis + MinIO/S3 (6 containers). Quote from Langfuse maintainer: "The docker‑compose deployment should have the smallest resource footprint if you want to keep things as small as possible" — and that minimum is still 6 containers ([Langfuse #5785](https://github.com/orgs/langfuse/discussions/5785)). Langfuse themselves recommend ≥16GB RAM for ClickHouse in larger deployments ([Langfuse scaling](https://langfuse.com/self-hosting/configuration/scaling)).

### 5.2 OTel‑native, genuinely

Phoenix uses the **OpenInference** instrumentation family (Apache 2.0) to map OTel GenAI spans into its native schema. Its collector endpoint is OTLP gRPC 4317 / OTLP HTTP 6006 — you just change one environment variable on your existing OTel Collector to fan‑out to both Jaeger *and* Phoenix ([LaunchDarkly + Langfuse tutorial](https://launchdarkly.com/docs/tutorials/otel-llm-practical-guide-with-langfuse) demonstrates identical fan‑out pattern). Phoenix is in Arize's own words "vendor‑agnostic of framework and language" and self‑hostable "with zero feature gates" ([Arize Phoenix overview](https://github.com/Arize-ai/phoenix); [AppSec Santa Arize review](https://appsecsanta.com/arize-ai)).

### 5.3 The ELv2 licence — read carefully, then move on

Elastic License 2.0 forbids only two things relevant to RunCoach: (a) providing Phoenix to third parties as a hosted service (you aren't — RunCoach is your product, Phoenix is internal tooling); and (b) removing/circumventing license keys (Phoenix has none) ([Phoenix LICENSE](https://github.com/Arize-ai/phoenix/blob/main/LICENSE)). The Arize team and community have answered this explicitly: using Phoenix internally inside a commercial product you ship is "totally fine" ([Phoenix discussion #2412](https://github.com/Arize-ai/phoenix/discussions/2412); [Arize community licensing thread](https://community.arize.com/x/phoenix-support/19t1wzp44apw/understanding-elastic-license-20-vs-mit-license-fo)). Note that ELv2 is not OSI‑approved — if that matters to you philosophically, Langfuse OSS (MIT) is the alternative, but at 10–20 users its infra cost doesn't justify the licence win.

### 5.4 Prompt iteration story without Langfuse

Your existing pattern is `onboarding-v1.yaml` in git, loaded by `ContextAssembler`. You don't need Langfuse's prompt registry to diff versions — you need two things: (1) every LLM trace tagged with `runcoach.prompt.name` and `runcoach.prompt.version`, and (2) a UI that can pivot cost/latency/token‑cache‑rate by that attribute. Phoenix's experiments and datasets primitives do this natively; you can also pin a dataset of "scenario inputs" and rerun them against v1 vs v2 with side‑by‑side metrics ([Arize Phoenix features](https://phoenix.arize.com/); [Arize AI comparison](https://arize.com/llm-evaluation-platforms-top-frameworks/)). When Slice 3+ introduces `adaptation-v1.yaml`, the same attribute shape just works.

### 5.5 Eval framework integration — extends, does not replace DEC‑036

Your existing `Microsoft.Extensions.AI.Evaluation` infrastructure gives offline evals in MSTest/xUnit with response caching and HTML reports ([M.E.AI.Evaluation libraries](https://learn.microsoft.com/en-us/dotnet/ai/evaluation/libraries); [M.E.AI.Evaluation blog](https://developer.microsoft.com/blog/put-your-ai-to-the-test-with-microsoft-extensions-ai-evaluation)). The Microsoft docs describe it as supporting both offline (CI) and **online evaluation by publishing scores to telemetry/monitoring dashboards**. The bridge to Phoenix: emit eval scores as OTel events named `gen_ai.evaluation.result` (defined in the [GenAI events spec](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-events/)) or as child spans with `gen_ai.evaluation.score.value` attributes ([GenAI registry](https://opentelemetry.io/docs/specs/semconv/registry/attributes/gen-ai/)). DEC‑036's caching (DEC‑039 replay cache) remains the offline test harness; Phoenix is where those scores *land* for trend analysis. **Extend, don't replace** — matches the user's preference exactly. Langfuse would require you to adopt `langfuse.create_score()` API calls as an alternative path; Opik similarly. Phoenix has no equivalent API demand — OTel events are sufficient.

---

## 6. Trace‑shape sketch for RunCoach

### 6.1 Slice 1 — one onboarding turn (inline LLM call)

```
HTTP POST /onboarding/{userId}/turns   (ActivitySource: "Microsoft.AspNetCore")
│  runcoach.session_id = userId
│  runcoach.turn_id    = turnId
│
├── Wolverine invoke OnboardingTurnCommand   (ActivitySource: "Wolverine", kind: Consumer)
│   │  messaging.conversation_id = traceId   (set via Envelope.ParentId — see §7)
│   │
│   ├── ContextAssembler.Build                (ActivitySource: "RunCoach.Llm", kind: Internal)
│   │     runcoach.prompt.name    = "onboarding"
│   │     runcoach.prompt.version = "v1"
│   │     runcoach.prompt.cache_breakpoints = 2
│   │
│   ├── ICoachingLlm.Ask                      (ActivitySource: "Microsoft.Extensions.AI", kind: Client)
│   │     gen_ai.provider.name               = "anthropic"
│   │     gen_ai.operation.name              = "chat"
│   │     gen_ai.request.model               = "claude-sonnet-4-5"
│   │     gen_ai.response.model              = "claude-sonnet-4-5-20260401"
│   │     gen_ai.usage.input_tokens          = 8132   (computed sum)
│   │     gen_ai.usage.output_tokens         = 412
│   │     gen_ai.usage.cache_read.input_tokens     = 7904
│   │     gen_ai.usage.cache_creation.input_tokens = 0
│   │     gen_ai.conversation.id             = turnId
│   │
│   └── AnthropicStructuredOutputClient.Extract  (ActivitySource: "RunCoach.Llm", kind: Internal)
│         runcoach.extraction.schema         = "OnboardingTurnResult/v1"
│         runcoach.extraction.success        = true
│
└── Marten SaveChangesAsync                  (ActivitySource: "Marten")
```

Parent spans naturally inherit Trace‑ID via `Activity.Current` propagation in .NET (the System.Diagnostics API IS the OTel API on .NET) ([OTel .NET instrumentation](https://opentelemetry.io/docs/languages/dotnet/instrumentation/); [opentelemetry-dotnet docs](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/customizing-the-sdk/README.md)). The `Microsoft.Extensions.AI.UseOpenTelemetry()` middleware emits under source name **`"Microsoft.Extensions.AI"`** — add that to your `TracerProviderBuilder` via `.AddSource("Microsoft.Extensions.AI")` alongside your existing `"Marten"`, `"Wolverine"`, `"RunCoach.Llm"` sources ([UseOpenTelemetry API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.opentelemetrychatclientbuilderextensions.useopentelemetry?view=net-10.0-pp); [bartwullems blog](https://bartwullems.blogspot.com/2025/08/microsoftextensionsaipart-ivtelemetry.html)). This is confirmed by the Microsoft Agent Framework docs: "source name for traces is 'Microsoft.Extensions.AI'. Metrics are emitted on a meter with the same name. This means your OpenTelemetry configuration needs to subscribe to that source explicitly" ([devleader.ca on MAF observability](https://www.devleader.ca/2026/04/02/opentelemetry-and-observability-in-microsoft-agent-framework)).

### 6.2 Slice 3 — adaptation via Wolverine outbox (cross‑process continuation)

```
HTTP POST /runs/{runId}/complete
│  (trace context + runcoach.session_id propagated to Envelope)
│
├── Marten append events + enqueue Wolverine outbox  (synchronous)
│     └── wolverine_outgoing_envelopes row: ParentId = <W3C traceparent>
│
└── commit
    [...moments later, possibly on another node...]

Wolverine dequeue AdaptPlanCommand              (ActivitySource: "Wolverine", kind: Consumer)
│  Envelope.ParentId → StartActivity(..., parentId) — resumes the trace
│
├── ContextAssembler / ICoachingLlm / extraction  (same shape as §6.1)
│
└── Marten SaveChangesAsync
```

Wolverine serialises the W3C traceparent into the `Envelope.ParentId` header on send and calls `ActivitySource.StartActivity(spanName, kind, envelope.ParentId)` on receive — this is the standard Wolverine tracing pattern visible in [`WolverineTracing.cs`](https://github.com/JasperFx/wolverine/blob/main/src/Wolverine/Runtime/WolverineTracing.cs) and confirmed by [Wolverine.Envelope](https://github.com/JasperFx/wolverine/blob/main/src/Wolverine/Envelope.cs) (comment: "The open telemetry activity parent id. Wolverine uses this to correctly correlate …"). Net result: the HTTP originator, Marten commit, outbox hop, and adaptation LLM call live in **one trace** in both Jaeger and Phoenix. No custom propagator needed.

---

## 7. `Program.cs` wiring snippet (Slice 1)

```csharp
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// --- Anthropic chat client wrapped with OTel middleware ---
//     See: learn.microsoft.com/.../microsoft.extensions.ai/.../useopentelemetry
builder.Services.AddSingleton<IChatClient>(sp =>
{
    // AnthropicStructuredOutputClient (DEC-037) exposes IChatClient — or wrap the
    // first-party SDK via Microsoft.Extensions.AI.Anthropic (see §9).
    var inner = sp.GetRequiredService<AnthropicStructuredOutputClient>();
    return new ChatClientBuilder(inner)
        .UseOpenTelemetry(sourceName: "Microsoft.Extensions.AI",
                          configure: c =>
                          {
                              // Off in prod per HBNR; on in dev only (R-049).
                              c.EnableSensitiveData =
                                  builder.Environment.IsDevelopment();
                          })
        .Build();
});

// --- RunCoach-owned activity source for ContextAssembler / extraction ---
builder.Services.AddSingleton<IActivitySourceProvider>(
    _ => new ActivitySourceProvider("RunCoach.Llm"));

// --- OTel Collector (Slice 0) + GenAI sources ---
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("RunCoach.Api"))
    .WithTracing(t =>
    {
        t.AddSource("Microsoft.Extensions.AI")   // IChatClient spans (GenAI semconv)
         .AddSource("RunCoach.Llm")              // ContextAssembler + extraction
         .AddSource("Wolverine")                 // outbox + handler spans
         .AddSource("Marten")                    // session + event append spans
         .AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddOtlpExporter();                     // → collector → Jaeger + Phoenix
    })
    .WithMetrics(m =>
    {
        m.AddMeter("Microsoft.Extensions.AI")    // gen_ai.client.operation.duration, token counters
         .AddMeter("Wolverine")
         .AddMeter("Marten")
         .AddOtlpExporter();
    });

var app = builder.Build();
// ...
```

`OTEL_EXPORTER_OTLP_ENDPOINT` is already set by `docker-compose.otel.yml` (Slice 0). Also set `OTEL_SEMCONV_STABILITY_OPT_IN=gen_ai_latest_experimental` in the same env file to pick up the 2026 attribute names ([GenAI spans spec stability plan](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-spans/)).

Add these attributes *per call* around `ICoachingLlm.Ask` in `ContextAssembler`:

```csharp
using var activity = activitySource.StartActivity("assemble_context", ActivityKind.Internal);
activity?.SetTag("runcoach.session_id", userId);
activity?.SetTag("runcoach.turn_id", turnId);
activity?.SetTag("runcoach.prompt.name", "onboarding");
activity?.SetTag("runcoach.prompt.version", "v1");
activity?.SetTag("runcoach.prompt.cache_breakpoints", 2);
```

### docker‑compose overlay addition

```yaml
# docker-compose.otel.yml (additive)
  phoenix:
    image: arizephoenix/phoenix:latest
    container_name: runcoach-phoenix
    ports:
      - "6006:6006"   # UI + OTLP HTTP
      - "4317:4317"   # OTLP gRPC (optional, from Collector)
    environment:
      - PHOENIX_WORKING_DIR=/mnt/data
      - PHOENIX_PROJECT_NAME=runcoach-mvp0
    volumes:
      - phoenix_data:/mnt/data
    restart: unless-stopped

volumes:
  phoenix_data:
```

And fan the Collector out to both Jaeger and Phoenix:

```yaml
# otel-collector-config.yaml
exporters:
  otlp/jaeger:
    endpoint: jaeger:4317
    tls: { insecure: true }
  otlp/phoenix:
    endpoint: phoenix:4317
    tls: { insecure: true }

service:
  pipelines:
    traces:
      receivers:  [otlp]
      processors: [batch]
      exporters:  [otlp/jaeger, otlp/phoenix]
    metrics:
      receivers:  [otlp]
      processors: [batch]
      exporters:  [otlp/phoenix]   # plus prometheus if/when you add it
```

Fan‑out from a single OTel Collector to multiple backends is a standard pattern ([LaunchDarkly tutorial](https://launchdarkly.com/docs/tutorials/otel-llm-practical-guide-with-langfuse); [Uptrace Collector exporters](https://uptrace.dev/opentelemetry/collector/exporters)).

---

## 8. Cost / resource projection at 10–20 users

**Phoenix (SQLite, single container)** on a commodity VPS or dev workstation:
- RAM: ~300–500 MB steady; Phoenix is a Python app with an internal queue ([Phoenix deployment overview](https://arize.com/docs/phoenix/self-hosting/deployment-options/docker)).
- CPU: negligible at your trace volume (50–100k spans lifetime over MVP‑0/1 per §2).
- Disk: SQLite growth – at your token volumes, each span is a few KB; budget ~100 MB for a full MVP‑0/1 cohort, well under the typical 8 Gi PVC Phoenix examples use.
- Dollars: at this footprint Phoenix fits on a $5–10/mo VPS *alongside* Postgres, Jaeger, Collector, and the API, or more realistically zero additional spend on your existing dev machine / VPS.

**Why this matters vs. Langfuse:** the Tinybird ClickHouse guide notes ClickHouse "needs at least 4 CPU cores and 8GB of RAM for basic workloads" and "production deployments typically start with 16GB" ([Tinybird self‑host ClickHouse](https://www.tinybird.co/blog/step-by-step-self-host-clickhouse); [ClickHouse vs self‑host](https://oneuptime.com/blog/post/2026-03-31-clickhouse-cloud-vs-self-hosted/view)). Langfuse echoes: "At least 16 GiB of memory for larger deployments" ([Langfuse scaling](https://langfuse.com/self-hosting/configuration/scaling)). Pushing that into MVP‑0 for 10–20 users is textbook over‑engineering.

---

## 9. Interaction with R‑052 (Anthropic SDK choice)

Three plausible paths to `ICoachingLlm`:

1. **First‑party `Anthropic.SDK` (Tghamm).** Exposes `MessagesEndpoint : IChatClient` already ([DeepWiki Anthropic.SDK](https://deepwiki.com/tghamm/Anthropic.SDK/4.2-semantic-kernel-integration)). Wrap in `.UseOpenTelemetry()` and you get the full GenAI semconv shape — subject to the instrumentor actually emitting `gen_ai.usage.cache_*`. The caveat: M.E.AI.'s built‑in OpenTelemetryChatClient predates the 2026 Anthropic semconv extension; verify in Phoenix that cache tokens are populated. If not, add them manually in `AnthropicStructuredOutputClient` by reading the Anthropic `usage` block post‑response (parallels Spring AI's pattern: `usage.cacheCreationInputTokens()` / `usage.cacheReadInputTokens()` — [Spring AI prompt caching blog](https://spring.io/blog/2025/10/27/spring-ai-anthropic-prompt-caching-blog/)).

2. **`Microsoft.Extensions.AI.Anthropic` (community).** Provides `AddAnthropicChatClient` with native `UseOpenTelemetry()` composition ([jeremy-schaab/M.E.AI.Anthropic](https://github.com/jeremy-schaab/Microsoft.Extensions.AI.Anthropic)). Same caveat as (1) on cache‑token emission.

3. **DEC‑037 `AnthropicStructuredOutputClient` bridge.** Keep it; just make sure it implements `IChatClient` or is wrapped by one, so the M.E.AI OTel middleware sits on top.

**Recommendation:** whichever SDK R‑052 picks, place the `UseOpenTelemetry()` middleware on `ICoachingLlm` at the **innermost** layer, and attach RunCoach business attributes at the **outermost** `ContextAssembler` span. Order matters: per the MAF docs, "Placing `UseOpenTelemetry()` before `UseFunctionInvocation()` means the telemetry middleware wraps the function invocation middleware" ([devleader.ca on MAF](https://www.devleader.ca/2026/04/02/opentelemetry-and-observability-in-microsoft-agent-framework)).

---

## 10. Interaction with R‑053 (multi‑turn eval)

Keep `Microsoft.Extensions.AI.Evaluation` (DEC‑036) as the offline harness — CI runs it against committed replay fixtures (DEC‑039) and renders HTML reports via `dotnet aieval` ([M.E.AI.Evaluation tutorial](https://learn.microsoft.com/en-us/dotnet/ai/evaluation/evaluate-with-reporting)). Microsoft's docs explicitly note the library supports "online evaluation of your application by publishing evaluation scores to telemetry and monitoring dashboards" ([M.E.AI.Evaluation intro blog](https://developer.microsoft.com/blog/put-your-ai-to-the-test-with-microsoft-extensions-ai-evaluation)). The integration point with Phoenix:

- Offline (CI): MSTest tests consume the replay cache, produce `EvaluationResult`s, write local HTML report, fail the build on regression. Unchanged.
- Online (prod): when evaluators run inline (e.g., a "was the plan reasonable?" LLM‑as‑judge), emit one `gen_ai.evaluation.result` event per score under the parent LLM span, with `gen_ai.evaluation.name`, `gen_ai.evaluation.score.value`, `gen_ai.evaluation.score.label` ([GenAI events spec](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-events/); [GenAI attribute registry](https://opentelemetry.io/docs/specs/semconv/registry/attributes/gen-ai/)).
- Phoenix will surface these as evaluation annotations on the trace; you don't need Phoenix's own evaluator UI in Slice 1 unless you want it.

Verdict: **extend, not replace** — exactly what the user asked for. Langfuse would have forced a parallel `create_score()` code path; Phoenix + OTel events avoid that.

---

## 11. Privacy / data‑residency note (R‑049 / DEC‑046)

Self‑hosting Phoenix inside your Compose network means no third‑party data processor sees user conversation content. Under FTC HBNR / PHR vendor posture this eliminates the need for a DPA with an observability provider. Two operational controls to keep in place:

1. Leave `EnableSensitiveData = false` (equivalently `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=false`) in production; the GenAI conventions *default* is to not record prompt/completion bodies ([OTel GenAI blog](https://opentelemetry.io/blog/2024/otel-generative-ai/); [GenAI spans – content capture section](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-spans/)). Bodies live in Marten events (your source of truth) and are replayable; observability only needs structure + metrics.
2. Use the OTel Collector's transform processor to drop any span events matching `gen_ai.prompt.content` / `gen_ai.completion.content` before export if you ever toggle content capture for debugging — the Uptrace pipeline shows the pattern ([Uptrace OTel for AI systems](https://uptrace.dev/blog/opentelemetry-ai-systems)).

Phoenix itself stays inside your cluster; its SQLite file is backed up alongside the RunCoach Postgres volume.

---

## 12. CritterWatch (R‑051 sub‑question 12) — evaluate‑only

CritterWatch is the JasperFx‑Software commercial observability console for Marten and Wolverine (and future "critters" like Polecat). Its feature set, per the JasperFx site, is strictly Marten/Wolverine operational: live dashboards for node/agent status, **dead‑letter queue replay with edit‑and‑replay**, async projection lag tracking, and alert lifecycle stored as Marten events ([CritterWatch home](https://critterwatch.jasperfx.net/); [Jeremy Miller on CritterWatch](https://jeremydmiller.com/tag/marten/)). It is designed to correlate with your OpenTelemetry data, not to replace an LLM‑observability tool ([Critter Stack WIP post, 2025](https://jeremydmiller.com/2025/03/30/critter-stack-work-in-progress/)): "Work with your OpenTelemetry tracking to correlate ongoing performance information to the artifacts in your system".

**Overlap with LLM observability: essentially none.** CritterWatch answers "is Wolverine's outbox draining?" and "how behind is this projection?" — Phoenix answers "what did Claude cost on turn 3 and did the cache hit?". The two are **orthogonal** and would live side by side in a fully‑instrumented system. Given the user has explicitly ruled CritterWatch out at this phase due to commercial licensing cost at family‑and‑friends scale, there is **no adoption recommendation**, but if the user ever revisits, the trace‑shape chosen here (OTel GenAI on top of Wolverine's existing `Wolverine` ActivitySource + Envelope.ParentId propagation) is already compatible with CritterWatch's OTel correlation model. No forward‑work tax.

---

## 13. Phased adoption plan (keyed to the 10–20 user envelope)

### Slice 1 (now) — minimum viable floor

- **Add** `Microsoft.Extensions.AI` + `.UseOpenTelemetry()` wrapping around `ICoachingLlm`.
- **Add** RunCoach business attributes (`runcoach.session_id`, `turn_id`, `prompt.name`, `prompt.version`) on the `ContextAssembler` span.
- **Add** Phoenix single container to `docker-compose.otel.yml`; fan the Collector out to Jaeger + Phoenix.
- **Set** `OTEL_SEMCONV_STABILITY_OPT_IN=gen_ai_latest_experimental`.
- **Leave** content capture off in non‑dev.
- **Do nothing** about prompt registries — keep `onboarding-v1.yaml` in git, tag spans with version.
- Total new work: ~2 hours. Total new infra: one container. No new database.

### Slice 3 (adaptation) — reuse, don't expand

- Ensure Wolverine outbox propagates `Envelope.ParentId` (default behaviour, confirm visually in Phoenix).
- Same `ContextAssembler` attribute shape applies to `adaptation-v1.yaml`.
- Publish evaluator scores (from DEC‑036) as `gen_ai.evaluation.result` events, rendered as annotations in Phoenix.

### Slice 4 (open conversation) — reuse again

- The trace shape is locked in. No retrofit cost. This is the payoff R‑048 predicted.

### Escape hatch — if volume outgrows Phoenix

Because every instrumentation attribute is **standard OTel GenAI** (no Phoenix‑proprietary SDK on the call site), migrating to Langfuse OSS later is a Collector‑config change:

```yaml
exporters:
  otlphttp/langfuse:
    endpoint: http://langfuse-web:3000/api/public/otel/v1/traces
    headers:
      Authorization: Basic <base64(pk:sk)>
```

(Langfuse OTLP endpoint spec: [Langfuse OTel integration](https://langfuse.com/integrations/native/opentelemetry).) No code changes. Move when you actually need ClickHouse analytics — i.e., 100× the current volume, not 2×.

### Minimum‑viable floor (in case even Phoenix feels heavy)

If the user wants to defer Phoenix entirely: **Jaeger alone + two Grafana dashboards** works. The `gen_ai.usage.cache_read.input_tokens` attribute is searchable in Jaeger as a tag, and Prometheus can scrape the `gen_ai.client.token.usage` histogram from the Collector for cache‑hit‑rate and cost‑per‑model rollups. Tradeoff: no prompt playground, no dataset/experiment UI, eval annotation becomes a manual grep. Not recommended as the default because Phoenix's incremental cost is near zero and its feature payoff is large, but it is a legitimate floor per the user's "actively consider whether minimal custom OTel + Jaeger + Grafana beats adopting a heavyweight platform" brief.

---

## 14. Rejected alternatives

### Ruled out by the 10–20 user volume envelope (OSS, technically available)

- **Langfuse self‑hosted v3.** Six‑container deployment (Web + Worker + Postgres + ClickHouse + Redis + MinIO/S3), ClickHouse alone wanting 8–16 GB RAM for comfortable operation. Best‑in‑class prompt registry, git sync, LLM‑as‑judge — but at this scale the infra tax buys nothing the team will see. Revisit if public‑beta volume ever justifies a Kubernetes deployment. No `.NET` SDK either, only an unofficial community one ([Langfuse .NET discussion](https://github.com/orgs/langfuse/discussions/9281); [Langfuse-dotnet](https://github.com/lukaszzborek/Langfuse-dotnet)). MIT‑licensed core is genuinely permissive ([Langfuse open source](https://langfuse.com/docs/open-source)) — this is a scale decision, not a licence one.
- **Opik self‑hosted.** Comparable feature set to Langfuse with Apache 2.0 licence; similar multi‑container footprint (Postgres + ClickHouse + Redis + backend + frontend) ([Opik README](https://github.com/comet-ml/opik)). Same scale critique.
- **Helicone self‑hosted.** Apache 2.0, now "just four" containers but still ClickHouse + Postgres + MinIO + app tiers ([Helicone self‑hosting journey](https://www.helicone.ai/blog/self-hosting-journey)). Also proxy‑based for ingestion — not OTel‑native, which means your existing Collector doesn't plug in; you'd change LLM `baseURL` instead. Conflicts with R‑050's Collector‑centric posture.
- **OpenLIT self‑hosted.** Three‑component stack (platform + ClickHouse + Collector) ([OpenLIT install](https://docs.openlit.io/latest/openlit/installation)). Lighter than Langfuse but still adds ClickHouse. No first‑party .NET SDK — would funnel through the Collector. Legitimate Phoenix alternative if the user later prefers the K8s operator model.
- **OpenLLMetry / Traceloop SDK (OSS).** Apache 2.0, and Traceloop's Python `opentelemetry-instrumentation-anthropic` is genuinely useful ([openllmetry-anthropic](https://github.com/traceloop/openllmetry/tree/main/packages/opentelemetry-instrumentation-anthropic)). But (a) no .NET SDK; (b) **Traceloop acquired by ServiceNow on 2 March 2026** for a reported $60–80M ([Morph OpenLLMetry overview](https://www.morphllm.com/openllmetry)); OSS remains Apache‑2 with 105 contributors but long‑term stewardship is an unresolved question. Use its OTel instrumentation *libraries* inside Phoenix if you want them, don't adopt it as a platform.
- **Custom Grafana (Tempo + Loki + Prometheus) stack.** Would work but adds four containers and substantial dashboard‑authoring work with no LLM‑aware views out of the box. You would effectively be reinventing Phoenix's UI.
- **Jaeger‑only with custom attribute conventions.** Listed above as the "minimum‑viable floor" fallback.

### Ruled out by constraint — paid SaaS

- **Langfuse Cloud** — ruled out by constraint (paid SaaS). If the constraint is ever lifted, Langfuse Cloud's 50k‑observation free tier plus MIT‑licensed self‑host fallback make it a strong upgrade path ([Langfuse CheckThat pricing](https://checkthat.ai/brands/langfuse/pricing); [Langfuse home](https://langfuse.com/)).
- **LangSmith (LangChain's hosted service)** — ruled out by constraint (paid SaaS).
- **Helicone hosted** — ruled out by constraint (paid SaaS).
- **Arize AX** (commercial tier above Phoenix) — ruled out by constraint (paid SaaS). Phoenix shares the OpenInference trace schema with AX so migration later is an option if ever desired ([Arize licensing Q&A](https://arize.com/llm-evaluation-platforms-top-frameworks/)).
- **Opik Cloud / Comet hosted** — ruled out by constraint (paid SaaS).
- **PostHog Cloud (LLM observability tier)** — ruled out by constraint (paid SaaS).
- **Datadog / New Relic / Dynatrace / Dash0** — ruled out by constraint (paid SaaS); New Relic integrates OpenLLMetry natively ([New Relic OpenLLMetry](https://docs.newrelic.com/docs/opentelemetry/get-started/traceloop-llm-observability/traceloop-llm-observability-intro/)) if ever revisited.
- **Microsoft Foundry / Application Insights agent observability** — ruled out by constraint (paid SaaS, Azure‑bound).

### Ruled out by user decision — not cost/infra

- **CritterWatch (JasperFx commercial)** — evaluate‑only per user instruction. Orthogonal to LLM observability (Marten/Wolverine‑scope only), does not overlap with Phoenix; revisitable if commercial licensing cost becomes acceptable later. See §12.

---

## 15. Citations consolidated (primary sources used)

- OpenTelemetry GenAI semconv — [index](https://opentelemetry.io/docs/specs/semconv/gen-ai/), [spans](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-spans/), [metrics](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-metrics/), [events](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-events/), [agent spans](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-agent-spans/), [**Anthropic** extension](https://opentelemetry.io/docs/specs/semconv/gen-ai/anthropic/), [attribute registry](https://opentelemetry.io/docs/specs/semconv/registry/attributes/gen-ai/), [AI Agent Observability post](https://opentelemetry.io/blog/2025/ai-agent-observability/), [semantic-conventions repo releases](https://github.com/open-telemetry/semantic-conventions/releases).
- OpenTelemetry .NET — [instrumentation guide](https://opentelemetry.io/docs/languages/dotnet/instrumentation/), [SDK trace customisation](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/customizing-the-sdk/README.md), [best practices](https://opentelemetry.io/docs/languages/dotnet/traces/best-practices/), [OTel for AI systems (Uptrace)](https://uptrace.dev/blog/opentelemetry-ai-systems).
- Anthropic prompt caching — [official docs](https://platform.claude.com/docs/en/build-with-claude/prompt-caching), [Spring AI implementation](https://spring.io/blog/2025/10/27/spring-ai-anthropic-prompt-caching-blog/), [Helicone on cache](https://docs.helicone.ai/gateway/concepts/prompt-caching), [DigitalOcean overview](https://www.digitalocean.com/community/tutorials/prompt-caching-explained), [MindStudio on verification](https://www.mindstudio.ai/blog/anthropic-prompt-caching-claude-subscription-limits), [Portkey normalisation](https://portkey.ai/docs/integrations/llms/anthropic/prompt-caching), [Vertex AI caching](https://docs.cloud.google.com/vertex-ai/generative-ai/docs/partner-models/claude/prompt-caching).
- Microsoft.Extensions.AI — [libraries overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai), [UseOpenTelemetry API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.opentelemetrychatclientbuilderextensions.useopentelemetry?view=net-10.0-pp), [dotnet/extensions README](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI/README.md), [NuGet OpenAI package](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI), [bartwullems telemetry post](https://bartwullems.blogspot.com/2025/08/microsoftextensionsaipart-ivtelemetry.html), [Microsoft Agent Framework observability](https://learn.microsoft.com/en-us/agent-framework/agents/observability), [devleader.ca on MAF](https://www.devleader.ca/2026/04/02/opentelemetry-and-observability-in-microsoft-agent-framework), [M.E.AI.Anthropic (community)](https://github.com/jeremy-schaab/Microsoft.Extensions.AI.Anthropic), [Anthropic.SDK IChatClient](https://deepwiki.com/tghamm/Anthropic.SDK/4.2-semantic-kernel-integration).
- Microsoft.Extensions.AI.Evaluation — [libraries](https://learn.microsoft.com/en-us/dotnet/ai/evaluation/libraries), [intro blog](https://developer.microsoft.com/blog/put-your-ai-to-the-test-with-microsoft-extensions-ai-evaluation), [tutorial](https://learn.microsoft.com/en-us/dotnet/ai/evaluation/evaluate-with-reporting), [quickstart](https://learn.microsoft.com/en-us/dotnet/ai/evaluation/evaluate-ai-response).
- Marten OTel — [Marten OTel docs](https://martendb.io/otel); event‑driven.io [Event Sourcing + OTel](https://event-driven.io/en/set_up_opentelemetry_wtih_event_sourcing_and_marten/); [issue #2909](https://github.com/JasperFx/marten/issues/2909).
- Wolverine OTel / outbox — [Wolverine instrumentation docs](https://wolverinefx.net/guide/logging), [runtime architecture](https://wolverinefx.net/guide/runtime), [durability / outbox](https://wolverinefx.net/guide/durability/), [Marten outbox integration](https://wolverinefx.net/guide/durability/marten/outbox.html), [interop / envelope propagation](https://wolverinefx.net/tutorials/interop.html), [WolverineTracing.cs](https://github.com/JasperFx/wolverine/blob/main/src/Wolverine/Runtime/WolverineTracing.cs), [Envelope.cs](https://github.com/JasperFx/wolverine/blob/main/src/Wolverine/Envelope.cs), [Jeremy Miller – Wolverine instrumentation](https://jeremydmiller.com/2023/01/02/wolverine-delivers-the-instrumentation-you-want-and-need/), [Tim Deschryver – Wolverine + OTel](https://timdeschryver.dev/blog/wolverine-embraces-observability), [Wolverine 5.0 release](https://jeremydmiller.com/2025/10/23/wolverine-5-0-is-here/), [Wolverine outbox blog](https://jeremydmiller.com/2024/12/08/build-resilient-systems-with-wolverines-transactional-outbox/).
- Arize Phoenix — [home](https://phoenix.arize.com/), [Docker deployment](https://arize.com/docs/phoenix/self-hosting/deployment-options/docker), [Kubernetes](https://docs.arize.com/phoenix/self-hosting/deployment-options/kubernetes), [LICENSE](https://github.com/Arize-ai/phoenix/blob/main/LICENSE), [licence overview](https://arize.com/docs/phoenix/self-hosting/license), [discussion #2412](https://github.com/Arize-ai/phoenix/discussions/2412), [community ELv2 Q&A](https://community.arize.com/x/phoenix-support/19t1wzp44apw/understanding-elastic-license-20-vs-mit-license-fo), [comparison vs Langfuse](https://arize.com/llm-evaluation-platforms-top-frameworks/), [phoenix-otel PyPI](https://pypi.org/project/arize-phoenix-otel/), [Arize AI GitHub org](https://github.com/arize-ai), [AppSec Santa Arize review](https://appsecsanta.com/arize-ai).
- Langfuse — [home](https://langfuse.com/), [open‑source strategy](https://langfuse.com/docs/open-source), [handbook – open source](https://langfuse.com/handbook/chapters/open-source), [self‑hosting](https://langfuse.com/self-hosting), [architecture](https://langfuse.com/handbook/product-engineering/architecture), [OTel integration](https://langfuse.com/integrations/native/opentelemetry), [ClickHouse config](https://langfuse.com/self-hosting/deployment/infrastructure/clickhouse), [Redis/Valkey](https://langfuse.com/self-hosting/deployment/infrastructure/cache), [scaling guide](https://langfuse.com/self-hosting/configuration/scaling), [v2→v3 migration](https://langfuse.com/self-hosting/upgrade/upgrade-guides/upgrade-v2-to-v3), [prompt management](https://langfuse.com/docs/prompt-management/overview), [prompt version control](https://langfuse.com/docs/prompt-management/features/prompt-version-control), [GitHub integration](https://langfuse.com/docs/prompt-management/features/github-integration), [discussion #1902 on v3 architecture](https://github.com/orgs/langfuse/discussions/1902), [discussion #5785 on footprint](https://github.com/orgs/langfuse/discussions/5785), [.NET SDK discussion](https://github.com/orgs/langfuse/discussions/9281), [Semantic Kernel/.NET guide](https://github.com/orgs/langfuse/discussions/4772), [Langfuse-dotnet community SDK](https://github.com/lukaszzborek/Langfuse-dotnet), [ClickHouse blog on Langfuse](https://clickhouse.com/blog/langfuse-and-clickhouse-a-new-data-stack-for-modern-llm-applications), [LaunchDarkly + Langfuse tutorial](https://launchdarkly.com/docs/tutorials/otel-llm-practical-guide-with-langfuse), [pricing overview](https://checkthat.ai/brands/langfuse/pricing).
- Opik / Comet — [GitHub README](https://github.com/comet-ml/opik), [self‑host overview](https://www.comet.com/docs/opik/self-host/overview), [FAQ](https://www.comet.com/docs/opik/faq), [pricing](https://www.comet.com/site/pricing/), [PyPI](https://pypi.org/project/opik/), [Medium user review](https://medium.com/@connectmdzahid/from-ai-chaos-to-control-my-deep-dive-into-opik-for-llm-observability-d898b71e7516), [ZenML LangSmith alternatives](https://www.zenml.io/blog/langsmith-alternatives), [Firecrawl 2026 roundup](https://www.firecrawl.dev/blog/best-llm-observability-tools).
- OpenLIT — [installation docs](https://docs.openlit.io/latest/openlit/installation), [OpenLLMetry integration](https://docs.openlit.io/latest/operator/instrumentations/python/openllmetry).
- OpenLLMetry / Traceloop — [OpenLLMetry GitHub](https://github.com/traceloop/openllmetry), [Anthropic instrumentor](https://github.com/traceloop/openllmetry/tree/main/packages/opentelemetry-instrumentation-anthropic), [opentelemetry-semantic-conventions-ai PyPI](https://pypi.org/project/opentelemetry-semantic-conventions-ai/), [Traceloop home](https://www.traceloop.com/), [Morph overview incl. ServiceNow acquisition](https://www.morphllm.com/openllmetry), [New Relic OpenLLMetry docs](https://docs.newrelic.com/docs/opentelemetry/get-started/traceloop-llm-observability/traceloop-llm-observability-intro/), [VictoriaMetrics comparison](https://victoriametrics.com/blog/ai-agents-observability/).
- Helicone — [self‑host docs](https://docs.helicone.ai/getting-started/self-host/manual), [Docker Compose guide](https://docs.helicone.ai/getting-started/self-deploy-docker), [self‑hosting blog](https://www.helicone.ai/blog/self-hosting-launch), [self‑hosting simplification journey](https://www.helicone.ai/blog/self-hosting-journey), [AI Gateway repo](https://github.com/Helicone/ai-gateway).
- MLflow (watch‑item) — [vs Arize Phoenix](https://mlflow.org/arize-phoenix-alternative).
- ClickHouse self‑host cost context — [Tinybird deployment models](https://www.tinybird.co/blog/clickhouse-deployment-options), [Tinybird self‑host guide](https://www.tinybird.co/blog/step-by-step-self-host-clickhouse), [OneUptime cloud vs self‑hosted](https://oneuptime.com/blog/post/2026-03-31-clickhouse-cloud-vs-self-hosted/view), [Docker Hub clickhouse image](https://hub.docker.com/_/clickhouse).
- Jaeger / observability landscape — [Uptrace top observability tools 2026](https://uptrace.dev/tools/top-observability-tools), [Uptrace on Jaeger](https://uptrace.dev/glossary/what-is-jaeger), [Dash0 Jaeger alternatives](https://www.dash0.com/comparisons/jaeger-alternatives-for-tracing), [Dash0 open‑source tracing tools 2026](https://www.dash0.com/comparisons/open-source-distributed-tracing-tools), [awesome‑opentelemetry](https://github.com/magsther/awesome-opentelemetry).
- CritterWatch / JasperFx commercial — [CritterWatch home](https://critterwatch.jasperfx.net/), [Critter Stack WIP (2025‑03)](https://jeremydmiller.com/2025/03/30/critter-stack-work-in-progress/), [JasperFx plans for Marten & Wolverine](https://jeremydmiller.com/2025/04/02/a-quick-note-about-jasperfxs-plans-for-marten-wolverine/), [Critter Stack AI skills (2026‑04)](https://jeremydmiller.com/2026/04/15/critter-stack-sample-projects-and-our-curated-ai-skills/), [Marten 5‑year retrospective (2026‑03)](https://jeremydmiller.com/2026/03/06/fun-five-year-retrospective-on-marten-adoption/), [Marten tag archive](https://jeremydmiller.com/tag/marten/).