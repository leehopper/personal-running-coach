// Pure derivations for THE WEEK's 7-day grid + progress string (Slice 2
// §2.5). Date math is UTC-midnight-epoch style throughout, matching
// `resolveCurrentWeek`'s style in `use-plan.hooks.ts` — NOT
// `logging/history/week-grouping.helpers.ts`'s local-`Date`/Monday-anchored
// style, which is the wrong week boundary (ISO-Monday vs. Sunday-training-
// week) for this surface. All log-join arithmetic imports the shared
// `plan-display.helpers.ts` primitives (`parseIsoDateUtc`,
// `resolveCalendarDateUtc`) rather than re-deriving them — "one
// implementation of the date math, not two."

import { PreferredUnits } from '~/api/generated'
import type { WorkoutLogDto } from '~/api/generated'
import { METERS_PER_MILE } from '~/modules/common/utils/unit-format.helpers'
import type { MesoWeekTemplateDto } from '~/modules/plan/models/plan.model'
import { DAY_SLOT_KEYS, parseIsoDateUtc, resolveCalendarDateUtc } from './plan-display.helpers'

const METERS_PER_KM = 1000
const KM_PER_MILE = METERS_PER_MILE / METERS_PER_KM

/** Visual state of one THE WEEK day cell. */
export type DayCellState = 'done' | 'today' | 'planned' | 'rest'

/** One day cell in THE WEEK's 7-cell grid (Sunday-first, index 0..6). */
export interface DayCell {
  dayOfWeek: number
  /** UTC-midnight-normalized calendar date, or `null` when `planStartDate` is unparseable. */
  date: Date | null
  state: DayCellState
}

/**
 * Returns whether any log in `logs` was `occurredOn` the calendar date named
 * by `dateEpoch` (a UTC-midnight epoch) — the ONE log-matching predicate
 * {@link resolveDayCells} calls for its `done` cell state and `home.page.tsx`
 * calls (via this same export) to decide whether `WorkoutHero` should render
 * its `logged` state for today (Slice 2 F1 fix). ALL fetched logs count
 * regardless of `completionStatus` (including `Skipped`) — a literal
 * reading of "a log exists for that date," matching {@link resolveDayCells}'s
 * own rule. Sharing this one function (never a second, parallel "is today
 * logged" check re-derived at the hero's mount site) is what makes it
 * IMPOSSIBLE for THE WEEK's today cell and the hero's logged-state affordance
 * to disagree about the same date — the exact bug this predicate's
 * extraction closes.
 */
export function isDateLogged(logs: readonly WorkoutLogDto[], dateEpoch: number): boolean {
  return logs.some((log) => parseIsoDateUtc(log.occurredOn) === dateEpoch)
}

/**
 * Resolves the 4-state (`done`/`today`/`planned`/`rest`) render state for
 * every day of `weekNumber`, joining the meso day-slot template against this
 * week's logged runs. Always returns 7 entries, Sunday-first.
 *
 * `done` beats `today` when both are true — a log already exists for
 * today's date, so it renders as a completed day, not an outlined "you are
 * here" marker. ALL fetched logs count toward `done` regardless of
 * `completionStatus` (including `Skipped`) — a literal reading of "a log
 * exists for that date."
 *
 * When `planStartDate` is unparseable, `resolveCalendarDateUtc` returns
 * `null` for every day, so no cell can resolve `done`/`today` (there is no
 * date to compare against) — cells degrade to `planned`/`rest` from the slot
 * template alone, never a crash.
 */
export function resolveDayCells(params: {
  week: MesoWeekTemplateDto | undefined
  weekNumber: number
  planStartDate: string
  logs: readonly WorkoutLogDto[]
  /** Pre-normalized UTC-midnight epoch — `plan-display.helpers.ts`'s `toUtcMidnight`. */
  todayUtc: number
}): DayCell[] {
  const { week, weekNumber, planStartDate, logs, todayUtc } = params

  const cells: DayCell[] = []
  for (let dayOfWeek = 0; dayOfWeek < 7; dayOfWeek++) {
    const cellDate = resolveCalendarDateUtc(planStartDate, weekNumber, dayOfWeek)
    const cellEpoch = cellDate === null ? null : cellDate.getTime()
    const hasLog = cellEpoch !== null && isDateLogged(logs, cellEpoch)
    const isToday = cellEpoch !== null && cellEpoch === todayUtc
    const slot = week?.[DAY_SLOT_KEYS[dayOfWeek]]

    let state: DayCellState
    if (hasLog) {
      state = 'done'
    } else if (isToday) {
      state = 'today'
    } else if (slot?.slotType === 'Run') {
      state = 'planned'
    } else {
      state = 'rest'
    }

    cells.push({ dayOfWeek, date: cellDate, state })
  }
  return cells
}

/**
 * Sums the distance (in kilometres) of every log whose `occurredOn` falls
 * within `weekNumber`'s Sunday–Saturday span. Returns `0` when
 * `planStartDate` is unparseable (no span to match against). Counts every
 * matching log regardless of `completionStatus`, mirroring
 * {@link resolveDayCells}'s `done`-cell rule.
 */
export function weekLoggedKm(params: {
  weekNumber: number
  planStartDate: string
  logs: readonly WorkoutLogDto[]
}): number {
  const { weekNumber, planStartDate, logs } = params

  const weekStart = resolveCalendarDateUtc(planStartDate, weekNumber, 0)
  const weekEnd = resolveCalendarDateUtc(planStartDate, weekNumber, 6)
  if (weekStart === null || weekEnd === null) {
    return 0
  }
  const weekStartEpoch = weekStart.getTime()
  const weekEndEpoch = weekEnd.getTime()

  const matching = logs.filter((log) => {
    const epoch = parseIsoDateUtc(log.occurredOn)
    return epoch !== null && epoch >= weekStartEpoch && epoch <= weekEndEpoch
  })
  const totalMeters = matching.reduce((sum, log) => sum + log.distanceMeters, 0)
  return totalMeters / METERS_PER_KM
}

/**
 * Formats THE WEEK's progress string as `"N.N/NN.N KM"` / `"N.N/NN.N MI"` —
 * one decimal on both sides, no space around `/`. Unlike
 * `formatDistanceKm`, this never returns `null` for a zero/non-positive
 * value — the progress string always shows both sides, including `0.0`.
 */
export function formatWeekProgress(
  loggedKm: number,
  targetKm: number,
  units: PreferredUnits,
): string {
  const isMiles = units === PreferredUnits.Miles
  const toDisplayUnit = (km: number): number => (isMiles ? km / KM_PER_MILE : km)
  const suffix = isMiles ? 'MI' : 'KM'
  return `${toDisplayUnit(loggedKm).toFixed(1)}/${toDisplayUnit(targetKm).toFixed(1)} ${suffix}`
}
