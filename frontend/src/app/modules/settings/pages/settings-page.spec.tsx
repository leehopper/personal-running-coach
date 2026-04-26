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
  RegeneratePlanDialog: ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="regenerate-plan-dialog-stub">dialog</div> : null,
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
})
