// Pace formatting helpers for the plan view.
//
// The backend emits paces as integer **seconds-per-kilometer** on the
// structured-output records (`TargetPaceEasySecPerKm`,
// `TargetPaceFastSecPerKm`, `WorkoutSegmentOutput.TargetPaceSecPerKm`). The
// home surface renders them as `MM:SS/km` strings.
//
// Slice 1 hard-defaults to kilometers because the existing `UserProfile`
// model is km-native (spec 13 § Unit 4 R04.6). When Slice 2 wires
// `Preferences.PreferredUnits`, this module's signatures will gain an
// optional `unit` argument — Slice 1 callers do not need to thread it.
//
// Per the trademark rule in the root `CLAUDE.md`, the user-facing strings
// produced here use the literal `/km` suffix; no zone-name coupling lives
// in this module.

/** Maximum pace value the formatter accepts, in seconds-per-kilometer. */
const MAX_PACE_SECONDS_PER_KM = 99 * 60 + 59 // 99:59/km
const SECONDS_PER_MINUTE = 60

/**
 * Formats a pace expressed in **seconds per kilometer** as a `MM:SS/km`
 * string suitable for direct rendering.
 *
 * Inputs are rounded to the nearest whole second before formatting — the
 * structured-output schema already emits integer seconds, but the helper is
 * defensive against fractional inputs that future logged-workout surfaces
 * may produce.
 *
 * Returns `null` when the input is invalid (NaN, infinite, negative, or
 * above the documented `99:59/km` ceiling). Callers render a placeholder
 * (`—/km` or similar) on `null` rather than crashing.
 */
export const formatPacePerKm = (secondsPerKm: number): string | null => {
  if (!Number.isFinite(secondsPerKm)) {
    return null
  }

  const rounded = Math.round(secondsPerKm)
  if (rounded < 0 || rounded > MAX_PACE_SECONDS_PER_KM) {
    return null
  }

  const minutes = Math.floor(rounded / SECONDS_PER_MINUTE)
  const seconds = rounded - minutes * SECONDS_PER_MINUTE
  const paddedMinutes = minutes.toString().padStart(2, '0')
  const paddedSeconds = seconds.toString().padStart(2, '0')
  return `${paddedMinutes}:${paddedSeconds}/km`
}

/**
 * Formats a pace **range** (typically from `targetPaceEasySecPerKm` and
 * `targetPaceFastSecPerKm` on `MicroWorkoutCard`) as a single
 * `FAST-SLOW/km` string. Faster (smaller seconds-per-km) is rendered first
 * to match the conventional display order.
 *
 * When the two values resolve to the same `MM:SS` after rounding, returns
 * the single-pace formatting (no degenerate `MM:SS-MM:SS/km`). When either
 * value is invalid, returns whichever side is valid; when both are invalid,
 * returns `null`.
 */
export const formatPaceRangePerKm = (
  fastSecondsPerKm: number,
  slowSecondsPerKm: number,
): string | null => {
  const formattedFast = formatPacePerKm(fastSecondsPerKm)
  const formattedSlow = formatPacePerKm(slowSecondsPerKm)

  if (formattedFast === null && formattedSlow === null) {
    return null
  }
  if (formattedFast === null) {
    return formattedSlow
  }
  if (formattedSlow === null) {
    return formattedFast
  }

  // Order so the **faster** pace (lower seconds) renders first.
  const fastRounded = Math.round(fastSecondsPerKm)
  const slowRounded = Math.round(slowSecondsPerKm)
  const [first, second] =
    fastRounded <= slowRounded ? [formattedFast, formattedSlow] : [formattedSlow, formattedFast]

  // Strip the trailing `/km` from the first half so the suffix is shown once.
  const firstWithoutSuffix = first.replace(/\/km$/u, '')
  if (first === second) {
    return first
  }
  return `${firstWithoutSuffix}-${second}`
}
