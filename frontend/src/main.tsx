import './app/api/otel' // MUST stay the first import — FetchInstrumentation patches the global `fetch` and registers the W3C propagators before any React or RTK Query code runs.
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './app/modules/app/app.component'
import { reportClientError } from './app/error-boundary/report-client-error'
import './index.css'

const root = document.getElementById('root')
if (!root) throw new Error('Root element not found')

// React 19 root-options reporters (DEC-068 §10.4 belt-and-suspenders).
// `onCaughtError` fires on every render-time error a boundary catches —
// even a future deeper boundary that <AppErrorBoundary> never sees;
// `onUncaughtError` fires when no boundary catches the error at all
// (the renderer then unmounts the tree). Both forward to
// `reportClientError` so every render-time failure reaches
// `POST /api/v1/client-errors` regardless of which layer ultimately
// catches it.
const toError = (value: unknown): Error =>
  value instanceof Error ? value : new Error(String(value))

createRoot(root, {
  onCaughtError: (error, info) => {
    reportClientError({
      kind: 'render',
      error: toError(error),
      componentStack: info.componentStack ?? undefined,
    })
  },
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
