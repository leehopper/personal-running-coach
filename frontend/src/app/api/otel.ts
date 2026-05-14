// Client-side OpenTelemetry bootstrap. Must be imported BEFORE React renders
// and BEFORE any RTK Query baseQuery is constructed — `FetchInstrumentation`
// monkey-patches the global `fetch`, and `WebTracerProvider.register()`
// installs the W3C TraceContext + Baggage propagators. `main.tsx`'s first
// `import './app/api/otel'` triggers that side-effecting setup.
//
// DEC-069 / R-074 §3. The SPA posts OTLP/HTTP-JSON directly to the local
// collector at :4318; the collector's CORS allow-list (T02) lets the
// preflight through, and its `attributes/scrub` processor (T02) strips
// `http.url` outright as belt-and-braces in case the client-side scrub
// below ever misses an attribute.

import { context, trace } from '@opentelemetry/api'
import type { Span } from '@opentelemetry/api'
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http'
import { registerInstrumentations } from '@opentelemetry/instrumentation'
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch'
import { resourceFromAttributes } from '@opentelemetry/resources'
import { BatchSpanProcessor, WebTracerProvider } from '@opentelemetry/sdk-trace-web'
import { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } from '@opentelemetry/semantic-conventions'
import * as lti from './last-trace-id'

const COLLECTOR_URL = import.meta.env.VITE_OTLP_TRACES_URL ?? 'http://localhost:4318/v1/traces'

const exporter = new OTLPTraceExporter({
  url: COLLECTOR_URL,
  // No `headers:` in dev. MVP-1 prod will add
  // `{ Authorization: 'Bearer <token>' }` per DEC-069's public-rollout
  // posture, gated by a build-time env var the same way `VITE_OTLP_TRACES_URL`
  // is gated above. The bundled token is treated as a rate-limit signal,
  // not a secret (R-074 §2).
})

// `scheduledDelayMillis: 5000` and the batch-size knobs match R-074 §1's
// recommended defaults; they balance trace freshness in Jaeger UI against
// network spend on a slow burst of fetches (e.g. an onboarding session
// that fires ~30 fetches in 60 seconds emits ~1 batch every 5s instead
// of 30 separate POSTs).
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
})

// `provider.register()` installs the default `StackContextManager` plus the
// W3C CompositePropagator (TraceContext + Baggage). RTK Query's
// `fetchBaseQuery` resolves a single `fetch` per request, so the active
// span for an HTTP call is always created inside `FetchInstrumentation.
// patchFetch()` immediately before the call — async context is bounded by
// the instrumentation, not by user code. No `ZoneContextManager` needed.
provider.register()

// Two URLs must be excluded from `FetchInstrumentation`:
//
//   1. `/v1/traces` — the OTLP exporter's own POST. Tracing it would
//      create an infinite recursion of "post traces -> span -> post traces".
//
//   2. `/api/v1/client-errors` — the SPA's React-error-boundary report
//      endpoint. The Fallback shows the support code held by
//      `last-trace-id` (the trace-id of the failed upstream fetch). If
//      this POST were traced, its own completion would call
//      `applyCustomAttributesOnSpan`, which would in turn call
//      `recordLastTraceId` with the client-errors POST's trace-id and
//      OVERWRITE the upstream-cause trace-id — the visible support
//      code would point at the error-report span, not the failure that
//      triggered the boundary.
//
// `\/?$` tolerates a stray trailing slash from a misconfigured
// collector or backend URL. The regexes match both path-only forms
// (`/api/v1/client-errors`) and origin+path forms
// (`https://app.runcoach.io/api/v1/client-errors`).
// `internal export for unit tests`.
export const ignoreUrls: RegExp[] = [/\/v1\/traces\/?$/, /\/api\/v1\/client-errors\/?$/]

// PII scrub Option A (DEC-069 §7). Runs *before* the span is exported,
// so the collector receives a query-string-less and fragment-less
// `http.url` even though the collector also deletes the attribute
// outright as belt-and-braces (T02). Two layers, neither alone
// load-bearing. Also records the trace-id of the just-completed fetch
// into `last-trace-id`, which the Fallback surfaces as a support code.
// Calls `recordLastTraceId` via the module namespace `lti` so unit
// tests can `vi.spyOn(lti, 'recordLastTraceId')`.
// `internal export for unit tests`.
export const applyCustomAttributesOnSpan = (
  span: Span,
  request: Request | RequestInit | string,
): void => {
  lti.recordLastTraceId(span.spanContext().traceId)
  const urlStr = typeof request === 'string' ? request : ((request as Request).url ?? '')
  try {
    const u = new URL(urlStr, window.location.origin)
    span.setAttribute('http.url', `${u.origin}${u.pathname}`)
    span.setAttribute('http.target', u.pathname)
  } catch {
    // Leave the original `http.url` / `http.target` in place if the
    // URL constructor rejects the input (e.g. a malformed
    // `fetch('//bogus')` from a third-party SDK). Better to ship a
    // possibly-unscrubbed attribute than to drop the span — the
    // collector still has its own `attributes/scrub` processor.
  }
}

registerInstrumentations({
  instrumentations: [
    new FetchInstrumentation({
      ignoreUrls,
      clearTimingResources: true,
      applyCustomAttributesOnSpan,
    }),
  ],
})

// Visibility flush: `BatchSpanProcessor` does NOT automatically flush on
// `pagehide` / `visibilitychange` (open-telemetry/opentelemetry-js issue
// #3489). Without this hook, up to 5s of buffered spans can be lost when
// the user closes a tab. `forceFlush()` calls `exporter.export()` with
// the current queue; `OTLPTraceExporter` uses `fetch(..., {keepalive: true})`
// so the request survives the tab teardown. Best-effort — we explicitly
// ignore the returned promise rejection because there is nothing useful
// to do with a flush failure during page unload.
if (typeof document !== 'undefined') {
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') {
      provider.forceFlush().then(
        () => {
          // No-op on success. Buffered spans flushed to the exporter,
          // which uses `fetch(..., {keepalive: true})` so the request
          // survives the tab teardown.
        },
        () => {
          // Silent drop per DEC-069 — telemetry failures must never
          // surface to the user.
        },
      )
    }
  })
}

export const tracer = trace.getTracer('runcoach-frontend')
export { context }
