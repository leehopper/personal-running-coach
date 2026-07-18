// Pure display-derivation helpers for the /log form restyle (Slice 4 PR-B).
//
// These are display-only computations that sit in front of the log form's
// React Hook Form state and the prescribed-workout banner — none of them
// touch the wire. Pace formatting is intentionally NOT re-implemented here:
// `deriveDisplayPace` reuses the history surface's `formatLogPace` so the
// form's live pace preview and the history list's rendered pace agree
// byte-for-byte, and `formatPaceRange` reuses `unit-format.helpers`'s
// `formatPaceSecPerKm` for the same reason. Per the root `CLAUDE.md`
// trademark rule, nothing here references zone names — only neutral
// distance/duration/pace strings.

import type { PreferredUnits } from '~/api/generated'
import {
  distanceUnitLabel,
  formatPaceSecPerKm,
  preferredDistanceToMeters,
} from '~/modules/common/utils/unit-format.helpers'
import { formatLogPace } from '~/modules/logging/history/history-format.helpers'

const SECONDS_PER_MINUTE = 60

/** Em dash placeholder for a pace range with no valid bound (both invalid). */
const EMPTY_RANGE_PLACEHOLDER = '—'

/** En dash (U+2013) joining a pace range's fast/easy bounds — never a plain hyphen. */
const EN_DASH = '–'

/**
 * Placeholder rendered by {@link formatDateChipLabel} when `occurredOn`
 * doesn't parse to a valid calendar date — e.g. the native date input was
 * cleared via the browser's clear control, leaving RHF's field value `''`.
 * Uppercase to match the module's other baked-caps abbreviations; the
 * `DateChip` applies no CSS `uppercase` of its own.
 */
const DATE_CHIP_PLACEHOLDER = 'SELECT DATE'

const WEEKDAY_ABBR = ['SUN', 'MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT'] as const

const MONTH_ABBR = [
  'JAN',
  'FEB',
  'MAR',
  'APR',
  'MAY',
  'JUN',
  'JUL',
  'AUG',
  'SEP',
  'OCT',
  'NOV',
  'DEC',
] as const

/**
 * Derives a display-only pace for the /log form's live preview from the raw
 * React Hook Form `watch()` strings for distance and duration-in-minutes.
 * Returns `null` for empty, non-numeric, zero, or negative input on either
 * field — never throws, and never surfaces `NaN` or a degenerate `00:00`
 * pace while the runner is still typing. The distance is interpreted in the
 * runner's preferred unit (`preferredDistanceToMeters`) before handing off
 * to the shared `formatLogPace` pace derivation, so this stays unit-aware.
 */
export const deriveDisplayPace = (
  distanceRaw: string,
  durationMinutesRaw: string,
  units: PreferredUnits,
): string | null => {
  const distance = Number(distanceRaw.trim())
  const durationMinutes = Number(durationMinutesRaw.trim())
  if (!Number.isFinite(distance) || distance <= 0) {
    return null
  }
  if (!Number.isFinite(durationMinutes) || durationMinutes <= 0) {
    return null
  }
  const metres = preferredDistanceToMeters(distance, units)
  return formatLogPace(metres, durationMinutes * SECONDS_PER_MINUTE, units)
}

/**
 * Formats an ISO `YYYY-MM-DD` date-only string as an uppercase date-chip
 * label (e.g. `"WED, JUL 8"`). Parsed as a LOCAL calendar date (DEC-076:
 * training dates are local, never UTC) — this is a log-module-local
 * formatter, distinct from `history-format.helpers`'s mixed-case
 * `formatLogDate` (`"Sun, Jun 7"`), which this module deliberately does not
 * reuse.
 *
 * Never throws and never surfaces a garbage label: if `occurredOn` doesn't
 * split into three finite Y/M/D numbers, or the constructed `Date` doesn't
 * round-trip back to those same Y/M/D values (`Date`'s constructor silently
 * ROLLS OVER an out-of-range month/day — e.g. `2026-13-40` — rather than
 * producing an `Invalid Date`, so a bare `Number.isNaN(date.getTime())`
 * check alone would miss it), this returns {@link DATE_CHIP_PLACEHOLDER}.
 */
export const formatDateChipLabel = (occurredOn: string): string => {
  const [year, month, day] = occurredOn.split('-').map(Number)
  if (![year, month, day].every(Number.isFinite)) {
    return DATE_CHIP_PLACEHOLDER
  }

  const date = new Date(year, month - 1, day)
  const isValidCalendarDate =
    date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day
  if (!isValidCalendarDate) {
    return DATE_CHIP_PLACEHOLDER
  }

  return `${WEEKDAY_ABBR[date.getDay()]}, ${MONTH_ABBR[date.getMonth()]} ${date.getDate()}`
}

/**
 * Formats the prescribed-workout banner's pace range (e.g. `"4:00–4:30/km"`)
 * from the fast/easy pace-zone-index bounds, joined with an EN DASH (never a
 * hyphen) and no surrounding spaces — the unit suffix renders once, at the
 * end. Reuses `formatPaceSecPerKm` so this agrees byte-for-byte with every
 * other pace rendering in the app.
 *
 * Null-robust like the existing `formatPaceRangeSecPerKm`: an invalid bound
 * falls back to the other side, both invalid fall back to an em-dash
 * placeholder, and bounds that round to the same `MM:SS` collapse to a
 * single formatted pace (no degenerate `"4:00–4:00/km"`). The faster pace
 * (fewer sec/km) renders first, regardless of argument order. The caller
 * applies `uppercase` via CSS for the banner — this returns the
 * lowercase-suffixed form (e.g. `"4:00–4:30/km"`).
 */
export const formatPaceRange = (
  fastSecPerKm: number,
  easySecPerKm: number,
  units: PreferredUnits,
): string => {
  const formattedFast = formatPaceSecPerKm(fastSecPerKm, units)
  const formattedEasy = formatPaceSecPerKm(easySecPerKm, units)

  if (formattedFast === null) {
    return formattedEasy ?? EMPTY_RANGE_PLACEHOLDER
  }
  if (formattedEasy === null) {
    return formattedFast
  }

  const [first, second] =
    fastSecPerKm <= easySecPerKm ? [formattedFast, formattedEasy] : [formattedEasy, formattedFast]

  if (first === second) {
    return first
  }

  const suffix = `/${distanceUnitLabel(units)}`
  return `${first.slice(0, -suffix.length)}${EN_DASH}${second}`
}
