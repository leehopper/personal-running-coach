import type { ReactElement } from 'react'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import type { PreferredUnits, WorkoutLogDto } from '~/api/generated'
import { useGetWorkoutLogHistoryInfiniteQuery } from '~/api/workout-log.api'
import { WorkoutHistoryList } from '~/modules/logging/history/workout-history-list.component'
import { usePreferredUnits } from '~/modules/settings/hooks/use-preferred-units.hooks'

interface HistoryBodyProps {
  logs: WorkoutLogDto[]
  isLoading: boolean
  isError: boolean
  hasNextPage: boolean
  isFetchingNextPage: boolean
  loadOlder: () => void
  units: PreferredUnits
}

/**
 * The state-dependent body of the history surface: loading, error, empty, or
 * the week-grouped list with its "Load older" control. Guard-clause returns
 * keep the branches flat (no nested ternaries).
 */
const HistoryBody = ({
  logs,
  isLoading,
  isError,
  hasNextPage,
  isFetchingNextPage,
  loadOlder,
  units,
}: HistoryBodyProps): ReactElement => {
  if (isLoading) {
    return (
      <div role="status" aria-live="polite" className="py-12 text-center">
        <span className="text-sm text-muted-foreground">Loading…</span>
      </div>
    )
  }

  if (isError) {
    return (
      <div
        role="alert"
        data-testid="workout-history-error"
        className="flex flex-col items-center gap-2 py-12 text-center"
      >
        <p className="text-sm text-muted-foreground">
          We couldn’t load your workout history. Reload the page in a moment.
        </p>
      </div>
    )
  }

  if (logs.length === 0) {
    return (
      <div
        data-testid="workout-history-empty"
        className="flex flex-col items-center gap-3 py-12 text-center"
      >
        <p className="text-base font-semibold text-foreground">No workouts logged yet</p>
        <p className="max-w-sm text-sm text-muted-foreground">
          Once you log a run it’ll show up here, grouped by week.
        </p>
        <Button asChild size="sm">
          <Link to="/log">Log a workout</Link>
        </Button>
      </div>
    )
  }

  return (
    <>
      <WorkoutHistoryList logs={logs} units={units} />
      {hasNextPage ? (
        <Button
          type="button"
          variant="outline"
          data-testid="workout-history-load-older"
          disabled={isFetchingNextPage}
          onClick={loadOlder}
          className="self-center"
        >
          {isFetchingNextPage ? 'Loading…' : 'Load older'}
        </Button>
      ) : null}
    </>
  )
}

/**
 * Protected `/history` route: the runner's logged-workout history. Consumes the
 * DB-driven keyset query endpoint via an RTK `infiniteQuery` ("Load older"),
 * flattens the fetched pages into one newest-first list, and hands it to
 * `WorkoutHistoryList` which groups it by ISO week (spec § Unit 7). "Load older"
 * appears only while the server still has a cursor to hand back.
 */
export const HistoryPage = (): ReactElement => {
  const { data, isLoading, isError, hasNextPage, isFetchingNextPage, fetchNextPage } =
    useGetWorkoutLogHistoryInfiniteQuery(undefined)
  const units = usePreferredUnits()

  const logs = data?.pages.flatMap((logPage) => logPage.logs) ?? []

  return (
    <main
      data-testid="workout-history-page"
      className="mx-auto flex min-h-full w-full max-w-3xl flex-col gap-6 bg-background px-4 py-8"
    >
      <header className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-foreground">Workout history</h1>
      </header>

      <HistoryBody
        logs={logs}
        isLoading={isLoading}
        isError={isError}
        hasNextPage={hasNextPage}
        isFetchingNextPage={isFetchingNextPage}
        loadOlder={() => {
          void fetchNextPage()
        }}
        units={units}
      />
    </main>
  )
}

export default HistoryPage
