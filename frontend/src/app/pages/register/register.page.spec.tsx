import { configureStore } from '@reduxjs/toolkit'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent, { type UserEvent } from '@testing-library/user-event'
import { Provider } from 'react-redux'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'
import { authSlice } from '~/modules/auth/store/auth.slice'

const {
  registerUnwrap,
  registerTrigger,
  registerMutationState,
  loginUnwrap,
  loginTrigger,
  loginMutationState,
  navigateMock,
} = vi.hoisted(() => {
  const rUnwrap = vi.fn()
  const lUnwrap = vi.fn()
  return {
    registerUnwrap: rUnwrap,
    registerTrigger: vi.fn(() => ({ unwrap: rUnwrap })),
    registerMutationState: { isLoading: false },
    loginUnwrap: lUnwrap,
    loginTrigger: vi.fn(() => ({ unwrap: lUnwrap })),
    loginMutationState: { isLoading: false },
    navigateMock: vi.fn(),
  }
})

vi.mock('~/api/auth.api', () => ({
  useRegisterMutation: () => [registerTrigger, registerMutationState],
  useLoginMutation: () => [loginTrigger, loginMutationState],
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
  await user.type(screen.getByLabelText('Password', { exact: true }), value)
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
// digit + non-alphanumeric (Program.cs lines 108–127). The client-side Zod
// schema is generated from RegisterRequestDto's DataAnnotations (DEC-066 /
// R-071) which carry only the length + email-format constraints; the
// character-class rules are enforced server-side at submit time and the
// 400 / 409 ProblemDetails responses are surfaced via parseProblem.
// eslint-disable-next-line sonarjs/no-hardcoded-passwords
const VALID_PASSWORD = 'StrongPassw0rd!'

describe('RegisterPage', () => {
  beforeEach(() => {
    registerUnwrap.mockReset()
    registerTrigger.mockClear()
    registerMutationState.isLoading = false
    loginUnwrap.mockReset()
    loginTrigger.mockClear()
    loginMutationState.isLoading = false
    navigateMock.mockReset()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('renders the poster header and a visible Start here h1', () => {
    renderRegister()

    expect(screen.getByTestId('auth-poster-header')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Split' })).toBeInTheDocument()
    const heading = screen.getByRole('heading', { name: 'Start here' })
    expect(heading).toBeVisible()
    expect(heading).toHaveClass('t-screen-title')
  })

  it('renders the poster frame', () => {
    renderRegister()

    expect(screen.getByRole('main')).toHaveClass(
      'mx-auto',
      'min-h-dvh',
      'max-w-md',
      'gap-[26px]',
      'px-[26px]',
      'pb-10',
    )
  })

  // eslint-disable-next-line sonarjs/assertions-in-tests
  it('keeps the poster surface in both themes', () => {
    const result = renderInBothThemes(
      <Provider store={makeStore()}>
        <MemoryRouter initialEntries={['/register']}>
          <RegisterPage />
        </MemoryRouter>
      </Provider>,
    )

    expectDualThemeParity(result, 'auth-poster-header')
  })

  it('preserves auth field semantics and autocomplete values', () => {
    renderRegister()

    const email = screen.getByLabelText('Email', { exact: true })
    const password = screen.getByLabelText('Password', { exact: true })
    expect(email).toHaveAttribute('autocomplete', 'email')
    expect(email).toHaveFocus()
    expect(password).toHaveAttribute('autocomplete', 'new-password')
    expect(email).toHaveClass('h-12', 'px-[14px]')
    expect(password).toHaveClass('h-12', 'px-[14px]', 'pr-12')
    expect(email.parentElement).not.toHaveClass('relative')
    expect(password.parentElement).toHaveClass('relative')
    expect(password.parentElement?.querySelector('[data-slot="form-control"]')).toBe(password)
    expect(screen.getByText('Email', { exact: true })).toHaveClass('t-data-label')
    expect(screen.getByText('Password', { exact: true })).toHaveClass('t-data-label')
    const helper = screen.getByText(
      '12 characters or more, with an uppercase letter, a lowercase letter, a digit, and a symbol.',
    )
    expect(helper).toHaveClass('font-mono')
  })

  it('flips password visibility without changing autocomplete', async () => {
    const { user } = renderRegister()
    const password = screen.getByLabelText('Password', { exact: true })
    const toggle = screen.getByTestId('password-visibility-toggle')

    expect(password).toHaveAttribute('type', 'password')
    expect(password).toHaveAttribute('autocomplete', 'new-password')
    await user.click(toggle)
    expect(password).toHaveAttribute('type', 'text')
    expect(password).toHaveAttribute('autocomplete', 'new-password')
    expect(toggle).toHaveAttribute('aria-pressed', 'true')
    expect(toggle).toHaveAccessibleName('Hide password')
  })

  it('renders an empty OAuth reserve', () => {
    renderRegister()
    const reserve = screen.getByTestId('auth-oauth-reserve')

    expect(reserve).toHaveClass('min-h-[52px]')
    expect(reserve).toHaveAttribute('aria-hidden', 'true')
    expect(reserve).toBeEmptyDOMElement()
  })

  it('renders the Sign in link to /login', () => {
    renderRegister()
    const link = screen.getByRole('link', { name: /sign in/i })

    expect(link).toHaveTextContent('Sign in \u2192')
    expect(link).toHaveAttribute('href', '/login')
  })

  it('renders the secondary link with a 44px target and the canonical focus ring', () => {
    renderRegister()
    const link = screen.getByRole('link', { name: /sign in/i })

    expect(link).toHaveClass(
      'relative',
      'hit-target-44',
      'rounded-sm',
      'font-condensed',
      'font-bold',
      'uppercase',
      'tracking-[0.12em]',
      'text-clay-text',
      'outline-none',
      'focus-visible:border-ring',
      'focus-visible:ring-[3px]',
      'focus-visible:ring-ring/[0.22]',
    )
  })

  it('renders the pending create-account copy', () => {
    registerMutationState.isLoading = true
    renderRegister()

    expect(screen.getByRole('button', { name: 'Creating account\u2026' })).toBeDisabled()
  })

  describe('client-side validation mirrors the generated Zod schema (DataAnnotations only)', () => {
    it('shows validation errors when the form is submitted empty and does not invoke the mutation', async () => {
      renderRegister()
      submitForm()
      // An empty string fails the schema's `zod.email()` check (the
      // surfaced default message in zod v4) — the schema does not carry
      // an explicit `.min(1, 'Email is required.')` refinement because the
      // backend's `[Required] + [EmailAddress]` collapses both signals to
      // the format check on the wire.
      expect(await screen.findByText(/invalid email/i)).toBeInTheDocument()
      // Password fails the schema's `.min(12)` check on an empty string —
      // the default zod v4 message is "Too small: expected string to have
      // >=12 characters". Matching loosely on ">=12 characters" keeps this
      // test resilient to minor formatting changes in zod's default text.
      expect(await screen.findByText(/>=\s*12 characters/i)).toBeInTheDocument()
      expect(registerTrigger).not.toHaveBeenCalled()
    })

    it('rejects an improperly formatted email', async () => {
      const { user } = renderRegister()
      await fillEmail(user, 'not-an-email')
      await fillPassword(user, VALID_PASSWORD)
      submitForm()
      expect(await screen.findByText(/invalid email/i)).toBeInTheDocument()
      expect(registerTrigger).not.toHaveBeenCalled()
    })

    it('rejects a too-short password (< 12 chars)', async () => {
      const { user } = renderRegister()
      await fillEmail(user, VALID_EMAIL)
      await fillPassword(user, 'Short1!')
      submitForm()
      // Character-class rules (uppercase / lowercase / digit /
      // non-alphanumeric) are NOT on the generated client schema — they
      // are Identity-policy server-side rules. The 400 ProblemDetails the
      // server returns when the policy fails is exercised by the
      // server-side error-surfacing tests further down.
      expect(await screen.findByText(/>=\s*12 characters/i)).toBeInTheDocument()
      expect(registerTrigger).not.toHaveBeenCalled()
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
        data: { title: 'Temporary sign-in failure \u2014 please sign in manually.', status: 500 },
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
