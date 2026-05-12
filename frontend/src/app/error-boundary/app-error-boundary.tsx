import { useEffect, useRef, useState, type ReactNode } from 'react'
import { ErrorBoundary, type FallbackProps } from 'react-error-boundary'
import { reportClientError } from './report-client-error'

// `role="alert"` without `aria-live` — some screen readers double-announce
// when both are present (DEC-068 §3.4). The heading carries `tabIndex={-1}`
// so the focus management `useEffect` can move keyboard focus onto it as
// soon as the fallback mounts.
const SHORT_ID_LEN = 8

// `react-error-boundary` v6 types `error` as `unknown` to mirror what
// `catch` blocks see at runtime — any value can be thrown. We coerce to
// an Error shape so the Fallback can read name / message / stack.
interface ErrorShape {
  name: string
  message: string
  stack?: string
}

const toErrorShape = (value: unknown): ErrorShape => {
  if (value instanceof Error) return value
  return { name: 'UnknownError', message: String(value) }
}

interface FallbackInternalProps extends FallbackProps {
  correlationId: string
}

const Fallback = ({ error: raw, resetErrorBoundary, correlationId }: FallbackInternalProps) => {
  const headingRef = useRef<HTMLHeadingElement>(null)

  useEffect(() => {
    headingRef.current?.focus()
  }, [])

  const error = toErrorShape(raw)
  const shortId = correlationId.slice(0, SHORT_ID_LEN)

  return (
    <div
      role="alert"
      data-testid="app-error-boundary"
      className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-12 motion-reduce:transition-none"
    >
      <div className="w-full max-w-md space-y-4 rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <h1
          ref={headingRef}
          tabIndex={-1}
          className="text-xl font-semibold text-slate-900 outline-none focus-visible:ring-2 focus-visible:ring-slate-900"
        >
          Something went wrong
        </h1>
        <p className="text-sm text-slate-700">
          RunCoach hit an unexpected error while rendering this page. Your data is safe — this
          hasn’t been sent anywhere.
        </p>
        <p className="text-sm text-slate-700">
          Error ID:{' '}
          <code className="select-all rounded bg-slate-100 px-1.5 py-0.5 font-mono text-xs text-slate-900">
            {shortId}
          </code>
        </p>
        {/* T03.4 slot: <TraceIdRow /> rendered here once `useLastTraceId()`
            lands, showing the OTel trace-id formatted xxxxxxxx-xxxxxxxx-
            xxxxxxxx-xxxxxxxx with a copy button. Gate on
            `traceId !== null` to hide the row when no fetch has happened
            yet (DEC-069 / R-074 §5). */}
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={resetErrorBoundary}
            className="rounded bg-slate-900 px-3 py-1.5 text-sm font-medium text-white transition-colors duration-200 ease-out hover:bg-slate-700 motion-reduce:transition-none"
          >
            Try again
          </button>
          <button
            type="button"
            onClick={() => window.location.reload()}
            className="rounded border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition-colors duration-200 ease-out hover:bg-slate-100 motion-reduce:transition-none"
          >
            Reload page
          </button>
        </div>
        <details className="text-sm text-slate-700">
          <summary className="cursor-pointer select-none text-slate-500">
            Show error details
          </summary>
          <div className="mt-2 space-y-2">
            <p>
              Full ID:{' '}
              <code className="select-all rounded bg-slate-100 px-1 font-mono text-xs">
                {correlationId}
              </code>
            </p>
            <p>
              <strong className="font-semibold">{error.name}</strong>: {error.message}
            </p>
            {error.stack !== undefined && error.stack.length > 0 && (
              <pre className="max-h-48 overflow-auto whitespace-pre-wrap break-all rounded bg-slate-50 p-2 font-mono text-xs text-slate-700">
                {error.stack}
              </pre>
            )}
          </div>
        </details>
      </div>
    </div>
  )
}

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
        // crypto.randomUUID() is v4 (RFC 9562 §5.4); always 36 chars
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
