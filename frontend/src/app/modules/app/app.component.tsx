import { type ReactElement } from 'react'
import { Provider } from 'react-redux'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { store } from './app.store'
import { useGetOnboardingStateQuery } from '~/api/onboarding.api'
import { RequireAuth } from '~/modules/auth/components/require-auth.component'
import { useAuthBootstrap, useAuthBroadcastListener } from '~/modules/auth/hooks/auth.hooks'
import { OnboardingPage } from '~/modules/onboarding/pages/onboarding.page'
import { HomePage } from '~/modules/plan/pages/home.page'
import { SettingsPage } from '~/modules/settings/pages/settings.page'
import { LoginPage } from '~/pages/login/login.page'
import { RegisterPage } from '~/pages/register/register.page'

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
        className="flex min-h-screen items-center justify-center bg-slate-50"
      >
        <span className="text-sm text-slate-500">Loading…</span>
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
        className="flex min-h-screen flex-col items-center justify-center gap-3 bg-slate-50 px-4 text-center"
      >
        <p className="text-sm text-slate-700">
          We couldn’t reach the onboarding service. Check your connection and try again.
        </p>
        <button
          type="button"
          onClick={() => {
            void refetch()
          }}
          className="rounded bg-slate-900 px-3 py-1 text-xs font-medium text-white"
        >
          Retry
        </button>
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
// `useDispatch` / `useSelector` resolve to the right store.
const AppShell = () => {
  useAuthBootstrap()
  useAuthBroadcastListener()

  return (
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
        path="/"
        element={
          <RequireAuth>
            <OnboardingRedirectGuard expectComplete={false} redirectTo="/onboarding">
              <HomePage />
            </OnboardingRedirectGuard>
          </RequireAuth>
        }
      />
      <Route
        path="/settings"
        element={
          <RequireAuth>
            <SettingsPage />
          </RequireAuth>
        }
      />
    </Routes>
  )
}

export const App = () => {
  return (
    <Provider store={store}>
      <BrowserRouter>
        <AppShell />
      </BrowserRouter>
    </Provider>
  )
}
