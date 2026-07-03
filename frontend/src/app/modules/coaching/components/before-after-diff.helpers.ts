import { PreferredUnits } from '~/api/generated'
import { formatDistanceKm } from '~/modules/common/utils/unit-format.helpers'
import { DAY_OF_WEEK_LABELS } from '~/modules/plan/components/plan-display.helpers'
import type {
  WeeklyTargetChangeDto,
  WorkoutChangeDto,
} from '~/modules/coaching/models/conversation.model'
import type { MicroWorkoutCardDto } from '~/modules/plan/models/plan.model'

// Pure line-builders for the "Show what changed" expander. The diff renders
// from the structured `PlanAdaptationDiff` payload, never from parsed prose,
// so these helpers own the entire copy surface of the expander body. Distance
// flows through the shared, preference-aware `unit-format.helpers` module so
// the expander agrees with the rest of the app on km/mi display (DEC-086);
// the underlying `PlanAdaptationDiff` payload stays km-native.

/** `Threshold Intervals (6.2 mi)` — one side of a workout change. */
const describeWorkout = (workout: MicroWorkoutCardDto, units: PreferredUnits): string =>
  `${workout.title} (${formatDistanceKm(workout.targetDistanceKm, units) ?? '—'})`

/**
 * Where a workout change applies: `Week 2 · Tuesday`. Falls back to the raw
 * index for an out-of-range `dayOfWeek` so a malformed payload still renders
 * a defined locus instead of throwing.
 */
export const workoutChangeLocus = (change: WorkoutChangeDto): string => {
  const day = DAY_OF_WEEK_LABELS[change.dayOfWeek] ?? `Day ${change.dayOfWeek}`
  return `Week ${change.weekNumber} · ${day}`
}

/**
 * The before → after copy for one workout change. A null `before` denotes an
 * added workout, a null `after` a removed one; both-null is meaningless and
 * yields `null` so the caller skips the line. Defaults to Kilometers so
 * callers that predate the unit preference (and isolated tests) render the
 * km form unchanged.
 */
export const describeWorkoutChange = (
  change: WorkoutChangeDto,
  units: PreferredUnits = PreferredUnits.Kilometers,
): string | null => {
  if (change.before !== null && change.after !== null) {
    return `${describeWorkout(change.before, units)} → ${describeWorkout(change.after, units)}`
  }
  if (change.after !== null) {
    return `Added ${describeWorkout(change.after, units)}`
  }
  if (change.before !== null) {
    return `Removed ${describeWorkout(change.before, units)}`
  }
  return null
}

/** Where a weekly-target change applies: `Week 3 volume`. */
export const weeklyTargetChangeLocus = (change: WeeklyTargetChangeDto): string =>
  `Week ${change.weekNumber} volume`

/**
 * The before → after copy for one weekly volume-target change. Defaults to
 * Kilometers so callers that predate the unit preference (and isolated
 * tests) render the km form unchanged.
 */
export const describeWeeklyTargetChange = (
  change: WeeklyTargetChangeDto,
  units: PreferredUnits = PreferredUnits.Kilometers,
): string =>
  `${formatDistanceKm(change.beforeWeeklyTargetKm, units) ?? '—'} → ${
    formatDistanceKm(change.afterWeeklyTargetKm, units) ?? '—'
  }`
