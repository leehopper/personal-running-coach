// Pure conversion helpers for the form-first onboarding intake (slice 4C-onboarding).
//
// Two conversion concerns live here, both deterministic and unit-tested so the
// form component and the wire mapper share ONE implementation:
//
//   1. Distance — the runner types weekly volume / long-run / race distances in
//      their preferred display unit (km or mi); the wire is km-native (DEC-086).
//      These delegate to the shared `unit-format.helpers` module so there is no
//      parallel converter and no second `1609.344` in the frontend (FR-2.3).
//
//   2. Duration — the optional finish/race times are entered as `H:MM:SS` or
//      `MM:SS` and the wire carries an ISO-8601 (`xsd:duration`) string like
//      `PT1H45M30S`. The backend parses it with `XmlConvert.ToTimeSpan` and
//      rejects a negative/absurd value, so we only ever emit a well-formed,
//      non-negative duration.

import { PreferredUnits } from '~/api/generated'
import {
  metresToPreferredDistance,
  preferredDistanceToMeters,
} from '~/modules/common/utils/unit-format.helpers'

/** Metres in one kilometre — the km leg of the km↔display conversion (NOT the mile constant). */
const METERS_PER_KM = 1000

/**
 * Interprets a distance the runner typed in their preferred unit and returns
 * the canonical value in **kilometres** for the wire. Composes the shared
 * `preferredDistanceToMeters` (the single miles→SI conversion) with the km leg,
 * so the only `1609.344` in the frontend stays in `unit-format.helpers`.
 */
export const displayDistanceToKm = (value: number, units: PreferredUnits): number =>
  preferredDistanceToMeters(value, units) / METERS_PER_KM

/**
 * Inverse of {@link displayDistanceToKm}: a canonical **kilometre** value → the
 * numeric value in the preferred unit, for pre-filling the form on resume.
 */
export const kmToDisplayDistance = (kilometres: number, units: PreferredUnits): number =>
  metresToPreferredDistance(kilometres * METERS_PER_KM, units)

/** A parsed clock time as whole hour/minute/second components. */
export interface TimeComponents {
  hours: number
  minutes: number
  seconds: number
}

const SECONDS_PER_MINUTE = 60
const MINUTES_PER_HOUR = 60

const isNonNegativeIntString = (value: string): boolean => /^\d+$/.test(value)

/**
 * Parses a runner-entered clock time — `MM:SS`, `M:SS`, or `H:MM:SS` — into
 * whole components, or `null` when the shape is invalid.
 *
 * A blank string is NOT valid here (it is "no value" — callers check for blank
 * before calling). Seconds must be 0-59; the minutes field is 0-59 in the
 * three-part form (hours carry the overflow) and 0-599 in the two-part form so
 * a `90:00` tempo entry is accepted without forcing an hours segment.
 */
export const parseTimeInput = (raw: string): TimeComponents | null => {
  const trimmed = raw.trim()
  if (trimmed === '') {
    return null
  }

  const parts = trimmed.split(':')
  if (!parts.every(isNonNegativeIntString)) {
    return null
  }

  if (parts.length === 2) {
    const minutes = Number(parts[0])
    const seconds = Number(parts[1])
    if (minutes > 599 || seconds > 59) {
      return null
    }
    return { hours: 0, minutes, seconds }
  }

  if (parts.length === 3) {
    const hours = Number(parts[0])
    const minutes = Number(parts[1])
    const seconds = Number(parts[2])
    if (hours > 99 || minutes > 59 || seconds > 59) {
      return null
    }
    return { hours, minutes, seconds }
  }

  return null
}

/**
 * `true` when a runner-entered time is acceptable: blank (the field is optional,
 * meaning "no value") OR a well-formed `MM:SS` / `H:MM:SS`. Used by the Zod
 * schema so an unparseable time surfaces a field error instead of a wire 400.
 */
export const isValidTimeInput = (raw: string): boolean =>
  raw.trim() === '' || parseTimeInput(raw) !== null

/** Formats parsed components as an ISO-8601 `xsd:duration` (`PT{h}H{m}M{s}S`). */
export const formatIsoDuration = ({ hours, minutes, seconds }: TimeComponents): string =>
  `PT${hours}H${minutes}M${seconds}S`

/**
 * Maps a runner-entered clock time to the wire's ISO-8601 duration, or `null`
 * when the field is blank (no value). Assumes the value already passed
 * {@link isValidTimeInput}; an unexpectedly-unparseable non-blank value also
 * yields `null` so the mapper never emits a malformed duration.
 */
export const timeInputToIsoDuration = (raw: string): string | null => {
  if (raw.trim() === '') {
    return null
  }
  const components = parseTimeInput(raw)
  return components === null ? null : formatIsoDuration(components)
}

const ISO_DURATION_PATTERN = /^PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)(?:\.\d+)?S)?$/

/**
 * Parses an ISO-8601 `xsd:duration` the backend stored (`PT1H45M30S`) back into
 * clock components for resume-hydration, or `null` when it is absent/unparseable.
 * Fractional seconds are floored (the form only edits whole seconds).
 */
export const parseIsoDuration = (iso: string | null | undefined): TimeComponents | null => {
  if (iso === null || iso === undefined) {
    return null
  }
  const match = ISO_DURATION_PATTERN.exec(iso)
  if (
    match === null ||
    (match[1] === undefined && match[2] === undefined && match[3] === undefined)
  ) {
    return null
  }
  const totalSeconds =
    Number(match[1] ?? 0) * MINUTES_PER_HOUR * SECONDS_PER_MINUTE +
    Number(match[2] ?? 0) * SECONDS_PER_MINUTE +
    Number(match[3] ?? 0)
  return {
    hours: Math.floor(totalSeconds / (MINUTES_PER_HOUR * SECONDS_PER_MINUTE)),
    minutes: Math.floor(
      (totalSeconds % (MINUTES_PER_HOUR * SECONDS_PER_MINUTE)) / SECONDS_PER_MINUTE,
    ),
    seconds: totalSeconds % SECONDS_PER_MINUTE,
  }
}

const pad2 = (value: number): string => value.toString().padStart(2, '0')

/**
 * Formats an ISO-8601 duration as a `H:MM:SS` (or `MM:SS` when under an hour)
 * clock string for pre-filling the form on resume. Returns `''` for an
 * absent/unparseable value so the field hydrates empty.
 */
export const isoDurationToTimeInput = (iso: string | null | undefined): string => {
  const components = parseIsoDuration(iso)
  if (components === null) {
    return ''
  }
  const { hours, minutes, seconds } = components
  if (hours > 0) {
    return `${hours}:${pad2(minutes)}:${pad2(seconds)}`
  }
  return `${minutes}:${pad2(seconds)}`
}

/** One decimal place — the form's canonical distance-string precision. */
const DISTANCE_DECIMALS = 1

/**
 * Re-expresses a distance the runner typed in one unit as the equivalent string
 * in another unit, preserving the physical distance. Used when the runner
 * changes their units mid-form so a typed `10 km` becomes `6.2 mi` (not a silent
 * reinterpretation). A blank entry stays blank.
 */
export const convertDistanceInput = (
  raw: string,
  from: PreferredUnits,
  to: PreferredUnits,
): string => {
  const trimmed = raw.trim()
  if (trimmed === '' || from === to) {
    return trimmed
  }
  const parsed = Number(trimmed)
  if (!Number.isFinite(parsed)) {
    return trimmed
  }
  const kilometres = displayDistanceToKm(parsed, from)
  return kmToDisplayDistance(kilometres, to).toFixed(DISTANCE_DECIMALS)
}
