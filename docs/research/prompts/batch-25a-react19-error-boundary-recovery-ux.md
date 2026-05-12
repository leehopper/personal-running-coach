# Research Prompt: Batch 25a — R-073

# React 19 + React Router 7 Top-Level Error-Boundary Strategy with Recovery UX (Vite SPA, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a React 19.2.x + React Router 7.15.x Vite 8 SPA with TypeScript-strict, what is the canonical 2026 pattern for a **top-level error boundary** that (a) catches render-time and lifecycle exceptions across the entire component tree, (b) renders a recovery affordance instead of a blank screen, (c) integrates cleanly with React Router 7's two routing APIs (`<BrowserRouter>` + `<Routes>` declarative vs `createBrowserRouter` data-router), and (d) is exercised by a Playwright test that deliberately forces a child render-time throw and asserts the boundary catches it?

Pick a default approach plus one fallback. Recommend exact package names, versions known to work on React 19.2 + RR7.15 + TS-strict, and the migration cost from a declarative `<BrowserRouter>` setup (current) to whatever the recommendation requires.

### Sub-questions the artifact must answer

1. **Library choice.** Compare the leading 2026 options:
    - Hand-rolled class component with `static getDerivedStateFromError` + `componentDidCatch` — the React-docs canonical pattern. Maintenance cost? React 19 `onCaughtError` / `onUncaughtError` root-options interaction?
    - `react-error-boundary` (Kent C. Dodds, 5.x line) — `ErrorBoundary` component + `useErrorBoundary` hook + `withErrorBoundary` HOC + `FallbackComponent` render prop. Bundle size? React 19 compatibility (the lib's last release date and any 19-specific issues)?
    - React 19's built-in `onCaughtError` / `onUncaughtError` on `createRoot(...)` / `hydrateRoot(...)` — does this replace the class-component pattern, or sit alongside it? What does it catch that boundaries miss / vice versa?
    - React Router 7's `errorElement` route property (data-router only) — when does it complement vs replace a top-level boundary?

    Score each on: API ergonomics, recovery-affordance expressiveness (can `reset` be a function, not just "remount"?), TS-strict friendliness, bundle delta, maintenance signal.

2. **Router integration.** RunCoach currently uses declarative `<BrowserRouter>` + `<Routes>` + `<Route>` in `frontend/src/app/modules/app/app.component.tsx`. React Router 7's `errorElement` route property is **only available on `createBrowserRouter` / data-routers**, not on the declarative `<Routes>` API. Does Slice 1B need to migrate to the data-router to get route-level error handling, or is a single app-root boundary sufficient and the migration deferred to a future slice? What surface is lost without route-level error handling?

3. **Recovery UX.** What 2026 UX patterns work best for SPA render-error recovery? Compare:
    - Hard reload (`window.location.reload()`) — simplest, throws away client state but is the safest reset
    - Soft reset (clear the error, re-render the tree from the boundary down) — requires `react-error-boundary`'s `reset` semantics or a hand-rolled `key`-bump pattern; client state survives but may itself be the cause of the throw
    - Navigate-home (`navigate('/', { replace: true })`) — preserves session, may put the user back where the throw happened on the next interaction
    - Inline retry (an "X" close on the error card, render the children again) — only safe when the error is known to be transient (network)

    Which is the right default for a coaching-app SPA where the user has session state (active onboarding, chat transcript)? What's the conventional copy ("Something went wrong" vs more specific)? Show one or two production examples (Linear, Notion, GitHub web UI) of how the affordance is presented.

4. **Correlation with backend errors.** The boundary needs a correlation ID so a user-reported "I got the error card" can be mapped to backend traces. Three options: client-generated UUID, captured `traceparent` from a recent fetch's response headers, or captured-on-throw from an OpenTelemetry span. R-074 owns the OTel question; R-073 just needs to confirm what the boundary should *include in the rendered card* and *attach to the logged error*. Format? Length? Show on screen verbatim or under a "details" disclosure?

5. **Logging on catch.** Boundary's `componentDidCatch` (or `react-error-boundary`'s `onError`) fires once per caught error. What's logged, where, when there's no client-side OTel yet? Options: `console.error` (dev-only utility), POST to a backend `/api/v1/client-errors` endpoint (CORS-free, leverages existing cookie auth), a window.onerror+window.onunhandledrejection global hook in parallel. RunCoach has zero client logging today. R-074 will decide the OTel-instrumented version; R-073 needs to lock the **MVP-0** logging shape so the boundary can ship before R-074's recommendation is implemented.

