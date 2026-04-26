import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { buildPlanFixture } from '~/modules/plan/components/plan-display.fixture'
import type { PlanProjectionDto } from '~/modules/plan/models/plan.model'

interface QueryResult {
  data?: PlanProjectionDto
  isLoading: boolean
  isError: boolean
  error?: { status?: number }
  refetch: () => void
}

const { getCurrentPlanMock } = vi.hoisted(() => ({
  getCurrentPlanMock: vi.fn<() => QueryResult>(),
}))

vi.mock('~/api/plan.api', () => ({
  useGetCurrentPlanQuery: () => getCurrentPlanMock(),
}))

import { HomePage } from './home.page'

const renderHome = () =>
  render(
    <MemoryRouter initialEntries={['/']}>
      <HomePage />
    </MemoryRouter>,
  )

describe('HomePage', () => {
  beforeEach(() => {
    getCurrentPlanMock.mockReset()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('shows the loading status while the plan is in flight', () => {
    getCurrentPlanMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('renders the macro strip, today card, and upcoming list when the plan is loaded', () => {
    // Anchor "today" so the fixture's Monday workout is the prominent one
    // and the rest-of-week section has Wednesday + Saturday remaining.
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 3, 27)) // Monday → dayOfWeek = 1

    getCurrentPlanMock.mockReturnValue({
      data: buildPlanFixture(),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()

    // (a) all three sections present
    expect(screen.getByTestId('macro-phase-strip')).toBeInTheDocument()
    expect(screen.getByTestId('today-card')).toBeInTheDocument()
    expect(screen.getByTestId('upcoming-list')).toBeInTheDocument()

    // (d) trademark-clean — zero matches for `vdot` (case-insensitive) on
    //     the rendered home surface.
    const home = screen.getByTestId('home-page')
    expect(home.textContent ?? '').not.toMatch(/vdot/i)

    vi.useRealTimers()
  })

  it('renders the rest-day variant of TodayCard when today maps to a rest slot', () => {
    // Sunday is a rest day in `baseWeek` of the fixture.
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 3, 26)) // Sunday → dayOfWeek = 0

    getCurrentPlanMock.mockReturnValue({
      data: buildPlanFixture(),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()

    const todayCard = screen.getByTestId('today-card')
    expect(todayCard.getAttribute('data-variant')).toBe('rest')

    vi.useRealTimers()
  })

  it('renders the populated plan when targetEvent is null (general-fitness path)', () => {
    // Slice 1 surfaces the goal/event distinction via `macro.goalDescription`
    // — the projection itself does not carry a top-level `targetEvent`.
    // Simulate a general-fitness plan by replacing the goal copy.
    const generalFitnessPlan: PlanProjectionDto = {
      ...buildPlanFixture(),
      macro: {
        ...(buildPlanFixture().macro ?? {
          totalWeeks: 12,
          phases: [],
          goalDescription: '',
          rationale: '',
          warnings: '',
        }),
        goalDescription: 'General fitness — no target race',
      },
    }
    getCurrentPlanMock.mockReturnValue({
      data: generalFitnessPlan,
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()

    expect(screen.getByTestId('home-page')).toBeInTheDocument()
    expect(screen.getByTestId('macro-phase-strip')).toBeInTheDocument()
    expect(screen.getByTestId('today-card')).toBeInTheDocument()
    expect(screen.getByTestId('upcoming-list')).toBeInTheDocument()
  })

  it('renders the no-plan-yet state on a 404 response', () => {
    getCurrentPlanMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 404 },
      refetch: vi.fn(),
    })
    renderHome()
    expect(screen.getByTestId('home-page-no-plan')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /go to onboarding/i })).toHaveAttribute(
      'href',
      '/onboarding',
    )
  })

  it('renders the generic error state when the request fails for non-404 reasons', () => {
    getCurrentPlanMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 500 },
      refetch: vi.fn(),
    })
    renderHome()
    expect(screen.getByTestId('home-page-error')).toBeInTheDocument()
  })
})
