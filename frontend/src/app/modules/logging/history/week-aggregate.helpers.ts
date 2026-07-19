// Per-week ledger summary for the LOG BOOK history surface (Slice 4 PR-C,
// spec § 4.2). Computed over a week group's logs (see week-grouping.helpers)
// and rendered as a single right-aligned line beside the "Week of …" header.

import { CompletionStatus, PreferredUnits, type WorkoutLogDto } from '~/api/generated'
import { formatDistanceMeters } from '~/modules/common/utils/unit-format.helpers'

/** The distance + run/skip counts for one ISO-week bucket of logs. */
export interface WeekAggregate {
  /** Sum of the week's RUN (Complete + Partial) logs' `distanceMeters`. */
  distanceMeters: number
  /** Count of logs whose `completionStatus` is not `Skipped`. */
  runCount: number
  /** Count of logs whose `completionStatus` is `Skipped`. */
  skipCount: number
}

/**
 * Reduces a week's logs into a {@link WeekAggregate}: a run is any log whose
 * `completionStatus` is not `Skipped` — `Complete` and `Partial` both
 * happened, so both count toward `runCount` and contribute their (actual, for
 * `Partial`) `distanceMeters` to the sum. A `Skipped` log persists `0` and
 * contributes nothing to distance, only to `skipCount`.
 */
export const computeWeekAggregate = (logs: readonly WorkoutLogDto[]): WeekAggregate => {
  let distanceMeters = 0
  let runCount = 0
  let skipCount = 0

  for (const log of logs) {
    if (log.completionStatus === CompletionStatus.Skipped) {
      skipCount++
    } else {
      runCount++
      distanceMeters += log.distanceMeters
    }
  }

  return { distanceMeters, runCount, skipCount }
}

/**
 * Formats a {@link WeekAggregate} as the ledger's week-summary line, e.g.
 * `"15.2 km · 3 RUNS · 1 SKIP"` (source stays natural-case; the caller's
 * `uppercase` CSS class is what renders it all-caps). The skip clause is
 * appended AFTER the run clause only when at least one skip occurred
 * (spec § 9 #2); a week with no skips renders just the distance + run count.
 */
export const formatWeekAggregate = (agg: WeekAggregate, units: PreferredUnits): string => {
  const km = formatDistanceMeters(agg.distanceMeters, units) ?? '0.0 km'
  const base = `${km} · ${agg.runCount} RUN${agg.runCount === 1 ? '' : 'S'}`
  return agg.skipCount > 0 ? `${base} · ${agg.skipCount} SKIP` : base
}