6. **Playwright test pattern.** How do you deliberately force a child render-time throw without polluting production code? Common patterns:
    - A `?throw=render` query-string the dev/test build honors, gated behind `import.meta.env.DEV`
    - A test-only route `/__test/throw` registered when `VITE_TEST_HARNESS=1` is set
    - A test-only React component imported in development that throws when a prop is set
    - Cypress / Playwright's network mock returning an unparseable payload, letting a parse error surface as a throw inside RTK Query

    Pick the cleanest 2026 pattern. The test must (a) navigate, (b) trigger the throw, (c) assert the boundary's fallback is rendered, (d) assert the recovery affordance works (click "reload" or "retry"). Show the Playwright spec sketch.

7. **What the boundary cannot catch.** React error boundaries do NOT catch: errors in event handlers, errors in async code (setTimeout, requestAnimationFrame, fetch.then), errors thrown during server-side rendering, errors thrown in the error boundary itself. RunCoach is client-only Vite SPA so SSR isn't a factor, but the event-handler / async gap is real. What's the 2026 pattern to fill the gap — `window.addEventListener('error', ...)` + `window.addEventListener('unhandledrejection', ...)` in a top-level `useEffect`, or `react-error-boundary`'s `useErrorBoundary().showBoundary(error)` called from async code?

8. **TypeScript ergonomics under strict mode.** The `ErrorInfo` type from React 19 has `componentStack: string | null` — null when React can't compute it. What's the conventional way to handle that in a strict-null-checks codebase without `!` assertions? `react-error-boundary`'s exported types?

