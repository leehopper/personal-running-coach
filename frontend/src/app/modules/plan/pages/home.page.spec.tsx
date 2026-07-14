import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { QueryWorkoutLogsResponseDto } from '~/api/generated'
import { PreferredUnits } from '~/api/generated'
import { buildPlanFixture } from '~/modules/plan/components/plan-display.fixture'
import { resolveCurrentWeek } from '~/modules/plan/hooks/use-plan.hooks'
import type { PlanProjectionDto } from '~/modules/plan/models/plan.model'

interface PlanQueryResult {
  data?: PlanProjectionDto
  isLoading: boolean
  isError: boolean
  error?: { status?: number }
  refetch: () => void
}

interface HistoryQueryResult {
  data?: { pages: QueryWorkoutLogsResponseDto[]; pageParams: unknown[] }
  isLoading: boolean
  isError: boolean
  hasNextPage: boolean
  isFetchingNextPage: boolean
  fetchNextPage: () => void
  refetch: () => void
}

const {
  getCurrentPlanMock,
  preferredUnitsMock,
  conversationTimelineMock,
  historyQueryMock,
  fetchNextPageMock,
} = vi.hoisted(() => ({
  getCurrentPlanMock: vi.fn<() => PlanQueryResult>(),
  preferredUnitsMock: vi.fn<() => PreferredUnits>(),
  conversationTimelineMock: vi.fn(),
  historyQueryMock: vi.fn<(arg: unknown) => HistoryQueryResult>(),
  fetchNextPageMock: vi.fn(),
}))

vi.mock('~/api/plan.api', () => ({
  useGetCurrentPlanQuery: () => getCurrentPlanMock(),
}))

// `HomePage` reads the unit preference via this hook, which wraps a real RTK
// Query hook; the page renders here without a Redux store, so stub it through a
// mockable ref. Defaulted to Kilometers in `beforeEach` (keeps the km-based
// assertions intact); the miles-wiring test overrides it to prove the page
// actually threads the preference into every new render site.
vi.mock('~/modules/settings/hooks/use-preferred-units.hooks', () => ({
  usePreferredUnits: () => preferredUnitsMock(),
}))

// `HomePage` mounts `CoachDigest`, which calls this real RTK Query hook
// internally — with no Redux `<Provider>` in `renderHome()`'s tree, an
// unmocked call throws "could not find react-redux context value" the
// instant `CoachDigest` renders. Same `~/api/conversation.api` mocking idiom
// `coach-chat.component.spec.tsx` established (Slice 2 §1 PR-C's F2 catch).
vi.mock('~/api/conversation.api', () => ({
  useGetConversationTimelineQuery: () => conversationTimelineMock(),
}))

// `HomePage` mounts `TheWeek`, sourcing its `logs` prop from this real RTK
// Query hook (the SAME hook, same `undefined` arg, `/history` already
// uses — a shared cache entry, never double-fetched). Mocked per
// `history.page.spec.tsx`'s established `historyQueryMock`/
// `fetchNextPageMock` hoisted-mock idiom (Slice 2 §1 PR-D's F2 audit).
// Forwards the real call-site argument to `historyQueryMock` (rather than
// discarding it) so tests can assert WHAT `home.page.tsx` passes, not just
// THAT it called the hook — see the `historyQueryMock` argument-contract
// test below.
vi.mock('~/api/workout-log.api', () => ({
  useGetWorkoutLogHistoryInfiniteQuery: (arg: unknown) => historyQueryMock(arg),
}))

// Wraps the REAL `resolveCurrentWeek` in a spy (delegating to its actual
// implementation via `vi.fn(actual.resolveCurrentWeek)`) rather than
// replacing it — every other export of this module (`usePlan`,
// `findCurrentMesoWeek`, `findCurrentWeekWorkouts`) stays real. Lets one
// test assert the single-date-pipeline contract (§2.1): `home.page.tsx`
// must pass its own `today` explicitly as the 2nd argument, never rely on
// `resolveCurrentWeek`'s `referenceDate = new Date()` default (which would
// silently reintroduce a second `new Date()` call site).
vi.mock('~/modules/plan/hooks/use-plan.hooks', async (importOriginal) => {
  const actual = await importOriginal<typeof import('~/modules/plan/hooks/use-plan.hooks')>()
  return {
    ...actual,
    resolveCurrentWeek: vi.fn(actual.resolveCurrentWeek),
  }
})

import { HomePage } from './home.page'

const renderHome = () =>
  render(
    <MemoryRouter initialEntries={['/']}>
      <HomePage />
    </MemoryRouter>,
  )

const SECTION_TESTIDS = [
  'today-header',
  'workout-hero',
  'the-week',
  'coach-digest',
  'up-next',
  'the-block',
]

