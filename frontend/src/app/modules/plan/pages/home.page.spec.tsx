import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { QueryWorkoutLogsResponseDto, WorkoutLogDto } from '~/api/generated'
import { PreferredUnits } from '~/api/generated'
import {
  buildPlanFixture,
  buildWorkoutLog as log,
} from '~/modules/plan/components/plan-display.fixture'
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
// instant `CoachDigest` renders. Same hoisted-mock idiom used everywhere
// else in this suite that this real hook needs stubbing.
vi.mock('~/api/conversation.api', () => ({
  useGetConversationTimelineQuery: () => conversationTimelineMock(),
}))

// `HomePage` mounts `TheWeek`, sourcing its `logs` prop from this real RTK
// Query hook (the SAME hook, same `undefined` arg, `/history` already
// uses — a shared cache entry, never double-fetched). Forwards the real
// call-site argument to `historyQueryMock` (rather than discarding it) so
// tests can assert WHAT `HomePage` passes, not just THAT it called the
// hook — see the `historyQueryMock` argument-contract test below.
vi.mock('~/api/workout-log.api', () => ({
  useGetWorkoutLogHistoryInfiniteQuery: (arg: unknown) => historyQueryMock(arg),
}))

// Wraps the REAL `resolveCurrentWeek` in a spy (delegating to its actual
// implementation via `vi.fn(actual.resolveCurrentWeek)`) rather than
// replacing it — every other export of this module (`usePlan`,
// `findCurrentMesoWeek`, `findCurrentWeekWorkouts`) stays real. Lets one
// test assert the single-date-pipeline contract: `HomePage` must pass its
// own `today` explicitly as the 2nd argument, never rely on
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

