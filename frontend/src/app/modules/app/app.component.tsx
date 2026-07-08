import { type ReactElement } from 'react'
import { Provider } from 'react-redux'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Toaster } from '@/components/ui/sonner'
import { store } from './app.store'
import { useGetOnboardingStateQuery } from '~/api/onboarding.api'
import { AppErrorBoundary } from '~/error-boundary/app-error-boundary'
import { useGlobalErrorReporter } from '~/error-boundary/use-global-error-reporter'
import { ShellLayout } from '~/modules/app/components/shell-layout/shell-layout.component'
import { RequireAuth } from '~/modules/auth/components/require-auth.component'
import { useAuthBootstrap, useAuthBroadcastListener } from '~/modules/auth/hooks/auth.hooks'
import { CoachPage } from '~/modules/coaching/pages/coach.page'
import { HistoryPage } from '~/modules/logging/pages/history.page'
import { LogPage } from '~/modules/logging/pages/log.page'
import { OnboardingPage } from '~/modules/onboarding/pages/onboarding.page'
import { HomePage } from '~/modules/plan/pages/home.page'
import { SettingsPage } from '~/modules/settings/pages/settings.page'
import { LoginPage } from '~/pages/login/login.page'
import { RegisterPage } from '~/pages/register/register.page'
import { ThemeDebugPage } from '~dev-only/theme-debug'
import { ThrowOnQuery } from '~dev-only/throw-on-query'

interface OnboardingRedirectGuardProps {
  /**
   * Where to send the user when their onboarding completion state matches
   * the `expectComplete` flag — the home route gates against `false` so
   * incomplete users land on `/onboarding`; the onboarding route gates
   * against `true` so completed users land on `/`.
   */
  expectComplete: boolean
  redirectTo: string
  children: ReactElement
}

const isErrorStatus = (error: unknown, expected: number): boolean => {
  if (typeof error !== 'object' || error === null) return false
  const candidate = error as { status?: unknown }
  return candidate.status === expected
}

/**
 * Inline route guard that consults `getOnboardingState` and routes the
 * authenticated user based on whether onboarding has finished. Sits
 * inside `<RequireAuth>` so the auth bootstrap has already settled the
 * session by the time this query fires.
 *
 *   expectComplete=false (home guard)  → if state.isComplete === false ->
 *                                        Navigate to `/onboarding`.
 *   expectComplete=true  (onboarding guard) → if state.isComplete === true ->
 *                                        Navigate to `/`.
 *
 * Loading state: render a tiny role="status" placeholder so the route
 * does not flash the wrong page before the state lands. A 404 means "no
 * stream yet" — by definition incomplete, so the home guard redirects to
 * `/onboarding` while the onboarding guard renders its children.
 *
 * Non-404 errors render an inline retry alert instead of falling through
 * to the redirect arm — otherwise a 5xx on `/` would bounce the user to
 * `/onboarding`, where the same query keeps failing and the guard sends
 * them back, producing an oscillation.
 *
 * Spec § Unit 3 R03.3.
 */
export const OnboardingRedirectGuard = ({
  expectComplete,
  redirectTo,
  children,
}: OnboardingRedirectGuardProps): ReactElement => {
  const { data, isLoading, isError, error, refetch } = useGetOnboardingStateQuery(undefined)

  if (isLoading) {
    return (
      <div
        role="status"
        aria-live="polite"
        className="flex min-h-screen items-center justify-center bg-background"
      >
        <span className="text-sm text-muted-foreground">Loading…</span>
      </div>
    )
  }

  // 404 = no stream yet = onboarding is incomplete by definition.
  const isComplete = data?.isComplete ?? false
  const treatAs404 = isError && isErrorStatus(error, 404)
  const hasFatalError = isError && !treatAs404

  if (hasFatalError) {
    return (
      <div
        role="alert"
        data-testid="onboarding-guard-error"
        className="flex min-h-screen flex-col items-center justify-center gap-3 bg-background px-4 text-center"
      >
        <p className="text-sm text-muted-foreground">
          We couldn’t reach the onboarding service. Check your connection and try again.
        </p>
        <Button
          type="button"
          size="sm"
          onClick={() => {
            void refetch()
          }}
        >
          Retry
        </Button>
      </div>
    )
  }

  if (expectComplete && isComplete) {
    return <Navigate to={redirectTo} replace />
  }
  // Home guard: both "server says incomplete" and "404 (no stream yet)" are
  // treated as incomplete — redirect to onboarding in either case.
  const shouldRedirectToOnboarding = !expectComplete && (!isComplete || treatAs404)
  if (shouldRedirectToOnboarding) {
    return <Navigate to={redirectTo} replace />
  }

  return children
}