describe('HomePage', () => {
  beforeEach(() => {
    getCurrentPlanMock.mockReset()
    preferredUnitsMock.mockReturnValue(PreferredUnits.Kilometers)
    conversationTimelineMock.mockReset()
    // `{ turns: [] }` renders `CoachDigest`'s empty state (state 4) — the
    // safest default: no wrapping `<Link>` that could intercept a click meant
    // for another section, and no content colliding with this file's own
    // assertions. `CoachDigest`'s own per-state content is
    // `coach-digest.component.spec.tsx`'s job, not this file's.
    conversationTimelineMock.mockReturnValue({
      data: { turns: [] },
      isLoading: false,
      isError: false,
    })
    historyQueryMock.mockReset()
    historyQueryMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
      hasNextPage: false,
      isFetchingNextPage: false,
      fetchNextPage: fetchNextPageMock,
      refetch: vi.fn(),
    })
  })

  afterEach(() => {
    // §2.5/C7's "never call fetchNextPage from Home" rule, pinned across
    // every rendered scenario in this file — calling it would permanently
    // append pages to the shared `/history` cache entry TheWeek reads from,
    // corrupting the history page's own pagination.
    expect(fetchNextPageMock).not.toHaveBeenCalled()
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

  it('mounts all six sections, in order, inside home-page', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 3, 20)) // Monday 2026-04-20 → dayOfWeek 1, week 1

    getCurrentPlanMock.mockReturnValue({
      data: buildPlanFixture(),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()

    const home = screen.getByTestId('home-page')
    const sectionIds = [...home.children].map((el) => el.getAttribute('data-testid'))
    expect(sectionIds).toEqual(SECTION_TESTIDS)
  })

  it('degrades gracefully when plan.macro is null — header shows WEEK N alone, THE BLOCK shows its unavailable state, every other section still renders', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 3, 20)) // Monday, week 1

    const planWithNullMacro: PlanProjectionDto = { ...buildPlanFixture(), macro: null }
    getCurrentPlanMock.mockReturnValue({
      data: planWithNullMacro,
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()

    const header = screen.getByTestId('today-header')
    expect(header.textContent).toMatch(/week 1/i)
    expect(header.textContent).not.toMatch(/of/i)

    expect(screen.getByTestId('the-block').dataset.state).toBe('unavailable')

    // Every other section still mounts — a null macro degrades one section,
    // not the whole page.
    for (const testid of ['workout-hero', 'the-week', 'coach-digest', 'up-next']) {
      expect(screen.getByTestId(testid)).toBeInTheDocument()
    }
  })

  it('threads the Miles preference across every new render site, with zero residual km-suffixed text', () => {
    vi.useFakeTimers()
    // Monday of training week 1 (planStartDate 2026-04-19), so the fixture's
    // Monday easy run is the hero's workout and week-1 micro workouts
    // populate UP NEXT.
    vi.setSystemTime(new Date(2026, 3, 20)) // Mon 2026-04-20 → dayOfWeek = 1, week 1
    preferredUnitsMock.mockReturnValue(PreferredUnits.Miles)

    getCurrentPlanMock.mockReturnValue({
      data: buildPlanFixture(),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()

    const home = screen.getByTestId('home-page')
    // Monday's easy run is 6 km -> 3.7 mi (hero stat band); week 1's target
    // is 30 km -> 18.6 mi (THE WEEK progress denominator, THE BLOCK's
    // upcoming-week row).
    expect(home.textContent).toContain('3.7')
    expect(home.textContent).toContain('MILES')
    expect(home.textContent).toContain('18.6')
    // Nothing on the plan surface should still be rendering kilometres.
    expect(screen.queryByText(/\d\.\d km/u)).not.toBeInTheDocument()
  })

  it("calls resolveCurrentWeek with the page's own `today`, not the default parameter (single-date-pipeline contract, §2.1)", () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 3, 20))

    getCurrentPlanMock.mockReturnValue({
      data: buildPlanFixture(),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()

    expect(vi.mocked(resolveCurrentWeek)).toHaveBeenCalledTimes(1)
    const call = vi.mocked(resolveCurrentWeek).mock.calls[0]
    expect(call).toHaveLength(2)
    expect(call[1]).toBeInstanceOf(Date)
  })

  it("fetches this week's logs via the shared /history cache entry (useGetWorkoutLogHistoryInfiniteQuery(undefined)) for TheWeek", () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 3, 20))

    getCurrentPlanMock.mockReturnValue({
      data: buildPlanFixture(),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()

    // Asserts WHAT was passed, not just THAT the hook was called: the
    // shared-cache contract (§2.5/C7) requires `home.page.tsx` to call this
    // hook with the exact same argument `/history` uses (`undefined`) — a
    // regression that started passing a different argument (e.g. an options
    // object) would silently mint a second, unshared RTK Query cache entry
    // and this test must fail.
    expect(historyQueryMock).toHaveBeenCalledExactlyOnceWith(undefined)
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

  it('renders the populated plan when targetEvent is null (general-fitness path) — THE BLOCK renders with no goal chip', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 3, 20))

    getCurrentPlanMock.mockReturnValue({
      data: buildPlanFixture(),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    renderHome()

    expect(screen.getByTestId('home-page')).toBeInTheDocument()
    const block = screen.getByTestId('the-block')
    expect(block).toBeInTheDocument()
    // THE BLOCK's own section-rule slot (the goal chip), scoped to THE
    // BLOCK's own subtree — TheWeek renders an unrelated `section-rule-slot`
    // of its own (the progress string) elsewhere on the page.
    expect(block.querySelector('[data-testid="section-rule-slot"]')).toBeNull()
  })
})