const logsPage = (
  logs: WorkoutLogDto[],
): { pages: QueryWorkoutLogsResponseDto[]; pageParams: unknown[] } => ({
  pages: [{ logs, nextCursor: null }],
  pageParams: [null],
})

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
    // assertions. `CoachDigest`'s own per-state rendering has its own
    // dedicated test coverage — this file only needs the empty state.
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
    // "Never call fetchNextPage from Home" rule, pinned across every
    // rendered scenario in this file — calling it would permanently append
    // pages to the shared `/history` cache entry TheWeek reads from,
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
    // Sentence-case source copy — StatCell's hero label applies `uppercase`
    // via CSS, so this asserts the RENDERED (source) text, not "MILES".
    expect(home.textContent).toContain('Miles')
    expect(home.textContent).toContain('18.6')
    // Nothing on the plan surface should still be rendering kilometres.
    expect(screen.queryByText(/\d\.\d km/u)).not.toBeInTheDocument()
  })

  it("calls resolveCurrentWeek with the page's own `today`, not the default parameter", () => {
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

    // Asserts WHAT was passed, not just THAT the hook was called: `HomePage`
    // must call this hook with the exact same argument `/history` uses
    // (`undefined`) so the two share one RTK Query cache entry — a
    // regression that started passing a different argument (e.g. an options
    // object) would silently mint a second, unshared cache entry, and this
    // test must fail.
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

  // Both assertions below read off ONE render of the real `HomePage` tree,
  // so a future change that re-splits the hero's and THE WEEK's log-join
  // logic into two independent checks (rather than sharing one predicate)
  // fails this test the moment the two disagree about whether today's run
  // is logged.
  describe('hero / THE WEEK agreement on logged state', () => {
    it("given a log exists for today's slot date, the hero renders LOGGED and THE WEEK's today cell renders done", () => {
      vi.useFakeTimers()
      vi.setSystemTime(new Date(2026, 3, 20)) // Monday 2026-04-20 → dayOfWeek 1, week 1 (Run/Easy slot)

      getCurrentPlanMock.mockReturnValue({
        data: buildPlanFixture(),
        isLoading: false,
        isError: false,
        refetch: vi.fn(),
      })
      historyQueryMock.mockReturnValue({
        data: logsPage([log('2026-04-20')]),
        isLoading: false,
        isError: false,
        hasNextPage: false,
        isFetchingNextPage: false,
        fetchNextPage: fetchNextPageMock,
        refetch: vi.fn(),
      })
      renderHome()

      const hero = screen.getByTestId('workout-hero')
      expect(hero.dataset.variant).toBe('logged')
      expect(screen.getByTestId('workout-hero-logged-action')).toBeInTheDocument()
      expect(screen.queryByTestId('workout-hero-log-action')).not.toBeInTheDocument()

      const todayCell = screen
        .getAllByTestId('the-week-day-cell')
        .find((cell) => cell.dataset.dayOfWeek === '1')
      expect(todayCell?.dataset.state).toBe('done')
    })

    it("given no log exists for today, the hero renders LOG RUN and THE WEEK's today cell renders today (still in agreement)", () => {
      vi.useFakeTimers()
      vi.setSystemTime(new Date(2026, 3, 20)) // Monday 2026-04-20 → dayOfWeek 1, week 1

      getCurrentPlanMock.mockReturnValue({
        data: buildPlanFixture(),
        isLoading: false,
        isError: false,
        refetch: vi.fn(),
      })
      // historyQueryMock keeps `beforeEach`'s default `data: undefined` (no logs).
      renderHome()

      const hero = screen.getByTestId('workout-hero')
      expect(hero.dataset.variant).toBe('run')
      expect(screen.getByTestId('workout-hero-log-action')).toBeInTheDocument()
      expect(screen.queryByTestId('workout-hero-logged-action')).not.toBeInTheDocument()

      const todayCell = screen
        .getAllByTestId('the-week-day-cell')
        .find((cell) => cell.dataset.dayOfWeek === '1')
      expect(todayCell?.dataset.state).toBe('today')
    })
  })

  describe('when the workout-log history fetch fails or is still in flight', () => {
    it('given the fetch failed, the hero does not claim LOGGED and THE WEEK does not claim 0.0 km logged', () => {
      vi.useFakeTimers()
      vi.setSystemTime(new Date(2026, 3, 20)) // Monday 2026-04-20 → dayOfWeek 1, week 1 (Run/Easy slot)

      getCurrentPlanMock.mockReturnValue({
        data: buildPlanFixture(),
        isLoading: false,
        isError: false,
        refetch: vi.fn(),
      })
      historyQueryMock.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: true,
        hasNextPage: false,
        isFetchingNextPage: false,
        fetchNextPage: fetchNextPageMock,
        refetch: vi.fn(),
      })
      renderHome()

      const hero = screen.getByTestId('workout-hero')
      // NOT 'logged' — an untrustworthy fetch must never resolve to the
      // logged affordance (that would risk hiding the fact a log might
      // already exist). Still shows the workout content via the 'run'
      // variant, just with an honest disclaimer rather than a confident
      // LOG RUN claim of "not logged".
      expect(hero.dataset.variant).toBe('run')
      expect(screen.getByTestId('workout-hero-log-status-unavailable')).toBeInTheDocument()

      const weekSection = screen.getByTestId('the-week')
      expect(weekSection.textContent).not.toContain('0.0/30.0 KM')
      expect(weekSection.textContent).toContain('—/30.0 KM')

      // No cell can honestly claim 'done' when the log fetch itself failed.
      const cells = screen.getAllByTestId('the-week-day-cell')
      expect(cells.every((cell) => cell.dataset.state !== 'done')).toBe(true)
    })

    it('given the fetch is still in flight, the hero does not claim LOGGED and THE WEEK does not claim 0.0 km logged', () => {
      vi.useFakeTimers()
      vi.setSystemTime(new Date(2026, 3, 20)) // Monday 2026-04-20 → dayOfWeek 1, week 1

      getCurrentPlanMock.mockReturnValue({
        data: buildPlanFixture(),
        isLoading: false,
        isError: false,
        refetch: vi.fn(),
      })
      historyQueryMock.mockReturnValue({
        data: undefined,
        isLoading: true,
        isError: false,
        hasNextPage: false,
        isFetchingNextPage: false,
        fetchNextPage: fetchNextPageMock,
        refetch: vi.fn(),
      })
      renderHome()

      const hero = screen.getByTestId('workout-hero')
      expect(hero.dataset.variant).toBe('run')
      expect(screen.getByTestId('workout-hero-log-status-unavailable')).toBeInTheDocument()

      const weekSection = screen.getByTestId('the-week')
      expect(weekSection.textContent).not.toContain('0.0/30.0 KM')
      expect(weekSection.textContent).toContain('—/30.0 KM')
    })
  })

  describe('resolveHeroContent branches exercised at the page level', () => {
    it('renders the rest variant naming the correct next workout on a Rest slot day', () => {
      vi.useFakeTimers()
      vi.setSystemTime(new Date(2026, 3, 19)) // Sunday 2026-04-19 → dayOfWeek 0, week 1 (Rest slot)

      getCurrentPlanMock.mockReturnValue({
        data: buildPlanFixture(),
        isLoading: false,
        isError: false,
        refetch: vi.fn(),
      })
      renderHome()

      const hero = screen.getByTestId('workout-hero')
      expect(hero.dataset.variant).toBe('rest')
      // Proves `nextWorkout` is threaded from `findNextWorkoutAfter`, not
      // some other workout — the fixture's next scheduled workout after
      // Sunday is Monday's easy run.
      const nextWorkoutRow = screen.getByTestId('workout-hero-next-workout')
      expect(nextWorkoutRow).toHaveTextContent(/mon/i)
      expect(nextWorkoutRow.textContent).toMatch(/easy/i)
    })

    it('renders the unavailable variant, not a crash, when the slot is a Run day with no matching micro-workout', () => {
      vi.useFakeTimers()
      vi.setSystemTime(new Date(2026, 3, 20)) // Monday 2026-04-20 → dayOfWeek 1, week 1 (Run slot)

      const planWithNoWorkouts: PlanProjectionDto = {
        ...buildPlanFixture(),
        microWorkoutsByWeek: { 1: { workouts: [] } },
      }
      getCurrentPlanMock.mockReturnValue({
        data: planWithNoWorkouts,
        isLoading: false,
        isError: false,
        refetch: vi.fn(),
      })
      renderHome()

      const hero = screen.getByTestId('workout-hero')
      expect(hero.dataset.variant).toBe('unavailable')
      expect(screen.getByText("This week's plan isn't ready yet.")).toBeInTheDocument()
    })
  })
})
