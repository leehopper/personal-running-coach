import { type ReactElement } from 'react'
import { Provider } from 'react-redux'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { store } from './app.store'
import { useGetOnboardingStateQuery } from '~/api/onboarding.api'
import { RequireAuth } from '~/modules/auth/components/require-auth.component'
import { useAuthBootstrap, useAuthBroadcastListener } from '~/modules/auth/hooks/auth.hooks'
import { OnboardingPage } from '~/modules/onboarding/pages/onboarding.page'
import { HomePage } from '~/modules/plan/pages/home.page'
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
 * Spec § Unit 3 R03.3.
 */
const OnboardingRedirectGuard = ({
  expectComplete,
  redirectTo,
  children,
}: OnboardingRedirectGuardProps): ReactElement => {
  const { data, isLoading, isError, error } = useGetOnboardingStateQuery(undefined)

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

  if (expectComplete && isComplete) {
    return <Navigate to={redirectTo} replace />
  }
  if (!expectComplete && !isComplete && !treatAs404) {
    // Home guard, server says incomplete -> redirect to onboarding.
    return <Navigate to={redirectTo} replace />
  }
  if (!expectComplete && treatAs404) {
    // Home guard, no stream yet -> redirect to onboarding.
    return <Navigate to={redirectTo} replace />
  }

  return children
}

const isErrorStatus = (error: unknown, expected: number): boolean => {
  if (typeof error !== 'object' || error === null) return false
  const candidate = error as { status?: unknown }
  return candidate.status === expected
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
