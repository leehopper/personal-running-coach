import { useEffect } from 'react'
import { reportClientError } from './report-client-error'

// Companion hook to `<AppErrorBoundary>`. The boundary catches *render-time*
// errors only — anything thrown from a `setTimeout`, an event handler, or
// a rejected promise bypasses React entirely and reaches the global window
// hooks instead. This hook log-only-reports those: it must NOT trigger the
// fallback UI, because (a) the React tree is still healthy and (b) async
// failures (e.g. a stale RTK Query rejection during page-unload) are not
// the user's problem. Code paths that *want* an async error to surface
// the boundary call `useErrorBoundary().showBoundary(error)` explicitly.
//
// React 19 fires `onCaughtError`/`onUncaughtError` on `createRoot` for the
// boundary-caught and uncaught render-error layers; this hook is the
// third layer for anything React never sees (DEC-068 §10.4 three-layer
// defence).

export const useGlobalErrorReporter = (): void => {
  useEffect(() => {
    const onError = (event: ErrorEvent): void => {
      const error =
        event.error instanceof Error ? event.error : new Error(event.message || 'window error')
      reportClientError({ kind: 'window-error', error })
    }

    const onUnhandledRejection = (event: PromiseRejectionEvent): void => {
      const error = event.reason instanceof Error ? event.reason : new Error(String(event.reason))
      reportClientError({ kind: 'unhandled-rejection', error })
    }

    // forwards every `window.error` and `unhandledrejection`, including third-party / extension
    // noise. filtering is deferred — when added, update the matching tests in
    // `use-global-error-reporter.spec.ts`.
    window.addEventListener('error', onError)
    window.addEventListener('unhandledrejection', onUnhandledRejection)
    return () => {
      window.removeEventListener('error', onError)
      window.removeEventListener('unhandledrejection', onUnhandledRejection)
    }
  }, [])
}
