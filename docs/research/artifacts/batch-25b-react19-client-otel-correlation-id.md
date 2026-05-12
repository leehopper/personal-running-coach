## batch-25b · React 19 + Vite 8 + RTK Query → OTel Collector → Jaeger: client-side instrumentation, W3C propagation, and correlation-ID surfacing

**Status:** Research artifact, advisory. Supports a forthcoming DEC locking SDK choice, deployment topology, and propagation pattern for RunCoach MVP-0.
**Date of research:** 2026-05-12. All versions and behaviours verified against the OpenTelemetry-JS main branch (SDK 2.x / instrumentation 0.20x series) and `opentelemetry-collector` ≥ 0.149.
**Out of scope:** Backend OTel changes beyond confirming default propagators work; LLM/Phoenix observability; production cloud-tracing topology (Tempo/Datadog/Honeycomb); client metrics & logs; the R-073 error-boundary library choice itself; persistence of correlation IDs in an errors table.

## TL;DR

- **Default:** Install `@opentelemetry/api@^1.9`, `@opentelemetry/sdk-trace-web@^2.7`, `@opentelemetry/sdk-trace-base@^2.7`, `@opentelemetry/exporter-trace-otlp-http@^0.20x`, `@opentelemetry/instrumentation@^0.20x`, `@opentelemetry/instrumentation-fetch@^0.20x`, `@opentelemetry/resources@^2.7`, `@opentelemetry/semantic-conventions@^1.3x`. Use the built-in `StackContextManager` (not `ZoneContextManager`) — `BatchSpanProcessor` → `OTLPTraceExporter` posting to `http://localhost:4318/v1/traces`. Add a 6-line `cors:` block to the OTLP receiver in `infra/otel/otel-collector-config.yaml` so the browser posts direct to the collector. Surface the last-seen **trace-id (32-hex)** to the error boundary via a tiny module-level singleton fed by `FetchInstrumentation.applyCustomAttributesOnSpan`.
- **Fallback:** If the gzipped delta (~30–45 KB after tree-shake; ~60 KB if you also keep `instrumentation-document-load`) is unacceptable, ship a **15-line hand-rolled `prepareHeaders` shim** that mints a `traceparent` per request from `crypto.getRandomValues`, POSTs a minimal OTLP/JSON envelope to `/v1/traces` via `fetch(..., {keepalive:true})`, and stashes the trace-id in the same module singleton. Loses Resource Timing API enrichment and auto-correlation between same-trace fetches, but is ~1.5 KB gzipped and TypeScript-strict clean.
- **Topology:** **browser → collector direct** with an origin allow-list (`http://localhost:5173` in dev, the deployed origin in prod). Adding `cors:` to the `otlphttp` receiver is the canonical pattern documented by `opentelemetry-collector` upstream. The backend-proxy variant is rejected as default because it loses the collector-as-network-boundary property and forces an OTLP-JSON re-emit endpoint in ASP.NET Core that has no security benefit at this stage.

---

## Recommendation summary table

| Decision | Default | Fallback |
| --- | --- | --- |
| Browser SDK | `@opentelemetry/sdk-trace-web` 2.7.x + `instrumentation-fetch` 0.20x | hand-rolled `prepareHeaders` + `crypto.getRandomValues` + `fetch keepalive` |
| Context manager | `StackContextManager` (default in sdk-trace-web; no `zone.js`) | n/a |
| Exporter | `@opentelemetry/exporter-trace-otlp-http` (OTLP/HTTP-JSON) → `:4318/v1/traces` | bare `fetch` POST of minimal OTLP/JSON |
| Span processor | `BatchSpanProcessor` (`scheduledDelayMillis: 5000`, `maxQueueSize: 2048`, `maxExportBatchSize: 64`) | `SimpleSpanProcessor` (1 span, 1 request) |
| Propagator | default `CompositePropagator` = W3C TraceContext + W3C Baggage (matches ASP.NET Core defaults) | manual `propagation.inject(context.active(), headers, defaultTextMapSetter)` |
| Topology | browser → collector direct, CORS allow-list on `otlphttp` receiver | reverse-proxy via Vite dev + ASP.NET Core `/v1/traces` pass-through |
| Correlation-ID format displayed | **trace-id only**, 32 lowercase hex chars, monospace, copy-to-clipboard | same |
| Correlation-ID seam | module-level singleton `lastTraceId.ts` updated from `applyCustomAttributesOnSpan` + read via a tiny hook | same singleton; updated from manual injector |
| Sampling (MVP-0 personal use) | `AlwaysOnSampler` (head-based 100%) | same |
| Sampling (MVP-1 public testers) | `ParentBasedSampler({ root: new TraceIdRatioBasedSampler(0.1) })` plus tail-based "always-on-error" via collector | same |
| PII posture | scrub URL search params, drop request bodies, never set user-typed answers as attributes | same |
| Privacy posture MVP-1 | **opt-in via cookie consent**, default off in EU | same |
| Failure mode | silent retry-with-backoff (default OTLP exporter), no user-visible error | same |

## 1 · SDK choice comparison

### Comparison

| Candidate | Gz delta (est.) | Tree-shake on Vite 8 / Rollup 4 | RTK Query compat | TS-strict | Maint signal | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| `sdk-trace-web` + `instrumentation-fetch` + `exporter-trace-otlp-http` (no `context-zone`) | ~30–45 KB gz | Good with SDK 2.x: packages declare `sideEffects: false`; explicit-import pattern (Last9 / SigNoz / OneUptime guides all use it) | Wraps global `fetch`. `fetchBaseQuery` internally calls `globalThis.fetch`, so a single instrumentation hook covers RTK Query, plain `fetch`, and any third-party code. No double-wrap. | Yes — official `.d.ts` shipped, strict-mode clean since 2.0 | Excellent — SDK 2.0 in Apr 2025, current 2.7.1 (~weekly cadence), CNCF-graduated | **DEFAULT** |
| `@opentelemetry/auto-instrumentations-web` meta | ~60+ KB gz | Pulls `instrumentation-fetch` + `instrumentation-xml-http-request` + `instrumentation-document-load` + `instrumentation-user-interaction`. Tree-shakeable only if you `getWebAutoInstrumentations({...})` with subset disabled — but most users include all. | Same | Yes | Same repo, same cadence | **Reject for MVP-0** — pulls instrumentations we don't need yet (user-interaction, document-load) |
| `@vercel/otel` | n/a (Node/Edge only) | Designed for Next.js `instrumentation.ts` server hook. Edge runtime + Node bundles. **Not a browser SDK.** Vercel's own doc states "no traces or metrics are recorded for any browser-side interactions" with this package alone. | n/a | n/a | Active | **Reject** — wrong layer |
| `@sentry/browser` + Sentry tracing | ~50–70 KB gz | OK | Yes | Yes | Active | **Reject as default** — Sentry browser SDK does NOT emit OTLP. Maintainers state on github.com/getsentry/sentry-javascript Discussion #7364 that browser OTel support is "on hold" pending the OTel Browser SIG's Phase-1 reset. You would get Sentry-format traces, not Jaeger-compatible OTLP, defeating the requirement that backend ASP.NET Core spans chain under the same trace in Jaeger. |
| `@grafana/faro-web-tracing` | ~40–50 KB gz (it re-exports the OTel SDK plus its own session-span processor) | OK | Yes | Yes | Active, ~weekly | **Plausible secondary choice** if RunCoach later wants Grafana Cloud or RUM (errors/web-vitals/sessions). Overkill for MVP-0 trace-only goal. Worth re-evaluating at MVP-1. |
| Hand-rolled `prepareHeaders` injector + tiny OTLP POST | **~1.5 KB gz** | Trivially perfect | Yes (direct seam) | Yes | n/a — your code | **FALLBACK** — chosen if the SDK delta is judged too costly for MVP-0 |

### Why `StackContextManager` and not `ZoneContextManager`

The `@opentelemetry/sdk-trace-web` package documents that `ZoneContextManager` requires transpiling back to ES2015 and pulls `zone.js` (~80–90 KB; see SigNoz 2026 article "Reducing OpenTelemetry Bundle Size in Browser Frontend" and OneUptime "How to Reduce OpenTelemetry Browser SDK Bundle Size with Tree Shaking"). RunCoach targets ES2022+ (Vite 8 default), so:

