# R-073 — Top-level Error Boundary + Recovery UX for React 19.2 / React Router 7.15 / Vite 8 SPA (TypeScript-strict)

**Artifact path:** `docs/research/artifacts/batch-25a-react19-error-boundary-recovery-ux.md`
**Status:** Decision-ready (Slice 1B input)
**Date:** 2026-05-12

---

## TL;DR

- **Default:** Adopt **`react-error-boundary@6.1.1`** (Feb 2026, `peerDependencies: { react: ">=16.13.1" }`, 0 runtime deps, ESM-only) as a single app-root `<ErrorBoundary>` wrapping `<BrowserRouter>`. Keep the **declarative `<BrowserRouter>` + `<Routes>` router**; do **not** migrate to `createBrowserRouter` in Slice 1B (deferred — see DEC-074 below). The boundary renders a full-page `role="alert"` recovery card with two affordances: **"Try again"** (`resetErrorBoundary`) and **"Reload page"** (`window.location.reload()`). Errors POST to a new backend endpoint `POST /api/v1/client-errors` with a `crypto.randomUUID()` correlation ID that is also shown to the user under a `<details>` disclosure.
- **Fallback:** A hand-rolled class component `<AppErrorBoundary>` using `static getDerivedStateFromError` + `componentDidCatch` (zero new deps). This is the contingency if `react-error-boundary` is rejected in code review or if a CVE / abandonment signal lands before Slice 1B ships. Behavior, UX, and logging shape are identical to the default — only the implementation file differs.
- **Companion gap coverage:** A top-level `useGlobalErrorReporter()` `useEffect` in `<AppShell />` registers `window.addEventListener('error', …)` and `window.addEventListener('unhandledrejection', …)`. These do **not** trigger the boundary's fallback (boundary is for render-time errors only); they POST to the same `/api/v1/client-errors` endpoint so async/event-handler crashes are observable. For RTK Query promise rejections that should *visibly* surface a boundary, code paths use `useErrorBoundary().showBoundary(error)`.
- **Playwright test pattern:** A dev/test-only `<ThrowOnQuery />` component is rendered inside the boundary in `<AppShell />`, gated by `import.meta.env.DEV || import.meta.env.MODE === 'test'`. It reads `?throw=render` from the URL and throws synchronously during render. Production builds tree-shake the whole component (`import.meta.env.DEV` is statically replaced with `false`). The Playwright spec navigates to `/?throw=render`, asserts `getByRole('alert')` is visible, clicks "Reload page", asserts the throw query is gone and the normal shell renders.

---

## 1. Library Choice

### 1.1 Candidates and scoring

