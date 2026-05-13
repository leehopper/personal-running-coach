// `POST /api/v1/client-errors` is intentionally fire-and-forget. The
// reporter must never throw — if it did, the error boundary's `onError`
// would unwind into the React renderer and we would lose the fallback
// card the user is supposed to see. Three nested guards enforce that:
//
//   1. `fetch(..., { keepalive: true })` survives an immediate
//      `window.location.reload()` from the "Reload page" affordance.
//      The Fetch standard caps the body at 64 KB when `keepalive` is
//      true, which is well above any realistic stack we emit.
//   2. `.catch()` swallows network failures and falls back to
//      `navigator.sendBeacon` — a best-effort UDP-ish queue the
//      browser flushes during page-unload. `sendBeacon` returns
//      `false` when it cannot enqueue (queue full, body too large);
//      we ignore the boolean since there is nothing useful to do with
//      the failure.
//   3. The outer `try { ... } catch { /* swallow */ }` covers
//      everything else: a thrown `TypeError` from `JSON.stringify`
//      against a circular object, a missing `navigator.sendBeacon`
//      polyfill, a same-origin policy throw from the URL constructor.
//
// Note on antiforgery: the endpoint is XSRF-exempt server-side
// (spec § "Antiforgery exemption"). The fallback UI may render before
// the SPA has had a chance to seed the `__Host-Xsrf-Request` cookie
// (e.g. boundary fires on the first paint), so we cannot rely on
// `readXsrfCookie()` from `base-query.ts`. Auth is still enforced via
// the `__Host-Session` cookie carried by `credentials: 'include'`.

export type ClientErrorKind = 'render' | 'window-error' | 'unhandled-rejection'

export interface ReportClientErrorArgs {
  kind: ClientErrorKind
  correlationId?: string
  error: Error
  componentStack?: string
}

const ENDPOINT = '/api/v1/client-errors'

const buildBody = ({
  kind,
  correlationId,
  error,
  componentStack,
}: ReportClientErrorArgs): string => {
  const payload = {
    correlationId: correlationId ?? crypto.randomUUID(),
    occurredAt: new Date().toISOString(),
    kind,
    errorName: error.name,
    message: error.message,
    stack: error.stack ?? '',
    componentStack: componentStack ?? '',
    // Redact query string and fragment to avoid leaking tokens, emails, or
    // other identifiers in error telemetry. Pathname alone is enough to
    // locate the failing route; URL params are reproducible via repro steps.
    url: window.location.origin + window.location.pathname,
    userAgent: navigator.userAgent,
    appVersion: import.meta.env.VITE_APP_VERSION ?? 'unknown',
  }
  return JSON.stringify(payload)
}

const sendBeaconFallback = (body: string): void => {
  try {
    navigator.sendBeacon(ENDPOINT, new Blob([body], { type: 'application/json' }))
  } catch {
    // Best-effort. Nothing useful to do if the browser refuses both
    // fetch and sendBeacon — the recovery card is still rendering.
  }
}

export const reportClientError = (args: ReportClientErrorArgs): void => {
  try {
    const body = buildBody(args)
    fetch(ENDPOINT, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body,
      keepalive: true,
    }).then(
      () => {
        // Ignore the response — fire-and-forget by design.
      },
      () => {
        sendBeaconFallback(body)
      },
    )
  } catch {
    // Outer guard: JSON.stringify can throw on a circular object, and
    // the URL / navigator getters can throw in exotic contexts (file://
    // protocols, sandboxed iframes). We must not propagate from here.
  }
}
