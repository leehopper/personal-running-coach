import { configureStore } from '@reduxjs/toolkit'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
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
  return {
    store,
    ...render(
      <Provider store={store}>
        <MemoryRouter initialEntries={initialEntries}>
          <LoginPage />
        </MemoryRouter>
      </Provider>,
    ),
  }
}

const fillEmail = (value: string): void => {
  fireEvent.change(screen.getByLabelText(/email/i), { target: { value } })
}

const fillPassword = (value: string): void => {
  fireEvent.change(screen.getByLabelText(/password/i), { target: { value } })
}

// Submit the <form> element directly so the assertion works even when the
// submit button is disabled (empty form). This mirrors the intent of the
// task's "empty-submit validation" bullet: exercise the RHF / Zod resolver's
// error-surfacing path, not the DOM-level button click.
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
      renderLogin()
      fillEmail('legacy@example')
      fillPassword('any-non-empty')
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
      renderLogin()
      fillEmail(VALID_EMAIL)
      fillPassword('x')
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
      renderLogin()
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled()
      })
    })
  })

  describe('happy-path submission', () => {
    it('invokes the login mutation, dispatches sessionVerified, and navigates to "/"', async () => {
      loginUnwrap.mockResolvedValue({ userId: 'usr_abc', email: VALID_EMAIL })
      const { store } = renderLogin()
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled()
      })
      submitForm()
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
      renderLogin({
        initialEntries: [{ pathname: '/login', state: { next: '/dashboard' } }],
      })
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled()
      })
      submitForm()
      await waitFor(() => {
        expect(navigateMock).toHaveBeenCalledWith('/dashboard', { replace: true })
      })
    })

    it('ignores an external-origin next-path on location.state', async () => {
      loginUnwrap.mockResolvedValue({ userId: 'usr_abc', email: VALID_EMAIL })
      renderLogin({
        initialEntries: [{ pathname: '/login', state: { next: '//evil.example.com' } }],
      })
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled()
      })
      submitForm()
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
      renderLogin()
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled()
      })
      submitForm()
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
      renderLogin()
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled()
      })
      submitForm()
      const alert = await screen.findByTestId('form-alert')
      expect(alert).toHaveTextContent('Invalid email or password.')
    })

    it('falls back to a generic alert when the server omits a title', async () => {
      loginUnwrap.mockRejectedValue({
        status: 500,
        data: {},
      })
      renderLogin()
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled()
      })
      submitForm()
      const alert = await screen.findByTestId('form-alert')
      expect(alert).toHaveTextContent(/login failed/i)
    })
  })
})
