import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import { describe, expect, it, vi } from 'vitest'
import { configureStore } from '@reduxjs/toolkit'
import { Provider } from 'react-redux'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { authSlice } from '~/modules/auth/store/auth.slice'
import type { AuthState } from '~/modules/auth/models/auth.model'

// `useAuthBootstrap` dispatches real RTK Query `initiate` calls for
// `xsrf` and `me`; in jsdom those hit a missing fetch stub and pollute
// test output with unhandled rejections. `useAuthBroadcastListener`
// subscribes to a BroadcastChannel. Neither side effect is under test
// here — this spec only asserts the top-level route table renders and
// that the auth slice's initial `unknown` status shows the RequireAuth
// loading fallback. Full bootstrap behavior is exercised in the
// dedicated auth-hooks/RequireAuth specs.
vi.mock('~/modules/auth/hooks/auth.hooks', async () => {
  const actual = await vi.importActual<typeof import('~/modules/auth/hooks/auth.hooks')>(
    '~/modules/auth/hooks/auth.hooks',
  )
  return {
    ...actual,
    useAuthBootstrap: () => undefined,
    useAuthBroadcastListener: () => undefined,
  }
})

// `useGetOnboardingStateQuery` is hoisted so each test can configure
// the return value independently without re-importing.
const { getOnboardingStateMock } = vi.hoisted(() => ({
  getOnboardingStateMock: vi.fn(),
}))

vi.mock('~/api/onboarding.api', () => ({
  useGetOnboardingStateQuery: () => getOnboardingStateMock(),
}))

// `useGlobalErrorReporter` is called unconditionally at the top of `AppShell`,
// inside `<AppErrorBoundary>`. Mocking it lets one test force a render-time
// throw from there to exercise the boundary catch; it is a noop by default so
// every other `<App>` render is unaffected.
const { useGlobalErrorReporterMock } = vi.hoisted(() => ({
  useGlobalErrorReporterMock: vi.fn(),
}))

vi.mock('~/error-boundary/use-global-error-reporter', () => ({
  useGlobalErrorReporter: () => useGlobalErrorReporterMock(),
}))

// Keep the boundary's fire-and-forget client-error report from attempting a
// real network call when the throw path runs under jsdom.
vi.mock('~/error-boundary/report-client-error', () => ({
  reportClientError: vi.fn(),
}))

import { App } from './app.component'
import { OnboardingRedirectGuard } from './app.component'

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

// Records the current pathname so redirect assertions can be made
// without rendering real page content.
const LocationProbe = () => {
  const { pathname } = useLocation()
  return <div data-testid="location">{pathname}</div>
}

const authenticatedAuth: AuthState = {
  status: 'authenticated',
  user: { userId: 'usr_1', email: 'runner@example.com' },
}

const makeStore = (auth: AuthState = authenticatedAuth) =>
  configureStore({
    reducer: { [authSlice.name]: authSlice.reducer },
    preloadedState: { [authSlice.name]: auth },
  })

/**
 * Renders `OnboardingRedirectGuard` in a minimal two-route harness so
 * `<Navigate>` redirects land on a probe instead of a 404.
 *
 * - Starting route: where the guard is mounted
 * - Redirect route: where the guard might Navigate to
 */
const renderGuard = ({
  expectComplete,
  redirectTo,
  startAt,
}: {
  expectComplete: boolean
  redirectTo: string
  startAt: string
}) =>
  render(
    <Provider store={makeStore()}>
      <MemoryRouter initialEntries={[startAt]}>
        <Routes>
          <Route
            path={startAt}
            element={
              <OnboardingRedirectGuard expectComplete={expectComplete} redirectTo={redirectTo}>
                <div data-testid="guarded-content">children</div>
              </OnboardingRedirectGuard>
            }
          />
          <Route path={redirectTo} element={<LocationProbe />} />
        </Routes>
      </MemoryRouter>
    </Provider>,
  )

// ---------------------------------------------------------------------------
// App smoke test
// ---------------------------------------------------------------------------

