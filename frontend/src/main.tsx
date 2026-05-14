import './app/api/otel' // MUST stay the first import — FetchInstrumentation patches the global `fetch` and registers the W3C propagators before any React or RTK Query code runs.
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './app/modules/app/app.component'
import { reportClientError } from './app/error-boundary/report-client-error'
import './index.css'

const root = document.getElementById('root')
if (!root) throw new Error('Root element not found')

// Two-layer render-error reporting:
// 1. `<AppErrorBoundary>`'s own `onError` prop calls `reportClientError` for
//    every error the boundary catches (react-error-boundary fires this once
//    per error with a caller-supplied correlation ID).
// 2. `onUncaughtError` fires only when no boundary catches the error at all
//    (the renderer then unmounts the tree). `onCaughtError` is intentionally
//    omitted — it fires for every boundary catch and would duplicate the
//    report already sent by `<AppErrorBoundary>`'s `onError`.
const toError = (value: unknown): Error =>
  value instanceof Error ? value : new Error(String(value))

createRoot(root, {
  onUncaughtError: (error, info) => {
    reportClientError({
      kind: 'render',
      error: toError(error),
      componentStack: info.componentStack ?? undefined,
    })
  },
}).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
