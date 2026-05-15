// Fallback UI rendered by `<AppErrorBoundary>` when a child throws during
// render. Handles support-code formatting, focus management, and the
// "Try again" / "Reload page" controls.

import { useEffect, useRef } from 'react'
import { type FallbackProps } from 'react-error-boundary'
import { useLastTraceId } from '~/api/last-trace-id'

// Format a 32-hex W3C trace-id as `xxxxxxxx-xxxxxxxx-xxxxxxxx-xxxxxxxx`
// (DEC-069 / R-074 §5). The 8-char grouping makes the support code
// easier to read aloud over a phone call and easier to copy by hand
// if the clipboard write fails.
const TRACE_ID_LEN = 32
const TRACE_GROUP_REGEX = /.{1,8}/g
const formatTraceId = (traceId: string): string => {
  if (traceId.length !== TRACE_ID_LEN) return traceId
  return (traceId.match(TRACE_GROUP_REGEX) ?? [traceId]).join('-')
}

const SHORT_ID_LEN = 8

// `react-error-boundary` v6 types `error` as `unknown` since any value
// can be thrown; coerce so the Fallback can read name/message/stack.
interface ErrorShape {
  name: string
  message: string
  stack?: string
}

const toErrorShape = (value: unknown): ErrorShape => {
  if (value instanceof Error) return value
  return { name: 'UnknownError', message: String(value) }
}

export interface FallbackInternalProps extends FallbackProps {
  correlationId: string
}

export const Fallback = ({
  error: raw,
  resetErrorBoundary,
  correlationId,
}: FallbackInternalProps) => {
  const headingRef = useRef<HTMLHeadingElement>(null)
  // `useLastTraceId` is fed by the OTel `FetchInstrumentation` span hook;
  // it returns `null` when no fetch has fired since boot (the right UX for
  // a render error with no preceding HTTP context).
  const traceId = useLastTraceId()

  useEffect(() => {
    headingRef.current?.focus()
  }, [])

  const error = toErrorShape(raw)
  const shortId = correlationId.slice(0, SHORT_ID_LEN)

  // `role="alert"` without `aria-live` — some screen readers double-announce
  // when both are present (DEC-068 §3.4). The heading carries `tabIndex={-1}`
  // so the focus management `useEffect` can move keyboard focus onto it as
  // soon as the fallback mounts.
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
          RunCoach hit an unexpected error while rendering this page. We may send technical
          diagnostics to help investigate, but your training data remains safe.
        </p>
        <p className="text-sm text-slate-700">
          Error ID:{' '}
          <code className="select-all rounded bg-slate-100 px-1.5 py-0.5 font-mono text-xs text-slate-900">
            {shortId}
          </code>
        </p>
        {traceId !== null && (
          <p className="text-sm text-slate-700">
            Support code:{' '}
            <code className="select-all rounded bg-slate-100 px-1.5 py-0.5 font-mono text-xs text-slate-900">
              {formatTraceId(traceId)}
            </code>{' '}
            <button
              type="button"
              onClick={() => {
                // `navigator.clipboard.writeText` requires HTTPS and a
                // user gesture in some browsers; it's a nice-to-have.
                // The `select-all` styling on the `<code>` block above
                // lets the user fall back to manual copy on failure.
                navigator.clipboard?.writeText(traceId).then(
                  () => {},
                  () => {},
                )
              }}
              aria-label="Copy support code"
              className="rounded border border-slate-300 px-1.5 py-0.5 text-xs font-medium text-slate-700 transition-colors duration-200 ease-out hover:bg-slate-100 motion-reduce:transition-none"
            >
              Copy
            </button>
          </p>
        )}
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
