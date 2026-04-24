import { configureStore } from '@reduxjs/toolkit'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Provider } from 'react-redux'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { AuthState } from '~/modules/auth/models/auth.model'
import { authSlice } from '~/modules/auth/store/auth.slice'

const { logoutUnwrap, logoutTrigger, navigateMock, postLogoutBroadcastMock } = vi.hoisted(() => {
  const unwrap = vi.fn()
  return {
    logoutUnwrap: unwrap,
    logoutTrigger: vi.fn(() => ({ unwrap })),
    navigateMock: vi.fn(),
    postLogoutBroadcastMock: vi.fn(),
  }
})

vi.mock('~/api/auth.api', () => ({
  useLogoutMutation: () => [logoutTrigger, { isLoading: false }],
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')
  return { ...actual, useNavigate: () => navigateMock }
})

vi.mock('~/modules/auth/lib/broadcast-auth', () => ({
  postLogoutBroadcast: postLogoutBroadcastMock,
}))

import { HomePage } from './home.page'

const AUTHENTICATED_USER = { userId: 'usr_abc', email: 'runner@example.com' }

const makeStore = () => {
  const preloadedAuth: AuthState = { status: 'authenticated', user: AUTHENTICATED_USER }
  return configureStore({
    reducer: { [authSlice.name]: authSlice.reducer },
    preloadedState: { [authSlice.name]: preloadedAuth },
  })
}

const renderHome = () => {
  const store = makeStore()
  const user = userEvent.setup()
  return {
    store,
    user,
    ...render(
      <Provider store={store}>
        <MemoryRouter initialEntries={['/']}>
          <HomePage />
        </MemoryRouter>
      </Provider>,
    ),
  }
}

describe('HomePage', () => {
  beforeEach(() => {
    logoutUnwrap.mockReset()
    logoutTrigger.mockClear()
    navigateMock.mockReset()
    postLogoutBroadcastMock.mockReset()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('renders the authenticated user email in the greeting', () => {
    renderHome()
    expect(screen.getByText(`Logged in as ${AUTHENTICATED_USER.email}`)).toBeInTheDocument()
  })

  it('signs out: invokes the logout mutation, flips the slice, broadcasts, and navigates to /login', async () => {
    logoutUnwrap.mockResolvedValue(undefined)
    const { store, user } = renderHome()

    await user.click(screen.getByRole('button', { name: /sign out/i }))

    await waitFor(() => {
      expect(logoutTrigger).toHaveBeenCalledWith(undefined)
    })
    await waitFor(() => {
      expect(navigateMock).toHaveBeenCalledWith('/login', { replace: true })
    })
    expect(postLogoutBroadcastMock).toHaveBeenCalledTimes(1)
    expect(store.getState().auth.status).toBe('unauthenticated')
    expect(store.getState().auth.user).toBeNull()
  })

  it('still flips to unauthenticated when the server-side logout fails', async () => {
    logoutUnwrap.mockRejectedValue(new Error('network down'))
    const { store, user } = renderHome()

    await user.click(screen.getByRole('button', { name: /sign out/i }))

    await waitFor(() => {
      expect(navigateMock).toHaveBeenCalledWith('/login', { replace: true })
    })
    expect(store.getState().auth.status).toBe('unauthenticated')
  })
})
