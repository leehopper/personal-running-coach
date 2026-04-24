import { configureStore } from '@reduxjs/toolkit'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent, { type UserEvent } from '@testing-library/user-event'
import { Provider } from 'react-redux'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { authSlice } from '~/modules/auth/store/auth.slice'

// Hoisted mock references — vi.mock is hoisted above imports, so the trigger
// and unwrap mocks must be created inside the factory or via `vi.hoisted`.
const { loginUnwrap, loginTrigger, navigateMock } = vi.hoisted(() => {
  const unwrap = vi.fn()
  return {
    loginUnwrap: unwrap,
    loginTrigger: vi.fn(() => ({ unwrap })),
    navigateMock: vi.fn(),
  }
})

vi.mock('~/api/auth.api', () => ({
  useLoginMutation: () => [loginTrigger, { isLoading: false }],
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')
  return { ...actual, useNavigate: () => navigateMock }
})

import { LoginPage } from './login.page'

const makeStore = () => configureStore({ reducer: { [authSlice.name]: authSlice.reducer } })

type RenderOptions = { initialEntries?: Array<string | { pathname: string; state: unknown }> }

const renderLogin = ({ initialEntries = ['/login'] }: RenderOptions = {}) => {
  const store = makeStore()
  const user = userEvent.setup()
  return {
    store,
    user,
    ...render(
      <Provider store={store}>
        <MemoryRouter initialEntries={initialEntries}>
          <LoginPage />
        </MemoryRouter>
      </Provider>,
    ),
  }
}

const fillEmail = async (user: UserEvent, value: string): Promise<void> => {
  await user.type(screen.getByLabelText(/email/i), value)
}

const fillPassword = async (user: UserEvent, value: string): Promise<void> => {
  await user.type(screen.getByLabelText(/password/i), value)
}

// Submits the <form> via a native submit event so the assertion works even
// when the submit button is disabled (userEvent.click on a disabled button
// is a no-op). This is the single justified use of fireEvent — no equivalent
// userEvent API for "submit a form whose button is disabled" exists.
const submitForm = (): void => {
  const form = document.querySelector('form')
  if (form === null) throw new Error('Login form not found')
  fireEvent.submit(form)
}

const VALID_EMAIL = 'runner@example.com'
const VALID_PASSWORD = 'StrongPassw0rd!'

describe('LoginPage', () => {
  beforeEach(() => {
    loginUnwrap.mockReset()
    loginTrigger.mockClear()
    navigateMock.mockReset()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  describe('client-side validation', () => {
    it('shows required errors when the form is submitted empty and does not invoke the mutation', async () => {
      renderLogin()
      submitForm()
      expect(await screen.findByText('Email is required.')).toBeInTheDocument()
      expect(await screen.findByText('Password is required.')).toBeInTheDocument()
      expect(loginTrigger).not.toHaveBeenCalled()
    })

    // Login's Zod schema is intentionally permissive (non-empty only) so
    // format + complexity pre-validation on the client cannot leak a
    // side-channel about whether a stored credential could possibly meet
    // the current rules. Full credential validation is the server's uniform
    // timing-safe 401 (DEC-053). The register page is where the format /
    // complexity assertions live — see register.page.spec.tsx.
    it('accepts an unusually-shaped email client-side and forwards it to the mutation', async () => {
      loginUnwrap.mockResolvedValue({ userId: 'usr_abc', email: 'legacy@example' })
      const { user } = renderLogin()
      await fillEmail(user, 'legacy@example')
      await fillPassword(user, 'any-non-empty')
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled()
      })
      submitForm()
      await waitFor(() => {
        expect(loginTrigger).toHaveBeenCalledWith({
          email: 'legacy@example',
          password: 'any-non-empty',
        })
      })
      expect(screen.queryByText(/email must be a valid address/i)).not.toBeInTheDocument()
    })

    // Login schema deliberately accepts any non-empty password (timing-safe
    // 401 posture — full credential validation is the server's job). A
    // "weak" password on the login page is a valid client-side submission;
    // the failure path lives in the server's uniform 401 response, exercised
    // by the server-error tests below.
    it('accepts a weak (but non-empty) password at the client tier', async () => {
      const { user } = renderLogin()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, 'x')
      await waitFor(() => {
        expect(screen.queryByText(/password must/i)).not.toBeInTheDocument()
      })
      expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled()
    })
  })

  describe('submit button gating', () => {
    it('is disabled while the form is structurally invalid', () => {
      renderLogin()
      expect(screen.getByRole('button', { name: /sign in/i })).toBeDisabled()
    })

    it('becomes enabled once email and password fields are filled with valid values', async () => {
      const { user } = renderLogin()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled()
      })
    })
  })

  describe('happy-path submission', () => {
    it('invokes the login mutation, dispatches sessionVerified, and navigates to "/"', async () => {
      loginUnwrap.mockResolvedValue({ userId: 'usr_abc', email: VALID_EMAIL })
      const { store, user } = renderLogin()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      const submitButton = screen.getByRole('button', { name: /sign in/i })
      await waitFor(() => {
        expect(submitButton).not.toBeDisabled()
      })
      await user.click(submitButton)
      await waitFor(() => {
        expect(loginTrigger).toHaveBeenCalledWith({
          email: VALID_EMAIL,
          password: VALID_PASSWORD,
        })
      })
      await waitFor(() => {
        expect(navigateMock).toHaveBeenCalledWith('/', { replace: true })
      })
      const state = store.getState()
      expect(state.auth.status).toBe('authenticated')
      expect(state.auth.user).toEqual({ userId: 'usr_abc', email: VALID_EMAIL })
    })

    it('respects a sanitized next-path on location.state', async () => {
      loginUnwrap.mockResolvedValue({ userId: 'usr_abc', email: VALID_EMAIL })
      const { user } = renderLogin({
        initialEntries: [{ pathname: '/login', state: { next: '/dashboard' } }],
      })
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      const submitButton = screen.getByRole('button', { name: /sign in/i })
      await waitFor(() => {
        expect(submitButton).not.toBeDisabled()
      })
      await user.click(submitButton)
      await waitFor(() => {
        expect(navigateMock).toHaveBeenCalledWith('/dashboard', { replace: true })
      })
    })

    it('ignores an external-origin next-path on location.state', async () => {
      loginUnwrap.mockResolvedValue({ userId: 'usr_abc', email: VALID_EMAIL })
      const { user } = renderLogin({
        initialEntries: [{ pathname: '/login', state: { next: '//evil.example.com' } }],
      })
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      const submitButton = screen.getByRole('button', { name: /sign in/i })
      await waitFor(() => {
        expect(submitButton).not.toBeDisabled()
      })
      await user.click(submitButton)
      await waitFor(() => {
        expect(navigateMock).toHaveBeenCalledWith('/', { replace: true })
      })
    })
  })

  describe('server-side error surfacing', () => {
    it('renders ValidationProblemDetails.errors at the field level', async () => {
      loginUnwrap.mockRejectedValue({
        status: 400,
        data: {
          type: 'https://httpstatuses.com/400',
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: {
            Email: ['Email is malformed.'],
            Password: ['Password cannot be blank.'],
          },
        },
      })
      const { user } = renderLogin()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      const submitButton = screen.getByRole('button', { name: /sign in/i })
      await waitFor(() => {
        expect(submitButton).not.toBeDisabled()
      })
      await user.click(submitButton)
      expect(await screen.findByText('Email is malformed.')).toBeInTheDocument()
      expect(await screen.findByText('Password cannot be blank.')).toBeInTheDocument()
    })

    it('renders ProblemDetails.title as a non-field-level alert', async () => {
      loginUnwrap.mockRejectedValue({
        status: 401,
        data: {
          type: 'https://httpstatuses.com/401',
          title: 'Invalid email or password.',
          status: 401,
        },
      })
      const { user } = renderLogin()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      const submitButton = screen.getByRole('button', { name: /sign in/i })
      await waitFor(() => {
        expect(submitButton).not.toBeDisabled()
      })
      await user.click(submitButton)
      const alert = await screen.findByTestId('form-alert')
      expect(alert).toHaveTextContent('Invalid email or password.')
    })

    it('falls back to a generic alert when the server omits a title', async () => {
      loginUnwrap.mockRejectedValue({
        status: 500,
        data: {},
      })
      const { user } = renderLogin()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      const submitButton = screen.getByRole('button', { name: /sign in/i })
      await waitFor(() => {
        expect(submitButton).not.toBeDisabled()
      })
      await user.click(submitButton)
      const alert = await screen.findByTestId('form-alert')
      expect(alert).toHaveTextContent(/login failed/i)
    })
  })
})
