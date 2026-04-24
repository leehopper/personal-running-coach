import { configureStore } from '@reduxjs/toolkit'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent, { type UserEvent } from '@testing-library/user-event'
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
  const user = userEvent.setup()
  return {
    store,
    user,
    ...render(
      <Provider store={store}>
        <MemoryRouter initialEntries={['/register']}>
          <RegisterPage />
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

// See login.page.spec.tsx — native form submit is the only way to trigger
// the RHF / Zod resolver while the submit button is still disabled.
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
      // `emailSchema` trims first so `.min(1)` surfaces "Email is required."
      // on an empty submission; the dedicated format test below covers the
      // `.email()` failure path for malformed-but-non-empty input.
      expect(await screen.findByText('Email is required.')).toBeInTheDocument()
      expect(
        await screen.findByText(/password must be at least 12 characters/i),
      ).toBeInTheDocument()
      expect(registerTrigger).not.toHaveBeenCalled()
    })

    it('rejects an improperly formatted email', async () => {
      const { user } = renderRegister()
      await fillEmail(user, 'not-an-email')
      await fillPassword(user, VALID_PASSWORD)
      submitForm()
      expect(await screen.findByText('Email must be a valid address.')).toBeInTheDocument()
      expect(registerTrigger).not.toHaveBeenCalled()
    })

    describe('weak-password validation mirrors the backend Identity policy', () => {
      it('rejects a too-short password (< 12 chars)', async () => {
        const { user } = renderRegister()
        await fillEmail(user, VALID_EMAIL)
        await fillPassword(user, 'Short1!')
        submitForm()
        expect(
          await screen.findByText(/password must be at least 12 characters/i),
        ).toBeInTheDocument()
        expect(registerTrigger).not.toHaveBeenCalled()
      })

      it('rejects a password missing an uppercase letter', async () => {
        const { user } = renderRegister()
        await fillEmail(user, VALID_EMAIL)
        await fillPassword(user, 'lowercase0!word')
        submitForm()
        expect(
          await screen.findByText(/password must contain an uppercase letter/i),
        ).toBeInTheDocument()
      })

      it('rejects a password missing a lowercase letter', async () => {
        const { user } = renderRegister()
        await fillEmail(user, VALID_EMAIL)
        await fillPassword(user, 'UPPERCASE0!WORD')
        submitForm()
        expect(
          await screen.findByText(/password must contain a lowercase letter/i),
        ).toBeInTheDocument()
      })

      it('rejects a password missing a digit', async () => {
        const { user } = renderRegister()
        await fillEmail(user, VALID_EMAIL)
        await fillPassword(user, 'NoDigitsHere!!')
        submitForm()
        expect(await screen.findByText(/password must contain a digit/i)).toBeInTheDocument()
      })

      it('rejects a password missing a non-alphanumeric character', async () => {
        const { user } = renderRegister()
        await fillEmail(user, VALID_EMAIL)
        await fillPassword(user, 'NoSymbolHere0')
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
      const { user } = renderRegister()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /create account/i })).not.toBeDisabled()
      })
    })
  })

  describe('happy-path submission', () => {
    it('chains register + login, dispatches sessionVerified, and navigates to "/"', async () => {
      registerUnwrap.mockResolvedValue({ userId: 'usr_abc', email: VALID_EMAIL })
      loginUnwrap.mockResolvedValue({ userId: 'usr_abc', email: VALID_EMAIL })
      const { store, user } = renderRegister()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      const submitButton = screen.getByRole('button', { name: /create account/i })
      await waitFor(() => {
        expect(submitButton).not.toBeDisabled()
      })
      await user.click(submitButton)
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
      const { user } = renderRegister()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      const submitButton = screen.getByRole('button', { name: /create account/i })
      await waitFor(() => {
        expect(submitButton).not.toBeDisabled()
      })
      await user.click(submitButton)
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
      const { user } = renderRegister()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      const submitButton = screen.getByRole('button', { name: /create account/i })
      await waitFor(() => {
        expect(submitButton).not.toBeDisabled()
      })
      await user.click(submitButton)
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
      const { store, user } = renderRegister()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      const submitButton = screen.getByRole('button', { name: /create account/i })
      await waitFor(() => {
        expect(submitButton).not.toBeDisabled()
      })
      await user.click(submitButton)
      const alert = await screen.findByTestId('form-alert')
      expect(alert).toHaveTextContent(/temporary sign-in failure/i)
      expect(navigateMock).not.toHaveBeenCalled()
      // Register succeeded, login failed — the Redux slice MUST stay in its
      // pre-submit state rather than flip to authenticated. Guards against
      // a future refactor that accidentally dispatches `sessionVerified`
      // outside the `try` that awaits login.
      const state = store.getState()
      expect(state.auth.status).toBe('unknown')
      expect(state.auth.user).toBeNull()
    })

    it('falls back to a generic alert when the server omits a title', async () => {
      registerUnwrap.mockRejectedValue({ status: 500, data: {} })
      const { user } = renderRegister()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, VALID_PASSWORD)
      const submitButton = screen.getByRole('button', { name: /create account/i })
      await waitFor(() => {
        expect(submitButton).not.toBeDisabled()
      })
      await user.click(submitButton)
      const alert = await screen.findByTestId('form-alert')
      expect(alert).toHaveTextContent(/registration failed/i)
    })
  })
})
