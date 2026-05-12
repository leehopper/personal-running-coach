# Research Prompt: Batch 25b — R-074

# Client-Side OpenTelemetry + W3C `traceparent` Correlation Propagation for a React 19 + RTK Query SPA (OTLP HTTP → otel-collector-contrib → Jaeger, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a React 19.2.x + Vite 8 + RTK Query (Redux Toolkit 2.x) SPA targeting a self-hosted `otel/opentelemetry-collector-contrib` 0.150.x with OTLP HTTP on `:4318` and Jaeger 1.76 as the trace backend, what is the canonical 2026 pattern for (a) instrumenting the browser to emit traces, (b) propagating W3C `traceparent` headers from `fetch` calls so backend ASP.NET Core spans chain under the same trace, and (c) exposing a correlation ID to the React top-level error boundary (R-073) and to user-visible support-flow text — with what bundle-size budget and what CORS / proxy decision?

Pick a default approach plus one fallback. Recommend exact package names, versions known to work on React 19.2 + Vite 8 + RTK Query 2.x + TS-strict, and the deployment-topology decision (browser → collector directly, or browser → backend proxy → collector).

### Sub-questions the artifact must answer

1. **SDK choice.** Compare the leading 2026 options:
    - Full `@opentelemetry/sdk-trace-web` + `@opentelemetry/instrumentation-fetch` + `@opentelemetry/exporter-trace-otlp-http` + `@opentelemetry/context-zone` (or `context-async-hooks`) + `@opentelemetry/propagator-b3` if needed. Bundle size? Tree-shaking story?
    - "Auto-instrumentation web" meta-package (`@opentelemetry/auto-instrumentations-web`) — does it tree-shake usefully, or pull in everything?
    - `@vercel/otel` — a thinner edge-first wrapper, designed for Next.js but reusable. Bundle? Customizability?
    - Sentry's tracing SDK (`@sentry/browser` + `BrowserTracing` integration) — produces OTel-compatible traces and can export via OTLP. Bundle? Vendor lock-in?
    - Pure-fetch alternative: hand-rolled `traceparent`-generating `prepareHeaders` in RTK Query's `baseQuery` + a tiny `POST /v1/traces` client. Bundle is 1–2 KB. What's lost vs an SDK?

    Score on: bundle size delta (gzipped, after tree-shake), tree-shake friendliness with Vite, RTK Query compatibility (the SDK's `fetch` instrumentation must not double-wrap RTK Query's own `fetch` and must coexist with `prepareHeaders`), TS-strict friendliness, maintenance signal.

2. **Deployment topology.** RunCoach's `otel-collector-contrib` exposes OTLP HTTP on `localhost:4318/v1/traces` with **no CORS headers configured on the receiver** (`infra/otel/otel-collector-config.yaml`). For a browser running at `http://localhost:5173` to POST to `4318`, one of three patterns is needed:
    - **(a)** Add CORS to the collector via the `otlphttp` receiver's `cors:` block. Pros / cons? Production-deployment implication (`Access-Control-Allow-Origin: *` is unsafe in prod; what's the canonical pattern — origin allow-list + auth header)?
    - **(b)** Reverse-proxy `/v1/traces` from Vite dev server + ASP.NET Core in prod, so the browser hits a same-origin path. Adds latency? Loses the "collector is the network boundary" property?
    - **(c)** Send traces to a thin backend endpoint (`POST /api/v1/client-traces`) that re-emits them via the backend's existing OTLP exporter. Bundle implication on backend? Sampling-decision loss?

    Pick a default. Note: backend OTLP exporter is gated on `OTEL_EXPORTER_OTLP_ENDPOINT` env var and only runs when the `docker-compose.otel.yml` overlay is up — production-OTel topology is a future decision (R-075 candidate, deferred to MVP-1 deployment planning).

