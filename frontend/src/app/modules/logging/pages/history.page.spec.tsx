import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { QueryWorkoutLogsResponseDto, WorkoutLogDto } from '~/api/generated'
import { CompletionStatus, PreferredUnits } from '~/api/generated'

const { historyQueryMock, fetchNextPageMock, preferredUnitsMock } = vi.hoisted(() => ({
  historyQueryMock: vi.fn(),
  fetchNextPageMock: vi.fn(),
  preferredUnitsMock: vi.fn<() => PreferredUnits>(),
}))

vi.mock('~/api/workout-log.api', () => ({
  useGetWorkoutLogHistoryInfiniteQuery: () => historyQueryMock(),
}))
// `HistoryPage` reads the unit preference via this hook, which wraps a real
// RTK Query hook; the page renders here without a Redux store, so stub it
// through a mockable ref.
vi.mock('~/modules/settings/hooks/use-preferred-units.hooks', () => ({
  usePreferredUnits: () => preferredUnitsMock(),
}))

import HistoryPage from './history.page'

const log = (occurredOn: string): WorkoutLogDto => ({
  workoutLogId: occurredOn,
  occurredOn,
  distanceMeters: 5000,
  durationSeconds: 1800,
  completionStatus: CompletionStatus.Complete,
})

const page = (
  logs: WorkoutLogDto[],
  nextCursor: string | null = null,
): QueryWorkoutLogsResponseDto => ({
  logs,
  nextCursor,
})

interface QueryStateOverrides {
  data?: { pages: QueryWorkoutLogsResponseDto[]; pageParams: unknown[] }
  isLoading?: boolean
  isError?: boolean
  hasNextPage?: boolean
  isFetchingNextPage?: boolean
}

const setQueryState = (overrides: QueryStateOverrides): void => {
  historyQueryMock.mockReturnValue({
    data: undefined,
    isLoading: false,
    isError: false,
    hasNextPage: false,
    isFetchingNextPage: false,
    fetchNextPage: fetchNextPageMock,
    refetch: vi.fn(),
    ...overrides,
  })
}

const dataOf = (...pages: QueryWorkoutLogsResponseDto[]) => ({
  pages,
  pageParams: pages.map(() => null),
})

const renderPage = () => {
  const user = userEvent.setup()
  return {
    user,
    ...render(
      <MemoryRouter initialEntries={['/history']}>
        <HistoryPage />
      </MemoryRouter>,
    ),
  }
}

describe('HistoryPage', () => {
  beforeEach(() => {
    preferredUnitsMock.mockReturnValue(PreferredUnits.Kilometers)
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('shows a loading state while the first page is in flight', () => {
    setQueryState({ isLoading: true })
    renderPage()
    expect(screen.getByRole('status')).toHaveTextContent(/loading/i)
  })

  it('shows an error surface when the query fails', () => {
    setQueryState({ isError: true })
    renderPage()
    expect(screen.getByTestId('workout-history-error')).toBeInTheDocument()
  })

  it('shows an empty state when there are no logged workouts', () => {
    setQueryState({ data: dataOf(page([])) })
    renderPage()
    expect(screen.getByTestId('workout-history-empty')).toBeInTheDocument()
    expect(screen.queryByTestId('workout-history-list')).toBeNull()
  })

  it('renders week-grouped entries flattened across pages, merging a split week', () => {
    setQueryState({
      data: dataOf(page([log('2026-06-07'), log('2026-06-03')], 'c1'), page([log('2026-06-01')])),
    })
    renderPage()

    expect(screen.getAllByTestId('workout-history-entry')).toHaveLength(3)
    // The three logs span one ISO week split across two pages — single header.
    const headers = screen.getAllByTestId('workout-history-week-header')
    expect(headers).toHaveLength(1)
    expect(headers[0]).toHaveTextContent('Week of Jun 1, 2026')
  })

  it('shows "Load older" when more pages exist and calls fetchNextPage on click', async () => {
    setQueryState({ data: dataOf(page([log('2026-06-07')], 'c1')), hasNextPage: true })
    const { user } = renderPage()

    const loadOlder = screen.getByTestId('workout-history-load-older')
    expect(loadOlder).toBeEnabled()
    await user.click(loadOlder)
    expect(fetchNextPageMock).toHaveBeenCalledTimes(1)
  })

  it('disables "Load older" while the next page is loading', () => {
    setQueryState({
      data: dataOf(page([log('2026-06-07')], 'c1')),
      hasNextPage: true,
      isFetchingNextPage: true,
    })
    renderPage()
    expect(screen.getByTestId('workout-history-load-older')).toBeDisabled()
  })

  it('hides "Load older" when there are no more pages', () => {
    setQueryState({ data: dataOf(page([log('2026-06-07')])), hasNextPage: false })
    renderPage()
    expect(screen.queryByTestId('workout-history-load-older')).toBeNull()
  })

  it('renders entries in miles when the unit preference is Miles', () => {
    preferredUnitsMock.mockReturnValueOnce(PreferredUnits.Miles)
    setQueryState({ data: dataOf(page([log('2026-06-07')])) })
    renderPage()

    // 5000 m / 1609.344 = 3.107... -> 3.1 mi
    expect(screen.getByText('3.1 mi')).toBeInTheDocument()
  })
})