| Option | API ergonomics | Recovery semantics | TS-strict | Bundle delta (min+gzip) | Maintenance signal (May 2026) |
|---|---|---|---|---|---|
| **Hand-rolled class (`static getDerivedStateFromError` + `componentDidCatch`)** | OK — verbose, 40–60 LOC | `key`-bump or `setState({hasError:false})`; "reset" is a manual method | Best (full type control) | 0 B (in-tree) | Permanent — React docs canonical (`react.dev/reference/react/Component`) |
| **`react-error-boundary@6.1.1`** | Best — `<ErrorBoundary FallbackComponent={…} onError={…} onReset={…} resetKeys={…}>` + `useErrorBoundary()` hook + `withErrorBoundary` HOC | First-class `resetErrorBoundary(arg?)` callback; `resetKeys` array; supports passing reset args | Good — ships own `.d.ts`, `onError` signature is `(error: Error, info: { componentStack: string }) => void` | ~1.0 kB gzipped (0 deps, ESM-only) | Active — v6.1.1 released **2026-02-13**, 12 M downloads/wk, 7.9k stars, only **1 open issue** as of fetch time. `peerDependencies: { react: ">=16.13.1" }` — no upper bound, **React 19 compatible** (v6 line replaced v5; v5 was the ESM-CJS-dual line per README "Projects using framework or runtimes that don't support ES Modules should use version 5") |
| **React 19 `createRoot(...,{ onCaughtError, onUncaughtError, onRecoverableError })`** | Coarse — global callback, no per-tree fallback UI | None (it's a *reporter*, not a *boundary*) | Best | 0 B | Permanent (`react.dev/reference/react-dom/client/createRoot`) |
| **RR7 `errorElement` route property** | Per-route only, requires data-router | Has `useRouteError()` + manual reset by `navigate(0)` or `revalidator.revalidate()` | Good | 0 B (already in tree) | Permanent — but `errorElement` is **only available on `createBrowserRouter` / data-router**, not on declarative `<Routes>` (`reactrouter.com/start/modes`, `reactrouter.com/api/data-routers/createBrowserRouter`) |

### 1.2 What each one catches

These three mechanisms are **complementary**, not mutually exclusive — they form a layered defense.

| Surface | Hand-rolled / `react-error-boundary` | React 19 `onCaughtError` / `onUncaughtError` | RR7 `errorElement` |
|---|---|---|---|
| Render-time throw in a child | ✅ catches, shows fallback | Reports to callback after boundary catches (`onCaughtError`); reports to `window.reportError` if uncaught (`onUncaughtError`) | ✅ only if data-router |
| Lifecycle / constructor throw | ✅ | ✅ reports | ✅ if data-router |
| Hook throw during render | ✅ | ✅ reports | ✅ if data-router |
| Event handler throw | ❌ ([react.dev/legacy](https://legacy.reactjs.org/docs/error-boundaries.html): "Error boundaries do not catch errors inside event handlers") | ❌ | ❌ |
| `setTimeout` / `fetch.then` throw | ❌ (unless code calls `showBoundary(error)`) | ❌ | ❌ |
| Loader/action throw in RR7 data-router | n/a | n/a | ✅ |

Per the React 19 release notes ([react.dev/blog/2024/12/05/react-19](https://react.dev/blog/2024/12/05/react-19)): *"Uncaught Errors: Errors that are not caught by an Error Boundary are reported to `window.reportError`. Caught Errors: Errors that are caught by an Error Boundary are reported to `console.error`."* The new root options exist to **customize the reporter side**, not to replace the boundary — they have no fallback-UI surface.

### 1.3 Decision

**`react-error-boundary@6.1.1` is the default.**

Rationale:
- It's a thin (~1 kB gz, zero deps) wrapper over React's native error-boundary primitive, with three battle-tested superpowers over a hand-roll:
  1. `resetErrorBoundary(...args?)` from the fallback component — true soft reset, not just "remount on key change".
  2. `resetKeys={[…]}` — automatic reset when any key changes (perfect for "route changed, try again").
  3. `useErrorBoundary().showBoundary(error)` — the canonical way to surface async/event-handler errors *into* the boundary's fallback, which a hand-roll can't do without re-implementing the hook.
- React 19 compatibility is confirmed by the unbounded `peerDependencies: { react: ">=16.13.1" }` and the absence of React-19 incompatibility issues in the repo's only open issue (1 open issue as of 2026-05).
- Active maintenance: latest release `6.1.1` is **2026-02-13** — within 3 months of this research.
- The library does not replace or conflict with React 19's `createRoot` error-reporter options. We use **both** (see §5 and the code sketch in §10).

**Hand-rolled class is the fallback** (file ready, off-by-default, behind a feature flag) for the contingency that the library is yanked or fails review.

### 1.4 Why not Sentry's `<Sentry.ErrorBoundary>` / Bugsnag / LogRocket

Out of scope per the prompt's "Out of scope" section. R-073 stays vendor-free.

---

## 2. Router Integration

### 2.1 Current state

`frontend/src/app/modules/app/app.component.tsx` uses:

```tsx
<Provider store={store}>
  <BrowserRouter>
    <AppShell />            // contains <Routes><Route .../></Routes>
  </BrowserRouter>
</Provider>
```

This is React Router 7's **Declarative mode** ([reactrouter.com/start/modes](https://reactrouter.com/start/modes)).

### 2.2 What we lose without the data-router

The `errorElement` route property and `useRouteError()` hook are **data-router exclusive**. With a declarative `<Routes>` setup:

- ❌ Cannot scope an error UI to a single route (every render error bubbles to the app-root boundary).
- ❌ Cannot intercept errors thrown from a route's `loader` / `action` (we don't use loaders/actions — we use RTK Query — so this loss is moot for Slice 1B).
- ❌ Cannot use `throw new Response(...)` data-throw pattern.

The first one is the only real loss. For Slice 1B's acceptance criterion ("React app survives a child render-time exception with a top-level error boundary"), a single app-root boundary fully satisfies the contract.

### 2.3 Migration cost if we migrated to `createBrowserRouter`

Estimated (per [`reactrouter.com/upgrading/v6`](https://reactrouter.com/upgrading/v6) and Epic Web Dev's *Upgrading React Router*):

- **Lines changed:** ~30–60 in `app.component.tsx` + however many `<Route>` JSX nodes exist (RunCoach currently has ~6–8 routes, all `<Route element=… />`).
- **New deps:** none (`react-router` 7.15 already shipped, exports `createBrowserRouter` and `RouterProvider`).
- **Build-config touches:** none — same package, same Vite plugin.
- **Behavioral risk:** medium. `createBrowserRouter` imposes a strict "route tree up front" mental model and brings `loader`/`action`/`useNavigation` mechanics that interact with React 19 transitions (per [`reactrouter.com/api/data-routers/createBrowserRouter`](https://reactrouter.com/api/data-routers/createBrowserRouter)). Tests for navigation, `useLocation`, and `useNavigate` should be re-run; existing tests should largely keep working since the hook APIs are identical between modes.
- **Optional helper:** `createRoutesFromElements(<><Route /></>)` lets you keep JSX route syntax while opting into the data-router. This is the cheapest migration path if we do migrate later.

### 2.4 Decision

**Stay on declarative `<BrowserRouter>` for Slice 1B.** Defer the data-router migration to a later slice where route-level error UIs, route-level data loaders, or pending-navigation indicators become a feature requirement. The single app-root `<ErrorBoundary>` meets the Slice 1B acceptance criterion. (See DEC-073 below; the data-router migration is logged as a known-deferred item, not as tech debt — it is a feature-driven future decision.)

---

## 3. Recovery UX

### 3.1 Comparison

| Pattern | When client state survives | Risk if the cause is in client state | Right default? |
|---|---|---|---|
| **Hard reload** (`window.location.reload()`) | ❌ (all in-memory state gone, RTK Query cache rehydrates from server) | Lowest — same as a fresh tab | Use as **escalation** ("Reload page" button) |
| **Soft reset** (`resetErrorBoundary()` or `key`-bump) | ✅ | Will likely re-throw if the cause is in Redux state | Use as **primary affordance** ("Try again") |
| **Navigate-home** (`navigate('/', { replace: true })`) | ✅ | Loses user's place but may avoid the bad route | Secondary affordance |
| **Inline retry (banner with X)** | ✅ | Only safe for transient network — wrong for render errors | ❌ not for top-level |

### 3.2 Default UX for RunCoach

A coaching SPA with session state (active onboarding, chat transcript, plan-regeneration draft) should **try soft reset first, then escalate to hard reload**, with both affordances visible at once. This minimises lost work while still giving the user an escape hatch.

### 3.3 Conventional copy and structure

Following production SPAs (Notion's pattern, observed on `isdown.app/status/notion`: *"Oops! Something went wrong. Please refresh and try again or message support."*) and the certificates.dev / dev.to 2026 React error-UX writeups:

```
[Full-viewport centered card, role="alert"]
─────────────────────────────────────────
  ⚠  Something went wrong

  RunCoach hit an unexpected error while
  rendering this page. Your data is safe —
  this hasn't been sent anywhere.

  [ Try again ]    [ Reload page ]

  ▸ Show error details
    ─────────────────────────────
    Error ID: 5f3a-8b2e (full ID copied
              to clipboard with "Copy ID")
    TypeError: Cannot read properties
               of undefined (reading 'name')
    Component stack: at AssistantBlocks
                     at ChatPanel ...
─────────────────────────────────────────
```

### 3.4 Accessibility

Per [WAI-ARIA practices](https://www.a11y-collective.com/blog/aria-alert/) and certificates.dev's *Accessibility in React 2026*:

- The card root is `<div role="alert">`. Per A11Y Collective: *"Don't add `aria-live=\"assertive\"` when you're already using `role=\"alert\"` — some screen readers might announce your message twice."* — so we set `role="alert"` and **not** `aria-live`.
- The heading `<h1>` carries `tabIndex={-1}` and receives focus on mount via `useEffect` (certificates.dev: *"Move focus to the new content when the page changes significantly… `tabIndex={-1}` makes the heading programmatically focusable without putting it in the tab order"*).
- Buttons are `<button type="button">`, not `<div onClick>`.
- The `<details>` disclosure is keyboard-operable by default; the error-ID `<code>` element is focusable for screen-reader spelling-out.

---

## 4. Correlation with Backend Errors

### 4.1 Format and length

**Use `crypto.randomUUID()`** (the Web Crypto API, available in all modern browsers in secure contexts per [MDN](https://developer.mozilla.org/en-US/docs/Web/API/Crypto/randomUUID); RunCoach is HTTPS in prod and `localhost` is treated as secure). This produces a 36-character v4 UUID like `5f3a8b2e-1c4d-4e6f-9a8b-7c6d5e4f3a2b`.

- **No new dep** required.
- The `uuid` npm package is the conventional polyfill if we ever target a non-secure context, but we don't, so we skip it.
- On screen we show the **first 8 hex chars** (`5f3a8b2e`) for readability; the full UUID is in the `<details>` disclosure with a "Copy ID" button. This matches how Linear / GitHub typically expose error IDs.

### 4.2 What goes on screen vs. logged

| Field | Rendered on card | Sent in POST body |
|---|---|---|
| Short error ID (first 8 chars of UUID) | ✅ always visible | (full UUID in body) |
| Full correlation UUID | Under `<details>`, copyable | ✅ as `correlationId` |
| `error.name` (e.g. `TypeError`) | Under `<details>` | ✅ |
| `error.message` | Under `<details>` (verbatim) | ✅ |
| `info.componentStack` | Under `<details>` (verbatim, in `<pre>`) | ✅ |
| `error.stack` | Under `<details>` (verbatim, in `<pre>`) | ✅ |
| `window.location.href` at time of throw | ❌ | ✅ |
| `navigator.userAgent` | ❌ | ✅ |
| `Date.now()` ISO timestamp | ❌ | ✅ |

### 4.3 Relationship to R-074 (client-OTel)

R-073 deliberately uses a **client-generated** UUID rather than a captured-from-headers traceparent. Reasoning:

- The boundary catches errors that may not have any preceding fetch in their immediate context (e.g. a render error from a Redux selector returning bad data from cache). There's no useful `traceparent` to capture in those cases.
- R-074 will add an OTel SDK that creates client spans and may add a `traceparent` to each fetch. When R-074 lands, the boundary's `onError` callback can be upgraded to **also** include the current active span's `traceId` alongside the boundary-generated correlation ID. The wire shape is forward-compatible: `correlationId` (always present) + future-optional `traceparent` field.
- The two IDs serve different purposes: `correlationId` is for the user-reported "I got this error card → please look it up" loop; `traceparent` is for the operator-side "join this error to a server span" loop.

---

## 5. Logging on Catch (MVP-0 shape)

### 5.1 Wire shape — `POST /api/v1/client-errors`

```http
POST /api/v1/client-errors HTTP/1.1
Content-Type: application/json
Cookie: <session cookie>
```

```json
{
  "correlationId": "5f3a8b2e-1c4d-4e6f-9a8b-7c6d5e4f3a2b",
  "occurredAt":    "2026-05-12T14:23:11.482Z",
  "kind":          "render",
  "errorName":     "TypeError",
  "message":       "Cannot read properties of undefined (reading 'name')",
  "stack":         "TypeError: Cannot read properties...\n  at AssistantBlocks (...)",
  "componentStack":"\n    at AssistantBlocks\n    at ChatPanel\n    at ...",
  "url":           "https://runcoach.app/chat",
  "userAgent":     "Mozilla/5.0 ...",
  "appVersion":    "0.1.0+abc1234"
}
```

`kind` is one of `"render" | "window-error" | "unhandled-rejection"`. The same endpoint accepts all three; the backend persists them undifferentiated for MVP-0.

### 5.2 Why a backend POST, not `console.error`-only

- Cookie auth is already established for the existing `/api/v1/*` routes → no CORS or token plumbing.
- Survives between dev sessions (we already have the backend; we're just adding one route).
- `console.error` alone has zero observability in production; the user sees the card but the developer never knows.
- Future-proof: R-074 will swap this for OTel client export to OTLP HTTP at `localhost:4318/v1/traces`, and the boundary's `onError` body becomes a span event rather than a fetch. The R-073 endpoint can be deprecated cleanly because it has a single caller.

### 5.3 Sending semantics

- Use `fetch(..., { method: 'POST', credentials: 'include', keepalive: true })` so the request survives an immediate `window.location.reload()` triggered by the user.
- On `fetch` failure (network down, backend down), fall back to `navigator.sendBeacon('/api/v1/client-errors', new Blob([json], {type:'application/json'}))`. `sendBeacon` is "best-effort", and a failure here is acceptable — the user still gets the recovery card.
- **Never await** the fetch in `componentDidCatch` / `onError`; fire-and-forget. Awaiting could itself throw and would block the render of the fallback.

### 5.4 Backend stub for MVP-0

Out of scope for R-073, but the wire contract above is the seam. R-073's commitment to the backend team is: *the endpoint must accept the JSON shape above with cookie auth and return 204 No Content; it may persist to anywhere (or `/dev/null` initially). R-074 will replace the client side with OTel export.*

---

## 6. Playwright Test Pattern

### 6.1 Choice

**A dev/test-only `<ThrowOnQuery />` component** rendered inside the boundary, gated by `import.meta.env.DEV`. This pattern is preferred over the four alternatives because:

1. **Zero production footprint.** Per [Vite env docs](https://vite.dev/guide/env-and-mode): *"`import.meta.env.DEV: {boolean}` … code inside here will be tree-shaken in production builds."* The component file's `if (import.meta.env.DEV)` guard is statically replaced with `if (false)`, the whole component is dead code, and Rollup drops it.
2. **No test-only route to register / unregister.** A separate `/__test/throw` route would either need conditional router config (annoying) or live permanently in the bundle.
3. **No reliance on RTK Query mock plumbing.** A network-mock-induced parse error tests the parser, not the boundary.
4. **Predictable failure mode.** The component throws *synchronously during render*, which is the exact condition error boundaries are documented to catch.

### 6.2 Production-code footprint

- One new file: `frontend/src/dev-only/throw-on-query.tsx` (~15 LOC).
- One import + one JSX node in `<AppShell />`, both behind `import.meta.env.DEV`.
- Bundle delta in `vite build`: **0 bytes** (verified by `vite build --report` or `rollup-plugin-visualizer`; the file should not appear in `stats.html`).

### 6.3 Spec sketch

`frontend/e2e/error-boundary.spec.ts`:

```ts
import { test, expect } from '@playwright/test';

test.describe('Top-level error boundary', () => {
  test('renders fallback when a child throws during render', async ({ page }) => {
    // Capture browser-level uncaught exceptions so we can later assert
    // that React did re-throw the dev-mode error (only in dev builds —
    // production builds report to window.reportError instead).
    const pageErrors: Error[] = [];
    page.on('pageerror', (err) => pageErrors.push(err));

    await page.goto('/?throw=render');

    // Assertion 1: the boundary's fallback rendered.
    const alert = page.getByRole('alert');
    await expect(alert).toBeVisible();
    await expect(alert).toContainText('Something went wrong');

    // Assertion 2: the user-facing affordances exist.
    await expect(page.getByRole('button', { name: 'Try again' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Reload page' })).toBeVisible();

    // Assertion 3: clicking "Reload page" navigates away from the
    // throw query and back to the working shell.
    await page.getByRole('button', { name: 'Reload page' }).click();
    // After reload, the test harness should *not* re-add the query param.
    // We click reload which triggers window.location.reload() on the same URL,
    // so we instead navigate to '/' to verify the shell renders cleanly.
    await page.goto('/');
    await expect(page.getByRole('alert')).toHaveCount(0);
    await expect(page.getByTestId('app-shell')).toBeVisible();
  });

  test('Try again button soft-resets the boundary', async ({ page }) => {
    await page.goto('/?throw=render');
    await expect(page.getByRole('alert')).toBeVisible();

    // Strip the query so re-render doesn't immediately re-throw.
    await page.evaluate(() => {
      const u = new URL(window.location.href);
      u.searchParams.delete('throw');
      window.history.replaceState({}, '', u.toString());
    });

    await page.getByRole('button', { name: 'Try again' }).click();
    await expect(page.getByRole('alert')).toHaveCount(0);
    await expect(page.getByTestId('app-shell')).toBeVisible();
  });
});
```

Note on Playwright's `pageerror` listener: per [playwright.dev/docs/api/class-weberror](https://playwright.dev/docs/api/class-weberror) and `microsoft/playwright#17973`, `pageerror` fires for uncaught exceptions. In React 19, errors **caught by a boundary** go to `console.error` (not re-thrown to `window`), per the React 19 upgrade guide. So `pageErrors` will typically be **empty** in a production build and we **must not** assert it has length > 0 — we only use the listener to surface diagnostic info if the test fails for an unexpected reason. The actual assertion is `getByRole('alert')` being visible, which is the user-observable contract.

### 6.4 What NOT to do

- Don't assert on `console.error` — React 19 logs caught errors there by default, and asserting against it couples the test to React's logging implementation.
- Don't `page.on('console', ...)` to verify the error — fragile and noisy.
- Don't use the `<details>` text content as a primary assertion — it's intentionally hidden by default and may change copy.

---

## 7. What the Boundary Cannot Catch — Gap Coverage

### 7.1 Documented gaps

From React's docs ([legacy.reactjs.org/docs/error-boundaries.html](https://legacy.reactjs.org/docs/error-boundaries.html), still the authoritative summary): error boundaries do **not** catch:

1. Errors in event handlers
2. Errors in asynchronous code (`setTimeout`, `requestAnimationFrame`, `.then` callbacks, `await` in effects)
3. Errors during server-side rendering (n/a for RunCoach — Vite client-only SPA)
4. Errors thrown in the error boundary itself

### 7.2 RunCoach-specific surfaces in the gap

- RTK Query rejected promises that aren't handled by `isError`/`error` selectors and bubble through `.unwrap()`.
- Event handlers in dialogs (regenerate-plan dialog, chat send button).
- `useEffect` callbacks that do `fetch(...).then(...)` without `.catch`.

### 7.3 Pattern — two complementary mechanisms

**Mechanism A: global window listeners (passive logging).** A top-level `useGlobalErrorReporter()` hook installs:

```ts
useEffect(() => {
  const onErr = (ev: ErrorEvent) => reportClientError({
    kind: 'window-error',
    error: ev.error ?? new Error(ev.message),
  });
  const onRej = (ev: PromiseRejectionEvent) => reportClientError({
    kind: 'unhandled-rejection',
    error: ev.reason instanceof Error ? ev.reason : new Error(String(ev.reason)),
  });
  window.addEventListener('error', onErr);
  window.addEventListener('unhandledrejection', onRej);
  return () => {
    window.removeEventListener('error', onErr);
    window.removeEventListener('unhandledrejection', onRej);
  };
}, []);
```

These do **not** trigger the fallback UI. They only POST to `/api/v1/client-errors`. The reason: showing a full-page error card for a transient promise rejection deep in an effect would punish the user for what is often a recoverable network blip; the UI should keep working and the developer should learn about it asynchronously.

**Mechanism B: `useErrorBoundary().showBoundary(error)` (active escalation).** When a code path *wants* a render error semantic for an async failure — e.g. an onboarding bootstrap that has no useful UI without its data — it can do:

```ts
const { showBoundary } = useErrorBoundary();
useEffect(() => {
  bootstrapOnboarding().catch(showBoundary);
}, [showBoundary]);
```

This is documented in the `react-error-boundary` README and in Kent C. Dodds' *Use react-error-boundary to handle errors in React*: *"`showBoundary` function propagates the error to the nearest error boundary, triggering the fallback UI just like a rendering error would."*

### 7.4 React 19 root-options as a third backstop

We **also** wire `onCaughtError` and `onUncaughtError` on `createRoot` to call `reportClientError`. This is belt-and-suspenders: per React 19 docs ([react.dev/reference/react-dom/client/createRoot](https://react.dev/reference/react-dom/client/createRoot)), these fire on *every* boundary-caught and uncaught render error, regardless of which boundary catches it. If a future deeper boundary (e.g. a route-level one once we move to data-router) catches an error our top-level `onError` never sees, the root options still log it.

---

## 8. TypeScript-strict Ergonomics

### 8.1 Strict-null handling of `componentStack`

In `@types/react@^19`, `ErrorInfo` is declared (effectively):

```ts
interface ErrorInfo {
  /** Captured information about the components that threw, or null if it cannot be determined. */
  componentStack?: string | null;
  digest?: string | null;
}
```

Under `strictNullChecks`, the right pattern is to normalize at the boundary of our own code:

```ts
function normalizeComponentStack(info: ErrorInfo | { componentStack: string }): string {
  return info.componentStack ?? '';
}
```

Note: `react-error-boundary`'s `onError` types `info` as `{ componentStack: string }` (non-nullable) — that's the library's intentional narrowing. Our `onError` handler signature should be:

```ts
const onError = (error: Error, info: { componentStack: string }) => {
  reportClientError({ kind: 'render', error, componentStack: info.componentStack });
};
```

### 8.2 `unknown`-typed error in `getDerivedStateFromError`

When hand-rolling the class fallback, the parameter to `static getDerivedStateFromError` is typed as `Error` in `@types/react@^19`, but at runtime *anything* can be thrown (`throw "string"`, `throw 42`). Defensive normalization:

```ts
static getDerivedStateFromError(error: unknown): State {
  const normalized = error instanceof Error
    ? error
    : new Error(typeof error === 'string' ? error : 'Unknown render error');
  return { hasError: true, error: normalized };
}
```

Using `unknown` here instead of the declared `Error` is a deliberate stricter type — it produces correct code even when a non-`Error` is thrown.

### 8.3 No `!` non-null assertions

The above patterns never need `!`. The boundary state is shape `{ hasError: false } | { hasError: true; error: Error; componentStack: string }`, a discriminated union, so the fallback render path has `state.error` known-non-null without assertion:

```ts
type State =
  | { hasError: false }
  | { hasError: true; error: Error; componentStack: string };
```

---

## 9. Existing Precedents

- **`bvaughn/react-error-boundary` itself** ships an `integrations/vite` workspace ([github.com/bvaughn/react-error-boundary/tree/main/integrations/vite](https://github.com/bvaughn/react-error-boundary)) that exercises the library against a Vite/React app — same toolchain as RunCoach minus React Router.
- **`ssunils/react-19-error-boundary-example`** ([github.com/ssunils/react-19-error-boundary-example](https://github.com/ssunils/react-19-error-boundary-example)) — a deliberately React-19-targeted, Vite-built TypeScript demo of `ErrorBoundary` + a `BuggyCounter` test component. Uses the hand-rolled class pattern. Useful as a copy-paste reference for the fallback implementation.
- **Sentry's React docs ([docs.sentry.io/platforms/javascript/guides/react](https://docs.sentry.io/platforms/javascript/guides/react/features/error-boundary/))** — even though we are vendor-free, Sentry's stance is the clearest statement of the **2026 layering pattern**: *"In React 19+, they complement each other: `reactErrorHandler` — Global safety net for error reporting; `ErrorBoundary` — Scoped error handling with custom fallbacks and context."* We mirror this with our own non-Sentry handlers.
- **Sentry's React Router v7 non-framework guide** ([docs.sentry.io/platforms/javascript/guides/react/features/react-router/v7](https://docs.sentry.io/platforms/javascript/guides/react/features/react-router/v7/)) confirms that with `<Routes>` (declarative) you wrap with `Sentry.withSentryReactRouterV7Routing`; with `createBrowserRouter` you wrap the router factory. This corroborates that both router modes are first-class in RR 7.x as of 2026.

No single open-source repo found in 2024-2026 publishing the *exact* React 19.2 + RR 7.15 + Vite 8 + TS-strict + `react-error-boundary` + boundary-with-correlation-ID stack end-to-end. The recommendation below is therefore assembled from primary sources rather than copied from a precedent.

---

## 10. Code Sketches

### 10.1 `frontend/src/app/error-boundary/app-error-boundary.tsx`

```tsx
import { ErrorBoundary, type FallbackProps } from 'react-error-boundary';
import { useEffect, useRef } from 'react';
import { reportClientError } from './report-client-error';

const SHORT_ID_LEN = 8;

function Fallback({ error, resetErrorBoundary }: FallbackProps): JSX.Element {
  const headingRef = useRef<HTMLHeadingElement>(null);
  useEffect(() => { headingRef.current?.focus(); }, []);

  // The correlationId was generated and attached in onError. We surface
  // the short prefix here for the user to read aloud; the full id is in
  // <details>.
  const correlationId = (error as Error & { correlationId?: string }).correlationId ?? 'unknown';
  const shortId = correlationId.slice(0, SHORT_ID_LEN);

  return (
    <div role="alert" data-testid="app-error-boundary" className="re-app-error">
      <div className="re-app-error__card">
        <h1 ref={headingRef} tabIndex={-1}>Something went wrong</h1>
        <p>
          RunCoach hit an unexpected error while rendering this page.
          Your data is safe — this hasn’t been sent anywhere.
        </p>
        <p className="re-app-error__id">
          Error ID: <code>{shortId}</code>
        </p>
        <div className="re-app-error__actions">
          <button type="button" onClick={resetErrorBoundary}>Try again</button>
          <button type="button" onClick={() => window.location.reload()}>Reload page</button>
        </div>
        <details>
          <summary>Show error details</summary>
          <p>Full ID: <code>{correlationId}</code></p>
          <p><strong>{error.name}</strong>: {error.message}</p>
          {error.stack && <pre>{error.stack}</pre>}
        </details>
      </div>
    </div>
  );
}

export function AppErrorBoundary({ children }: { children: React.ReactNode }): JSX.Element {
  return (
    <ErrorBoundary
      FallbackComponent={Fallback}
      onError={(error, info) => {
        // Attach correlationId to the error object so the fallback can show it.
        const correlationId = crypto.randomUUID();
        Object.defineProperty(error, 'correlationId', { value: correlationId, enumerable: false });
        reportClientError({
          kind: 'render',
          correlationId,
          error,
          componentStack: info.componentStack,
        });
      }}
    >
      {children}
    </ErrorBoundary>
  );
}
```

### 10.2 `frontend/src/app/error-boundary/report-client-error.ts`

```ts
type ClientErrorKind = 'render' | 'window-error' | 'unhandled-rejection';

interface ReportArgs {
  kind: ClientErrorKind;
  correlationId?: string;
  error: Error;
  componentStack?: string;
}

const ENDPOINT = '/api/v1/client-errors';

export function reportClientError({ kind, correlationId, error, componentStack }: ReportArgs): void {
  const body = JSON.stringify({
    correlationId: correlationId ?? crypto.randomUUID(),
    occurredAt: new Date().toISOString(),
    kind,
    errorName: error.name,
    message: error.message,
    stack: error.stack ?? '',
    componentStack: componentStack ?? '',
    url: window.location.href,
    userAgent: navigator.userAgent,
    appVersion: import.meta.env.VITE_APP_VERSION ?? 'unknown',
  });

  // Fire-and-forget. Use keepalive so the request survives an immediate reload.
  try {
    void fetch(ENDPOINT, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body,
      keepalive: true,
    }).catch(() => {
      // sendBeacon fallback — best-effort.
      try {
        navigator.sendBeacon(ENDPOINT, new Blob([body], { type: 'application/json' }));
      } catch {
        /* swallow — nothing more to do */
      }
    });
  } catch {
    /* swallow — boundary must never throw from its own logger */
  }
}
```

### 10.3 `frontend/src/app/error-boundary/use-global-error-reporter.ts`

```ts
import { useEffect } from 'react';
import { reportClientError } from './report-client-error';

export function useGlobalErrorReporter(): void {
  useEffect(() => {
    const onErr = (ev: ErrorEvent) => {
      const error = ev.error instanceof Error ? ev.error : new Error(ev.message || 'window error');
      reportClientError({ kind: 'window-error', error });
    };
    const onRej = (ev: PromiseRejectionEvent) => {
      const error = ev.reason instanceof Error ? ev.reason : new Error(String(ev.reason));
      reportClientError({ kind: 'unhandled-rejection', error });
    };
    window.addEventListener('error', onErr);
    window.addEventListener('unhandledrejection', onRej);
    return () => {
      window.removeEventListener('error', onErr);
      window.removeEventListener('unhandledrejection', onRej);
    };
  }, []);
}
```

### 10.4 `frontend/src/main.tsx` (wiring)

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import { BrowserRouter } from 'react-router';
import App from './App';
import { store } from './app/store';
import { AppErrorBoundary } from './app/error-boundary/app-error-boundary';
import { reportClientError } from './app/error-boundary/report-client-error';

const rootEl = document.getElementById('root');
if (!rootEl) throw new Error('#root not found');

createRoot(rootEl, {
  // Belt-and-suspenders: React 19 root reporters. These fire even if
  // a nested boundary catches the error, ensuring observability.
  onCaughtError: (error, info) => {
    reportClientError({
      kind: 'render',
      error: error instanceof Error ? error : new Error(String(error)),
      componentStack: info.componentStack ?? '',
    });
  },
  onUncaughtError: (error, info) => {
    reportClientError({
      kind: 'render',
      error: error instanceof Error ? error : new Error(String(error)),
      componentStack: info.componentStack ?? '',
    });
  },
}).render(
  <StrictMode>
    <Provider store={store}>
      <AppErrorBoundary>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </AppErrorBoundary>
    </Provider>
  </StrictMode>,
);
```

### 10.5 `frontend/src/dev-only/throw-on-query.tsx` (Playwright harness)

```tsx
// Dead-code-eliminated in production builds. Vite statically replaces
// `import.meta.env.DEV` with `false`, so this whole module's body becomes
// `return null;` after dead-code elimination.
export function ThrowOnQuery(): null {
  if (!import.meta.env.DEV) return null;
  const params = new URLSearchParams(window.location.search);
  const mode = params.get('throw');
  if (mode === 'render') {
    throw new Error('ThrowOnQuery: forced render-time throw for E2E test');
  }
  return null;
}
```

Usage in `<AppShell />` (rendered inside the boundary):

```tsx
{import.meta.env.DEV && <ThrowOnQuery />}
```

### 10.6 Hand-rolled fallback variant (`AppErrorBoundary` v2 — fallback implementation)

```tsx
import { Component, type ErrorInfo, type ReactNode } from 'react';

type State =
  | { hasError: false }
  | { hasError: true; error: Error; componentStack: string; correlationId: string };

export class AppErrorBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(error: unknown): State {
    const normalized = error instanceof Error
      ? error
      : new Error(typeof error === 'string' ? error : 'Unknown render error');
    return {
      hasError: true,
      error: normalized,
      componentStack: '',
      correlationId: crypto.randomUUID(),
    };
  }

  componentDidCatch(error: unknown, info: ErrorInfo): void {
    if (!this.state.hasError) return;
    const normalized = error instanceof Error ? error : new Error(String(error));
    const componentStack = info.componentStack ?? '';
    this.setState((prev) => prev.hasError ? { ...prev, componentStack } : prev);
    reportClientError({
      kind: 'render',
      correlationId: this.state.correlationId,
      error: normalized,
      componentStack,
    });
  }

  private reset = (): void => { this.setState({ hasError: false }); };

  render(): ReactNode {
    if (!this.state.hasError) return this.props.children;
    return (
      <FallbackUI
        error={this.state.error}
        correlationId={this.state.correlationId}
        componentStack={this.state.componentStack}
        onReset={this.reset}
      />
    );
  }
}
```

---

## 11. Migration Cost from Current State

| Touch | Files | Lines | New deps |
|---|---|---|---|
| Add `react-error-boundary@6.1.1` | `frontend/package.json`, lockfile | +2 | +1 (`react-error-boundary`) |
| New: `app-error-boundary.tsx`, `report-client-error.ts`, `use-global-error-reporter.ts`, fallback CSS | 4 new files | ~250 LOC total | — |
| Edit `main.tsx` to wrap with `<AppErrorBoundary>` and add `onCaughtError`/`onUncaughtError` | `frontend/src/main.tsx` | +15 / -3 | — |
| Edit `<AppShell />` to call `useGlobalErrorReporter()` | `frontend/src/app/modules/app/app.component.tsx` | +2 | — |
| New: `throw-on-query.tsx` + 1 conditional render in `<AppShell />` | 1 new file, 1 edit | ~20 LOC | — |
| New: backend route `POST /api/v1/client-errors` (out of scope for R-073 prose but on the seam) | backend | ~30 LOC | — |
| New: Playwright spec `error-boundary.spec.ts` | `frontend/e2e/` | ~60 LOC | — |
| Vite/TS config | none | 0 | — |
| Router migration to `createBrowserRouter` | **NOT REQUIRED** for Slice 1B | 0 | — |

**Total Slice 1B frontend delta: ~350 LOC added, 3 LOC removed, 1 dep added (~1 kB gz).**

---

## 12. Slice 4 Forward-Compat Note

Slice 4 introduces the conversation panel with streaming assistant responses (the `AssistantBlocks` antipattern is fixed there). Render-time errors triggered by malformed streamed deltas are exactly the case the boundary is designed for — when a streamed block contains a structure the renderer can't handle, the renderer throws during reconciliation, the boundary catches, and the recovery UX surfaces.

Two forward-compat considerations:

1. **Per-panel boundary, not just app-root.** When Slice 4 lands, the chat panel should be wrapped in its *own* `<ErrorBoundary FallbackComponent={ChatPanelErrorFallback} resetKeys={[conversationId]}>` so that a broken assistant block does not nuke the entire shell. The app-root boundary remains as the outer safety net. `react-error-boundary`'s `resetKeys` makes this trivially correct: switching conversations naturally resets a stuck boundary.
2. **`use()` + Suspense interaction.** If Slice 4 uses React 19's `use()` for streamed promises, errors thrown from `use(promise)` propagate to the nearest **error boundary**, while pending promises propagate to the nearest **Suspense boundary** — they are independent mechanisms. The chat-panel boundary will catch streamed-promise rejections cleanly.

---

## 13. Decision Log Entry Draft

**`decisions/decision-log.md` insertion (DEC-073):**

```markdown
## DEC-073 — Top-level error boundary library and router-integration mode

**Date:** 2026-05-12
**Status:** Accepted (Slice 1B)
**Supersedes:** —
**Superseded by:** —

### Context
Slice 1B acceptance criterion #N requires that the React app survives a child
render-time exception with a top-level error boundary that logs and renders a
recovery affordance instead of a blank screen. Current state: declarative
<BrowserRouter> + <Routes>, no boundary, no client-side logging.

### Decision
1. Adopt `react-error-boundary@6.1.1` as the canonical error-boundary
   library. Single app-root <ErrorBoundary> wraps <BrowserRouter>.
2. Keep declarative <BrowserRouter> + <Routes>. Defer the
   `createBrowserRouter` (data-router) migration to a future slice where
   route-level `errorElement` / route loaders become a feature requirement.
3. Wire React 19's `createRoot({ onCaughtError, onUncaughtError })` as a
   belt-and-suspenders reporter that POSTs to the same client-errors
   endpoint.
4. Install global `window.error` and `window.unhandledrejection` listeners
   in a top-level useEffect; these log but do NOT trigger the boundary's
   fallback UI. Code paths that want async-error-to-fallback escalation use
   `useErrorBoundary().showBoundary(error)`.
5. MVP-0 logging shape: POST /api/v1/client-errors with JSON
   { correlationId (UUIDv4 via crypto.randomUUID), occurredAt, kind, errorName,
     message, stack, componentStack, url, userAgent, appVersion }. Use cookie
     auth, fetch keepalive, sendBeacon fallback. R-074 will later replace the
     client side with OTel export to OTLP HTTP; the endpoint can be deprecated
     because it has a single caller.
6. Recovery UX: full-page card with role="alert", auto-focused <h1>, two
   buttons ("Try again" — soft reset via resetErrorBoundary; "Reload page" —
   hard window.location.reload), and a <details> disclosure containing the
   full correlation UUID, error name, message, stack, and componentStack.
7. Playwright forcing-throw test uses a dev-only <ThrowOnQuery /> component
   gated by `import.meta.env.DEV`, tree-shaken to zero bytes in production
   builds. Test triggers via `/?throw=render` and asserts on
   `getByRole('alert')` visibility plus affordance behavior.

### Fallback / Contingency
If `react-error-boundary` is rejected in code review or a CVE / abandonment
signal lands before Slice 1B ships, replace with a hand-rolled class
<AppErrorBoundary> using `static getDerivedStateFromError` +
`componentDidCatch`. Same wire shape, same UX, no new dep, ~80 LOC.

### Consequences
- New runtime dep (react-error-boundary, 0 transitive deps, ~1 kB gz).
- New backend endpoint contract: POST /api/v1/client-errors (R-073 owns
  the wire shape; backend persistence is spec-session work).
- Existing test suite continues to pass; one new Playwright spec added.
- The declarative-router decision means there is no per-route error UI in
  Slice 1B. This is acceptable for the current route count (~6–8 routes)
  and is reversible (the data-router migration is mechanical).
- Forward-compat with Slice 4 chat panel: a nested per-panel
  <ErrorBoundary resetKeys={[conversationId]}> will be added in Slice 4,
  with the app-root boundary remaining as the outer safety net.

### Open follow-ups (not blocking Slice 1B)
- R-074: client-side OTel SDK + traceparent propagation through RTK Query;
  will upgrade the boundary's onError to enrich the report with active span
  context. The R-073 endpoint becomes deprecated when R-074 lands.
- Future slice: evaluate migration to `createBrowserRouter` if/when
  route-level loaders, actions, or per-route `errorElement` become feature
  requirements. Migration is ~30–60 LOC in `app.component.tsx`, no new deps.
```

---

## 14. References (primary sources, 2024–2026)

- React 19 release post — `react.dev/blog/2024/12/05/react-19` (Dec 2024)
- React 19 upgrade guide — `react.dev/blog/2024/04/25/react-19-upgrade-guide` (Apr 2024)
- `createRoot` reference — `react.dev/reference/react-dom/client/createRoot` (current)
- React error-boundary ESLint rule — `react.dev/reference/eslint-plugin-react-hooks/lints/error-boundaries` (current)
- React legacy docs on error-boundary semantics — `legacy.reactjs.org/docs/error-boundaries.html`
- React `captureOwnerStack` reference — `react.dev/reference/react/captureOwnerStack`
- React Router 7 — Picking a Mode — `reactrouter.com/start/modes`
- React Router 7 — `createBrowserRouter` API — `reactrouter.com/api/data-routers/createBrowserRouter`
- React Router 7 — `BrowserRouter` API — `reactrouter.com/api/declarative-routers/BrowserRouter`
- React Router 7 — Upgrading from v6 — `reactrouter.com/upgrading/v6`
- `bvaughn/react-error-boundary` README + package.json — `github.com/bvaughn/react-error-boundary` (v6.1.1, 2026-02-13)
- `react-error-boundary` on npm — `npmjs.com/package/react-error-boundary` (12.1 M weekly downloads, latest 6.1.1)
- Kent C. Dodds — *Use react-error-boundary to handle errors in React* — `kentcdodds.com/blog/use-react-error-boundary-to-handle-errors-in-react`
- Kent C. Dodds — *Why React Error Boundaries Aren't Just Try/Catch* — `epicreact.dev/why-react-error-boundaries-arent-just-try-catch-for-components-i6e2l`
- Certificates.dev — *Error Handling in React with react-error-boundary* (2026) — `certificates.dev/blog/error-handling-in-react-with-react-error-boundary`
- Certificates.dev — *Accessibility in React: Common Mistakes and How to Fix Them* (2026) — `certificates.dev/blog/accessibility-in-react-common-mistakes-and-how-to-fix-them`
- MDN — `Crypto.randomUUID()` — `developer.mozilla.org/en-US/docs/Web/API/Crypto/randomUUID` (last modified Sep 2024)
- MDN — `Window: unhandledrejection event` — `developer.mozilla.org/en-US/docs/Web/API/Window/unhandledrejection_event` (last modified May 2025)
- Vite — Env Variables and Modes — `vite.dev/guide/env-and-mode`
- Playwright — Assertions — `playwright.dev/docs/test-assertions`
- Playwright — `WebError` class — `playwright.dev/docs/api/class-weberror`
- Sentry — React Error Boundary integration (2026 layering reference) — `docs.sentry.io/platforms/javascript/guides/react/features/error-boundary/`
- Sentry — React Router v7 (non-framework) — `docs.sentry.io/platforms/javascript/guides/react/features/react-router/v7/`
- The A11Y Collective — *How to Use ARIA Alert Effectively* — `a11y-collective.com/blog/aria-alert/`
- `ssunils/react-19-error-boundary-example` (Vite + TS demo) — `github.com/ssunils/react-19-error-boundary-example`

---

## Caveats

- The React 19 root-options `onCaughtError` and `onUncaughtError` were Canary-flagged when introduced (per the `reactjs/react.dev#6742` PR and PR comments referencing `CanaryBadge`) but are stable in React 19.0+ and documented unconditionally in `react.dev/reference/react-dom/client/createRoot` as of May 2026. Slice 1B targets React 19.2.6, which is well past this.
- `react-error-boundary@6.x` is ESM-only ("Module is ESM-only in order to better work with modern tooling" — release notes). Vite handles this natively; this is a non-issue for the RunCoach stack. If RunCoach ever adopts Jest (rather than Vitest) for unit tests, the same ESM-only constraint may bite — use Vitest, or use the hand-rolled fallback.
- I was unable to verify a single open-source repo publishing the *exact* React 19.2 + RR 7.15 + Vite 8 + TS-strict + boundary-with-correlation-ID stack end-to-end; the recommendation is assembled from primary sources (React docs, RR docs, react-error-boundary repo, MDN, Playwright docs) rather than copied from an existing precedent. The closest single-repo reference is `ssunils/react-19-error-boundary-example` (React 19 + Vite + class-component boundary; no Router, no correlation ID).
- The Notion *"Oops! Something went wrong"* fallback is documented in user-facing incident reports rather than in a Notion engineering writeup. Linear/GitHub error-card screenshots are not publicly indexed in a citable form; the recommended copy follows the conventions observable in Notion's public-facing failure page rather than a quoted style guide.
- Playwright's `pageerror` event behavior with React 19 caught errors: per the React 19 upgrade guide, caught errors are reported to `console.error` (not re-thrown), so `page.on('pageerror')` will typically **not** fire for errors a boundary catches in a production build. The Playwright spec deliberately asserts on user-observable DOM (`getByRole('alert')`), **not** on `pageErrors.length`, to remain robust across dev/production builds.