describe('App', () => {
  it('mounts and renders the RequireAuth loading fallback on first render', () => {
    // OnboardingRedirectGuard will call the query — return loading so it
    // never proceeds past RequireAuth's own unknown-auth loading state.
    getOnboardingStateMock.mockReturnValue({ data: undefined, isLoading: true, isError: false })
    render(<App />)
    // Initial auth slice status === 'unknown' → RequireAuth shows the
    // loading fallback.
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('mounts the toast region so app-wide toasts render (pre-stages PR6b success toast)', async () => {
    getOnboardingStateMock.mockReturnValue({ data: undefined, isLoading: true, isError: false })
    render(<App />)

    act(() => {
      toast.success('Workout logged')
    })

    expect(await screen.findByText('Workout logged')).toBeInTheDocument()
    toast.dismiss()
  })

  it('keeps the toast outlet mounted when a render-time throw is caught by the error boundary', async () => {
    getOnboardingStateMock.mockReturnValue({ data: undefined, isLoading: true, isError: false })
    // React logs the caught render error to console.error — silence it so the
    // intentional throw does not leak to the test console.
    const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {})
    // Force a render-time throw *inside* AppErrorBoundary (AppShell calls this
    // hook unconditionally) so the boundary catches it and swaps in the
    // recovery card. The <Toaster> is a sibling mounted *outside* the boundary,
    // so it must survive the catch and still surface app-wide toasts. The throw
    // must be consistent across React 19's concurrent attempt *and* its
    // synchronous-recovery retry, otherwise React treats it as a recoverable
    // error instead of routing it to the boundary — so use mockImplementation,
    // not mockImplementationOnce. AppShell is unmounted once the fallback takes
    // over, so the hook is not called again and there is no throw loop.
    useGlobalErrorReporterMock.mockImplementation(() => {
      throw new Error('render boom')
    })

    try {
      render(<App />)

      // The boundary's recovery card replaced the route tree...
      expect(
        await screen.findByRole('heading', { name: /something went wrong/i }),
      ).toBeInTheDocument()

      // ...and a toast fired afterwards still appears, proving the Toaster was
      // not unmounted by the boundary catch (distinct text avoids colliding
      // with the prior toast spec's global sonner state).
      act(() => {
        toast.success('Logged during recovery')
      })
      expect(await screen.findByText('Logged during recovery')).toBeInTheDocument()
    } finally {
      // Always restore — a failed assertion above must not leak the throwing
      // mock or the console spy into later tests.
      toast.dismiss()
      useGlobalErrorReporterMock.mockReset()
      consoleErrorSpy.mockRestore()
    }
  })
})

// ---------------------------------------------------------------------------
// OnboardingRedirectGuard branch coverage
// ---------------------------------------------------------------------------

describe('OnboardingRedirectGuard', () => {
  // (a) 404 + expectComplete=true (on /onboarding) → render children
  it('renders children when a 404 is returned and expectComplete=true', () => {
    getOnboardingStateMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 404 },
      refetch: vi.fn(),
    })
    renderGuard({ expectComplete: true, redirectTo: '/', startAt: '/onboarding' })
    expect(screen.getByTestId('guarded-content')).toBeInTheDocument()
    expect(screen.queryByTestId('location')).not.toBeInTheDocument()
  })

  // (b) 404 + expectComplete=false (on /) → navigates to /onboarding
  it('navigates to /onboarding when a 404 is returned and expectComplete=false', () => {
    getOnboardingStateMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 404 },
      refetch: vi.fn(),
    })
    renderGuard({ expectComplete: false, redirectTo: '/onboarding', startAt: '/' })
    expect(screen.getByTestId('location')).toHaveTextContent('/onboarding')
    expect(screen.queryByTestId('guarded-content')).not.toBeInTheDocument()
  })

  // (c) data.isComplete=true + expectComplete=true (on /onboarding) → navigates to /
  it('navigates to / when isComplete=true and expectComplete=true', () => {
    getOnboardingStateMock.mockReturnValue({
      data: { isComplete: true },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderGuard({ expectComplete: true, redirectTo: '/', startAt: '/onboarding' })
    expect(screen.getByTestId('location')).toHaveTextContent('/')
    expect(screen.queryByTestId('guarded-content')).not.toBeInTheDocument()
  })

  // (d) data.isComplete=false + expectComplete=false (on /) → navigates to /onboarding
  it('navigates to /onboarding when isComplete=false and expectComplete=false', () => {
    getOnboardingStateMock.mockReturnValue({
      data: { isComplete: false },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderGuard({ expectComplete: false, redirectTo: '/onboarding', startAt: '/' })
    expect(screen.getByTestId('location')).toHaveTextContent('/onboarding')
    expect(screen.queryByTestId('guarded-content')).not.toBeInTheDocument()
  })

  // (e) non-404 error → renders the error alert; clicking Retry calls refetch
  it('renders the error alert for non-404 errors and clicking Retry calls refetch', async () => {
    const refetch = vi.fn()
    getOnboardingStateMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 500 },
      refetch,
    })
    const user = userEvent.setup()
    renderGuard({ expectComplete: false, redirectTo: '/onboarding', startAt: '/' })

    const alert = screen.getByTestId('onboarding-guard-error')
    expect(alert).toBeInTheDocument()
    expect(screen.queryByTestId('guarded-content')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /retry/i }))
    expect(refetch).toHaveBeenCalledOnce()
  })

  // isErrorStatus helper behaviour: status=503 is treated as fatal (non-404)
  it('renders the error alert for other non-404 status codes (e.g. 503)', () => {
    getOnboardingStateMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 503 },
      refetch: vi.fn(),
    })
    renderGuard({ expectComplete: false, redirectTo: '/onboarding', startAt: '/' })
    expect(screen.getByTestId('onboarding-guard-error')).toBeInTheDocument()
  })

  // isLoading renders the spinner placeholder
  it('renders the loading placeholder while the query is in flight', () => {
    getOnboardingStateMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      refetch: vi.fn(),
    })
    renderGuard({ expectComplete: false, redirectTo: '/onboarding', startAt: '/' })
    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.queryByTestId('guarded-content')).not.toBeInTheDocument()
  })

  // data.isComplete=true + expectComplete=false → renders children (home guard pass-through)
  it('renders children when isComplete=true and expectComplete=false', () => {
    getOnboardingStateMock.mockReturnValue({
      data: { isComplete: true },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderGuard({ expectComplete: false, redirectTo: '/onboarding', startAt: '/' })
    expect(screen.getByTestId('guarded-content')).toBeInTheDocument()
    expect(screen.queryByTestId('location')).not.toBeInTheDocument()
  })

  // (f) data.isComplete=false + expectComplete=true (on /onboarding) → renders
  // children. This is the most common in-progress hot path — every onboarding
  // page load with a server-side stream hits it. Regressions here would be
  // user-visible (e.g., a guard accidentally redirecting away mid-flow).
  it('renders children when isComplete=false and expectComplete=true (in-progress hot path)', () => {
    getOnboardingStateMock.mockReturnValue({
      data: { isComplete: false },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderGuard({ expectComplete: true, redirectTo: '/', startAt: '/onboarding' })
    expect(screen.getByTestId('guarded-content')).toBeInTheDocument()
    expect(screen.queryByTestId('location')).not.toBeInTheDocument()
  })
})
