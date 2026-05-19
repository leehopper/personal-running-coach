// Fallback UI rendered by `<AppErrorBoundary>` when a child throws during
// render. Handles support-code formatting, focus management, and the
// "Try again" / "Reload page" controls.

import { useEffect, useRef } from 'react'
import { type FallbackProps } from 'react-error-boundary'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
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
      className="flex min-h-screen items-center justify-center bg-background px-4 py-12 motion-reduce:transition-none"
    >
      <Card className="w-full max-w-md gap-4 p-6">
        <h1
          ref={headingRef}
          tabIndex={-1}
          className="text-xl font-semibold text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          Something went wrong
        </h1>
        <p className="text-sm text-muted-foreground">
          RunCoach hit an unexpected error while rendering this page. We may send technical
          diagnostics to help investigate, but your training data remains safe.
        </p>
        <p className="text-sm text-muted-foreground">
          Error ID:{' '}
          <code className="select-all rounded bg-muted px-1.5 py-0.5 font-mono text-xs text-foreground">
            {shortId}
          </code>
        </p>
        {traceId !== null && (
          <p className="text-sm text-muted-foreground">
            Support code:{' '}
            <code className="select-all rounded bg-muted px-1.5 py-0.5 font-mono text-xs text-foreground">
              {formatTraceId(traceId)}
            </code>{' '}
            <Button
              type="button"
              variant="outline"
              size="xs"
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
            >
              Copy
            </Button>
          </p>
        )}
        <div className="flex flex-wrap gap-2">
          <Button type="button" size="sm" onClick={resetErrorBoundary}>
            Try again
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => window.location.reload()}
          >
            Reload page
          </Button>
        </div>
        <details className="text-sm text-muted-foreground">
          <summary className="cursor-pointer select-none text-muted-foreground">
            Show error details
          </summary>
          <div className="mt-2 space-y-2">
            <p>
              Full ID:{' '}
              <code className="select-all rounded bg-muted px-1 font-mono text-xs">
                {correlationId}
              </code>
            </p>
            <p>
              <strong className="font-semibold">{error.name}</strong>: {error.message}
            </p>
            {error.stack !== undefined && error.stack.length > 0 && (
              <pre className="max-h-48 overflow-auto whitespace-pre-wrap break-all rounded bg-muted p-2 font-mono text-xs text-muted-foreground">
                {error.stack}
              </pre>
            )}
          </div>
        </details>
      </Card>
    </div>
  )
}
