import { DAY_OF_WEEK_LABELS } from '~/modules/plan/components/plan-display.helpers'
import type {
  WeeklyTargetChangeDto,
  WorkoutChangeDto,
} from '~/modules/coaching/models/conversation.model'
import type { MicroWorkoutCardDto } from '~/modules/plan/models/plan.model'

// Pure line-builders for the "Show what changed" expander. The diff renders
// from the structured `PlanAdaptationDiff` payload, never from parsed prose,
// so these helpers own the entire copy surface of the expander body.

/** `Threshold Intervals (10 km)` — one side of a workout change. */
const describeWorkout = (workout: MicroWorkoutCardDto): string =>
  `${workout.title} (${workout.targetDistanceKm} km)`

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
 * yields `null` so the caller skips the line.
 */
export const describeWorkoutChange = (change: WorkoutChangeDto): string | null => {
  if (change.before !== null && change.after !== null) {
    return `${describeWorkout(change.before)} → ${describeWorkout(change.after)}`
  }
  if (change.after !== null) {
    return `Added ${describeWorkout(change.after)}`
  }
  if (change.before !== null) {
    return `Removed ${describeWorkout(change.before)}`
  }
  return null
}

/** Where a weekly-target change applies: `Week 3 volume`. */
export const weeklyTargetChangeLocus = (change: WeeklyTargetChangeDto): string =>
  `Week ${change.weekNumber} volume`

/** The before → after copy for one weekly volume-target change. */
export const describeWeeklyTargetChange = (change: WeeklyTargetChangeDto): string =>
  `${change.beforeWeeklyTargetKm} km → ${change.afterWeeklyTargetKm} km`
