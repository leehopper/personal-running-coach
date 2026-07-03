// Display formatting for the workout-history surface (PR7).
//
// The wire shape (`WorkoutLogDto`) carries distance in **metres** and duration
// in **seconds**; this module renders them for humans. Pace is derived
// (distance + duration) and formatted via the plan module's single
// `MM:SS/km` formatter so the history list and the plan view agree on pace
// presentation. Per the root `CLAUDE.md` trademark rule, no zone-name coupling
// lives here — only neutral distance/duration/pace strings.

import { CompletionStatus, PreferredUnits } from '~/api/generated'
import {
  formatDistanceMeters,
  formatPaceSecPerKm,
} from '~/modules/common/utils/unit-format.helpers'

const METERS_PER_KM = 1000
const SECONDS_PER_MINUTE = 60
const SECONDS_PER_HOUR = 3600

const MONTH_LABELS = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
] as const

const WEEKDAY_LABELS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'] as const

const pad2 = (value: number): string => value.toString().padStart(2, '0')

/**
 * Formats a distance in metres in the preferred unit (e.g. `"5.0 km"` /
 * `"3.1 mi"`). Returns `null` for a non-positive or non-finite distance — a
 * skipped run persists `0 m`, which the caller renders as a placeholder
 * rather than a misleading `"0.0 km"`.
 *
 * Defaults to Kilometers so callers that predate the unit preference (and
 * isolated tests) render the km form unchanged — byte-identical to the
 * previous inline `(metres / 1000).toFixed(1)` implementation.
 */
export const formatHistoryDistanceKm = (
  distanceMeters: number,
  units: PreferredUnits = PreferredUnits.Kilometers,
): string | null => formatDistanceMeters(distanceMeters, units)

/**
 * Formats a duration in seconds as `M:SS` under an hour and `H:MM:SS` at or
 * above an hour. Returns `null` for a non-positive or non-finite duration.
 */
export const formatDuration = (durationSeconds: number): string | null => {
  if (!Number.isFinite(durationSeconds) || durationSeconds <= 0) {
    return null
  }
  const total = Math.round(durationSeconds)
  const hours = Math.floor(total / SECONDS_PER_HOUR)
  const minutes = Math.floor((total % SECONDS_PER_HOUR) / SECONDS_PER_MINUTE)
  const seconds = total % SECONDS_PER_MINUTE
  if (hours > 0) {
    return `${hours}:${pad2(minutes)}:${pad2(seconds)}`
  }
  return `${minutes}:${pad2(seconds)}`
}

/**
 * Derives average pace from distance + duration and formats it in the
 * preferred unit (`MM:SS/km` / `MM:SS/mi`). Returns `null` when either input
 * is non-positive (no meaningful pace for a zero-distance or zero-duration
 * log). Defaults to Kilometers so callers that predate the unit preference
 * (and isolated tests) render the km form unchanged.
 */
export const formatLogPace = (
  distanceMeters: number,
  durationSeconds: number,
  units: PreferredUnits = PreferredUnits.Kilometers,
): string | null => {
  if (!Number.isFinite(distanceMeters) || distanceMeters <= 0) {
    return null
  }
  if (!Number.isFinite(durationSeconds) || durationSeconds <= 0) {
    return null
  }
  const kilometres = distanceMeters / METERS_PER_KM
  return formatPaceSecPerKm(durationSeconds / kilometres, units)
}

/**
 * Formats an ISO `YYYY-MM-DD` date-only string as a short local label
 * (e.g. `"Sun, Jun 7"`). Parsed as a local-calendar date so the displayed day
 * never shifts under timezone conversion (DEC-076: dates are local training
 * dates).
 */
export const formatLogDate = (occurredOn: string): string => {
  const [year, month, day] = occurredOn.split('-').map(Number)
  const date = new Date(year, month - 1, day)
  return `${WEEKDAY_LABELS[date.getDay()]}, ${MONTH_LABELS[date.getMonth()]} ${date.getDate()}`
}

/** Shared month-label lookup for the `Week of …` header (week-grouping). */
export const monthLabel = (monthIndex: number): string => MONTH_LABELS[monthIndex]

/** User-facing labels for each `CompletionStatus`. */
export const COMPLETION_STATUS_LABELS: Record<CompletionStatus, string> = {
  [CompletionStatus.Complete]: 'Completed',
  [CompletionStatus.Partial]: 'Partial',
  [CompletionStatus.Skipped]: 'Skipped',
}
