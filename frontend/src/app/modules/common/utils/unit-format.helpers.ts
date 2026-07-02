// Shared, deterministic unit-format layer (slice 4C-units).
//
// A single preference-aware home for rendering canonical km/SI values in the
// runner's chosen display unit. Storage, the wire, and the plan-gen prompt stay
// km-native (DEC-010 / DEC-041 / DEC-086) — this module converts for *display*
// only, and the LLM performs zero unit conversion.
//
// The kilometre output is byte-identical to the pre-existing km-only helpers, so
// consumers' existing null/ceiling guards continue to hold when those helpers are
// consolidated behind this module: pace parity is with `formatPacePerKm` /
// `formatPaceRangePerKm`, and distance parity is `formatDistanceMeters` ↔ the
// existing metre-taking `formatDistanceKm`. NOTE the unit contracts differ by the
// same name: the existing `formatDistanceKm` takes METRES, whereas this module's
// `formatDistanceKm` takes KILOMETRES — metre-carrying call sites must use
// `formatDistanceMeters`, never this `formatDistanceKm`. The miles path is a
// net-new inverse conversion.
//
// Per the root/`frontend` `CLAUDE.md` trademark rule, the user-facing suffixes
// are the literal `/km`, `/mi`, `km`, `mi`; no zone-name coupling lives here.
//
// Race distances (`5K`/`10K`/half/marathon) and track intervals (`400m`) are
// literal proper nouns handled at their render sites — not this pure numeric
// layer.

import { PreferredUnits } from '~/api/generated'

/** Metres in one statute mile — the single source of truth for the conversion. */
export const METERS_PER_MILE = 1609.344

const METERS_PER_KM = 1000
const SECONDS_PER_MINUTE = 60

/** Kilometres → miles (and sec/km → sec/mi) multiplier: `1.609344`. */
const KM_PER_MILE = METERS_PER_MILE / METERS_PER_KM

/** Maximum pace the km formatter accepts, in seconds-per-kilometre (`99:59/km`). */
const MAX_PACE_SECONDS_PER_KM = 99 * SECONDS_PER_MINUTE + 59

/**
 * Miles pace ceiling: the mile-equivalent of the `99:59/km` guard, so the miles
 * path rejects the same integer-second paces the km path would (the backend emits
 * integer seconds; the two ceilings can diverge only on sub-second inputs).
 */
const MAX_PACE_SECONDS_PER_MILE = MAX_PACE_SECONDS_PER_KM * KM_PER_MILE

const isMiles = (units: PreferredUnits): boolean => units === PreferredUnits.Miles

/**
 * Formats a canonical distance in **kilometres** in the preferred unit as a
 * one-decimal `X.X km` / `X.X mi` string (miles via `÷ 1.609344`).
 *
 * Returns `null` for a non-finite or non-positive distance — mirroring the
 * skip-run contract of the existing `formatDistanceKm` (a skipped run persists
 * `0`, which callers render as a placeholder rather than a misleading
 * `"0.0 mi"`).
 */
export const formatDistanceKm = (kilometres: number, units: PreferredUnits): string | null => {
  if (!Number.isFinite(kilometres) || kilometres <= 0) {
    return null
  }
  if (isMiles(units)) {
    return `${(kilometres / KM_PER_MILE).toFixed(1)} mi`
  }
  return `${kilometres.toFixed(1)} km`
}

/**
 * Formats a canonical distance in **metres** in the preferred unit. Thin
 * adapter over {@link formatDistanceKm} for wire shapes that carry metres
 * (`WorkoutLogDto.distanceMeters`).
 */
export const formatDistanceMeters = (metres: number, units: PreferredUnits): string | null =>
  formatDistanceKm(metres / METERS_PER_KM, units)

/**
 * Shared pace renderer: rounds `seconds` to a whole second, rejects negative /
 * above-ceiling values (`null`), and formats the survivors as `MM:SS{suffix}`.
 */
const formatPaceSeconds = (seconds: number, ceiling: number, suffix: string): string | null => {
  const rounded = Math.round(seconds)
  if (rounded < 0 || rounded > ceiling) {
    return null
  }
  const minutes = Math.floor(rounded / SECONDS_PER_MINUTE)
  const remainder = rounded - minutes * SECONDS_PER_MINUTE
  const paddedMinutes = minutes.toString().padStart(2, '0')
  const paddedSeconds = remainder.toString().padStart(2, '0')
  return `${paddedMinutes}:${paddedSeconds}${suffix}`
}

/**
 * Formats a pace expressed in **seconds-per-kilometre** in the preferred unit.
 *
 * Kilometres → `MM:SS/km`, byte-identical to the existing `formatPacePerKm`.
 * Miles → net-new inverse conversion `sec/km × 1.609344 = sec/mi`, rounded to a
 * whole second and rendered `MM:SS/mi`.
 *
 * Returns `null` when the input is non-finite, negative, or above the ceiling —
 * `99:59/km` for kilometres and its mile-equivalent for miles — so consumers'
 * existing null-guards continue to hold.
 */
export const formatPaceSecPerKm = (secondsPerKm: number, units: PreferredUnits): string | null => {
  if (!Number.isFinite(secondsPerKm)) {
    return null
  }
  if (isMiles(units)) {
    return formatPaceSeconds(secondsPerKm * KM_PER_MILE, MAX_PACE_SECONDS_PER_MILE, '/mi')
  }
  return formatPaceSeconds(secondsPerKm, MAX_PACE_SECONDS_PER_KM, '/km')
}

/**
 * Formats a pace **range** (both bounds in seconds-per-kilometre) in the
 * preferred unit as a single `FAST-SLOW/unit` string — mirroring
 * `formatPaceRangePerKm`'s composition but preference-aware.
 *
 * The faster pace (fewer seconds in the *display* unit) renders first; the unit
 * suffix is shown once at the end. When both bounds round to the same
 * `MM:SS` in the display unit, returns the single formatted pace (no degenerate
 * `MM:SS-MM:SS`). When one bound is invalid, returns whichever side is valid;
 * when both are invalid, returns `null`.
 */
export const formatPaceRangeSecPerKm = (
  fastSecondsPerKm: number,
  slowSecondsPerKm: number,
  units: PreferredUnits,
): string | null => {
  const formattedFast = formatPaceSecPerKm(fastSecondsPerKm, units)
  const formattedSlow = formatPaceSecPerKm(slowSecondsPerKm, units)

  if (formattedFast === null && formattedSlow === null) {
    return null
  }
  if (formattedFast === null) {
    return formattedSlow
  }
  if (formattedSlow === null) {
    return formattedFast
  }

  // Faster pace (fewer seconds) renders first. Ordering by the canonical
  // seconds-per-km is identical to ordering by the display-unit value — the
  // km→unit factor is a single positive constant, so it preserves order — and
  // any two inputs that round to the same display MM:SS collapse via the
  // `first === second` branch below regardless of order.
  const [first, second] =
    fastSecondsPerKm <= slowSecondsPerKm
      ? [formattedFast, formattedSlow]
      : [formattedSlow, formattedFast]

  if (first === second) {
    return first
  }

  // Strip the trailing unit suffix from the first half so it is shown once.
  const suffix = isMiles(units) ? '/mi' : '/km'
  return `${first.slice(0, -suffix.length)}-${second}`
}
