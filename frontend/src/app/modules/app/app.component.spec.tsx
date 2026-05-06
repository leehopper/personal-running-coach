import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
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
})