3. **Propagation through RTK Query.** RunCoach's `frontend/src/app/api/base-query.ts` exports `rawBaseQuery = fetchBaseQuery({ baseUrl: '/api', credentials: 'include', prepareHeaders })`. `prepareHeaders` only injects `X-XSRF-TOKEN` on mutations. To propagate W3C TraceContext, the headers `traceparent` (and optionally `tracestate`, `baggage`) need to be injected per-request, starting from the current active span. Two patterns:
    - The OTel `instrumentation-fetch` wraps the global `fetch`, injects headers transparently, and creates a span per request. Does this play with RTK Query's `fetchBaseQuery` (which uses the same global `fetch`)? Any double-wrap issues? Order-of-init concerns?
    - Hand-rolled: `prepareHeaders` calls `propagation.inject(context.active(), headers, defaultTextMapSetter)` per request, with an explicit `tracer.startActiveSpan(...)` wrapping the call site. More verbose but explicit.

    Which is the 2026 default? Show the actual code at the RTK Query seam. Does the OTel-fetch span auto-propagate to a child span when the response triggers an async re-render?

4. **Backend chaining.** Backend ASP.NET Core uses default OTel propagators (W3C TraceContext + Baggage — no `Sdk.SetDefaultTextMapPropagator(...)` call). Backend `ActivitySource` names registered: `Marten`, `Wolverine`, `RunCoach.Llm`, `Npgsql`, plus `AddAspNetCoreInstrumentation()`. Confirm: a `traceparent` header from the browser will chain into `AspNetCoreInstrumentation`'s automatically-created span, which is the parent of any subsequent custom-`ActivitySource` span in the request lifecycle. Anything that would silently break this (sampling decisions, baggage size limits)?

5. **Correlation-ID surfacing.** R-073's error boundary needs to display a correlation ID on the fallback card ("Show this code to support: `4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01`"). Two options for what to display:
    - The full `traceparent` value (`{version}-{trace-id}-{parent-id}-{flags}`) from the most recent fetch.
    - Just the `trace-id` (16-byte hex, 32 chars) — shorter, still uniquely identifies the trace in Jaeger UI.

    What's the conventional UX? Length budget for a user-visible code on the error card? Should it be copy-clickable? Does the error boundary have access to the active trace context at render time, or does it need a `traceparent`-capture hook installed in `prepareHeaders` that stashes the last-seen value somewhere (Redux? a ref? `localStorage`?)? Show the seam.

6. **Bundle-size budget.** Vite production build of `frontend` currently ships React 19 + RTK + Router + Tailwind. What's the actual gzipped delta of adding the recommended SDK? Quantify for: (a) full `sdk-trace-web` + `instrumentation-fetch` + OTLP-HTTP exporter; (b) the recommended default if smaller. Tree-shaking config notes (Vite's Rollup is mostly automatic; any explicit `optimizeDeps` or chunking needed?).

7. **Sampling and PII.** RunCoach's onboarding traces include user free-text answers (running history, injuries, schedule). The backend already applies a containment-first `LayeredPromptSanitizer` (DEC-059) before LLM calls, but the **client-side `traceparent` capture has no sanitization layer** — any HTTP attribute the SDK auto-records (URL, query string, headers) could leak PII into Jaeger. What's the 2026 canonical pattern to scrub HTTP attributes on the client SDK before export? `applyCustomAttributesOnSpan` hook on `instrumentation-fetch`? A `SpanProcessor` that mutates `setAttribute` calls?

8. **Privacy / GDPR.** What's the recommended posture for client-side OTel in 2026 — opt-in via cookie consent, opt-out, defer-to-DNT, always-on with PII scrubbing? RunCoach is currently personal-use only (no public users), but the answer locks in a default for MVP-1's public-tester cohort.

9. **Failure modes.** What happens when the collector is down (OTLP exporter retry/backoff)? When the network is offline? When the browser tab is backgrounded (mid-trace)? When the user is on a slow connection (synchronous-flush-on-page-hide concerns)? The SDK's `BatchSpanProcessor` configuration knobs.

10. **Existing precedents.** What 2026 open-source React 19 + Vite + OTel-web SPAs publish their full setup? Bonus: any using RTK Query with the OTel fetch instrumentation co-existing.

## Context

I'm planning Slice 1B of the MVP-0 cycle for RunCoach, an AI running coach. Slice 1B is a **hardening slice** (not a feature slice) that closes the structural fragilities surfaced during Slice 1's debugging. One of the four acceptance criteria is:

> The React app survives a child render-time exception with a top-level error boundary that logs and renders a recovery affordance instead of a blank screen.

