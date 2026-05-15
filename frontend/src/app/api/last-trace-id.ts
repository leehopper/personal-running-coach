import { useSyncExternalStore } from 'react'

// Module-level singleton fed by `FetchInstrumentation.applyCustomAttributesOnSpan`
// in `./otel.ts`. Holds the W3C trace-id (32 hex) of the most recently
// completed fetch, so `<Fallback>` can surface a support code when the user
// hits the error boundary AFTER a fetch has fired. Render errors with no
// preceding fetch leave `current === null` and the Fallback hides the
// support-code block entirely (correct UX — there is nothing to look up).
//
// Why a module singleton + `useSyncExternalStore` and not Redux / Context:
// - The store sees a write on *every* fetch (~tens per onboarding); a
//   Redux dispatch would re-render the entire connected subtree on each
//   write. `useSyncExternalStore` only re-renders subscribers — which is
//   the single `<Fallback>` consumer.
// - The error boundary may be a class component (`react-error-boundary`'s
//   internal `ErrorBoundary` class) that cannot call hooks. `getLastTraceId`
//   stays callable from a class via prop drilling without the React
//   hook constraint.
// - Tear-down is cheap: the Set holds at most one listener (the Fallback
//   on a mounted boundary). No memory pressure.
//
// DEC-069 / R-074 §5.

type Listener = () => void

let current: string | null = null
const listeners = new Set<Listener>()

/**
 * Called from `FetchInstrumentation.applyCustomAttributesOnSpan` on every
 * fetch span completion. Idempotent on identical input so a burst of
 * same-trace spans (e.g. an RTK Query fan-out) only notifies once.
 */
export const recordLastTraceId = (traceId: string): void => {
  if (traceId === current) return
  current = traceId
  for (const listener of listeners) listener()
}

/** Synchronous accessor; safe to call from class-component render. */
export const getLastTraceId = (): string | null => current

const subscribe = (listener: Listener): (() => void) => {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

/**
 * React hook for the Fallback. `useSyncExternalStore`'s server-snapshot
 * always returns null because the trace-id is by definition client-only
 * (no SPA SSR in MVP-0; if RSC arrives later, the hook still trivially
 * returns null during hydration). The trace-id appears on the first
 * post-fetch render and updates in place on subsequent fetches.
 */
export const useLastTraceId = (): string | null =>
  useSyncExternalStore(subscribe, getLastTraceId, () => null)

/**
 * Test-only reset. Production code must never call this — it would clear
 * a valid trace-id mid-session. Exported with a `__` prefix so the
 * intent is obvious in call-site grep.
 */
export const __resetLastTraceIdForTests = (): void => {
  current = null
  for (const listener of listeners) listener()
}
