import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { PreferredUnits } from '~/api/generated'
import { buildPlanFixture } from '~/modules/plan/components/plan-display.fixture'
import type { PlanProjectionDto } from '~/modules/plan/models/plan.model'

interface QueryResult {
  data?: PlanProjectionDto
  isLoading: boolean
  isError: boolean
  error?: { status?: number }
  refetch: () => void
}

const { getCurrentPlanMock, preferredUnitsMock } = vi.hoisted(() => ({
  getCurrentPlanMock: vi.fn<() => QueryResult>(),
  preferredUnitsMock: vi.fn<() => PreferredUnits>(),
}))

vi.mock('~/api/plan.api', () => ({
  useGetCurrentPlanQuery: () => getCurrentPlanMock(),
}))

// `HomePage` reads the unit preference via this hook, which wraps a real RTK
// Query hook; the page renders here without a Redux store, so stub it through a
// mockable ref. Defaulted to Kilometers in `beforeEach` (keeps the existing
// km-based assertions intact); the miles-wiring test overrides it to prove the
// page actually threads the preference into the render sections.
vi.mock('~/modules/settings/hooks/use-preferred-units.hooks', () => ({
  usePreferredUnits: () => preferredUnitsMock(),
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
    preferredUnitsMock.mockReturnValue(PreferredUnits.Kilometers)
  })

  afterEach(() => {
    vi.useRealTimers()
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

    expect(screen.getByTestId('macro-phase-strip')).toBeInTheDocument()
    expect(screen.getByTestId('today-card')).toBeInTheDocument()
    expect(screen.getByTestId('upcoming-list')).toBeInTheDocument()
  })

  it('threads the Miles preference from the hook into the rendered distances', () => {
    vi.useFakeTimers()
    // Monday of training week 1 (planStartDate 2026-04-19), so the fixture's
    // Monday easy run is the prominent workout card and week-1 micro workouts
    // populate the upcoming list.
    vi.setSystemTime(new Date(2026, 3, 20)) // Mon 2026-04-20 → dayOfWeek = 1, week 1
    preferredUnitsMock.mockReturnValue(PreferredUnits.Miles)

    getCurrentPlanMock.mockReturnValue({
      data: buildPlanFixture(),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()

    // Monday's easy run is 6 km -> 3.7 mi (today card); meso targets 30 km -> 18.6 mi.
    expect(screen.getByTestId('today-card').textContent).toContain('3.7 mi')
    expect(screen.getAllByText(/18\.6 mi/u).length).toBeGreaterThan(0)
    // Nothing on the plan surface should still be rendering kilometres.
    expect(screen.queryByText(/\d\.\d km/u)).not.toBeInTheDocument()
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
  })

  it('renders the populated plan when targetEvent is null (general-fitness path)', () => {
    // Slice 1 surfaces the goal/event distinction via `macro.goalDescription`
    // — the projection itself does not carry a top-level `targetEvent`.
    // Simulate a general-fitness plan by replacing the goal copy.
    const base = buildPlanFixture()
    const generalFitnessPlan: PlanProjectionDto = {
      ...base,
      macro: {
        ...(base.macro ?? {
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

  it('no longer mounts the coach chat on home — it lives on its own /coach route', () => {
    getCurrentPlanMock.mockReturnValue({
      data: buildPlanFixture(),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()

    expect(screen.queryByTestId('coach-chat')).not.toBeInTheDocument()
  })

  it('omits the macro phase strip but still renders today-card and upcoming-list when plan.macro is null', () => {
    const planWithNullMacro: PlanProjectionDto = {
      ...buildPlanFixture(),
      macro: null,
    }
    getCurrentPlanMock.mockReturnValue({
      data: planWithNullMacro,
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()

    expect(screen.queryByTestId('macro-phase-strip')).toBeNull()
    expect(screen.getByTestId('today-card')).toBeInTheDocument()
    expect(screen.getByTestId('upcoming-list')).toBeInTheDocument()
  })
})
