import type { ReactElement } from 'react'

import { PreferredUnits, type WorkoutLogDto } from '~/api/generated'
import { LedgerRow } from './ledger-row.component'
import { formatWeekAggregate } from './week-aggregate.helpers'
import { groupLogsByIsoWeek } from './week-grouping.helpers'

export interface WorkoutHistoryListProps {
  /** The full merged, newest-first log list across all fetched pages. */
  logs: readonly WorkoutLogDto[]
  /**
   * Display unit for each entry's distance + pace. Defaults to Kilometers so
   * callers that predate the unit preference (and isolated tests) render the
   * km form unchanged.
   */
  units?: PreferredUnits
}

/**
 * The presentational history list: groups the flat (possibly multi-page) log
 * list into ISO-week buckets and renders a `Week of …` section per week, each
 * with its newest-first entries. Grouping is done here over the whole merged
 * list (not per page), so a week split across a page boundary renders under a
 * single header (spec § Unit 7).
 */
export const WorkoutHistoryList = ({
  logs,
  units = PreferredUnits.Kilometers,
}: WorkoutHistoryListProps): ReactElement => {
  const weeks = groupLogsByIsoWeek(logs)

  return (
    <div data-testid="workout-history-list" className="flex flex-col gap-8">
      {weeks.map((week) => (
        <section key={week.weekStartIso} aria-label={week.label} className="flex flex-col gap-1">
          <div className="flex items-baseline justify-between gap-2">
            <h2
              data-testid="workout-history-week-header"
              className="t-section-label uppercase text-muted-foreground"
            >
              {week.label}
            </h2>
            <span
              data-testid="workout-history-week-aggregate"
              className="t-data-label uppercase text-muted-foreground"
            >
              {formatWeekAggregate(week.aggregate, units)}
            </span>
          </div>
          <ul className="flex flex-col">
            {week.logs.map((log) => (
              <li key={log.workoutLogId}>
                <LedgerRow log={log} units={units} />
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  )
}