R-073 (queued separately, sister prompt) owns the error-boundary library + recovery UX + Playwright forcing-throw test. R-074 owns the client-side OTel layer that R-073's logging seam upgrades into. R-073's boundary ships first with a "POST `/api/v1/client-errors` with cookie auth" logging shape; R-074's recommendation upgrades that to OTel-instrumented traces that chain into backend spans.

**Current state (verified 2026-05-11):**

- `frontend/package.json` — React 19.2.6, Redux Toolkit 2.11.2, React Router 7.15.0, TypeScript 5.9.3, Vite 8.0.11. **Zero `@opentelemetry/*` packages** installed (one transitive optional peer-dep declared by vitest, not installed).
- `frontend/src/app/api/base-query.ts` — single shared `rawBaseQuery = fetchBaseQuery({ baseUrl: '/api', credentials: 'include', prepareHeaders })`. `prepareHeaders` only injects `X-XSRF-TOKEN` on mutations. Wrapped by `baseQueryWith401Handler` which dispatches `loggedOut()` on 401. Single `apiSlice` (`api-slice.ts`) consumed by all four `*.api.ts` modules via `injectEndpoints`. `tagTypes: ['Auth', 'Onboarding', 'Plan']`.
- `frontend/src/main.tsx` — `createRoot(root).render(<StrictMode><App /></StrictMode>)`. No OTel initialization.
- `frontend/src/app/modules/app/app.component.tsx` — declarative `<BrowserRouter>` + `<Routes>` (NOT data-router).
- Cookie-based auth: `credentials: 'include'`, `__Host-` cookies, XSRF token in `__Host-Xsrf-Request` cookie echoed in `X-XSRF-TOKEN` header on mutations.

**Backend OTel state (verified 2026-05-11):**

- `backend/src/RunCoach.Api/Program.cs:260-298` registers a single `AddOpenTelemetry()` pipeline with resource `serviceName: "runcoach-api"`, `serviceVersion: "0.1.0"`.
- `AddSource` list: `"Marten"`, `"Wolverine"`, `"RunCoach.Llm"`, `"Npgsql"` (plus `AddAspNetCoreInstrumentation()` + `AddHttpClientInstrumentation()`).
- Same four names registered as Meters.
- OTLP exporter is gated on env var `OTEL_EXPORTER_OTLP_ENDPOINT` — when unset, exporter is omitted entirely (default for normal dev `dotnet run` without the OTel overlay).
- Concrete `ActivitySource` instances live at `PlanGenerationService.cs:132` and `LayeredPromptSanitizer.cs:31` (both `"RunCoach.Llm"`).
- **No `Sdk.SetDefaultTextMapPropagator(...)` call** anywhere — defaults apply (W3C TraceContext + Baggage are the OTel-.NET defaults).

**Collector wire (verified 2026-05-11):**

- `docker-compose.otel.yml` runs `otel/opentelemetry-collector-contrib:0.150.1` + `jaegertracing/all-in-one:1.76.0`. **(Note: an earlier project-internal note referred to "Phoenix" — that was a planning candidate from R-051 LLM-observability research, not the actual implementation. The shipped collector is `otel-collector-contrib`, not Phoenix.)**
- Host ports: OTLP gRPC on `4317`, OTLP HTTP on `4318`. No auth headers.
- `infra/otel/otel-collector-config.yaml` binds both OTLP receivers on `0.0.0.0`, runs a `batch` processor (5s / 1024), and exports traces to `otlp/jaeger` (insecure) + `debug`. Metrics go only to `debug`.
- **No CORS configuration on the receiver.** A browser POSTing directly to `http://localhost:4318/v1/traces` from `http://localhost:5173` will be blocked by the browser's preflight unless CORS is added or the request is proxied.

**Slice-1B acceptance criteria touched by R-074:**

- The error boundary's correlation ID (R-073) needs to be a value that a developer can paste into Jaeger UI and find the corresponding backend trace — meaning client and backend MUST chain under one trace, which means `traceparent` propagation MUST work end-to-end. This is R-074's load-bearing piece.

## Why It Matters

