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

// Hoisted mock references — vi.mock is hoisted above imports, so the trigger
// and unwrap mocks must be created inside the factory or via `vi.hoisted`.
const { loginUnwrap, loginTrigger, loginMutationState, navigateMock } = vi.hoisted(() => {
  const unwrap = vi.fn()
  return {
    loginUnwrap: unwrap,
    loginTrigger: vi.fn(() => ({ unwrap })),
    loginMutationState: { isLoading: false },
    navigateMock: vi.fn(),
  }
})

vi.mock('~/api/auth.api', () => ({
  useLoginMutation: () => [loginTrigger, loginMutationState],
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
  await user.type(screen.getByLabelText('Password', { exact: true }), value)
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
// eslint-disable-next-line sonarjs/no-hardcoded-passwords
const VALID_PASSWORD = 'StrongPassw0rd!'

describe('LoginPage', () => {
  beforeEach(() => {
    loginUnwrap.mockReset()
    loginTrigger.mockClear()
    loginMutationState.isLoading = false
    navigateMock.mockReset()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('renders the poster header and a visually hidden Sign in h1', () => {
    renderLogin()

    expect(screen.getByTestId('auth-poster-header')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Split' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Sign in' })).toHaveClass('sr-only')
    expect(screen.getByTestId('auth-poster-header').parentElement).toHaveClass(
      'mx-auto',
      'flex',
      'min-h-dvh',
      'w-full',
      'max-w-md',
      'flex-col',
      'justify-center',
      'gap-[26px]',
      'bg-background',
      'px-[26px]',
      'pb-10',
    )
  })

  it('renders the poster frame', () => {
    renderLogin()

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
        <MemoryRouter initialEntries={['/login']}>
          <LoginPage />
        </MemoryRouter>
      </Provider>,
    )

    expectDualThemeParity(result, 'auth-poster-header')
  })

  it('preserves auth field semantics and autocomplete values', () => {
    renderLogin()

    const email = screen.getByLabelText('Email', { exact: true })
    const password = screen.getByLabelText('Password', { exact: true })
    expect(email).toHaveAttribute('autocomplete', 'email')
    expect(email).toHaveFocus()
    expect(password).toHaveAttribute('autocomplete', 'current-password')
    expect(email).toHaveClass('h-12', 'px-[14px]')
    expect(password).toHaveClass('h-12', 'px-[14px]', 'pr-12')
    expect(email.parentElement).not.toHaveClass('relative')
    expect(password.parentElement).toHaveClass('relative')
    expect(password.parentElement?.querySelector('[data-slot="form-control"]')).toBe(password)
    expect(screen.getByText('Email', { exact: true })).toHaveClass('t-data-label')
    expect(screen.getByText('Password', { exact: true })).toHaveClass('t-data-label')
  })

  it('flips password visibility without changing autocomplete', async () => {
    const { user } = renderLogin()
    const password = screen.getByLabelText('Password', { exact: true })
    const toggle = screen.getByTestId('password-visibility-toggle')

    expect(password).toHaveAttribute('type', 'password')
    expect(password).toHaveAttribute('autocomplete', 'current-password')
    await user.click(toggle)
    expect(password).toHaveAttribute('type', 'text')
    expect(password).toHaveAttribute('autocomplete', 'current-password')
    expect(toggle).toHaveAttribute('aria-pressed', 'true')
    expect(toggle).toHaveAccessibleName('Hide password')
  })

  it('renders an empty OAuth reserve', () => {
    renderLogin()
    const reserve = screen.getByTestId('auth-oauth-reserve')

    expect(reserve).toHaveClass('min-h-[52px]')
    expect(reserve).toHaveAttribute('aria-hidden', 'true')
    expect(reserve).toBeEmptyDOMElement()
  })

  it('renders the Create account link to /register', () => {
    renderLogin()
    const link = screen.getByRole('link', { name: /create account/i })

    expect(link).toHaveTextContent('Create account \u2192')
    expect(link).toHaveAttribute('href', '/register')
  })

  it('renders the secondary link with a 44px target and the canonical focus ring', () => {
    renderLogin()
    const link = screen.getByRole('link', { name: /create account/i })

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

  it('lays out the form on the poster rhythm', () => {
    renderLogin()

    expect(document.querySelector('form')).toHaveClass('flex', 'flex-col', 'gap-3.5')
    expect(screen.getByLabelText('Email', { exact: true }).parentElement).toHaveClass('gap-1.5')
  })

  it('renders the 52px poster primary', () => {
    renderLogin()

    expect(screen.getByRole('button', { name: 'Sign in' })).toHaveClass(
      'w-full',
      'h-[52px]',
      'text-[17px]',
      'tracking-[0.14em]',
    )
  })

  it('renders the pending sign-in copy', () => {
    loginMutationState.isLoading = true
    renderLogin()

    expect(screen.getByRole('button', { name: 'Signing in\u2026' })).toBeDisabled()
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
          // eslint-disable-next-line sonarjs/no-hardcoded-passwords
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