> "You can choose to use the ZoneContextManager if you want to trace asynchronous operations. Please note that the ZoneContextManager does not work with JS code targeting ES2017+. In order to use the ZoneContextManager, please transpile back to ES2015." — [@opentelemetry/sdk-trace-web README, npm](https://www.npmjs.com/package/@opentelemetry/sdk-trace-web)

We do **not** need zone tracking because RTK Query's hooks `useQuery`/`useMutation` resolve a single `fetch` per request, and the active span for an HTTP call is created inside `FetchInstrumentation.patchFetch()` immediately before the `fetch` call — async context is bounded by the instrumentation, not by user code. `StackContextManager` is the default in `sdk-trace-web` when no `contextManager:` is passed to `provider.register({})`.

### Why fetchBaseQuery + FetchInstrumentation coexist correctly

`fetchBaseQuery` in RTK 2.x is documented as "a lightweight fetch wrapper" that invokes `globalThis.fetch` (or a `fetchFn` you supply) ([redux-toolkit.js.org/rtk-query/api/fetchBaseQuery](https://redux-toolkit.js.org/rtk-query/api/fetchBaseQuery)). `@opentelemetry/instrumentation-fetch` patches `globalThis.fetch` exactly once on `enable()` (source: `experimental/packages/opentelemetry-instrumentation-fetch/src/fetch.ts`). Because RTK Query never holds a reference to the *original* unpatched fetch (it reads `globalThis.fetch` lazily on each call), the patch transparently applies. There is no double-wrap. Confirmed by multiple production reports including Honeycomb's "Configuring a React Application with Honeycomb for Frontend Observability" guide and the Elastic Observability Labs "Web Frontend Instrumentation" article.

The only sequencing constraint: **the instrumentation must be `enable()`d before the first render** (so before `createRoot(...).render(<App/>)` runs and triggers any auth-bootstrap query). The standard pattern is to import the instrumentation module as the **first** import in `src/main.tsx`, mirroring the pattern in `liteverge/liteverge-opentelemetry/examples/react-vite-app` ([github.com](https://github.com/liteverge/liteverge-opentelemetry/tree/main/examples/react-vite-app)).

## 2 · Deployment topology decision

### The CORS reality

RunCoach's `infra/otel/otel-collector-config.yaml` exposes `otlphttp` on `0.0.0.0:4318` with **no** `cors:` block. A browser at `http://localhost:5173` POSTing to `http://localhost:4318/v1/traces` will issue an `OPTIONS` preflight that the collector will reject (no `Access-Control-Allow-Origin` response header), and the trace POST will never fire. The OpenTelemetry docs are explicit:

> "If your website and collector are hosted at a different origin, your browser might block the requests going out to your collector. You need to configure special headers for Cross-Origin Resource Sharing (CORS). The OpenTelemetry Collector provides a feature for http-based receivers to add the required headers to allow the receiver to accept traces from a web browser." — [opentelemetry.io/docs/languages/js/exporters/](https://opentelemetry.io/docs/languages/js/exporters/)

### Three patterns

#### (a) Add `cors:` to the OTLP receiver — RECOMMENDED DEFAULT

Apply this patch to `infra/otel/otel-collector-config.yaml`:

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318
        cors:
          allowed_origins:
            - http://localhost:5173        # Vite dev
            - http://localhost:4173        # Vite preview
            # MVP-1 prod: add deployed origin(s) here, e.g.
            # - https://runcoach.example.com
          allowed_headers:
            - traceparent
            - tracestate
            - baggage
            - Content-Type
          max_age: 7200
```

**Sources for this exact schema:**
- `opentelemetry-collector/receiver/otlpreceiver/README.md`: documents `cors:` with `allowed_origins`, `allowed_headers`, `max_age` under `protocols.http`. ([github.com](https://github.com/open-telemetry/opentelemetry-collector/tree/main/receiver/otlpreceiver))
- `opentelemetry-collector/config/confighttp/README.md` warns: *"Do not use a plain wildcard `[\"*\"]`, as our CORS response includes `Access-Control-Allow-Credentials: true`, which makes browsers disallow a plain wildcard"*. Use `["http://*", "https://*"]` if you truly need wildcards, but **always prefer an explicit allow-list in prod**. ([github.com](https://github.com/open-telemetry/opentelemetry-collector/blob/main/config/confighttp/README.md))
- Verified example identical in shape: `vitest-dev/vitest@1ec3a8b` browser-mode setup adds the same `cors: allowed_origins: ["http://localhost:*"]` block.

**Pros:**
- One config change in one file.
- The collector remains the network boundary — auth headers, sampling, tail-based sampling, and PII redaction processors live in collector pipelines as designed.
- Latency: direct browser→collector POST, no extra hop.
- Matches upstream OpenTelemetry's own recommendation.

**Cons:**
- The collector port (4318) must be reachable from the user's network. In prod that means putting the collector behind a TLS-terminating reverse proxy at e.g. `https://otel.runcoach.example.com/v1/traces`, or behind the same load balancer fronting the API.
- Browser sends `Origin:` only; if you later need auth on the receiver, you'll add a static API-key header via `OTLPTraceExporter({headers: {…}})`. **Note that the API key is bundled in the JS** — accept this is "shipped-in-client" credentials and treat the receiver auth as rate-limiting, not as a secret.

#### (b) Reverse-proxy `/v1/traces` from Vite dev + ASP.NET Core in prod

Vite proxy snippet (already used at RunCoach for `/api`):
```ts
// vite.config.ts
server: {
  proxy: {
    '/v1/traces': {
      target: 'http://localhost:4318',
      changeOrigin: true,
    },
  },
},
```

ASP.NET Core "pass-through" minimal-api proxy:
```csharp
// Program.cs (sketch, prod only)
app.MapPost("/v1/traces", async (HttpRequest req, IHttpClientFactory f) =>
{
    using var c = f.CreateClient();
    using var content = new StreamContent(req.Body);
    content.Headers.ContentType = req.ContentType is { } ct
        ? new MediaTypeHeaderValue(ct) : null;
    var resp = await c.PostAsync("http://otel-collector:4318/v1/traces", content);
    return Results.StatusCode((int)resp.StatusCode);
});
```

**Pros:** Same-origin; no CORS. Browser only knows about `/v1/traces` on the API host.
**Cons:** Extra hop, extra Kestrel allocations per export batch, your backend becomes a critical path for telemetry export. Loses the "collector is the network boundary" property — the API process now sees raw client OTLP payloads. **Reject as default.**

#### (c) Browser POSTs to a backend endpoint that re-emits via the existing OTLP exporter

```csharp
app.MapPost("/api/v1/client-traces", async (
    OTel.Sdk.ITracerProvider tp, HttpRequest req) => { /* parse OTLP-JSON, replay spans */ });
```

This requires either (i) re-deserializing OTLP-JSON via `OtlpExporter` packages and `Activity.AddEvent` plumbing (non-trivial, ~150 LOC) or (ii) just trusting the JSON and forwarding bytes — which makes it equivalent to option (b) with extra work. **Reject** — gives nothing that (a) doesn't and forces backend changes that are explicitly out-of-scope for this slice.

### Production-deployment note for (a)

For MVP-1 public-tester rollout:
1. **Replace** `allowed_origins: [http://localhost:5173]` with the actual deployed origin(s). Never use `*`.
2. **Front the collector** with TLS (terminate at nginx/Caddy/Traefik in front of the collector container — the collector's own TLS support is fine but most teams prefer ingress termination).
3. Add a `headers: { Authorization: 'Bearer …' }` config to `OTLPTraceExporter` and validate it at the collector via the `bearertokenauth` extension. Treat the secret as a rate-limit token, not as confidential.
4. Add `Content-Security-Policy: connect-src 'self' https://otel.runcoach.example.com;` to the index.html CSP meta (per opentelemetry.io/docs/languages/js/exporters/).

## 3 · Propagation through RTK Query

### What `FetchInstrumentation` does to headers

Reading `experimental/packages/opentelemetry-instrumentation-fetch/src/fetch.ts` on `main`, the patched `fetch` calls the configured propagator's `inject(context.active(), headers, ...)` *after* user-set headers and *before* the actual network call. For **same-origin** requests it always injects (so RunCoach's `/api/...` calls — same origin as `/`, served via the existing Vite proxy at `:5173` and same-origin in prod — automatically get `traceparent`/`tracestate`/`baggage`). For **cross-origin** requests it injects only if the URL matches `propagateTraceHeaderCorsUrls`.

> "// urls which should include trace headers when origin doesn't match" — comment on `FetchInstrumentationConfig.propagateTraceHeaderCorsUrls` in the source.

Because RunCoach uses **same-origin** `/api/*` calls (the Vite dev server proxies to ASP.NET Core; in prod the SPA is served from the same origin as the API), `propagateTraceHeaderCorsUrls` can be left undefined — `traceparent` propagation just works. Tracetest confirms this in their 2024 "Propagating the OTel Context from Browser to Backend" article. If you ever split origins (e.g. `app.runcoach.com` + `api.runcoach.com`), add:

```ts
propagateTraceHeaderCorsUrls: [/^https:\/\/api\.runcoach\./],
```

### Recommended seam — frontend/src/app/api/otel.ts (new file)

```ts
// frontend/src/app/api/otel.ts
// Must be imported BEFORE React renders and BEFORE any RTK Query baseQuery is constructed.
import { trace, context } from '@opentelemetry/api';
import {
  WebTracerProvider,
  BatchSpanProcessor,
} from '@opentelemetry/sdk-trace-web';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch';
import { resourceFromAttributes } from '@opentelemetry/resources';
import {
  ATTR_SERVICE_NAME,
  ATTR_SERVICE_VERSION,
} from '@opentelemetry/semantic-conventions';
import { recordLastTraceId } from './last-trace-id';

const COLLECTOR_URL =
  import.meta.env.VITE_OTLP_TRACES_URL ?? 'http://localhost:4318/v1/traces';

const exporter = new OTLPTraceExporter({
  url: COLLECTOR_URL,
  // no `headers:` in dev; MVP-1 prod will add { Authorization: `Bearer ${KEY}` }
});

const provider = new WebTracerProvider({
  resource: resourceFromAttributes({
    [ATTR_SERVICE_NAME]: 'runcoach-frontend',
    [ATTR_SERVICE_VERSION]: import.meta.env.VITE_APP_VERSION ?? '0.0.0-dev',
  }),
  spanProcessors: [
    new BatchSpanProcessor(exporter, {
      scheduledDelayMillis: 5000,   // default 5s
      maxQueueSize: 2048,
      maxExportBatchSize: 64,
      exportTimeoutMillis: 30000,
    }),
  ],
});

provider.register(); // uses default StackContextManager + W3C TraceContext + Baggage

registerInstrumentations({
  instrumentations: [
    new FetchInstrumentation({
      // RunCoach uses same-origin /api/*; this is only needed if origins split later.
      // propagateTraceHeaderCorsUrls: [/^https:\/\/api\./],
      ignoreUrls: [/\/v1\/traces$/], // don't trace the trace exporter itself
      clearTimingResources: true,
      applyCustomAttributesOnSpan: (span, request, _response) => {
        // (a) record trace id for the error boundary
        recordLastTraceId(span.spanContext().traceId);
        // (b) strip query string from http.url to avoid PII (DEC-059 alignment)
        const urlStr = typeof request === 'string'
          ? request
          : (request as Request).url;
        try {
          const u = new URL(urlStr, window.location.origin);
          span.setAttribute('http.url', `${u.origin}${u.pathname}`);
          span.setAttribute('http.target', u.pathname);
        } catch { /* leave as-is */ }
      },
    }),
  ],
});

export const tracer = trace.getTracer('runcoach-frontend');
export { context };
```

### Update `frontend/src/main.tsx` — first import wins

```ts
import './app/api/otel';  // ← MUST be first, before React/RTK
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './app/modules/app/app.component';

createRoot(document.getElementById('root')!).render(
  <StrictMode><App /></StrictMode>,
);
```

### `prepareHeaders` is left untouched

Because `FetchInstrumentation` injects after user headers, the existing `prepareHeaders` (X-XSRF-TOKEN on mutations) needs **zero changes**. The RTK Query baseQuery file (`frontend/src/app/api/base-query.ts`) stays as-is. This is the lowest-risk seam: there is no `fetchFn` override, no wrap of `fetchBaseQuery`, and no replacement of the API slice.

### React 18 StrictMode double-invocation

React 18+ StrictMode invokes effects twice in dev to surface side-effects. The OTel setup runs at module load (top-level), not in a `useEffect`, so it is **not** affected. Multiple imports of `./app/api/otel` from different chunks are also safe — ES modules are evaluated once.

### Order of init vs createRoot — explicit ordering rules

1. `otel.ts` module body runs (registers `WebTracerProvider`, patches global `fetch`).
2. `main.tsx` continues, imports React, RTK store, `App`.
3. `createRoot(...).render(<App/>)` — first render triggers `useGetMe` or similar bootstrap query.
4. RTK Query reads `globalThis.fetch` → now patched → emits a span → batches.
5. After 5 s, `BatchSpanProcessor` calls `exporter.export()` → POST `/v1/traces`.

If you reverse steps 1 and 2 (e.g. by importing `otel.ts` inside a `useEffect`), the bootstrap query in step 4 misses the patch, you'll lose the first trace, and the OpenTelemetry docs warn: *"If you fail to initialize the SDK or initialize it too late, no-op implementations will be provided to any library which acquires a tracer or meter from the API."*

## 4 · Backend chaining verification

### Default propagators line up exactly

Both stacks default to the W3C TraceContext + W3C Baggage composite. Confirmed:

- **Browser:** OpenTelemetry-JS exposes `CompositePropagator({ propagators: [new W3CTraceContextPropagator(), new W3CBaggagePropagator()] })` as the default registered by `WebTracerProvider.register()` when no `propagator:` override is passed. (See uptrace.dev "OpenTelemetry Trace Context Propagation [JavaScript]" and the OTel JS source.)
- **.NET:** From the official `opentelemetry-dotnet` source / DeepWiki: *"By default, OpenTelemetry .NET SDK configures a `CompositeTextMapPropagator` that includes: W3C Trace Context Propagator (`TraceContextPropagator`) … Baggage Propagator (`BaggagePropagator`)."* RunCoach has no `Sdk.SetDefaultTextMapPropagator(...)` call — defaults apply.

### Chain mechanics

`AddAspNetCoreInstrumentation()` (from `OpenTelemetry.Instrumentation.AspNetCore`) subscribes to `Microsoft.AspNetCore.Hosting` diagnostic events. The ASP.NET Core 8/9 hosting layer itself parses the incoming `traceparent` header and creates a server `Activity` with the extracted `parentId` — **independent of OTel**. AspNetCoreInstrumentation simply observes that activity. Therefore:

> "ASP.NET Core automatically extracts traceparent headers from incoming requests when OpenTelemetry is configured." — uptrace.dev "OpenTelemetry Context Propagation [.NET]"

> "When the default propagator is just w3c we respect the work AspNetCore does instead of calling the SDK logic." — code comment in `OpenTelemetry.Instrumentation.AspNetCore/Implementation/HttpInListener.cs` (linked from open-telemetry/opentelemetry-dotnet#4214)

The chain looks like:

```
browser fetch span (client kind)
  └─ http.method=GET, http.target=/api/plan, traceparent injected
        ↓
  ASP.NET Core auto-span (server kind, parented by traceparent)
        └─ Marten ActivitySource span
              └─ Npgsql ActivitySource span
        └─ Wolverine ActivitySource span
        └─ RunCoach.Llm ActivitySource span (PlanGenerationService)
```

All emit under one `trace_id`. Verified in Jaeger UI by searching the displayed trace-id.

### Things that would silently break the chain

| Risk | Likely in RunCoach? | Mitigation |
| --- | --- | --- |
| Backend sampling decision overrides client decision and drops the trace | No — backend uses default `AlwaysOn`. | If a sampler is added later, use `ParentBasedSampler` so the parent's `sampled` flag wins. |
| `Sdk.SetDefaultTextMapPropagator(new B3Propagator())` somewhere | No — confirmed not present. | Avoid configuring custom propagators unless you also change the browser side. |
| CORS preflight strips `traceparent` from `Access-Control-Allow-Headers` on cross-origin variant | Not relevant for same-origin RunCoach. If you go cross-origin, your ASP.NET Core CORS policy must include `WithHeaders("traceparent", "tracestate", "baggage")`. | Documented in [oneuptime.com](https://oneuptime.com/blog/post/2026-02-06-trace-cross-origin-api-requests-opentelemetry/view): *"If the server's CORS response does not include `traceparent` in the `Access-Control-Allow-Headers` list, the browser will not send it on the actual request."* |
| Baggage exceeds size limits | W3C Baggage limit is 8 192 bytes total. RunCoach has no plans to populate baggage. | Don't put user-typed answers in baggage. |
| Wolverine/Marten/Npgsql ActivitySource not registered with backend OTel | No — all four are in `AddSource(...)` per `Program.cs:260-298`. | n/a |

### Smoke test (curl + Jaeger UI)

```bash
# 1. Pick a known browser-shaped traceparent.
TRACE=4bf92f3577b34da6a3ce929d0e0e4736
SPAN=00f067aa0ba902b7
curl -i http://localhost:5000/api/plan \
  -H "traceparent: 00-${TRACE}-${SPAN}-01" \
  -H "tracestate:" \
  -H "Cookie: __Host-Session=...; __Host-Xsrf-Request=..." \
  -H "X-XSRF-TOKEN: ..."

# 2. Open Jaeger UI:
#    http://localhost:16686/trace/${TRACE}
#    → Expect a single trace with: 
#         (no client span — that's the curl)
#         + ASP.NET Core span ("GET /api/plan" or similar)
#         + Marten + Wolverine + Npgsql + RunCoach.Llm spans nested
```

Once the browser is wired, the same trace-id appears under both the `runcoach-frontend` service span (with the fetch client span) and the `runcoach-api` server span. **A single trace, both services, no manual stitching.**

## 5 · Correlation-ID surfacing for the error boundary

### UX format decision

**Show trace-id only (32 lowercase hex chars), grouped 8-8-8-8 for human eyes, copy-button next to it.**

Rationale:
- The full traceparent (`00-{trace}-{span}-{flags}`) is 55 characters and includes a span-id that is **only meaningful for the most recent fetch** — it would change if the user hits "retry" and could mislead support. The trace-id is the stable identifier in Jaeger UI (`/trace/{trace-id}` is the Jaeger URL pattern).
- W3C trace-context: *"This is the ID of the whole trace forest and is used to uniquely identify a distributed trace through a system."* ([w3.org/TR/trace-context](https://www.w3.org/TR/trace-context/))
- 32 hex chars fits comfortably on a mobile-width fallback card; the example in the prompt (`4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01`) was *illustrative* — the canonical UX is shorter.

Display: monospace font, `select-all`, copy button, and a hint text:

> Show this code to support: `4bf92f3577b34da6a-3ce929d0e0e4736` — [Copy]

### The last-seen-trace-id seam

The error boundary renders **after** an exception is thrown — at which point `context.active()` may already be empty. So we cannot rely on `trace.getActiveSpan()?.spanContext().traceId` at render time. We need to **stash** the last-seen trace-id as fetches happen.

**Pick: module-level singleton with React hook.** Rejected alternatives:
- Redux slice — would re-render every consumer on every fetch (every 100–1000 ms during active usage). Wasted work.
- `useContext` provider — same re-render problem unless we wrap it in `useSyncExternalStore`, at which point it's identical to a module singleton + hook.
- `localStorage` — synchronous I/O on the main thread, persists across tabs, no cleanup.
- `ref` — can't be read from a class-component error boundary's render without prop drilling.

```ts
// frontend/src/app/api/last-trace-id.ts
type Listener = () => void;
let current: string | null = null;
const listeners = new Set<Listener>();

export function recordLastTraceId(traceId: string): void {
  if (traceId === current) return;
  current = traceId;
  listeners.forEach((l) => l());
}

export function getLastTraceId(): string | null {
  return current;
}

export function subscribe(l: Listener): () => void {
  listeners.add(l);
  return () => { listeners.delete(l); };
}

// React hook (function components)
import { useSyncExternalStore } from 'react';
export function useLastTraceId(): string | null {
  return useSyncExternalStore(subscribe, getLastTraceId, () => null);
}
```

### Error-boundary integration (R-073)

Whatever R-073 picks (`react-error-boundary` from bvaughn or a hand-rolled class), the fallback reads via `useLastTraceId` if functional, or via `getLastTraceId()` directly if a class component:

```tsx
// frontend/src/app/modules/errors/fallback-card.tsx
import { getLastTraceId } from '@/app/api/last-trace-id';

export function FallbackCard({ error }: { error: Error }) {
  const traceId = getLastTraceId();
  const display = traceId
    ? traceId.match(/.{1,8}/g)!.join('-')
    : null;

  return (
    <div role="alert" className="...">
      <h2>Something went wrong</h2>
      <p>We've recorded the problem and our team will take a look.</p>
      {display && (
        <div className="font-mono text-sm select-all flex items-center gap-2">
          <span>Support code:</span>
          <code>{display}</code>
          <button
            type="button"
            onClick={() => navigator.clipboard.writeText(traceId!)}
            aria-label="Copy support code"
          >
            Copy
          </button>
        </div>
      )}
    </div>
  );
}
```

### Filling the singleton

Already wired in §3's `otel.ts`: every patched fetch fires `applyCustomAttributesOnSpan` which calls `recordLastTraceId(span.spanContext().traceId)`. That covers ~99 % of cases (the error boundary fires *after* a render that read data from a fetch).

For purely synchronous render errors with no preceding fetch (e.g. a typo in an early bootstrap), `getLastTraceId()` returns `null` and the fallback hides the support-code block entirely. That is the correct UX — there's nothing to look up.

## 6 · Bundle-size budget

### Numbers from the literature

Two independent 2026 sources analyze the OTel-JS browser bundle:

- **SigNoz, "Reducing OpenTelemetry Bundle Size in Browser Frontend":** *"the official browser auto-instrumentation bundle was about 300 KB uncompressed [~60 KB gzipped] after recent optimisations."* That number is for `@opentelemetry/auto-instrumentations-web` (all instrumentations enabled). The same article notes SDK 2.0 (released 2025) "explicitly removed certain patterns (like extensive classes or namespaces) to improve tree-shakability and minification."
- **OneUptime, "How to Reduce OpenTelemetry Browser SDK Bundle Size with Tree Shaking":** *"If you import all of these without tree shaking, you are looking at over 300KB before gzip. With proper tree shaking and the right choices, you can cut this by more than half."* Lists a "minimal" configuration estimated at **~25 KB gzipped**, comprising: `sdk-trace-web` + `sdk-trace-base` + `exporter-trace-otlp-http` + `instrumentation-fetch` + `api` — i.e. exactly the RunCoach default set.

### Conservative estimate for the default

| Package | Approx min+gz contribution (estimate) |
| --- | --- |
| `@opentelemetry/api` | ~3 KB |
| `@opentelemetry/sdk-trace-web` + `sdk-trace-base` | ~15 KB |
| `@opentelemetry/exporter-trace-otlp-http` + transformer | ~8 KB |
| `@opentelemetry/instrumentation` + `instrumentation-fetch` | ~5 KB |
| `@opentelemetry/resources` + `semantic-conventions` (incubating subset tree-shaken) | ~3 KB |
| `@opentelemetry/core` (composite propagators, W3C TraceContext + Baggage) | ~3 KB |
| **Total estimated delta on top of current React 19 + RTK + Router + Tailwind bundle** | **~30–45 KB gz** |

This is consistent with the 25 KB "minimal" floor from OneUptime once you add a `propagator` and `resources` semantic-conventions imports that real-world code touches.

### Vite 8 / Rollup 4 chunking advice

Vite 8 inherits Rollup 4's tree-shaker, which honours `"sideEffects": false` in `package.json`. The OTel-JS 2.x packages declare this. Run `npx vite build && npx vite-bundle-visualizer` (or the `rollup-plugin-visualizer` plugin) to confirm.

Recommend an explicit `manualChunks` to keep OTel in its own long-cached chunk (since it changes far less often than app code):

```ts
// vite.config.ts (addition)
export default defineConfig({
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          otel: [
            '@opentelemetry/api',
            '@opentelemetry/sdk-trace-web',
            '@opentelemetry/sdk-trace-base',
            '@opentelemetry/exporter-trace-otlp-http',
            '@opentelemetry/instrumentation',
            '@opentelemetry/instrumentation-fetch',
            '@opentelemetry/resources',
            '@opentelemetry/semantic-conventions',
            '@opentelemetry/core',
          ],
        },
      },
    },
    chunkSizeWarningLimit: 100,
  },
});
```

### Known Vite gotcha: Node built-ins in `@opentelemetry/instrumentation`

[open-telemetry/opentelemetry-js-contrib#1892](https://github.com/open-telemetry/opentelemetry-js-contrib/issues/1892) reports that `@opentelemetry/instrumentation` imports `path.normalize` for Node-only `InstrumentationNodeModuleFile`. Vite externalises `path` for browser builds, producing a warning *"Module 'path' has been externalized for browser compatibility"*. With OTel-JS 2.x and `instrumentation` ≥ 0.20x this is tree-shaken away in browser builds because the Node-only modules are behind conditional imports. **Verify on first build.** If the warning still appears in `vite build`, add to `vite.config.ts`:

```ts
optimizeDeps: { exclude: ['@opentelemetry/instrumentation'] },
build: { rollupOptions: { external: ['node:path', 'path'] } },
```

### Fallback (~1.5 KB gz)

```ts
// frontend/src/app/api/lite-traceparent.ts (≈40 LOC, no deps)
const hex = (n: number) => n.toString(16).padStart(2, '0');
const rand = (n: number) =>
  Array.from(crypto.getRandomValues(new Uint8Array(n)), hex).join('');

let currentTraceId: string | null = null;
export const getLastTraceId = () => currentTraceId;

export function mintTraceparent(): { traceparent: string; traceId: string } {
  const traceId = rand(16);     // 32 hex
  const spanId  = rand(8);      // 16 hex
  currentTraceId = traceId;
  return { traceparent: `00-${traceId}-${spanId}-01`, traceId };
}

// usage in fetchBaseQuery:
// prepareHeaders: (headers, { type }) => {
//   if (type === 'mutation') headers.set('X-XSRF-TOKEN', readXsrf());
//   headers.set('traceparent', mintTraceparent().traceparent);
//   return headers;
// }
```

To also emit a browser span (so Jaeger shows a client span, not just the server span), POST a minimal OTLP/JSON payload to `:4318/v1/traces` with `keepalive: true` on every Nth fetch. ~50 extra LOC. Documented in the OneUptime "Reduce Bundle Size" article as the "LightweightExporter" pattern.

**Choose the fallback only if** the production gzipped bundle exceeds an agreed budget (commonly cited threshold for a SPA "first JS payload" is ~150–170 KB gz total; if React 19 + RTK + Router + Tailwind already consumes ≥125 KB gz, the +30–45 KB OTel default may breach budget).

## 7 · Sampling, PII, and attribute scrubbing

### What the SDK auto-records that could leak PII

`FetchInstrumentation` v0.20x (stable HTTP semconv selected via `semconvStabilityOptIn`, or the v1.7.0 fallback) auto-sets on each fetch span:

- `http.method` / `http.request.method`
- `http.url` — **the full URL including query string** ← #1 PII risk
- `http.host`, `http.scheme`, `http.target` ← #2 risk via path params
- `http.status_code`, `http.response_content_length`
- `http.user_agent`

It does **not** set request/response bodies (that would be `instrumentation-undici` style enrichment which doesn't apply to browser fetch).

For RunCoach, the path templates are well-known:
- `/api/auth/login`, `/api/auth/logout` — no PII in path/query
- `/api/onboarding/{step}` — step ids are non-PII
- `/api/plan` — no PII in URL

So URL leakage is bounded. But query strings *could* leak if any client code appends `?email=...` etc. **Don't rely on developer discipline; scrub at the SDK layer.**

### The 2026 canonical client-side scrub pattern

Two equally valid hooks:

#### Option A — `applyCustomAttributesOnSpan` on `FetchInstrumentation` (already used in §3)

```ts
new FetchInstrumentation({
  applyCustomAttributesOnSpan: (span, request, _response) => {
    recordLastTraceId(span.spanContext().traceId);
    const urlStr = typeof request === 'string' ? request : (request as Request).url;
    try {
      const u = new URL(urlStr, window.location.origin);
      // Replace http.url with origin+pathname only (no query/fragment).
      span.setAttribute('http.url', `${u.origin}${u.pathname}`);
      span.setAttribute('url.full', `${u.origin}${u.pathname}`); // stable semconv
      span.setAttribute('http.target', u.pathname);
      span.setAttribute('url.path', u.pathname);
    } catch { /* ignore */ }
  },
})
```

This is the **lighter** option — works directly on `FetchInstrumentation` attributes only, doesn't touch other instrumentations.

#### Option B — Custom `SpanProcessor.onStart`/`onEnd` (defence-in-depth)

```ts
import type { Context } from '@opentelemetry/api';
import type { ReadableSpan, Span, SpanProcessor } from '@opentelemetry/sdk-trace-base';

class PiiScrubbingSpanProcessor implements SpanProcessor {
  private readonly EMAIL = /[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/g;
  private readonly UUID  = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;

  onStart(span: Span, _ctx: Context): void {
    // attribute mutation only valid before span.end() — onStart is the safe hook.
    const url = span.attributes['http.url'];
    if (typeof url === 'string') {
      const scrubbed = url
        .replace(this.EMAIL, '[REDACTED_EMAIL]')
        .replace(this.UUID,  '[REDACTED_UUID]')
        .split('?')[0]; // strip query
      span.setAttribute('http.url', scrubbed);
    }
  }
  onEnd(_: ReadableSpan): void {}
  shutdown(): Promise<void> { return Promise.resolve(); }
  forceFlush(): Promise<void> { return Promise.resolve(); }
}

// wire as the FIRST processor so it runs before BatchSpanProcessor reads attributes
new WebTracerProvider({
  spanProcessors: [new PiiScrubbingSpanProcessor(), new BatchSpanProcessor(exporter, {...})],
});
```

### Recommendation

Use **Option A by default** because it's narrower and easier to reason about, and add **Option B at MVP-1** (when public testers join) as defence-in-depth. The OpenTelemetry "Handling sensitive data" doc explicitly endorses both: *"The best way to prevent the collection of sensitive data is not to collect data that might be sensitive."* ([opentelemetry.io/docs/security/handling-sensitive-data/](https://opentelemetry.io/docs/security/handling-sensitive-data/))

### Don't forget collector-side redaction as belt-and-braces

Already worth adding now (cheap, low risk):

```yaml
# infra/otel/otel-collector-config.yaml — additions
processors:
  attributes/scrub:
    actions:
      - key: http.url
        action: hash      # or 'delete' / 'update' / regex pattern
      - key: user.email
        action: delete
      - key: http.user_agent
        action: hash      # hashing UA preserves cardinality but loses identifiability

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [attributes/scrub, batch]
      exporters: [otlp/jaeger, debug]
```

The collector's `attributesprocessor` is the canonical place to **enforce** scrub rules (a developer can't accidentally bypass it from the browser). See [github.com/open-telemetry/opentelemetry-collector/blob/main/processor/attributesprocessor/README.md](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/processor/attributesprocessor) (in collector-contrib).

### What we explicitly do NOT instrument

- **User-typed answers** to onboarding (running history, injuries, schedule) — never created as span attributes by the SDK because they live in request *bodies*, not URLs or headers.
- **Auth cookies** — `__Host-Session`, `__Host-Xsrf-Request` are not captured by `instrumentation-fetch` (it does not record `Cookie` or `Set-Cookie` headers in the browser SDK).
- **Bearer tokens / API keys** — none in RunCoach today (cookie-auth only); future API-key auth must add explicit scrubs.

### Confirmation the LayeredPromptSanitizer is *not* sufficient for client traces

LayeredPromptSanitizer (DEC-059) runs server-side, after the request hits the API, and protects the **LLM prompt**. It does not see browser-side span attributes. Hence the need for the client-side scrub layer above.

## 8 · Privacy / GDPR posture

### MVP-0 (personal use, no public users)

**Posture:** Always-on. Localhost-only. No consent UI. No DNT check.

Rationale: there is exactly one user (the developer), and traces never leave the dev machine. There is no GDPR data subject other than the operator. This matches OpenTelemetry's own [security guidance](https://opentelemetry.io/docs/security/handling-sensitive-data/): *"Only collect data that serves an observability purpose. Avoid collecting personal information unless absolutely necessary."*

### MVP-1 (public-tester cohort)

**Posture:** **Opt-in via cookie-consent banner.** Default off. No tracing emitted before consent.

Implementation seam:
```ts
// frontend/src/app/api/otel.ts (MVP-1 version)
if (cookieConsentGranted('analytics')) {
  await import('./otel-bootstrap'); // lazy import the SDK on consent
}
```

This pattern is documented in OneUptime "How to Build a Telemetry Data Governance Framework with OpenTelemetry":

> "Only initialize telemetry if the user has consented to analytics… User has not consented. Use a no-op provider."

### Why not "always-on with PII scrubbing"?

Three legal-philosophical reasons:
1. Trace-id itself is a pseudonymous identifier created without consent. Even with URL scrubbing, the sequence of timestamps and pathnames per session reveals usage patterns that GDPR Recital 30 / Art 4(1) consider personal data when combinable with other signals.
2. The `runcoach-frontend` Resource attributes (`service.instance.id`, user-agent, IP at the collector ingress) make trace data identifiable.
3. The collector logs each batch (in dev `debug` exporter currently active per `otel-collector-config.yaml`). Logs persist.

### Why not "defer-to-DNT"?

Do Not Track is effectively deprecated by major browsers; Safari removed it 2019, Chrome's `Sec-GPC` has limited adoption. It is not a reliable consent signal in 2026.

### Cookie-banner integration sketch

```tsx
// MVP-1 consent state in Redux 'consent' slice
const consent = useAppSelector(s => s.consent.analytics);

useEffect(() => {
  if (consent === 'granted') {
    import('@/app/api/otel-bootstrap'); // side-effect: registers provider
  }
}, [consent]);
```

The bundle is **lazy-loaded** on consent, so users who decline pay 0 KB of OTel JS.

### Documentation to add

- `docs/legal/privacy.md` — disclose: trace-id (pseudonym), URL pathnames, HTTP status, duration, user-agent. State retention period (Jaeger default 24h, configurable).
- DEC entry covering this section verbatim. Reference DEC-059 for backend sanitisation alignment.

## 9 · Failure modes

### Default behaviours

| Scenario | What the SDK does | What the user sees |
| --- | --- | --- |
| Collector down (connection refused on `:4318`) | `OTLPTraceExporter` retries with exponential backoff per OTLP spec. Per the npm `@opentelemetry/exporter-trace-otlp-http` README: *"DEFAULT_EXPORT_MAX_ATTEMPTS: The maximum number of attempts, including the original request. Defaults to 5. DEFAULT_EXPORT_INITIAL_BACKOFF: The initial backoff duration."* On final failure, `BatchSpanProcessor` drops the batch and logs to `diag` channel only. | Nothing. Page renders normally. |
| Collector returns 4xx/5xx | Same retry-then-drop. | Nothing. |
| Browser offline | Same — `fetch` rejects with `TypeError: Failed to fetch`, retry, drop. `keepalive` POSTs queue inside the browser briefly. | Nothing. |
| Tab backgrounded mid-batch | `BatchSpanProcessor` keeps running. On `visibilitychange === 'hidden'`, no automatic flush is triggered by the default web SDK (this is a known gap; see issue #3489 referencing sendBeacon limits). | Up to `scheduledDelayMillis` of buffered spans may be lost on tab close. Recommend adding a manual flush hook (below). |
| Tab closed | `BatchSpanProcessor.forceFlush()` is **not** automatically wired to `pagehide`. The OTLPTraceExporter uses `fetch(..., {keepalive: true})` in modern browsers; if you don't flush, spans buffered in the queue (not yet sent) are lost. | Some spans may be lost — invisible to the user; acceptable for dev/MVP-0. |
| Slow connection | OTLP POSTs continue in background. Default `exportTimeoutMillis: 30_000`. No blocking. | Nothing. |
| CSP blocks `connect-src` to collector | `fetch` rejects, retry-then-drop. Console error visible in DevTools. | Nothing. |

### Knobs we recommend

```ts
new BatchSpanProcessor(exporter, {
  maxQueueSize: 2048,        // span buffer; drops oldest after this
  maxExportBatchSize: 64,    // per-POST batch size
  scheduledDelayMillis: 5_000,
  exportTimeoutMillis: 30_000,
})
```

### Optional manual flush on page-hide

```ts
// In otel.ts after provider.register()
document.addEventListener('visibilitychange', () => {
  if (document.visibilityState === 'hidden') {
    provider.forceFlush().catch(() => { /* swallow */ });
  }
});
```

MDN strongly recommends `visibilitychange` over `pagehide`/`unload` for analytics-style flushes. ([developer.mozilla.org/en-US/docs/Web/API/Navigator/sendBeacon](https://developer.mozilla.org/en-US/docs/Web/API/Navigator/sendBeacon)).

### "Should be invisible (silent drop) — confirm"

**Confirmed.** The default OTLP-HTTP exporter logs only via `diag` (set to `INFO` and above by default, no `console.error` cascades). There is no UI surface for failed telemetry export. The user's experience is identical whether traces succeed or fail.

## 10 · Existing precedents and primary sources

### Open-source React + Vite + OTel-web reference repos

| Repo | Notes |
| --- | --- |
| [`nsalexamy/service-foundry` "Implementing End-to-End Observability in React Applications with OpenTelemetry"](https://nsalexamy.github.io/service-foundry/pages/documents/o11y-foundry/o11y-in-react/) | React + TypeScript + Vite (`npm create vite@latest react-o11y-app --template react-ts`), Spring backend, full OTLP-HTTP-JSON setup, includes the exact CORS block for the collector that matches our recommendation. Most-aligned reference. |
| [`liteverge/liteverge-opentelemetry/examples/react-vite-app`](https://github.com/liteverge/liteverge-opentelemetry/tree/main/examples/react-vite-app) | Minimal React + Vite + TypeScript example: *"src/main.tsx imports instrumentation as its first import, before React renders."* Exactly the seam pattern we recommend in §3. |
| [`obs-nebula/frontend-react`](https://github.com/obs-nebula/frontend-react) | Older (CRA + React, not Vite) but demonstrates the OTel collector → Jaeger chain end-to-end with `instrumentation-fetch`. Red Hat Developer wrote it up at [developers.redhat.com/articles/2023/03/22/how-enable-opentelemetry-traces-react-applications](https://developers.redhat.com/articles/2023/03/22/how-enable-opentelemetry-traces-react-applications). |
| [`vitest-dev/vitest@1ec3a8b`](https://github.com/vitest-dev/vitest/commit/1ec3a8b68) | The "feat: support openTelemetry for browser mode" commit — shows the canonical Vitest browser-mode wiring including the same `cors: allowed_origins: ["http://localhost:*"]` block we recommend. |
| [`pkanal/otel-react-example`](https://github.com/pkanal/otel-react-example) | Small example app set up for tracing with OpenTelemetry; useful for diffing minimal setup. |

We did not find a public 2026 OSS repo combining **React 19 + Vite 8 + RTK Query + OTel-web** specifically; RTK Query × `FetchInstrumentation` coexistence is documented only in vendor blogs (Honeycomb, SigNoz) and confirmed at the source-code level by reading `instrumentation-fetch/src/fetch.ts` and RTK's `fetchBaseQuery` source. The patterns above (Liteverge, service-foundry) cover the React-19/Vite/OTel half cleanly; the RTK Query half is invariant under the SDK because `fetchBaseQuery` is a thin `globalThis.fetch` wrapper.

### Other primary sources cited

- OpenTelemetry-JS source: [github.com/open-telemetry/opentelemetry-js](https://github.com/open-telemetry/opentelemetry-js) (esp. `experimental/packages/opentelemetry-instrumentation-fetch`)
- Collector receiver docs: [github.com/open-telemetry/opentelemetry-collector/tree/main/receiver/otlpreceiver](https://github.com/open-telemetry/opentelemetry-collector/tree/main/receiver/otlpreceiver) — the `cors:` block is documented under "Receiver Configuration" with the exact YAML shape we use.
- Collector confighttp docs: [github.com/open-telemetry/opentelemetry-collector/blob/main/config/confighttp/README.md](https://github.com/open-telemetry/opentelemetry-collector/blob/main/config/confighttp/README.md) — warns against `["*"]` with `Access-Control-Allow-Credentials: true`.
- W3C Trace Context Level 1: [w3.org/TR/trace-context/](https://www.w3.org/TR/trace-context/) — defines `version-format = trace-id "-" parent-id "-" trace-flags`, trace-id as 32 lowercase hex.
- W3C Trace Context Level 2: [w3.org/TR/trace-context-2/](https://www.w3.org/TR/trace-context-2/) — Candidate Recommendation; adds `random-trace-id` flag. Backward-compatible.
- OpenTelemetry-JS SDK 2.0 announcement: [opentelemetry.io/blog/2025/otel-js-sdk-2-0/](https://opentelemetry.io/blog/2025/otel-js-sdk-2-0/) — *"Optimization: removing classes and namespaces to allow better minification and tree-shaking."*
- `opentelemetry-dotnet` propagation: [deepwiki.com/open-telemetry/opentelemetry-dotnet/8-context-propagation](https://deepwiki.com/open-telemetry/opentelemetry-dotnet/8-context-propagation) — confirms default `CompositeTextMapPropagator` = W3C TraceContext + Baggage.
- ASP.NET Core Instrumentation README: [github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md) — confirms auto-extraction of traceparent on incoming requests.
- Honeycomb React guide: [honeycomb.io/blog/configuring-react-application-honeycomb-for-frontend-observability](https://www.honeycomb.io/blog/configuring-react-application-honeycomb-for-frontend-observability)
- SigNoz frontend monitoring with OTel: [signoz.io/docs/frontend-monitoring/sending-traces-with-opentelemetry/](https://signoz.io/docs/frontend-monitoring/sending-traces-with-opentelemetry/)
- Tracetest's "Propagating the OTel Context from Browser to Backend" (covers cross-origin and user-interaction gotchas): [tracetest.io/blog/propagating-the-opentelemetry-context-from-the-browser-to-the-backend](https://tracetest.io/blog/propagating-the-opentelemetry-context-from-the-browser-to-the-backend)
- Elastic Observability Labs "Web Frontend Instrumentation": [elastic.co/observability-labs/blog/web-frontend-instrumentation-with-opentelemetry](https://www.elastic.co/observability-labs/blog/web-frontend-instrumentation-with-opentelemetry)
- Sentry browser × OTel status: [github.com/getsentry/sentry-javascript/discussions/7364](https://github.com/getsentry/sentry-javascript/discussions/7364) — confirms "no OTel browser support" stance.
- `@vercel/otel` scope clarification: [vercel.com/docs/tracing](https://vercel.com/docs/tracing) + checklyhq.com guide — *"No traces or metrics are recorded for any browser-side interactions."*

## 11 · Migration cost from current state

### New dependencies in `frontend/package.json`

```jsonc
{
  "dependencies": {
    "@opentelemetry/api": "^1.9.0",
    "@opentelemetry/core": "^2.7.1",
    "@opentelemetry/exporter-trace-otlp-http": "^0.207.0",
    "@opentelemetry/instrumentation": "^0.207.0",
    "@opentelemetry/instrumentation-fetch": "^0.207.0",
    "@opentelemetry/resources": "^2.7.1",
    "@opentelemetry/sdk-trace-base": "^2.7.1",
    "@opentelemetry/sdk-trace-web": "^2.7.1",
    "@opentelemetry/semantic-conventions": "^1.37.0"
  }
}
```

> **Version note:** the experimental packages (`exporter-trace-otlp-http`, `instrumentation`, `instrumentation-fetch`) version as `0.{2xx}.0` per the OpenTelemetry-JS release cadence (the SDK 2.0 announcement explained: stable packages release as 2.x, experimental as 0.20x+). Pin to the highest `0.20x` matching your `@opentelemetry/api ^1.9` — use `npm view @opentelemetry/instrumentation-fetch versions` to confirm the latest at install time. The 0.20x family is locked to the 2.x stable family by the OpenTelemetry-JS version-compatibility matrix.

### Files touched

| File | Change | LOC |
| --- | --- | --- |
| `frontend/package.json` | add 9 deps | +9 |
| `frontend/src/app/api/otel.ts` | NEW — SDK bootstrap (§3) | ~70 |
| `frontend/src/app/api/last-trace-id.ts` | NEW — module singleton + hook (§5) | ~25 |
| `frontend/src/main.tsx` | add `import './app/api/otel'` at top | +1 |
| `frontend/vite.config.ts` | add `manualChunks: { otel: [...] }` (optional) | +10 |
| `frontend/src/app/modules/errors/fallback-card.tsx` | NEW or modified by R-073 work — call `useLastTraceId()` / `getLastTraceId()` | ~20 (in R-073's diff) |
| `infra/otel/otel-collector-config.yaml` | add `cors:` block + (optional) `attributes/scrub` processor | +8 |
| `frontend/.env.development` | add `VITE_OTLP_TRACES_URL=http://localhost:4318/v1/traces`, `VITE_APP_VERSION=0.1.0-dev` | +2 |

**No changes** to: `frontend/src/app/api/base-query.ts`, `frontend/src/app/api/api-slice.ts`, any `*.api.ts` module, the store, the auth slice, the router, the existing 401 handler. **No changes** to backend ASP.NET Core code: `Program.cs:260-298` already wires the correct propagators by default.

### Effort estimate

~2–3 hours for an engineer familiar with the codebase. Most of the work is verifying the trace chain end-to-end (browser fetch → ASP.NET Core span → Marten/Wolverine/Npgsql nested spans visible in Jaeger UI under one trace-id) and confirming the bundle delta on a clean `vite build`.

### Verification checklist

1. `docker compose -f docker-compose.otel.yml up` brings collector + Jaeger up.
2. `cd frontend && npm install && npm run dev` runs without TypeScript errors (strict mode).
3. DevTools → Network: a request to `/api/...` shows a `traceparent: 00-…-…-01` header (extract trace-id).
4. DevTools → Network: a request to `:4318/v1/traces` shows a `200 OK` response (CORS preflight `OPTIONS` succeeds first).
5. Open `http://localhost:16686/trace/{trace-id}` — see browser client span + ASP.NET Core server span + nested backend spans under one trace tree.
6. Trigger a render error: error-boundary fallback shows the trace-id formatted `xxxxxxxx-xxxxxxxx-xxxxxxxx-xxxxxxxx`; copy button works.
7. `vite build` succeeds; `npx rollup-plugin-visualizer` (or `vite-bundle-visualizer`) shows the `otel` chunk weighing in around 30–45 KB gz.
8. Stop the collector container; refresh the SPA; verify no user-visible errors and DevTools console shows only diag `WARN` lines about export failure.

### Rollback plan

Remove the `import './app/api/otel'` line from `main.tsx`. Done. The SDK does nothing if not imported, and `fetchBaseQuery` reverts to the unpatched global `fetch`. No data is lost; no schema is migrated.

## Recommendations (staged, with thresholds)

**Now (MVP-0, this slice):**
1. Add `cors:` to `infra/otel/otel-collector-config.yaml` (8 lines).
2. Add 9 OTel deps to `frontend/package.json` and create `frontend/src/app/api/otel.ts` + `last-trace-id.ts` per §3 and §5.
3. Add the import line at the top of `main.tsx`.
4. Smoke-test via §11 verification checklist. Expect 30–45 KB gz bundle delta.
5. Leave sampling at `AlwaysOn` (head-based 100%).

**Threshold to revisit (move to fallback):** bundle audit shows total first-payload JS exceeds 170 KB gz **AND** measured LCP regression > 100 ms on a throttled 4G profile. If so, replace `otel.ts` with the 40-line hand-rolled injector from §6 and the 50-line minimal OTLP/JSON exporter; you keep correlation IDs and trace chaining, lose Resource Timing enrichment and auto-correlation across same-trace fetches.

**Threshold to add tail-based sampling:** Jaeger storage > 80 % of allocated disk **OR** more than ~100 traces/min hitting the collector. Switch to `ParentBasedSampler({ root: new TraceIdRatioBasedSampler(0.1) })` in the browser SDK; configure tail-based "always-on-error" in collector pipeline via the `tailsampling` processor in collector-contrib.

**Before MVP-1 public-tester rollout:**
6. Replace dev-only `allowed_origins` with the deployed origin(s); never use `*`.
7. Front the collector with TLS; add static `Authorization` header to `OTLPTraceExporter`.
8. Add CSP `connect-src 'self' https://otel.example.com`.
9. Wire cookie-consent gate (lazy-import `otel.ts` only after `consent === 'granted'`).
10. Add the `attributes/scrub` collector processor (defence-in-depth).
11. Document trace-data collection in `docs/legal/privacy.md`.
12. Reconsider `@grafana/faro-web-tracing` if Web Vitals / session-replay / error-tracking become desirable bundled features — its setup is a drop-in replacement for `otel.ts`.

**Never:**
- Don't propagate `traceparent` to third-party origins (`googleapis.com`, CDNs, etc.). Restrict `propagateTraceHeaderCorsUrls` to your own domains.
- Don't put user-typed answers, emails, or names in span attributes or baggage.
- Don't use `Access-Control-Allow-Origin: *` with the collector if `Allow-Credentials: true` — browsers reject this combination by spec.
- Don't use `ZoneContextManager` unless you also lower the Vite/TS target to ES2015 (you don't want to).

## Caveats and known unknowns

- **Bundle-size numbers in §6 are estimates.** They are extrapolated from two 2026 vendor-blog analyses (SigNoz, OneUptime) plus the structure of SDK 2.x. The only way to confirm the exact delta on RunCoach's specific dependency graph is to run `npm install && npm run build` and inspect `dist/`. The 30–45 KB gz range should bracket reality but is not a guarantee. A single `vite build` after the install will produce the authoritative number.
- **`@opentelemetry/instrumentation-fetch` version pinning.** At time of research, the latest sdk-trace-web is `2.7.1` and the experimental packages release as `0.20x.0` synchronised with the 2.x stable family. Use `npm view @opentelemetry/instrumentation-fetch versions --json` to pin the exact latest at install time. The OTel-JS version compatibility matrix at the top of [github.com/open-telemetry/opentelemetry-js](https://github.com/open-telemetry/opentelemetry-js) is the source of truth.
- **OpenTelemetry-JS docs themselves label browser instrumentation as experimental:** *"Client instrumentation for the browser is experimental and mostly unspecified. If you are interested in helping out, get in touch with the Browser SIG."* This means semconv attribute names may shift (the `semconvStabilityOptIn` migration window is open). The Resource and HTTP semconv we use (`url.full`, `http.url`, `http.target`) are stable enough for MVP-0 but should be re-checked at MVP-1.
- **W3C Trace Context Level 2 is a Candidate Recommendation Draft.** It adds a `random-trace-id` flag bit but is backward-compatible. Both OTel-JS and OTel-.NET emit Level 1 format today. No action required; documented for awareness.
- **Vite 8 Rollup 4 specifics.** We could not find a *Vite-8-specific* OTel issue in the 2026 timeframe — most reports (e.g. issue #1892) reference Vite 4. The fix landed in OTel-JS 2.x via cleaner browser/Node conditional exports. Verify on first build that no `Module "path" has been externalized` warnings appear.
- **No verified 2026 OSS reference combining React 19.2 + Vite 8 + RTK Query 2.x + OTel-web.** The closest precedents (Liteverge, service-foundry, obs-nebula/frontend-react) cover React + Vite + OTel cleanly; the RTK Query × `FetchInstrumentation` coexistence is established by reading source code, not by pointing at a single canonical sample app. The combination should still be sound — `fetchBaseQuery` is a thin `globalThis.fetch` wrapper and `FetchInstrumentation` patches `globalThis.fetch`. If you encounter an unusual interaction, it would be the first publicly-reported case and worth filing upstream.
- **The "Phoenix" note in the prompt** is a historical planning artefact (R-051 LLM-observability research); the shipped collector is `otel/opentelemetry-collector-contrib:0.150.1`. This artefact assumes that fact and does not consider Phoenix-specific receivers.
- **Sampling decisions made in the browser are advisory.** A `ParentBasedSampler` on the backend will honour the browser's `traceflags=01`; an `AlwaysOnSampler` (current backend default) ignores it. Both produce the same outcome for MVP-0 (always sample). Differences appear only once sampling is dialled down — re-decide then.
- **R-073's error-boundary implementation is out of scope** for this artefact. We've defined the API surface (`getLastTraceId()` / `useLastTraceId()`) the boundary will consume; the choice of `react-error-boundary` vs hand-rolled vs React 19's `onCaughtError` root-level handler is for that sister prompt.
- **Production deployment topology** (cloud-managed Tempo / Datadog / Honeycomb / SigNoz Cloud) is explicitly deferred. The default browser-direct-to-collector pattern works identically against a self-hosted collector or a cloud collector — only the URL and auth header change.

# batch-25b · React 19 + Vite 8 + RTK Query → OTel Collector → Jaeger: client-side instrumentation, W3C propagation, and correlation-ID surfacing

**Status:** Research artifact, advisory. Supports a forthcoming DEC locking SDK choice, deployment topology, and propagation pattern for RunCoach MVP-0.
**Date of research:** 2026-05-12. All versions and behaviours verified against the OpenTelemetry-JS main branch (SDK 2.x / instrumentation 0.20x series) and `opentelemetry-collector` ≥ 0.149.
**Out of scope:** Backend OTel changes beyond confirming default propagators work; LLM/Phoenix observability; production cloud-tracing topology (Tempo/Datadog/Honeycomb); client metrics & logs; the R-073 error-boundary library choice itself; persistence of correlation IDs in an errors table.

## TL;DR

- **Default:** Install `@opentelemetry/api@^1.9`, `@opentelemetry/sdk-trace-web@^2.7`, `@opentelemetry/sdk-trace-base@^2.7`, `@opentelemetry/exporter-trace-otlp-http@^0.20x`, `@opentelemetry/instrumentation@^0.20x`, `@opentelemetry/instrumentation-fetch@^0.20x`, `@opentelemetry/resources@^2.7`, `@opentelemetry/semantic-conventions@^1.3x`. Use the built-in `StackContextManager` (not `ZoneContextManager`) — `BatchSpanProcessor` → `OTLPTraceExporter` posting to `http://localhost:4318/v1/traces`. Add a 6-line `cors:` block to the OTLP receiver in `infra/otel/otel-collector-config.yaml` so the browser posts direct to the collector. Surface the last-seen **trace-id (32-hex)** to the error boundary via a tiny module-level singleton fed by `FetchInstrumentation.applyCustomAttributesOnSpan`.
- **Fallback:** If the gzipped delta (~30–45 KB after tree-shake; ~60 KB if you also keep `instrumentation-document-load`) is unacceptable, ship a **40-line hand-rolled `prepareHeaders` shim** that mints a `traceparent` per request from `crypto.getRandomValues`, POSTs a minimal OTLP/JSON envelope to `/v1/traces` via `fetch(..., {keepalive:true})`, and stashes the trace-id in the same module singleton. Loses Resource Timing API enrichment and auto-correlation between same-trace fetches, but is ~1.5 KB gzipped and TypeScript-strict clean.
- **Topology:** **browser → collector direct** with an origin allow-list (`http://localhost:5173` in dev, the deployed origin in prod). Adding `cors:` to the `otlphttp` receiver is the canonical pattern documented by `opentelemetry-collector` upstream. The backend-proxy variant is rejected as default because it loses the collector-as-network-boundary property and forces an OTLP-JSON re-emit endpoint in ASP.NET Core that has no security benefit at this stage.

## Key Findings

| Decision | Default | Fallback |
| --- | --- | --- |
| Browser SDK | `@opentelemetry/sdk-trace-web` 2.7.x + `instrumentation-fetch` 0.20x | hand-rolled `prepareHeaders` + `crypto.getRandomValues` + `fetch keepalive` |
| Context manager | `StackContextManager` (default; no `zone.js`) | n/a |
| Exporter | `@opentelemetry/exporter-trace-otlp-http` (OTLP/HTTP-JSON) → `:4318/v1/traces` | bare `fetch` POST of minimal OTLP/JSON |
| Span processor | `BatchSpanProcessor` (`scheduledDelayMillis: 5000`, `maxQueueSize: 2048`, `maxExportBatchSize: 64`) | `SimpleSpanProcessor` |
| Propagator | default `CompositePropagator` = W3C TraceContext + W3C Baggage (matches ASP.NET Core defaults) | manual `propagation.inject(context.active(), headers, defaultTextMapSetter)` |
| Topology | browser → collector direct, CORS allow-list on `otlphttp` receiver | reverse-proxy via Vite dev + ASP.NET Core `/v1/traces` pass-through |
| Correlation-ID format displayed | **trace-id only**, 32 lowercase hex chars, monospace, copy-to-clipboard | same |
| Correlation-ID seam | module-level singleton `lastTraceId.ts` updated from `applyCustomAttributesOnSpan` + read via a `useSyncExternalStore` hook | same singleton; updated from manual injector |
| Sampling (MVP-0 personal use) | `AlwaysOnSampler` (head-based 100%) | same |
| Sampling (MVP-1 public testers) | `ParentBasedSampler({ root: new TraceIdRatioBasedSampler(0.1) })` + tail-based "always-on-error" via collector | same |
| PII posture | scrub URL query strings on the client, drop request bodies, never set user-typed answers as attributes | same |
| Privacy posture MVP-1 | **opt-in via cookie consent**, default off | same |
| Failure mode | silent retry-with-backoff (default OTLP exporter), no user-visible error | same |

## Details

### 1 · SDK choice comparison

| Candidate | Gz delta (est.) | Tree-shake on Vite 8 / Rollup 4 | RTK Query compat | TS-strict | Maint signal | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| `sdk-trace-web` + `instrumentation-fetch` + `exporter-trace-otlp-http` (no `context-zone`) | ~30–45 KB gz | Good with SDK 2.x: packages declare `sideEffects: false` | Wraps global `fetch`. `fetchBaseQuery` internally calls `globalThis.fetch`, so one hook covers RTK Query, plain `fetch`, and third-party code. No double-wrap. | Yes — official `.d.ts`, strict-clean since 2.0 | Excellent — SDK 2.0 Apr 2025, current 2.7.1, ~weekly cadence | **DEFAULT** |
| `@opentelemetry/auto-instrumentations-web` meta | ~60+ KB gz | Pulls fetch + xhr + document-load + user-interaction. Tree-shakable only if you opt subsets out. | Same | Yes | Same repo | **Reject for MVP-0** — pulls instrumentations we don't need yet |
| `@vercel/otel` | n/a (Node/Edge only) | Designed for Next.js server-only `instrumentation.ts`. **Not a browser SDK.** Vercel's docs: *"no traces or metrics are recorded for any browser-side interactions."* | n/a | n/a | Active | **Reject** — wrong layer |
| `@sentry/browser` + Sentry tracing | ~50–70 KB gz | OK | Yes | Yes | Active | **Reject as default** — does NOT emit OTLP from the browser. Sentry maintainers (sentry-javascript discussion #7364): *"OpenTelemetry for the browser is pretty much doing a full reset… we are holding off on any work on it."* You'd get Sentry-format traces, not Jaeger-compatible OTLP, defeating the requirement that backend spans chain under the same trace in Jaeger. |
| `@grafana/faro-web-tracing` | ~40–50 KB gz (re-exports OTel SDK + own session-span processor) | OK | Yes | Yes | Active | **Plausible secondary** if RunCoach later wants Grafana RUM. Overkill for MVP-0 trace-only goal. |
| Hand-rolled `prepareHeaders` injector + tiny OTLP POST | **~1.5 KB gz** | Trivially perfect | Yes (direct seam) | Yes | n/a — your code | **FALLBACK** |

**Why `StackContextManager` and not `ZoneContextManager`:** From `@opentelemetry/sdk-trace-web` README: *"You can choose to use the ZoneContextManager if you want to trace asynchronous operations. Please note that the ZoneContextManager does not work with JS code targeting ES2017+. In order to use the ZoneContextManager, please transpile back to ES2015."* RunCoach targets ES2022+ (Vite 8 default). `zone.js` adds ~80–90 KB by itself per SigNoz / OneUptime 2026 bundle analyses. We don't need zone-tracking because `FetchInstrumentation` creates spans immediately before each `fetch` call — async context is bounded inside the patched function.

**Why `fetchBaseQuery` + `FetchInstrumentation` coexist correctly:** `fetchBaseQuery` is documented as "a lightweight fetch wrapper" that invokes `globalThis.fetch` (or your `fetchFn`). `@opentelemetry/instrumentation-fetch` patches `globalThis.fetch` exactly once on `enable()` (source: `experimental/packages/opentelemetry-instrumentation-fetch/src/fetch.ts`). RTK never caches the unpatched function. **No double-wrap.** The only sequencing constraint: the instrumentation must `enable()` before the first render — import `./app/api/otel` as the **first** import in `main.tsx` (the exact pattern used in `liteverge/liteverge-opentelemetry/examples/react-vite-app`).

### 2 · Deployment topology decision

#### The CORS reality

RunCoach's `infra/otel/otel-collector-config.yaml` exposes `otlphttp` on `0.0.0.0:4318` with **no** `cors:` block. A browser at `http://localhost:5173` POSTing to `http://localhost:4318/v1/traces` issues an `OPTIONS` preflight; the collector returns no `Access-Control-Allow-Origin` header; the trace POST never fires. OpenTelemetry docs (opentelemetry.io/docs/languages/js/exporters/): *"You need to configure special headers for Cross-Origin Resource Sharing (CORS). The OpenTelemetry Collector provides a feature for http-based receivers to add the required headers to allow the receiver to accept traces from a web browser."*

#### (a) Add `cors:` to the OTLP receiver — RECOMMENDED DEFAULT

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318
        cors:
          allowed_origins:
            - http://localhost:5173        # Vite dev
            - http://localhost:4173        # Vite preview
            # MVP-1 prod: add deployed origin(s) here, e.g.
            # - https://runcoach.example.com
          allowed_headers:
            - traceparent
            - tracestate
            - baggage
            - Content-Type
          max_age: 7200
```

Source: `opentelemetry-collector/receiver/otlpreceiver/README.md` and `opentelemetry-collector/config/confighttp/README.md`, which warns: *"Do not use a plain wildcard `[\"*\"]`, as our CORS response includes `Access-Control-Allow-Credentials: true`, which makes browsers disallow a plain wildcard (this is a security standard)."* Verified identical-shape example: `vitest-dev/vitest@1ec3a8b` browser-mode test config.

**Pros:** one-file change; collector remains the network boundary; auth/sampling/PII redaction processors live in collector pipelines as designed; direct browser→collector POST, no extra hop; matches upstream OTel's own recommendation. **Cons:** the collector port (4318) must be reachable from the user network in prod — terminate TLS at an ingress in front of it, and accept that any "API key" you add will be bundled in the JS (treat as a rate-limit token, not a secret).

#### (b) Reverse-proxy `/v1/traces` from Vite dev + ASP.NET Core in prod

```ts
// vite.config.ts
server: { proxy: { '/v1/traces': { target: 'http://localhost:4318', changeOrigin: true } } }
```

```csharp
// Program.cs (sketch, prod only)
app.MapPost("/v1/traces", async (HttpRequest req, IHttpClientFactory f) =>
{
    using var c = f.CreateClient();
    using var content = new StreamContent(req.Body);
    content.Headers.ContentType = req.ContentType is { } ct ? new MediaTypeHeaderValue(ct) : null;
    var resp = await c.PostAsync("http://otel-collector:4318/v1/traces", content);
    return Results.StatusCode((int)resp.StatusCode);
});
```

**Pros:** same-origin; no CORS. **Cons:** extra hop and Kestrel allocations per export batch; backend becomes a critical path for telemetry; loses the collector-as-network-boundary property. **Reject as default.**

#### (c) Browser POSTs to a backend endpoint that re-emits via the existing OTLP exporter

Requires either OTLP-JSON parsing + replay (~150 LOC) or byte-forwarding (equivalent to b with extra work). **Reject.**

#### Production-deployment note for (a) — MVP-1 rollout checklist

1. Replace `allowed_origins: [http://localhost:5173]` with deployed origin(s); never `*`.
2. Front the collector with TLS termination (nginx/Caddy/Traefik in ingress).
3. Add `headers: { Authorization: 'Bearer …' }` to `OTLPTraceExporter`; validate at the collector via `bearertokenauth` extension. Secret = rate-limit token, not confidential.
4. Add `Content-Security-Policy: connect-src 'self' https://otel.runcoach.example.com;` to `index.html`.

### 3 · Propagation through RTK Query

**What `FetchInstrumentation` does to headers:** the patched `fetch` calls `propagation.inject(context.active(), headers, ...)` after user-set headers and before the network call. **Same-origin** requests always get `traceparent`/`tracestate`/`baggage`. **Cross-origin** requests get them only if the URL matches `propagateTraceHeaderCorsUrls` (source comment: *"// urls which should include trace headers when origin doesn't match"*).

RunCoach uses same-origin `/api/*` (Vite proxies to API in dev; SPA served from same origin as API in prod), so `propagateTraceHeaderCorsUrls` can be left undefined. If origins ever split: `propagateTraceHeaderCorsUrls: [/^https:\/\/api\.runcoach\./]`.

#### Recommended seam — `frontend/src/app/api/otel.ts` (new file)

```ts
// Must be imported BEFORE React renders and BEFORE any RTK Query baseQuery is constructed.
import { trace, context } from '@opentelemetry/api';
import { WebTracerProvider, BatchSpanProcessor } from '@opentelemetry/sdk-trace-web';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } from '@opentelemetry/semantic-conventions';
import { recordLastTraceId } from './last-trace-id';

const COLLECTOR_URL =
  import.meta.env.VITE_OTLP_TRACES_URL ?? 'http://localhost:4318/v1/traces';

const exporter = new OTLPTraceExporter({ url: COLLECTOR_URL });

const provider = new WebTracerProvider({
  resource: resourceFromAttributes({
    [ATTR_SERVICE_NAME]: 'runcoach-frontend',
    [ATTR_SERVICE_VERSION]: import.meta.env.VITE_APP_VERSION ?? '0.0.0-dev',
  }),
  spanProcessors: [
    new BatchSpanProcessor(exporter, {
      scheduledDelayMillis: 5000,
      maxQueueSize: 2048,
      maxExportBatchSize: 64,
      exportTimeoutMillis: 30000,
    }),
  ],
});

provider.register(); // default StackContextManager + W3C TraceContext + Baggage

registerInstrumentations({
  instrumentations: [
    new FetchInstrumentation({
      ignoreUrls: [/\/v1\/traces$/], // don't trace the trace exporter itself
      clearTimingResources: true,
      applyCustomAttributesOnSpan: (span, request, _response) => {
        recordLastTraceId(span.spanContext().traceId);
        const urlStr = typeof request === 'string' ? request : (request as Request).url;
        try {
          const u = new URL(urlStr, window.location.origin);
          span.setAttribute('http.url', `${u.origin}${u.pathname}`);
          span.setAttribute('url.full', `${u.origin}${u.pathname}`);
          span.setAttribute('http.target', u.pathname);
          span.setAttribute('url.path', u.pathname);
        } catch { /* ignore */ }
      },
    }),
  ],
});

export const tracer = trace.getTracer('runcoach-frontend');
export { context };
```

#### Update `frontend/src/main.tsx`

```ts
import './app/api/otel';  // ← MUST be first, before React/RTK
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './app/modules/app/app.component';

createRoot(document.getElementById('root')!).render(
  <StrictMode><App /></StrictMode>,
);
```

`prepareHeaders` in `base-query.ts` needs **zero changes** — `FetchInstrumentation` injects after user headers. The X-XSRF-TOKEN injection on mutations remains untouched.

**Order of init vs `createRoot`:** (1) `otel.ts` module body runs (registers provider, patches global fetch), (2) `main.tsx` imports React + store + App, (3) `createRoot(...).render(<App/>)` triggers first render & bootstrap query, (4) RTK Query reads `globalThis.fetch` → now patched → span emitted → batched, (5) after 5s `BatchSpanProcessor` POSTs to `/v1/traces`. Reverse step 1 and the first trace is lost; the OTel docs warn: *"If you fail to initialize the SDK or initialize it too late, no-op implementations will be provided to any library which acquires a tracer or meter from the API."*

### 4 · Backend chaining verification

**Default propagators line up:**
- Browser: `WebTracerProvider.register()` registers `CompositePropagator({ propagators: [new W3CTraceContextPropagator(), new W3CBaggagePropagator()] })` when no override.
- .NET: From opentelemetry-dotnet docs/DeepWiki: *"By default, OpenTelemetry .NET SDK configures a `CompositeTextMapPropagator` that includes: W3C Trace Context Propagator (`TraceContextPropagator`) … Baggage Propagator (`BaggagePropagator`)."* RunCoach has no `Sdk.SetDefaultTextMapPropagator(...)` — defaults apply.

**Chain mechanics:** `AddAspNetCoreInstrumentation()` subscribes to `Microsoft.AspNetCore.Hosting` diagnostic events. The ASP.NET Core hosting layer itself parses the incoming `traceparent` and creates a server `Activity` with the extracted `parentId` independent of OTel; AspNetCoreInstrumentation observes that activity. From the OTel source comment (`HttpInListener.cs`, referenced in opentelemetry-dotnet#4214): *"When the default propagator is just w3c we respect the work AspNetCore does instead of calling the SDK logic."*

Resulting trace shape:
```
browser fetch span (client kind)  [traceparent injected]
  └─ ASP.NET Core auto-span (server kind, parented by traceparent)
        ├─ Marten ActivitySource span
        │     └─ Npgsql ActivitySource span
        ├─ Wolverine ActivitySource span
        └─ RunCoach.Llm ActivitySource span (PlanGenerationService)
```

All under one `trace_id`. **Risks that would silently break this:**
| Risk | RunCoach? | Mitigation |
| --- | --- | --- |
| Sampler drops the trace | No — backend uses default AlwaysOn. | If sampler added later, use `ParentBasedSampler` so parent's `sampled` flag wins. |
| Custom propagator overrides defaults on one side | Not present. | Don't add unless both sides change in lockstep. |
| CORS strips `traceparent` from `Access-Control-Allow-Headers` | Same-origin only — not relevant. | If cross-origin: ASP.NET Core CORS must include `WithHeaders("traceparent", "tracestate", "baggage")`. |
| Baggage > 8 192 bytes | No plans to populate. | Don't put user-typed answers in baggage. |

**Smoke test:**
```bash
TRACE=4bf92f3577b34da6a3ce929d0e0e4736
SPAN=00f067aa0ba902b7
curl -i http://localhost:5000/api/plan \
  -H "traceparent: 00-${TRACE}-${SPAN}-01" \
  -H "Cookie: __Host-Session=...; __Host-Xsrf-Request=..." \
  -H "X-XSRF-TOKEN: ..."
# Open: http://localhost:16686/trace/${TRACE}
# Expect server + nested backend spans under that ID.
```

### 5 · Correlation-ID surfacing for the error boundary

**UX format decision: trace-id only (32 lowercase hex), grouped 8-8-8-8, copy button.**

Rationale: the full traceparent (`00-{trace}-{span}-{flags}`, 55 chars) includes a span-id that is only meaningful for the *most recent* fetch — it changes on retry and could mislead support. The trace-id is the stable identifier and the literal Jaeger URL pattern (`/trace/{trace-id}`). W3C trace-context: *"This is the ID of the whole trace forest and is used to uniquely identify a distributed trace through a system."* 32 hex chars fits a mobile-width fallback card.

**Seam: module-level singleton + `useSyncExternalStore` hook.** Rejected: Redux slice (re-renders every consumer on every fetch), `useContext` provider (same problem unless wrapped in `useSyncExternalStore`, identical to singleton then), `localStorage` (sync I/O, persists across tabs), `ref` (can't be read from a class error-boundary's render without prop drilling).

```ts
// frontend/src/app/api/last-trace-id.ts
type Listener = () => void;
let current: string | null = null;
const listeners = new Set<Listener>();

export function recordLastTraceId(traceId: string): void {
  if (traceId === current) return;
  current = traceId;
  listeners.forEach((l) => l());
}
export function getLastTraceId(): string | null { return current; }
export function subscribe(l: Listener): () => void {
  listeners.add(l);
  return () => { listeners.delete(l); };
}

import { useSyncExternalStore } from 'react';
export function useLastTraceId(): string | null {
  return useSyncExternalStore(subscribe, getLastTraceId, () => null);
}
```

Already wired in §3: every patched fetch fires `applyCustomAttributesOnSpan` → `recordLastTraceId(span.spanContext().traceId)`. Covers ~99 % of cases (the error boundary fires after a render that read data from a fetch). For pure render errors with no preceding fetch, `getLastTraceId()` returns `null` and the fallback hides the support-code block. That is the correct UX.

```tsx
// frontend/src/app/modules/errors/fallback-card.tsx (R-073 will own this file)
import { getLastTraceId } from '@/app/api/last-trace-id';

export function FallbackCard({ error }: { error: Error }) {
  const traceId = getLastTraceId();
  const display = traceId ? traceId.match(/.{1,8}/g)!.join('-') : null;
  return (
    <div role="alert">
      <h2>Something went wrong</h2>
      {display && (
        <div className="font-mono text-sm select-all flex items-center gap-2">
          <span>Support code:</span><code>{display}</code>
          <button onClick={() => navigator.clipboard.writeText(traceId!)} aria-label="Copy support code">Copy</button>
        </div>
      )}
    </div>
  );
}
```

### 6 · Bundle-size budget

Two 2026 sources converge on the order of magnitude:
- **SigNoz, "Reducing OpenTelemetry Bundle Size in Browser Frontend":** *"the official browser auto-instrumentation bundle was about 300 KB uncompressed [~60 KB gzipped] after recent optimisations."* That number is for `@opentelemetry/auto-instrumentations-web` (all instrumentations).
- **OneUptime, "How to Reduce OpenTelemetry Browser SDK Bundle Size with Tree Shaking":** *"If you import all of these without tree shaking, you are looking at over 300KB before gzip. With proper tree shaking and the right choices, you can cut this by more than half."* Lists a "minimal" config at **~25 KB gz** — exactly the RunCoach default set.

Conservative estimate for the default config (sdk-trace-web + sdk-trace-base + exporter-trace-otlp-http + instrumentation + instrumentation-fetch + api + resources + semantic-conventions + core): **~30–45 KB gz on top of current React 19 + RTK + Router + Tailwind**.

**Vite 8 chunking:**
```ts
build: {
  rollupOptions: { output: { manualChunks: {
    otel: [
      '@opentelemetry/api', '@opentelemetry/sdk-trace-web', '@opentelemetry/sdk-trace-base',
      '@opentelemetry/exporter-trace-otlp-http', '@opentelemetry/instrumentation',
      '@opentelemetry/instrumentation-fetch', '@opentelemetry/resources',
      '@opentelemetry/semantic-conventions', '@opentelemetry/core',
    ],
  } } },
  chunkSizeWarningLimit: 100,
},
```

**Known Vite gotcha:** `@opentelemetry/instrumentation` historically imported Node's `path` (opentelemetry-js-contrib#1892, Vite 4). With OTel-JS 2.x this is tree-shaken in browser builds. Verify on first build; if `Module "path" has been externalized` still appears, add `optimizeDeps: { exclude: ['@opentelemetry/instrumentation'] }` and `build: { rollupOptions: { external: ['node:path', 'path'] } }`.

**Fallback (~1.5 KB gz):** mint `traceparent` per request with `crypto.getRandomValues`, POST minimal OTLP/JSON with `fetch(..., {keepalive: true})` on every Nth fetch.

```ts
// frontend/src/app/api/lite-traceparent.ts (~40 LOC, no deps)
const hex = (n: number) => n.toString(16).padStart(2, '0');
const rand = (n: number) => Array.from(crypto.getRandomValues(new Uint8Array(n)), hex).join('');
let currentTraceId: string | null = null;
export const getLastTraceId = () => currentTraceId;
export function mintTraceparent(): { traceparent: string; traceId: string } {
  const traceId = rand(16);
  const spanId  = rand(8);
  currentTraceId = traceId;
  return { traceparent: `00-${traceId}-${spanId}-01`, traceId };
}
```

Choose the fallback only if the production gzipped bundle exceeds an agreed budget (first-JS-payload threshold commonly cited at ~150–170 KB gz).

### 7 · Sampling, PII, and attribute scrubbing

**What the SDK auto-records that could leak PII:** `http.method`, `http.url` (full URL including query string — #1 risk), `http.host`, `http.scheme`, `http.target` (path — #2 risk), `http.status_code`, `http.response_content_length`, `http.user_agent`. It does **not** capture request or response bodies.

RunCoach's URL templates are PII-clean (`/api/auth/login`, `/api/onboarding/{step}`, `/api/plan`), but don't trust developer discipline — scrub at the SDK layer.

**Option A — `applyCustomAttributesOnSpan` (already in §3):** strips query strings and rewrites `http.url` / `url.full` / `http.target` / `url.path` to origin+pathname only. Narrow, easy to reason about.

**Option B — Custom `SpanProcessor.onStart` (defence-in-depth):**

```ts
import type { Context } from '@opentelemetry/api';
import type { ReadableSpan, Span, SpanProcessor } from '@opentelemetry/sdk-trace-base';

class PiiScrubbingSpanProcessor implements SpanProcessor {
  private readonly EMAIL = /[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/g;
  private readonly UUID  = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;
  onStart(span: Span, _ctx: Context): void {
    const url = span.attributes['http.url'];
    if (typeof url === 'string') {
      const scrubbed = url.replace(this.EMAIL, '[REDACTED_EMAIL]').replace(this.UUID, '[REDACTED_UUID]').split('?')[0];
      span.setAttribute('http.url', scrubbed);
    }
  }
  onEnd(_: ReadableSpan): void {}
  shutdown(): Promise<void> { return Promise.resolve(); }
  forceFlush(): Promise<void> { return Promise.resolve(); }
}

new WebTracerProvider({ spanProcessors: [new PiiScrubbingSpanProcessor(), new BatchSpanProcessor(exporter, {...})] });
```

**Recommendation:** Option A as default; add Option B at MVP-1 as defence-in-depth. The OTel "Handling sensitive data" doc: *"The best way to prevent the collection of sensitive data is not to collect data that might be sensitive."*

**Collector-side belt-and-braces** (cheap to add now):

```yaml
processors:
  attributes/scrub:
    actions:
      - key: http.url
        action: hash
      - key: user.email
        action: delete
      - key: http.user_agent
        action: hash

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [attributes/scrub, batch]
      exporters: [otlp/jaeger, debug]
```

**What we explicitly do NOT instrument:** user-typed onboarding answers (live in request bodies, not URLs/headers), auth cookies (not captured by browser `instrumentation-fetch`), bearer tokens (none in RunCoach today). The LayeredPromptSanitizer (DEC-059) runs server-side and protects LLM prompts — it does not see browser span attributes, hence this layer.

**Sampling:**
- MVP-0 personal use: `AlwaysOnSampler` (head 100%). Already the default.
- MVP-1 public testers: `ParentBasedSampler({ root: new TraceIdRatioBasedSampler(0.1) })` (browser samples 10% of new root traces; child requests inherit parent decision). Add tail-based "always-on-error" in the collector via the `tailsampling` processor in collector-contrib.

### 8 · Privacy / GDPR posture

**MVP-0 (personal use, no public users):** Always-on. Localhost-only. No consent UI. No DNT check. There is one user (developer); traces never leave the dev machine.

**MVP-1 (public-tester cohort):** **Opt-in via cookie-consent banner. Default off.** Lazy-load OTel only after consent:

```ts
if (cookieConsentGranted('analytics')) {
  await import('./otel-bootstrap');
}
```

Users who decline pay 0 KB of OTel JS. Pattern documented in OneUptime "Telemetry Data Governance Framework": *"Only initialize telemetry if the user has consented to analytics… User has not consented. Use a no-op provider."*

**Why not "always-on with PII scrubbing"?** (1) Trace-id itself is a pseudonymous identifier created without consent; under GDPR Recital 30 / Art 4(1) pseudonymous data combined with other signals can be personal data. (2) `Resource` attributes plus collector-ingress IP / user-agent make traces identifiable. (3) Collector logs each batch — currently `debug` exporter is active and logs persist.

**Why not "defer to DNT"?** Effectively deprecated by major browsers; Safari removed it 2019; Sec-GPC has limited adoption. Not a reliable consent signal in 2026.

**Documentation to add at MVP-1:** `docs/legal/privacy.md` disclosing trace-id (pseudonym), URL pathnames, HTTP status, duration, user-agent, and retention period (Jaeger default 24h).

### 9 · Failure modes

| Scenario | What the SDK does | What the user sees |
| --- | --- | --- |
| Collector down (`:4318` connection refused) | OTLPTraceExporter retries with exponential backoff. Per npm `@opentelemetry/exporter-trace-otlp-http` README: *"DEFAULT_EXPORT_MAX_ATTEMPTS: The maximum number of attempts, including the original request. Defaults to 5."* On final failure, `BatchSpanProcessor` drops the batch and logs to `diag` only. | Nothing. |
| Collector returns 4xx/5xx | Same retry-then-drop. | Nothing. |
| Browser offline | `fetch` rejects → retry → drop. | Nothing. |
| Tab backgrounded mid-batch | `BatchSpanProcessor` keeps running; no automatic flush on `visibilitychange === 'hidden'` (known gap, see opentelemetry-js#3489 re sendBeacon 64 KB limit). | Up to `scheduledDelayMillis` of buffered spans may be lost on tab close. |
| Tab closed | `forceFlush()` is not auto-wired to `pagehide`. OTLP-HTTP exporter uses `fetch(..., {keepalive: true})` in modern browsers; queued-but-not-yet-sent spans are lost. | Some spans may be lost — invisible. |
| Slow connection | OTLP POSTs continue in background. Default `exportTimeoutMillis: 30 000`. | Nothing. |
| CSP blocks `connect-src` to collector | `fetch` rejects → retry → drop. Console error in DevTools. | Nothing. |

**Optional manual flush on page-hide (recommended):**
```ts
document.addEventListener('visibilitychange', () => {
  if (document.visibilityState === 'hidden') {
    provider.forceFlush().catch(() => {});
  }
});
```
MDN: prefer `visibilitychange` over `pagehide`/`unload` for analytics-style flushes.

**Confirmed: silent drop.** The default OTLP-HTTP exporter logs only via `diag` (default INFO+), no `console.error`. No user-visible failure surface.

### 10 · Existing precedents and primary sources

| Repo | Notes |
| --- | --- |
| [`nsalexamy/service-foundry`](https://nsalexamy.github.io/service-foundry/pages/documents/o11y-foundry/o11y-in-react/) | React + TS + Vite (`npm create vite@latest ... --template react-ts`), Spring backend, full OTLP-HTTP-JSON setup, includes the exact `cors:` block matching our recommendation. Most-aligned reference. |
| [`liteverge/liteverge-opentelemetry/examples/react-vite-app`](https://github.com/liteverge/liteverge-opentelemetry/tree/main/examples/react-vite-app) | Minimal React + Vite + TS example: *"src/main.tsx imports instrumentation as its first import, before React renders."* The seam pattern from §3. |
| [`obs-nebula/frontend-react`](https://github.com/obs-nebula/frontend-react) + Red Hat Developer writeup | OTel collector → Jaeger chain with `instrumentation-fetch` end-to-end. |
| [`vitest-dev/vitest@1ec3a8b`](https://github.com/vitest-dev/vitest/commit/1ec3a8b68) | "feat: support openTelemetry for browser mode" — same `cors: allowed_origins: ["http://localhost:*"]` block. |
| [`pkanal/otel-react-example`](https://github.com/pkanal/otel-react-example) | Small example app set up for tracing with OpenTelemetry. |

We did not find a public 2026 OSS repo combining all four of **React 19 + Vite 8 + RTK Query + OTel-web** specifically. RTK Query × `FetchInstrumentation` coexistence is established by reading `instrumentation-fetch/src/fetch.ts` and RTK's `fetchBaseQuery` source — both wrap/use `globalThis.fetch` cleanly.

**Other primary sources:**
- OpenTelemetry-JS source: [github.com/open-telemetry/opentelemetry-js](https://github.com/open-telemetry/opentelemetry-js)
- Collector receiver: [github.com/open-telemetry/opentelemetry-collector/tree/main/receiver/otlpreceiver](https://github.com/open-telemetry/opentelemetry-collector/tree/main/receiver/otlpreceiver)
- Collector confighttp (CORS warning): [github.com/open-telemetry/opentelemetry-collector/blob/main/config/confighttp/README.md](https://github.com/open-telemetry/opentelemetry-collector/blob/main/config/confighttp/README.md)
- W3C Trace Context L1: [w3.org/TR/trace-context/](https://www.w3.org/TR/trace-context/)
- W3C Trace Context L2 (Candidate Recommendation Draft, backward-compatible): [w3.org/TR/trace-context-2/](https://www.w3.org/TR/trace-context-2/)
- OTel-JS SDK 2.0 announcement: [opentelemetry.io/blog/2025/otel-js-sdk-2-0/](https://opentelemetry.io/blog/2025/otel-js-sdk-2-0/)
- opentelemetry-dotnet propagation: [deepwiki.com/open-telemetry/opentelemetry-dotnet/8-context-propagation](https://deepwiki.com/open-telemetry/opentelemetry-dotnet/8-context-propagation)
- ASP.NET Core Instrumentation README: [github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
- Honeycomb React guide: [honeycomb.io/blog/configuring-react-application-honeycomb-for-frontend-observability](https://www.honeycomb.io/blog/configuring-react-application-honeycomb-for-frontend-observability)
- SigNoz frontend monitoring: [signoz.io/docs/frontend-monitoring/sending-traces-with-opentelemetry/](https://signoz.io/docs/frontend-monitoring/sending-traces-with-opentelemetry/)
- Tracetest cross-origin guide: [tracetest.io/blog/propagating-the-opentelemetry-context-from-the-browser-to-the-backend](https://tracetest.io/blog/propagating-the-opentelemetry-context-from-the-browser-to-the-backend)
- Elastic Observability Labs frontend instrumentation: [elastic.co/observability-labs/blog/web-frontend-instrumentation-with-opentelemetry](https://www.elastic.co/observability-labs/blog/web-frontend-instrumentation-with-opentelemetry)
- Sentry browser × OTel status: [github.com/getsentry/sentry-javascript/discussions/7364](https://github.com/getsentry/sentry-javascript/discussions/7364)
- @vercel/otel scope: [vercel.com/docs/tracing](https://vercel.com/docs/tracing)

### 11 · Migration cost from current state

```jsonc
// frontend/package.json — additions
{
  "dependencies": {
    "@opentelemetry/api": "^1.9.0",
    "@opentelemetry/core": "^2.7.1",
    "@opentelemetry/exporter-trace-otlp-http": "^0.207.0",
    "@opentelemetry/instrumentation": "^0.207.0",
    "@opentelemetry/instrumentation-fetch": "^0.207.0",
    "@opentelemetry/resources": "^2.7.1",
    "@opentelemetry/sdk-trace-base": "^2.7.1",
    "@opentelemetry/sdk-trace-web": "^2.7.1",
    "@opentelemetry/semantic-conventions": "^1.37.0"
  }
}
```

> The experimental packages version as `0.20x.0` locked to the 2.x stable family per the OTel-JS version-compatibility matrix. Use `npm view @opentelemetry/instrumentation-fetch versions` to pin the latest 0.20x at install time.

**Files touched:**

| File | Change | LOC |
| --- | --- | --- |
| `frontend/package.json` | add 9 deps | +9 |
| `frontend/src/app/api/otel.ts` | NEW — SDK bootstrap (§3) | ~70 |
| `frontend/src/app/api/last-trace-id.ts` | NEW — singleton + hook (§5) | ~25 |
| `frontend/src/main.tsx` | add `import './app/api/otel'` at top | +1 |
| `frontend/vite.config.ts` | add `manualChunks: { otel: [...] }` (optional) | +10 |
| `frontend/src/app/modules/errors/fallback-card.tsx` | (in R-073's diff) read `getLastTraceId()` | ~20 |
| `infra/otel/otel-collector-config.yaml` | add `cors:` block + (optional) `attributes/scrub` | +8 |
| `frontend/.env.development` | `VITE_OTLP_TRACES_URL=…`, `VITE_APP_VERSION=…` | +2 |

**No changes** to `base-query.ts`, `api-slice.ts`, any `*.api.ts`, the store, auth slice, router, or 401 handler. **No backend code changes** — `Program.cs:260-298` already wires correct defaults.

**Effort:** ~2–3 hours for an engineer familiar with the codebase. Most time is on end-to-end verification.

**Verification checklist:**
1. `docker compose -f docker-compose.otel.yml up` brings collector + Jaeger up.
2. `cd frontend && npm install && npm run dev` — no TS-strict errors.
3. DevTools → Network: `/api/...` request shows `traceparent: 00-…-…-01`.
4. DevTools → Network: a request to `:4318/v1/traces` returns 200 (`OPTIONS` preflight succeeds first).
5. Open `http://localhost:16686/trace/{trace-id}` — see browser client span + ASP.NET Core server span + nested backend spans.
6. Trigger a render error — fallback shows trace-id formatted `xxxxxxxx-xxxxxxxx-xxxxxxxx-xxxxxxxx`; copy works.
7. `vite build` succeeds; bundle visualizer shows `otel` chunk at ~30–45 KB gz.
8. Stop the collector; refresh; no user-visible errors; only diag `WARN` in console.

**Rollback:** remove the `import './app/api/otel'` line from `main.tsx`. Done.

## Recommendations

**Now (MVP-0, this slice):**
1. Add `cors:` to `infra/otel/otel-collector-config.yaml` (8 lines).
2. Add 9 OTel deps to `frontend/package.json` and create `frontend/src/app/api/otel.ts` + `last-trace-id.ts` per §3 and §5.
3. Add the import line at the top of `main.tsx`.
4. Smoke-test via §11 checklist. Expect 30–45 KB gz bundle delta.
5. Leave sampling at `AlwaysOn` (head-based 100%).

**Threshold to move to the fallback:** total first-payload JS exceeds 170 KB gz **AND** measured LCP regression > 100 ms on a throttled 4G profile. Then replace `otel.ts` with the 40-line `crypto.getRandomValues` injector + 50-line minimal OTLP/JSON exporter from §6.

**Threshold to add tail-based sampling:** Jaeger storage > 80 % of allocated disk **OR** ≥ ~100 traces/min hitting the collector. Move browser to `ParentBasedSampler({ root: new TraceIdRatioBasedSampler(0.1) })`; add `tailsampling` processor in collector-contrib for always-on-error.

**Before MVP-1 public-tester rollout:**
6. Replace dev `allowed_origins` with deployed origin(s); never `*`.
7. Front the collector with TLS; add static `Authorization` header to `OTLPTraceExporter`.
8. Add CSP `connect-src 'self' https://otel.example.com`.
9. Wire cookie-consent gate (lazy-import `otel.ts` only after `consent === 'granted'`).
10. Add the `attributes/scrub` collector processor (defence-in-depth).
11. Document trace-data collection in `docs/legal/privacy.md`.
12. Reconsider `@grafana/faro-web-tracing` if Web Vitals / session-replay / error-tracking become desirable.

**Never:**
- Don't propagate `traceparent` to third-party origins. Restrict `propagateTraceHeaderCorsUrls` to your own domains.
- Don't put user-typed answers, emails, or names in span attributes or baggage.
- Don't use `Access-Control-Allow-Origin: *` with `Allow-Credentials: true` — browsers reject this combination by spec.
- Don't use `ZoneContextManager` unless you also lower the Vite/TS target to ES2015.

## Caveats

- **Bundle-size numbers in §6 are estimates** extrapolated from two 2026 vendor analyses (SigNoz, OneUptime) plus SDK 2.x structure. A single `npm install && npm run build` will produce the authoritative number for RunCoach's specific dependency graph. The 30–45 KB gz range should bracket reality.
- **`@opentelemetry/instrumentation-fetch` version pinning.** Latest sdk-trace-web at research time is `2.7.1`; experimental packages release as `0.20x.0` synchronised with 2.x stable. Use `npm view @opentelemetry/instrumentation-fetch versions --json` to pin the latest 0.20x at install. The OTel-JS version compatibility matrix in the repo README is the source of truth.
- **OpenTelemetry-JS docs label browser instrumentation as experimental:** *"Client instrumentation for the browser is experimental and mostly unspecified."* Semconv attribute names may shift (the `semconvStabilityOptIn` migration window is open). `url.full` / `http.url` / `http.target` are stable enough for MVP-0 but re-check at MVP-1.
- **W3C Trace Context Level 2 is a Candidate Recommendation Draft.** Adds a `random-trace-id` flag bit; backward-compatible. Both OTel-JS and OTel-.NET emit Level 1 today. Documented for awareness.
- **Vite 8 specifics.** No Vite-8-specific OTel issue surfaced in 2026 research — most known reports (e.g. opentelemetry-js-contrib#1892) reference Vite 4. The fix landed in OTel-JS 2.x via cleaner browser/Node conditional exports. Verify on first build that no `Module "path" has been externalized` warnings appear.
- **No verified 2026 OSS reference combining React 19.2 + Vite 8 + RTK Query 2.x + OTel-web.** Closest precedents (Liteverge, service-foundry, obs-nebula/frontend-react) cover React + Vite + OTel cleanly; the RTK Query × `FetchInstrumentation` coexistence is established by reading source code, not by pointing at a canonical sample app. The combination should still be sound — `fetchBaseQuery` is a thin `globalThis.fetch` wrapper, and `FetchInstrumentation` patches `globalThis.fetch`.
- **The "Phoenix" note in the prompt** is a historical planning artefact (R-051); the shipped collector is `otel/opentelemetry-collector-contrib:0.150.1`. This artefact assumes that fact.
- **Sampling decisions made in the browser are advisory.** A `ParentBasedSampler` on the backend honours the browser's `traceflags=01`; an `AlwaysOnSampler` (current backend default) ignores it. Both produce the same outcome for MVP-0; differences appear only once sampling is dialled down — re-decide then.
- **R-073's error-boundary implementation is out of scope.** This artefact defines the API surface (`getLastTraceId()` / `useLastTraceId()`); the choice of `react-error-boundary` vs hand-rolled vs React 19's `onCaughtError` root-level handler is for that sister prompt.
- **Production deployment topology** (cloud-managed Tempo / Datadog / Honeycomb / SigNoz Cloud) is deferred. The default browser-direct-to-collector pattern works identically against a self-hosted or cloud collector — only the URL and auth header change.