9. **Existing precedents.** What 2026 production-grade React 19 + Vite + Router 7 SPAs publish their error-boundary setup (open-source examples or detailed blog posts)? Bonus: any using `react-error-boundary` 5.x with React 19 successfully (the lib's repo activity / issues).

## Context

I'm planning Slice 1B of the MVP-0 cycle for RunCoach, an AI running coach. Slice 1B is a **hardening slice** (not a feature slice) that closes the structural fragilities surfaced during Slice 1's debugging. One of the four acceptance criteria is:

> The React app survives a child render-time exception with a top-level error boundary that logs and renders a recovery affordance instead of a blank screen. The boundary's recovery path is tested via Playwright (forcing a child throw should not crash the shell).

**Current state (verified 2026-05-11):**

- `frontend/package.json` — React 19.2.6, React Router 7.15.0, TypeScript 5.9.3, Vite 8.0.11. Zero `@opentelemetry/*` packages. Zero `react-error-boundary`. Zero `Sentry` / `LogRocket` / similar.
- `frontend/src/app/modules/app/app.component.tsx` — uses declarative `<BrowserRouter>` + `<Routes>` + `<Route>` (NOT `createBrowserRouter` data-router). Root element is `<Provider store={store}><BrowserRouter><AppShell /></BrowserRouter></Provider>`. Zero `errorElement` or `ErrorBoundary` route props anywhere in src.
- `frontend/src/main.tsx` — `createRoot(root).render(<StrictMode><App /></StrictMode>)`. No `onCaughtError` / `onUncaughtError` options.
- **No render-throw fallback UI exists today.** A render-time throw crashes the entire shell to a blank screen.
- **No shared `ErrorBanner` / `FormError` / toast component exists.** Each page implements its own inline error pattern (login/register use `data-testid="form-alert"`, onboarding uses `data-testid="onboarding-bootstrap-error"`, regenerate-plan dialog uses local `useState`).
- **Zero client-side logging today.** No `console.error` in non-test source, no `window.onerror`, no `window.addEventListener('unhandledrejection', ...)`. The only error sink is RTK Query's `loggedOut()` dispatch on 401 in `frontend/src/app/api/base-query.ts:65-70`.
- Backend exposes OTLP HTTP at `localhost:4318/v1/traces` (no auth, no CORS configured on the receiver). Backend OTel is gated on `OTEL_EXPORTER_OTLP_ENDPOINT` env var and only when the `docker-compose.otel.yml` overlay is run. Backend uses default propagators (W3C TraceContext + Baggage).
- A separate research prompt (R-074) is queued for client-side OTel + correlation-ID propagation through RTK Query. This prompt (R-073) **must not assume R-074 has been implemented** — the boundary needs to ship in Slice 1B before the OTel work lands, with a logging shape that R-074 can later upgrade in place.

## Why It Matters

A render-time throw today produces a blank white screen with no recovery path. The user has lost their place mid-onboarding, mid-chat, or mid-plan-regeneration. The structural fix is a single top-level error boundary, but the *library choice* and *recovery semantics* have real tradeoffs that compound across Slices 2–4 as the surface grows. Picking the wrong default means refactoring it in Slice 4 or pre-public-release.

The bigger risk is shipping the boundary without a logging seam — if it catches errors but the developer has no signal that it caught them in production, the boundary becomes a *worse* outcome than the blank screen (the bug exists, the user is stuck on the fallback, but no one knows). The MVP-0 logging shape (a POST to a backend endpoint, leveraging cookie auth and dodging CORS) is the load-bearing piece.

The Playwright forcing-throw test is the only non-trivial part: too-clever test plumbing leaks into production, too-naive plumbing produces a flaky test.

## Deliverables

- **Recommended approach** with rationale: library choice (or hand-rolled), router integration decision (declarative-with-app-root-boundary vs migrate-to-data-router), recovery affordance default. One default + one fallback.
- **MVP-0 logging shape** that ships in Slice 1B: where the boundary POSTs the error, what the payload looks like (correlation-ID format, error name, message, componentStack), what the backend does with it (R-074 will upgrade this to OTel; R-073 just locks the seam).
- **Recovery UX** sketch: copy, structure (full-page vs banner), the affordance (button text, what clicking it does), accessibility (`role="alert"`, focus management on appear).
- **Playwright forcing-throw test pattern**: which approach (query-string, env-flag route, etc.), production-code footprint, the spec file shape with assertions.
- **Gap coverage**: a `window.error` / `window.unhandledrejection` companion in a top-level `useEffect`, or a `useErrorBoundary().showBoundary(...)` pattern for async surfaces — the boundary alone is not sufficient.
- **Slice 4 forward-compat note**: the upcoming Slice 4 conversation panel ships streaming responses (`AssistantBlocks` antipattern fix lands in Slice 4 per the cycle plan). Will the recommended boundary pattern handle streaming-render errors cleanly?
- **TypeScript-strict idioms** for `ErrorInfo.componentStack: string | null` and `unknown`-typed error in `getDerivedStateFromError`.
- **Reference precedent** — at least one open-source 2026 React 19 + Vite + Router 7 project that demonstrates the recommended pattern end-to-end, with a link.
- **Migration cost** estimate: from current declarative router setup to the recommendation's required setup. Lines changed, new deps, build-config touches.

## Out of scope

- Backend-side error endpoint design (R-073 specifies the wire shape; the backend endpoint's IaC, persistence, and retention are spec-session work).
- Sentry / LogRocket / Datadog adoption (R-073 stays vendor-free; a hosted-tool decision is post-MVP-0 if RunCoach grows beyond personal use).
- Client-side OTel SDK selection — R-074 owns that. R-073 just specifies what the boundary needs to attach to the logged error so R-074 can later weave OTel context in.
- The `JsonDocument`-in-DTO antipattern fix (Slice 4 deferred). Render errors triggered by malformed `AssistantBlocks` are in scope as a test scenario, but the structural fix is not.

The artifact lands at `docs/research/artifacts/batch-25a-react19-error-boundary-recovery-ux.md` and integrates into the Slice 1B spec plus a new DEC entry locking the library + router-integration decision.
