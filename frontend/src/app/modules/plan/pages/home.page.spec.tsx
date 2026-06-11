import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { buildNudgeTurn } from '~/modules/coaching/components/conversation.fixture'
import type { ConversationTurnsResponseDto } from '~/modules/coaching/models/conversation.model'
import { buildPlanFixture } from '~/modules/plan/components/plan-display.fixture'
import type { PlanProjectionDto } from '~/modules/plan/models/plan.model'

interface QueryResult {
  data?: PlanProjectionDto
  isLoading: boolean
  isError: boolean
  error?: { status?: number }
  refetch: () => void
}

interface ConversationQueryResult {
  data?: ConversationTurnsResponseDto
  isLoading: boolean
  isError: boolean
}

const { getCurrentPlanMock, getConversationTurnsMock } = vi.hoisted(() => ({
  getCurrentPlanMock: vi.fn<() => QueryResult>(),
  getConversationTurnsMock: vi.fn<() => ConversationQueryResult>(),
}))

vi.mock('~/api/plan.api', () => ({
  useGetCurrentPlanQuery: () => getCurrentPlanMock(),
}))

vi.mock('~/api/conversation.api', () => ({
  useGetConversationTurnsQuery: () => getConversationTurnsMock(),
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
    getConversationTurnsMock.mockReset()
    // Default: no turns yet — the read-only panel renders nothing.
    getConversationTurnsMock.mockReturnValue({
      data: { turns: [] },
      isLoading: false,
      isError: false,
    })
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

    // (a) all three sections present
    expect(screen.getByTestId('macro-phase-strip')).toBeInTheDocument()
    expect(screen.getByTestId('today-card')).toBeInTheDocument()
    expect(screen.getByTestId('upcoming-list')).toBeInTheDocument()

    // (d) trademark-clean — `queryByText(/vdot/i)` returns null on the
    //     populated home surface (per spec § Unit 4 Proof Artifacts).
    expect(screen.queryByText(/vdot/i)).toBeNull()
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
    expect(screen.getByRole('link', { name: /workout history/i })).toHaveAttribute(
      'href',
      '/history',
    )
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

  it('renders the read-only conversation panel between today-card and upcoming-list when turns exist', () => {
    const nudge = buildNudgeTurn()
    getCurrentPlanMock.mockReturnValue({
      data: buildPlanFixture(),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    getConversationTurnsMock.mockReturnValue({
      data: { turns: [nudge] },
      isLoading: false,
      isError: false,
    })
    renderHome()

    const panel = screen.getByTestId('conversation-panel')
    expect(within(panel).getByTestId('nudge-turn')).toHaveTextContent(nudge.content)
    // Document order: the coach's explanation reads in today's context,
    // before the forward-looking upcoming stack.
    const home = screen.getByTestId('home-page')
    const order = [...home.querySelectorAll('[data-testid]')].map((node) =>
      node.getAttribute('data-testid'),
    )
    expect(order.indexOf('conversation-panel')).toBeGreaterThan(order.indexOf('today-card'))
    expect(order.indexOf('conversation-panel')).toBeLessThan(order.indexOf('upcoming-list'))
  })

  it('renders no conversation panel when the turns query errors (supplementary surface)', () => {
    getCurrentPlanMock.mockReturnValue({
      data: buildPlanFixture(),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    getConversationTurnsMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })
    renderHome()

    expect(screen.queryByTestId('conversation-panel')).toBeNull()
    expect(screen.getByTestId('today-card')).toBeInTheDocument()
    expect(screen.getByTestId('upcoming-list')).toBeInTheDocument()
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
