// Kilometre-fixed pace formatters for the still-km logging/history surfaces.
//
// The backend emits paces as integer **seconds-per-kilometer** on the
// structured-output records (`TargetPaceEasySecPerKm`, `TargetPaceFastSecPerKm`,
// `WorkoutSegmentOutput.TargetPaceSecPerKm`) and on logged splits.
//
// The single home for `MM:SS` pace formatting is the shared, preference-aware
// `unit-format.helpers` module. These two helpers stay as thin km-pinned
// adapters over it, pinning `PreferredUnits.Kilometers` to keep their output
// byte-identical while removing the duplicate rounding/ceiling/formatting logic.
//
// The user-facing strings produced here use the literal `/km` suffix and no
// zone-name coupling, per the trademark restriction on the VDOT mark (DEC-042).

import { PreferredUnits } from '~/api/generated'
import {
  formatPaceRangeSecPerKm,
  formatPaceSecPerKm,
} from '~/modules/common/utils/unit-format.helpers'

/**
 * Formats a pace expressed in **seconds per kilometer** as a `MM:SS/km`
 * string. Returns `null` when the input is invalid (NaN, infinite, negative,
 * or above the documented `99:59/km` ceiling); callers render a placeholder.
 */
export const formatPacePerKm = (secondsPerKm: number): string | null =>
  formatPaceSecPerKm(secondsPerKm, PreferredUnits.Kilometers)

/**
 * Formats a pace **range** (both bounds in seconds-per-kilometer) as a single
 * `FAST-SLOW/km` string, faster (smaller seconds) first. Collapses to a single
 * pace when both bounds round equal; returns whichever side is valid when one
 * is invalid; returns `null` when both are invalid.
 */
export const formatPaceRangePerKm = (
  fastSecondsPerKm: number,
  slowSecondsPerKm: number,
): string | null =>
  formatPaceRangeSecPerKm(fastSecondsPerKm, slowSecondsPerKm, PreferredUnits.Kilometers)
