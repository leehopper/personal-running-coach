import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'
import type { PlanProjectionDto } from '~/modules/plan/models/plan.model'

interface QueryResult {
  data?: PlanProjectionDto
  isLoading: boolean
  isError: boolean
}

const { getCurrentPlanMock, authRef, signOutRef, signOutMock } = vi.hoisted(() => {
  const signOutMock = vi.fn<() => Promise<void>>()
  return {
    getCurrentPlanMock: vi.fn<() => QueryResult>(),
    authRef: {
      value: {
        status: 'authenticated',
        user: { userId: 'u1', email: 'runner@example.com' },
        isAuthenticated: true,
        isUnknown: false,
        isUnauthenticated: false,
      },
    },
    signOutMock,
    signOutRef: {
      value: {
        signOut: signOutMock,
        isSigningOut: false,
      },
    },
  }
})

vi.mock('~/api/plan.api', () => ({
  useGetCurrentPlanQuery: () => getCurrentPlanMock(),
}))

vi.mock('~/modules/auth/hooks/auth.hooks', () => ({
  useAuth: () => authRef.value,
  useSignOut: () => signOutRef.value,
}))

vi.mock('~/modules/settings/components/regenerate-plan-dialog.component', () => ({
  RegeneratePlanDialog: ({ isOpen, onClose }: { isOpen: boolean; onClose: () => void }) =>
    isOpen ? (
      <div data-testid="regenerate-plan-dialog-stub">
        dialog
        <button type="button" onClick={onClose} data-testid="regenerate-plan-dialog-close-stub">
          close
        </button>
      </div>
    ) : null,
}))

vi.mock('~/modules/settings/components/theme-toggle.component', () => ({
  ThemeToggle: () => <div data-testid="theme-toggle-stub">theme toggle</div>,
}))

vi.mock('~/modules/settings/components/units-toggle.component', () => ({
  UnitsToggle: () => <div data-testid="units-toggle-stub">units toggle</div>,
}))

import { SettingsPage } from './settings.page'

const renderPage = () =>
  render(
    <MemoryRouter initialEntries={['/settings']}>
      <SettingsPage />
    </MemoryRouter>,
  )

const buildPlanStub = (overrides: Partial<PlanProjectionDto> = {}): PlanProjectionDto => ({
  planId: 'plan-1',
  userId: 'user-1',
  generatedAt: '2026-06-29T12:00:00Z',
  planStartDate: '2026-06-29',
  previousPlanId: null,
  targetEventName: null,
  targetEventDistanceKm: null,
  targetEventDate: null,
  promptVersion: 'coaching-v1',
  modelId: 'claude-sonnet-4-6',
  macro: null,
  mesoWeeks: [],
  microWorkoutsByWeek: {},
  ...overrides,
})

const buildMacro = (goalDescription = 'Portland 10K') => ({
  totalWeeks: 12,
  goalDescription,
  phases: [],
  rationale: '',
  warnings: '',
})

const mockLoadedPlan = (overrides: Partial<PlanProjectionDto> = {}): void => {
  getCurrentPlanMock.mockReturnValue({
    data: buildPlanStub(overrides),
    isLoading: false,
    isError: false,
  })
}

