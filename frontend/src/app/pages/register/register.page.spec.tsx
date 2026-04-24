import { configureStore } from '@reduxjs/toolkit'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { Provider } from 'react-redux'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { authSlice } from '~/modules/auth/store/auth.slice'

const { registerUnwrap, registerTrigger, loginUnwrap, loginTrigger, navigateMock } = vi.hoisted(
  () => {
    const rUnwrap = vi.fn()
    const lUnwrap = vi.fn()
    return {
      registerUnwrap: rUnwrap,
      registerTrigger: vi.fn(() => ({ unwrap: rUnwrap })),
      loginUnwrap: lUnwrap,
      loginTrigger: vi.fn(() => ({ unwrap: lUnwrap })),
      navigateMock: vi.fn(),
    }
  },
)

vi.mock('~/api/auth.api', () => ({
  useRegisterMutation: () => [registerTrigger, { isLoading: false }],
  useLoginMutation: () => [loginTrigger, { isLoading: false }],
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')
  return { ...actual, useNavigate: () => navigateMock }
})

import { RegisterPage } from './register.page'

const makeStore = () => configureStore({ reducer: { [authSlice.name]: authSlice.reducer } })

const renderRegister = () => {
  const store = makeStore()
  return {
    store,
    ...render(
      <Provider store={store}>
        <MemoryRouter initialEntries={['/register']}>
          <RegisterPage />
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

const submitForm = (): void => {
  const form = document.querySelector('form')
  if (form === null) throw new Error('Register form not found')
  fireEvent.submit(form)
}

const VALID_EMAIL = 'runner@example.com'
// Mirrors the backend's ASP.NET Identity policy: ≥ 12 chars, upper + lower +
// digit + non-alphanumeric (Program.cs lines 108–127 / auth.schema.ts).
const VALID_PASSWORD = 'StrongPassw0rd!'

describe('RegisterPage', () => {
  beforeEach(() => {
    registerUnwrap.mockReset()
    registerTrigger.mockClear()
    loginUnwrap.mockReset()
    loginTrigger.mockClear()
    navigateMock.mockReset()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  describe('client-side validation', () => {
    it('shows validation errors when the form is submitted empty and does not invoke the mutation', async () => {
      renderRegister()
      submitForm()
      // Register uses `emailSchema` which runs `.email()` before `.min(1)`,
      // so an empty value surfaces the format message first. The login page
      // uses a permissive schema and surfaces "Email is required." instead.
      expect(await screen.findByText('Email must be a valid address.')).toBeInTheDocument()
      expect(
        await screen.findByText(/password must be at least 12 characters/i),
      ).toBeInTheDocument()
      expect(registerTrigger).not.toHaveBeenCalled()
    })

    it('rejects an improperly formatted email', async () => {
      renderRegister()
      fillEmail('not-an-email')
      fillPassword(VALID_PASSWORD)
      submitForm()
      expect(await screen.findByText('Email must be a valid address.')).toBeInTheDocument()
      expect(registerTrigger).not.toHaveBeenCalled()
    })

    describe('weak-password validation mirrors the backend Identity policy', () => {
      it('rejects a too-short password (< 12 chars)', async () => {
        renderRegister()
        fillEmail(VALID_EMAIL)
        fillPassword('Short1!')
        submitForm()
        expect(
          await screen.findByText(/password must be at least 12 characters/i),
        ).toBeInTheDocument()
        expect(registerTrigger).not.toHaveBeenCalled()
      })

      it('rejects a password missing an uppercase letter', async () => {
        renderRegister()
        fillEmail(VALID_EMAIL)
        fillPassword('lowercase0!word')
        submitForm()
        expect(
          await screen.findByText(/password must contain an uppercase letter/i),
        ).toBeInTheDocument()
      })

      it('rejects a password missing a lowercase letter', async () => {
        renderRegister()
        fillEmail(VALID_EMAIL)
        fillPassword('UPPERCASE0!WORD')
        submitForm()
        expect(
          await screen.findByText(/password must contain a lowercase letter/i),
        ).toBeInTheDocument()
      })

      it('rejects a password missing a digit', async () => {
        renderRegister()
        fillEmail(VALID_EMAIL)
        fillPassword('NoDigitsHere!!')
        submitForm()
        expect(await screen.findByText(/password must contain a digit/i)).toBeInTheDocument()
      })

      it('rejects a password missing a non-alphanumeric character', async () => {
        renderRegister()
        fillEmail(VALID_EMAIL)
        fillPassword('NoSymbolHere0')
        submitForm()
        expect(
          await screen.findByText(/password must contain a non-alphanumeric character/i),
        ).toBeInTheDocument()
      })
    })
  })

  describe('submit button gating', () => {
    it('is disabled while the form is structurally invalid', () => {
      renderRegister()
      expect(screen.getByRole('button', { name: /create account/i })).toBeDisabled()
    })

    it('becomes enabled only when email + policy-compliant password are present', async () => {
      renderRegister()
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /create account/i })).not.toBeDisabled()
      })
    })
  })

  describe('happy-path submission', () => {
    it('chains register + login, dispatches sessionVerified, and navigates to "/"', async () => {
      registerUnwrap.mockResolvedValue({ userId: 'usr_abc', email: VALID_EMAIL })
      loginUnwrap.mockResolvedValue({ userId: 'usr_abc', email: VALID_EMAIL })
      const { store } = renderRegister()
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /create account/i })).not.toBeDisabled()
      })
      submitForm()
      await waitFor(() => {
        expect(registerTrigger).toHaveBeenCalledWith({
          email: VALID_EMAIL,
          password: VALID_PASSWORD,
        })
      })
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
  })

  describe('server-side error surfacing', () => {
    it('renders ValidationProblemDetails.errors at the field level from the register response', async () => {
      registerUnwrap.mockRejectedValue({
        status: 400,
        data: {
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: {
            Email: ['Email is already in use.'],
            Password: ['Password does not meet the policy.'],
          },
        },
      })
      renderRegister()
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /create account/i })).not.toBeDisabled()
      })
      submitForm()
      expect(await screen.findByText('Email is already in use.')).toBeInTheDocument()
      expect(await screen.findByText('Password does not meet the policy.')).toBeInTheDocument()
      // Auto-login should NOT fire when register fails.
      expect(loginTrigger).not.toHaveBeenCalled()
    })

    it('renders ProblemDetails.title as a non-field-level alert on register failure', async () => {
      registerUnwrap.mockRejectedValue({
        status: 409,
        data: { title: 'This email is already registered.', status: 409 },
      })
      renderRegister()
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /create account/i })).not.toBeDisabled()
      })
      submitForm()
      const alert = await screen.findByTestId('form-alert')
      expect(alert).toHaveTextContent('This email is already registered.')
      expect(loginTrigger).not.toHaveBeenCalled()
    })

    it('surfaces a distinct alert when register succeeds but the auto-login step fails', async () => {
      registerUnwrap.mockResolvedValue({ userId: 'usr_abc', email: VALID_EMAIL })
      loginUnwrap.mockRejectedValue({
        status: 500,
        data: { title: 'Temporary sign-in failure — please sign in manually.', status: 500 },
      })
      renderRegister()
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /create account/i })).not.toBeDisabled()
      })
      submitForm()
      const alert = await screen.findByTestId('form-alert')
      expect(alert).toHaveTextContent(/temporary sign-in failure/i)
      expect(navigateMock).not.toHaveBeenCalled()
    })

    it('falls back to a generic alert when the server omits a title', async () => {
      registerUnwrap.mockRejectedValue({ status: 500, data: {} })
      renderRegister()
      fillEmail(VALID_EMAIL)
      fillPassword(VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /create account/i })).not.toBeDisabled()
      })
      submitForm()
      const alert = await screen.findByTestId('form-alert')
      expect(alert).toHaveTextContent(/registration failed/i)
    })
  })
})
