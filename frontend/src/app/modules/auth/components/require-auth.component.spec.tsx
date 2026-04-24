import { configureStore } from '@reduxjs/toolkit'
import { render, screen } from '@testing-library/react'
import { Provider } from 'react-redux'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import type { AuthState } from '~/modules/auth/models/auth.model'
import { authSlice } from '~/modules/auth/store/auth.slice'
import { RequireAuth } from './require-auth.component'

const makeStore = (preloadedAuth: AuthState) =>
  configureStore({
    reducer: { [authSlice.name]: authSlice.reducer },
    preloadedState: { [authSlice.name]: preloadedAuth },
  })

// Renders the current location.state so tests can assert what `<Navigate>`
// placed into router state after redirecting. Real LoginPage is not wired
// here so the component tree stays focused on the guard's output.
const LoginLocationProbe = () => {
  const location = useLocation()
  const state = location.state as { next?: string } | null
  return (
    <div data-testid="login-probe">
      <span data-testid="login-path">{location.pathname}</span>
      <span data-testid="login-next">{state?.next ?? ''}</span>
    </div>
  )
}

const renderGuarded = (
  auth: AuthState,
  startingEntry: string | { pathname: string; search?: string; hash?: string },
) =>
  render(
    <Provider store={makeStore(auth)}>
      <MemoryRouter initialEntries={[startingEntry]}>
        <Routes>
          <Route
            path="/"
            element={
              <RequireAuth>
                <div data-testid="protected">protected content</div>
              </RequireAuth>
            }
          />
          <Route path="/login" element={<LoginLocationProbe />} />
        </Routes>
      </MemoryRouter>
    </Provider>,
  )

describe('RequireAuth', () => {
  it('renders the loading fallback while auth status is unknown', () => {
    renderGuarded({ status: 'unknown', user: null }, '/')
    expect(screen.getByRole('status')).toHaveTextContent(/loading/i)
    expect(screen.queryByTestId('protected')).not.toBeInTheDocument()
    expect(screen.queryByTestId('login-probe')).not.toBeInTheDocument()
  })

  it('renders the protected children when the user is authenticated', () => {
    renderGuarded(
      { status: 'authenticated', user: { userId: 'usr_abc', email: 'runner@example.com' } },
      '/',
    )
    expect(screen.getByTestId('protected')).toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('redirects unauthenticated users to /login and preserves the attempted path in state.next', () => {
    renderGuarded({ status: 'unauthenticated', user: null }, '/')
    expect(screen.getByTestId('login-path')).toHaveTextContent('/login')
    expect(screen.getByTestId('login-next')).toHaveTextContent('/')
  })

  it('preserves pathname, search, and hash in state.next for deep-link restoration', () => {
    renderGuarded(
      { status: 'unauthenticated', user: null },
      { pathname: '/', search: '?tab=plan', hash: '#week-3' },
    )
    expect(screen.getByTestId('login-next')).toHaveTextContent('/?tab=plan#week-3')
  })
})