// Inner component runs inside the Redux Provider so auth hooks'
// `useDispatch` / `useSelector` resolve to the right store. Also the
// canonical home of `useGlobalErrorReporter()` (DEC-068 §10.3) — runs
// inside `<AppErrorBoundary>` so a throw from its own setup is still
// caught by the boundary instead of crashing the root render.
const AppShell = () => {
  useAuthBootstrap()
  useAuthBroadcastListener()
  useGlobalErrorReporter()

  return (
    <>
      {/* Build-time-stripped Playwright harness. `import.meta.env.DEV` is
          replaced with the literal `false` by Vite during `build`, so
          this entire JSX branch and the `ThrowOnQuery` import are
          tree-shaken to zero bytes in production. */}
      {import.meta.env.DEV && <ThrowOnQuery />}
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route
          path="/onboarding"
          element={
            <RequireAuth>
              <OnboardingRedirectGuard expectComplete={true} redirectTo="/">
                <OnboardingPage />
              </OnboardingRedirectGuard>
            </RequireAuth>
          }
        />
        <Route
          element={
            <RequireAuth>
              <ShellLayout />
            </RequireAuth>
          }
        >
          <Route
            path="/"
            element={
              <OnboardingRedirectGuard expectComplete={false} redirectTo="/onboarding">
                <HomePage />
              </OnboardingRedirectGuard>
            }
          />
          <Route
            path="/coach"
            element={
              <OnboardingRedirectGuard expectComplete={false} redirectTo="/onboarding">
                <CoachPage />
              </OnboardingRedirectGuard>
            }
          />
          <Route
            path="/log"
            element={
              <OnboardingRedirectGuard expectComplete={false} redirectTo="/onboarding">
                <LogPage />
              </OnboardingRedirectGuard>
            }
          />
          <Route
            path="/history"
            element={
              <OnboardingRedirectGuard expectComplete={false} redirectTo="/onboarding">
                <HistoryPage />
              </OnboardingRedirectGuard>
            }
          />
          {/* No onboarding guard — matches today's behavior. */}
          <Route path="/settings" element={<SettingsPage />} />
        </Route>
        {/* Dev-only design-token inspector. `import.meta.env.DEV` is
            replaced with the literal `false` by Vite during `build`, so
            this <Route> is never registered and `ThemeDebugPage` is
            tree-shaken from the production bundle — the route 404s in
            production. */}
        {import.meta.env.DEV && <Route path="/dev/theme-debug" element={<ThemeDebugPage />} />}
      </Routes>
    </>
  )
}

// `<AppErrorBoundary>` sits *inside* `<Provider>` so the Fallback can
// still consume Redux state if a future iteration of the card surfaces
// user-scoped diagnostics (e.g. tenant id, current route). It wraps
// `<BrowserRouter>` so render-time throws from any route component
// bubble to the single app-root card (DEC-068 §10.4).
export const App = () => {
  return (
    <Provider store={store}>
      <AppErrorBoundary>
        <BrowserRouter>
          <AppShell />
        </BrowserRouter>
      </AppErrorBoundary>
      {/* Single app-wide toast outlet. Mounted as a sibling *outside* the
          error boundary so a render-time throw in the route tree cannot
          unmount it, and toasts (e.g. the slice-2b log success) still surface
          on the recovery card. `Toaster` mirrors the `.dark` class itself. */}
      <Toaster />
    </Provider>
  )
}
