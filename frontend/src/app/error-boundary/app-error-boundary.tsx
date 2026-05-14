// Top-level error boundary. Wraps the application tree in
// `react-error-boundary` and forwards caught errors to the client-error
// reporter alongside a freshly-minted correlation id. The user-facing
// surface lives in `./fallback.component`.

import { useState, type ReactNode } from 'react'
import { ErrorBoundary } from 'react-error-boundary'
import { Fallback } from './fallback.component'
import { reportClientError } from './report-client-error'

export interface AppErrorBoundaryProps {
  children: ReactNode
}

export const AppErrorBoundary = ({ children }: AppErrorBoundaryProps) => {
  // Correlation id lives in wrapper state, not on the Error instance.
  // React 19 StrictMode double-invokes render in development, so
  // `getDerivedStateFromError` and `componentDidCatch` inside
  // `react-error-boundary` can land on different Error instances; we
  // would mutate one and the Fallback would read the other and see
  // `undefined`. A wrapper-local state slot dodges the instance
  // mismatch entirely while still being reset by `resetErrorBoundary`
  // (the `onReset` hook clears it back to null).
  const [correlationId, setCorrelationId] = useState<string | null>(null)

  return (
    <ErrorBoundary
      onError={(raw, info) => {
        // `crypto.randomUUID()` is v4 (RFC 9562 §5.4); always 36 chars
        // with four hyphens. The Fallback slices the first eight hex
        // for the user-readable short id surfaced on the card.
        const nextId = crypto.randomUUID()
        setCorrelationId(nextId)
        const error = raw instanceof Error ? raw : new Error(String(raw))
        reportClientError({
          kind: 'render',
          correlationId: nextId,
          error,
          componentStack: info.componentStack ?? undefined,
        })
      }}
      onReset={() => {
        setCorrelationId(null)
      }}
      fallbackRender={(props) => <Fallback {...props} correlationId={correlationId ?? 'unknown'} />}
    >
      {children}
    </ErrorBoundary>
  )
}