Without client-side OTel:
- Every backend trace in Jaeger starts at the ASP.NET Core ingress span. A user-reported error has no client-side parent — the developer cannot correlate "what was the user doing right before the 500?" with "what was the backend doing?" except by timestamp + manual correlation.
- The R-073 error boundary's correlation ID becomes a synthetic UUID with no link to backend telemetry. Useful for support workflow, useless for debugging.
- Slice 2's workout-logging flow (form → POST → background-LLM-context-extract → projection update) has at least four separately-traced backend hops. Without a parent client span, the four backend spans show as four unrelated traces.

The structural fix is industry-standard 2026 practice, but the .NET-backend / browser-client variant is less well-documented than Node-backend / browser-client. Picking the wrong default means a refactor in Slice 4 (when streaming responses will need their own span shape) or in MVP-1 (when public testers will demand a sampling strategy that excludes their PII from traces).

The CORS / proxy decision is the highest-uncertainty piece — the collector's "no CORS" default is documented but the canonical 2026 fix isn't (`cors:` block in the receiver vs proxy-via-backend vs SDK-level batching to a same-origin endpoint).

## Deliverables

- **Recommended SDK + version** with rationale: exact package names, gzipped bundle delta on Vite 8 production build, RTK-Query-compat verdict. One default + one fallback (where "fallback" can be "stay pure-fetch + hand-rolled `traceparent` injection if the SDK's bundle is too large").
- **Deployment-topology decision** (browser → collector vs browser → backend proxy → collector) with rationale. If "browser → collector," include the exact CORS `cors:` block for the `otlphttp` receiver and an "in prod this becomes an origin allow-list" note. If "proxy-via-backend," include the ASP.NET Core endpoint sketch (POST `/api/v1/client-traces`, body = OTLP JSON, re-emit via existing exporter).
- **RTK Query seam** code: where the SDK init goes, what `prepareHeaders` looks like after, whether `fetchBaseQuery` needs replacement or wrapping, the order of init relative to `createRoot(...)`.
- **Correlation-ID surfacing** code: how R-073's error boundary reads the current trace ID at render time. Last-seen-`traceparent` stash mechanism (Redux slice? `useContext` provider? module-level singleton?). The UX format (full traceparent vs trace-id-only, length, copy-to-clipboard).
- **Backend chaining verification** approach: a smoke test (curl + Jaeger UI check) that proves a browser-initiated request produces a single trace with both client and backend spans under it.
- **PII / sanitization** layer: which client-side hook scrubs which attributes, with the code shape (`applyCustomAttributesOnSpan` or `SpanProcessor.onStart`).
- **Sampling default** for MVP-0 (personal use): always-on, head-based 100%, or some basic always-on-error + 10% always-on-success?
- **Production-readiness notes**: what changes when this ships beyond personal use — CORS allow-list, auth header to collector, sampling rate, PII posture.
- **Bundle audit** with exact numbers if findable, qualitative comparison otherwise.
- **Failure-mode behavior**: what the user sees when traces fail to export. Should be invisible (silent drop) — confirm the SDK's default.
- **Reference precedent** — at least one 2026 open-source React 19 + Vite + OTel-web project, with a link.
- **Migration cost** estimate from current state to recommendation. Lines changed, new deps, build-config touches, backend config touches.

## Out of scope

- Backend ASP.NET Core OTel configuration changes beyond confirming default propagators work. Backend OTel is locked per existing wire; R-074 just lives on top.
- LLM-specific observability (R-051 covered Phoenix-self-hosted as a candidate for LLM-trace UI; that decision is independent of the general-purpose client-side OTel question R-074 answers).
- Production deployment topology — RunCoach's production OTel target (cloud-managed Tempo? Datadog? Honeycomb?) is a pre-MVP-1 decision (queued as a future research item, not R-074). R-074 must land a recommendation that ports cleanly to whatever production target lands later.
- Metrics and logs — Slice 1B touches traces only. Client metrics (Web Vitals, custom counters) are deferred.
- The R-073 error-boundary library / router-integration / Playwright pattern — sister prompt.
- The error-endpoint persistence and retention design — implementation detail of the spec session, not research.

The artifact lands at `docs/research/artifacts/batch-25b-react19-client-otel-correlation-id.md` and integrates into the Slice 1B spec plus a new DEC entry locking the SDK choice + deployment topology + propagation pattern.
