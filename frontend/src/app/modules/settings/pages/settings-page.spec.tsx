import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { PlanProjectionDto } from '~/modules/plan/models/plan.model'

interface QueryResult {
  data?: PlanProjectionDto
  isLoading: boolean
  isError: boolean
}

const { getCurrentPlanMock } = vi.hoisted(() => ({
  getCurrentPlanMock: vi.fn<() => QueryResult>(),
}))

vi.mock('~/api/plan.api', () => ({
  useGetCurrentPlanQuery: () => getCurrentPlanMock(),
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

// `ThemeToggle` reads `useTheme()` from the app-level `<ThemeProvider>`,
// which is mounted in `main.tsx` and not in this page-scoped test tree.
// Stubbed here — its own behaviour is covered by
// `theme-toggle.component.spec.tsx` — so this suite stays focused on the
// SettingsPage's plan logic.
vi.mock('~/modules/settings/components/theme-toggle.component', () => ({
  ThemeToggle: () => <div data-testid="theme-toggle-stub">theme toggle</div>,
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
  generatedAt: '2026-04-25T15:00:00.000Z',
  planStartDate: '2026-04-19',
  previousPlanId: null,
  promptVersion: 'coaching-v1',
  modelId: 'claude-sonnet-4-6',
  macro: null,
  mesoWeeks: [],
  microWorkoutsByWeek: {},
  ...overrides,
})

describe('SettingsPage', () => {
  beforeEach(() => {
    getCurrentPlanMock.mockReset()
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
    getCurrentPlanMock.mockReturnValue({
      data: buildPlanStub(),
      isLoading: false,
      isError: false,
    })
    renderPage()
    expect(screen.getByTestId('settings-plan-generated-at')).toBeInTheDocument()
    expect(screen.getByTestId('settings-regenerate-button')).toBeInTheDocument()
  })

  it('renders the Appearance section with the theme toggle', () => {
    getCurrentPlanMock.mockReturnValue({
      data: buildPlanStub(),
      isLoading: false,
      isError: false,
    })
    renderPage()
    expect(screen.getByTestId('settings-appearance-section')).toBeInTheDocument()
    expect(screen.getByTestId('theme-toggle-stub')).toBeInTheDocument()
  })

  it('does not render the previous-plan link when previousPlanId is null', () => {
    getCurrentPlanMock.mockReturnValue({
      data: buildPlanStub({ previousPlanId: null }),
      isLoading: false,
      isError: false,
    })
    renderPage()
    expect(screen.queryByTestId('settings-previous-plan-link')).not.toBeInTheDocument()
  })

  it('renders the placeholder previous-plan link when previousPlanId is non-null', () => {
    getCurrentPlanMock.mockReturnValue({
      data: buildPlanStub({ previousPlanId: 'plan-0' }),
      isLoading: false,
      isError: false,
    })
    renderPage()
    expect(screen.getByTestId('settings-previous-plan-link')).toBeInTheDocument()
  })

  it('opens the regenerate dialog when the button is clicked', async () => {
    getCurrentPlanMock.mockReturnValue({
      data: buildPlanStub(),
      isLoading: false,
      isError: false,
    })
    renderPage()
    expect(screen.queryByTestId('regenerate-plan-dialog-stub')).not.toBeInTheDocument()
    await userEvent.click(screen.getByTestId('settings-regenerate-button'))
    expect(screen.getByTestId('regenerate-plan-dialog-stub')).toBeInTheDocument()
  })

  it('falls back gracefully when the plan query errors', () => {
    getCurrentPlanMock.mockReturnValue({ data: undefined, isLoading: false, isError: true })
    renderPage()
    expect(screen.getByText(/could not load your current plan/i)).toBeInTheDocument()
    // The Regenerate button should still be available so the user can retry.
    expect(screen.getByTestId('settings-regenerate-button')).toBeInTheDocument()
  })

  it('closes the regenerate dialog when the dialog requests close', async () => {
    getCurrentPlanMock.mockReturnValue({
      data: buildPlanStub(),
      isLoading: false,
      isError: false,
    })
    renderPage()
    await userEvent.click(screen.getByTestId('settings-regenerate-button'))
    expect(screen.getByTestId('regenerate-plan-dialog-stub')).toBeInTheDocument()
    await userEvent.click(screen.getByTestId('regenerate-plan-dialog-close-stub'))
    expect(screen.queryByTestId('regenerate-plan-dialog-stub')).not.toBeInTheDocument()
  })

  it('renders the raw generatedAt string when Date parsing returns NaN', () => {
    getCurrentPlanMock.mockReturnValue({
      data: buildPlanStub({ generatedAt: 'definitely-not-a-date' }),
      isLoading: false,
      isError: false,
    })
    renderPage()
    const summary = screen.getByTestId('settings-plan-generated-at')
    expect(summary).toHaveTextContent('definitely-not-a-date')
    const time = summary.querySelector('time')
    expect(time).not.toBeNull()
    expect(time).toHaveAttribute('dateTime', 'definitely-not-a-date')
    expect(time?.textContent).toBe('definitely-not-a-date')
  })
})
