// ISO-calendar-week bucketing for the workout-history surface (PR7).
//
// The history query endpoint returns a flat, newest-first page of logs;
// week grouping is *presentation*, computed client-side over the merged
// flat list (DEC-075 / spec § Unit 7). Operating on the whole merged list —
// rather than per page — is what keeps the grouping page-boundary-safe: logs
// of the same ISO week that straddle two fetched pages land under one header,
// never a duplicated one.
//
// Dates are local-calendar (DEC-076). All arithmetic uses native `Date` with
// local Y/M/D construction (no library; no UTC parse) — month rollover is
// handled by `Date`'s own normalisation.

import type { WorkoutLogDto } from '~/api/generated'
import { monthLabel } from './history-format.helpers'
import { computeWeekAggregate, type WeekAggregate } from './week-aggregate.helpers'

/** A single ISO-week bucket of logs for the history surface. */
export interface WorkoutHistoryWeekGroup {
  /** ISO `YYYY-MM-DD` of the week's Monday — stable React key + bucket id. */
  weekStartIso: string
  /** Display header, e.g. `"Week of Jun 1"`. */
  label: string
  /** Logs in this week, newest-first. */
  logs: WorkoutLogDto[]
  /** The week's distance + run/skip summary (spec § 4.2). */
  aggregate: WeekAggregate
}

/** Parses an ISO `YYYY-MM-DD` string as a local-calendar date (never UTC). */
export const parseIsoDateOnly = (isoDate: string): Date => {
  const [year, month, day] = isoDate.split('-').map(Number)
  return new Date(year, month - 1, day)
}

/** Local `YYYY-MM-DD` for a date (never `toISOString`, which would UTC-shift). */
const toIsoDateOnly = (date: Date): string => {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/** Returns the Monday (ISO week start) of the given date's week. */
export const getIsoWeekStart = (date: Date): Date => {
  // `getDay()` is 0=Sunday..6=Saturday; `(day + 6) % 7` is 0=Monday..6=Sunday.
  const mondayOffset = (date.getDay() + 6) % 7
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() - mondayOffset)
}

// Source stays title-case (e.g. "Week of Jun 1") — the caller's `uppercase`
// CSS class is what renders the all-caps "WEEK OF …" presentation. The year
// is dropped: the ledger is always browsing recent history, where the month
// + day already disambiguate (spec § 4.2 / DEC-089 D5).
const formatWeekOfLabel = (weekStart: Date): string =>
  `Week of ${monthLabel(weekStart.getMonth())} ${weekStart.getDate()}`

/**
 * Groups a flat log list into ISO-week buckets, newest week first, with
 * newest-first logs inside each. Sorts defensively by `occurredOn` desc
 * (tiebreak `workoutLogId` desc, mirroring the backend keyset order) so a
 * merged multi-page list groups correctly even if the pages interleave.
 */
export const groupLogsByIsoWeek = (logs: readonly WorkoutLogDto[]): WorkoutHistoryWeekGroup[] => {
  const sorted = [...logs].sort((a, b) => {
    if (a.occurredOn !== b.occurredOn) {
      return a.occurredOn < b.occurredOn ? 1 : -1
    }
    if (a.workoutLogId === b.workoutLogId) {
      return 0
    }
    return a.workoutLogId < b.workoutLogId ? 1 : -1
  })

  const groups = new Map<string, WorkoutHistoryWeekGroup>()
  for (const entry of sorted) {
    const weekStart = getIsoWeekStart(parseIsoDateOnly(entry.occurredOn))
    const weekStartIso = toIsoDateOnly(weekStart)
    const existing = groups.get(weekStartIso)
    if (existing === undefined) {
      groups.set(weekStartIso, {
        weekStartIso,
        label: formatWeekOfLabel(weekStart),
        logs: [entry],
        aggregate: { distanceMeters: 0, runCount: 0, skipCount: 0 },
      })
    } else {
      existing.logs.push(entry)
    }
  }

  // `Map` preserves insertion order; sorted desc ⇒ groups + members are
  // newest-first. The aggregate is derived from each group's final `logs`
  // list, so it is computed in this second pass rather than incrementally
  // above (incremental accumulation would work too, but a pure derive-from-
  // `logs` step keeps `aggregate` unmistakably a function of `logs`, not
  // separately-mutated state that could drift).
  return [...groups.values()].map((group) => ({
    ...group,
    aggregate: computeWeekAggregate(group.logs),
  }))
}
