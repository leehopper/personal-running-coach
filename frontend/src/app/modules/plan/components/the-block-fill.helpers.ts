// THE BLOCK's fill-tier + goal-chip derivations (Slice 2 §2.6). Imports the
// shared phase-range/date primitives from `plan-display.helpers.ts` rather
// than re-deriving them — the single-date-pipeline / single-phase-math
// contract that module's own header establishes.

import type { PhaseRange } from './plan-display.helpers'
import { formatShortDateUtc, parseIsoDateUtc } from './plan-display.helpers'

/**
 * THE BLOCK's per-week fill tier — encodes proximity-to-current-week, not
 * phase identity (the separate phase-label row, driven by
 * `computePhaseRanges` directly, carries phase identity). `current` is the
 * single "you are here" week; `currentPhase` covers the remainder of the
 * active phase PLUS every earlier phase (Slice 2 spec §2.6, BD2 — the tier
 * system draws a defined-vs-unknown-training line, not a past-vs-present
 * one); `nextPhase` is the phase immediately following the current one;
 * `distant` is everything beyond that.
 */
export type BlockFillTier = 'current' | 'currentPhase' | 'nextPhase' | 'distant'

/**
 * Resolves one {@link BlockFillTier} per week, 1..totalWeeks (index i ↔
 * week i+1).
 *
 * `nextRange` walks forward from the current phase's array position,
 * skipping any zero-week (empty, `endWeek < startWeek`) entry —
 * `computePhaseRanges` deliberately keeps those in the array (see its own
 * doc comment) so a plain next-index lookup could land on one and starve
 * the true next phase of its tier. `currentRange` needs no equivalent
 * guard: a `currentWeek` can never fall inside an empty range by
 * definition.
 */
export function resolveBlockFillTiers(params: {
  ranges: PhaseRange[]
  currentWeek: number
  totalWeeks: number
}): BlockFillTier[] {
  const { ranges, currentWeek, totalWeeks } = params

  const currentRangeIndex = ranges.findIndex(
    (range) => currentWeek >= range.startWeek && currentWeek <= range.endWeek,
  )
  const currentRange = currentRangeIndex === -1 ? undefined : ranges[currentRangeIndex]
  const nextRange =
    currentRange === undefined
      ? undefined
      : ranges.slice(currentRangeIndex + 1).find((range) => range.endWeek >= range.startWeek)

  const tiers: BlockFillTier[] = []
  for (let week = 1; week <= totalWeeks; week++) {
    if (week === currentWeek) {
      tiers.push('current')
    } else if (
      currentRange !== undefined &&
      week >= currentRange.startWeek &&
      week <= currentRange.endWeek
    ) {
      tiers.push('currentPhase')
    } else if (currentRange !== undefined && week < currentRange.startWeek) {
      // Decided (BD2): weeks in phases before the current phase render
      // identically to the remaining weeks of the current phase.
      tiers.push('currentPhase')
    } else if (
      nextRange !== undefined &&
      week >= nextRange.startWeek &&
      week <= nextRange.endWeek
    ) {
      tiers.push('nextPhase')
    } else {
      tiers.push('distant')
    }
  }
  return tiers
}

/**
 * Maps a target-event distance to its literal race-distance proper noun —
 * trademark-safe (see `unit-format.helpers.ts`'s own comment: race
 * distances are proper nouns, not the enforced VDOT mark) — tolerant of a
 * small rounding band around each canonical distance.
 */
export function shortEventLabel(distanceKm: number): string {
  if (Math.abs(distanceKm - 5) <= 0.5) {
    return '5K'
  }
  if (Math.abs(distanceKm - 10) <= 0.5) {
    return '10K'
  }
  if (Math.abs(distanceKm - 21.1) <= 1) {
    return 'HALF'
  }
  if (Math.abs(distanceKm - 42.2) <= 1) {
    return 'MARATHON'
  }
  return `${Math.round(distanceKm)}K`
}

/**
 * Formats THE BLOCK's goal-chip text (`"10K — OCT 3"`), or `null` for a
 * general-fitness plan (either field absent) or a non-null-but-unparseable
 * `targetEventDate`. Null-propagates like every sibling date helper in
 * `plan-display.helpers.ts` (`resolveCalendarDateUtc`, `resolveDayCells`,
 * `weekLoggedKm`, `formatChangeLocusDate`) rather than trusting an upstream
 * null check alone.
 */
export function formatGoalChip(params: {
  targetEventDistanceKm: number | null
  targetEventDate: string | null
}): string | null {
  const { targetEventDistanceKm, targetEventDate } = params
  if (targetEventDistanceKm === null || targetEventDate === null) {
    return null
  }
  const eventDateUtc = parseIsoDateUtc(targetEventDate)
  if (eventDateUtc === null) {
    return null
  }
  return `${shortEventLabel(targetEventDistanceKm)} — ${formatShortDateUtc(new Date(eventDateUtc))}`
}