describe('SettingsPage', () => {
  beforeEach(() => {
    getCurrentPlanMock.mockReset()
    signOutMock.mockReset()
    authRef.value = {
      status: 'authenticated',
      user: { userId: 'u1', email: 'runner@example.com' },
      isAuthenticated: true,
      isUnknown: false,
      isUnauthenticated: false,
    }
    signOutRef.value = { signOut: signOutMock, isSigningOut: false }
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('renders the loading status while the plan is in flight', () => {
    getCurrentPlanMock.mockReturnValue({ data: undefined, isLoading: true, isError: false })
    renderPage()
    expect(screen.getByRole('status')).toHaveTextContent(/loading plan details/i)
  })

  it('shows the generatedAt timestamp and a regenerate button when the plan loads', () => {
    mockLoadedPlan()
    renderPage()
    expect(screen.getByTestId('settings-plan-generated-at')).toBeInTheDocument()
    expect(screen.getByTestId('settings-regenerate-button')).toBeInTheDocument()
  })

  it('renders the Appearance section with the theme toggle', () => {
    mockLoadedPlan()
    renderPage()
    expect(screen.getByTestId('settings-appearance-section')).toBeInTheDocument()
    expect(screen.getByTestId('theme-toggle-stub')).toBeInTheDocument()
  })

  it('renders all four rule sections and the title in both themes', () => {
    mockLoadedPlan()
    const result = renderInBothThemes(
      <MemoryRouter initialEntries={['/settings']}>
        <SettingsPage />
      </MemoryRouter>,
    )

    expectDualThemeParity(result, 'settings-page')
    for (const view of [result.dark, result.light]) {
      expect(view.getByRole('heading', { name: 'Settings', level: 1 })).toBeInTheDocument()
      expect(view.getByRole('heading', { name: 'The plan', level: 2 })).toBeInTheDocument()
      expect(view.getByRole('heading', { name: 'Appearance', level: 2 })).toBeInTheDocument()
      expect(view.getByRole('heading', { name: 'Units', level: 2 })).toBeInTheDocument()
      expect(view.getByRole('heading', { name: 'Account', level: 2 })).toBeInTheDocument()
    }
  })

  it('renders the full current-plan line', () => {
    mockLoadedPlan({ macro: buildMacro() })
    renderPage()
    const summary = screen.getByTestId('settings-plan-generated-at')
    expect(summary).toHaveTextContent('Generated Jun 29, 2026 \u00B7 12 weeks \u00B7 Portland 10K')
    expect(summary.querySelector('time')).toHaveAttribute('dateTime', '2026-06-29T12:00:00Z')
  })

  it('omits macro fields when macro is null', () => {
    mockLoadedPlan({ macro: null })
    renderPage()
    const summary = screen.getByTestId('settings-plan-generated-at')
    expect(summary).toHaveTextContent('Generated Jun 29, 2026')
    expect(summary).not.toHaveTextContent(/weeks|\u00B7/)
  })

  it('omits a blank goal description', () => {
    mockLoadedPlan({ macro: buildMacro('   ') })
    renderPage()
    const summary = screen.getByTestId('settings-plan-generated-at')
    expect(summary).toHaveTextContent('Generated Jun 29, 2026 \u00B7 12 weeks')
    expect(summary).not.toHaveTextContent('Portland')
    expect(summary.textContent?.trim().endsWith('\u00B7')).toBe(false)
  })

  it('renders the raw generatedAt string when Date parsing returns NaN', () => {
    mockLoadedPlan({ generatedAt: 'definitely-not-a-date' })
    renderPage()
    const summary = screen.getByTestId('settings-plan-generated-at')
    expect(summary).toHaveTextContent('Generated definitely-not-a-date')
    expect(summary.querySelector('time')).toHaveAttribute('dateTime', 'definitely-not-a-date')
    expect(summary.querySelector('time')).toHaveTextContent('definitely-not-a-date')
  })

  it('renders the clay-outline regenerate action and warning', () => {
    mockLoadedPlan()
    renderPage()
    const button = screen.getByTestId('settings-regenerate-button')
    expect(button).toHaveClass(
      'border-clay-text',
      'text-clay-text',
      'active:text-secondary-foreground',
    )
    expect(button).toHaveTextContent('Regenerate plan')
    expect(
      screen.getByText('Replaces your current plan. The coach starts fresh from your log book.'),
    ).toHaveClass('t-data-label', 'text-muted-foreground')
  })

  it('renders the account email from auth state', () => {
    mockLoadedPlan()
    renderPage()
    expect(screen.getByTestId('settings-account-email')).toHaveTextContent('runner@example.com')
  })

  it('invokes sign out and disables the button while signing out', async () => {
    mockLoadedPlan()
    const view = renderPage()
    await userEvent.click(screen.getByTestId('settings-sign-out-button'))
    expect(signOutMock).toHaveBeenCalledTimes(1)

    signOutRef.value = { signOut: signOutMock, isSigningOut: true }
    view.rerender(
      <MemoryRouter initialEntries={['/settings']}>
        <SettingsPage />
      </MemoryRouter>,
    )
    expect(screen.getByTestId('settings-sign-out-button')).toBeDisabled()
  })

  it('renders the exact version footer in both themes', () => {
    mockLoadedPlan()
    const result = renderInBothThemes(
      <MemoryRouter initialEntries={['/settings']}>
        <SettingsPage />
      </MemoryRouter>,
    )
    expectDualThemeParity(result, 'settings-version')
    expect(result.dark.getByTestId('settings-version')).toHaveTextContent('Split 0.9.0 \u2014 MVP')
    expect(result.light.getByTestId('settings-version')).toHaveTextContent('Split 0.9.0 \u2014 MVP')
  })

  it('opens the regenerate dialog when the button is clicked', async () => {
    mockLoadedPlan()
    renderPage()
    expect(screen.queryByTestId('regenerate-plan-dialog-stub')).not.toBeInTheDocument()
    await userEvent.click(screen.getByTestId('settings-regenerate-button'))
    expect(screen.getByTestId('regenerate-plan-dialog-stub')).toBeInTheDocument()
  })

  it('falls back gracefully when the plan query errors', () => {
    getCurrentPlanMock.mockReturnValue({ data: undefined, isLoading: false, isError: true })
    renderPage()
    expect(screen.getByText(/could not load your current plan/i)).toBeInTheDocument()
    expect(screen.getByTestId('settings-regenerate-button')).toBeInTheDocument()
  })

  it('closes the regenerate dialog when the dialog requests close', async () => {
    mockLoadedPlan()
    renderPage()
    await userEvent.click(screen.getByTestId('settings-regenerate-button'))
    await userEvent.click(screen.getByTestId('regenerate-plan-dialog-close-stub'))
    expect(screen.queryByTestId('regenerate-plan-dialog-stub')).not.toBeInTheDocument()
  })
})
