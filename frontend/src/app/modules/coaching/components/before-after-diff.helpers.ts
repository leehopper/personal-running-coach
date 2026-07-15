import { PreferredUnits } from '~/api/generated'
import { formatDistanceKm } from '~/modules/common/utils/unit-format.helpers'
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
 * A change-row's value-line copy, split so the `→` glyph can be rendered in
 * clay by the component (spec §3 PR-C). An `arrow` row has a real before/after
 * pair to join with a clay `→`; a `text` row (an added/removed workout) has no
 * before/after split — its whole copy renders as one plain run.
 */
export type ChangeDescriptionParts =
  { kind: 'arrow'; before: string; after: string } | { kind: 'text'; text: string }

/**
 * The before → after copy for one workout change, arrow-split. A null `before`
 * denotes an added workout, a null `after` a removed one; both-null is
 * meaningless and yields `null` so the caller skips the line. Defaults to
 * Kilometers so callers that predate the unit preference (and isolated tests)
 * render the km form unchanged.
 */
export const describeWorkoutChangeParts = (
  change: WorkoutChangeDto,
  units: PreferredUnits = PreferredUnits.Kilometers,
): ChangeDescriptionParts | null => {
  if (change.before !== null && change.after !== null) {
    return {
      kind: 'arrow',
      before: describeWorkout(change.before, units),
      after: describeWorkout(change.after, units),
    }
  }
  if (change.after !== null) {
    return { kind: 'text', text: `Added ${describeWorkout(change.after, units)}` }
  }
  if (change.before !== null) {
    return { kind: 'text', text: `Removed ${describeWorkout(change.before, units)}` }
  }
  return null
}

/** The before → after copy for one weekly volume-target change — always an `arrow` row. */
export const describeWeeklyTargetChangeParts = (
  change: WeeklyTargetChangeDto,
  units: PreferredUnits = PreferredUnits.Kilometers,
): ChangeDescriptionParts => ({
  kind: 'arrow',
  before: formatDistanceKm(change.beforeWeeklyTargetKm, units) ?? '—',
  after: formatDistanceKm(change.afterWeeklyTargetKm, units) ?? '—',
})
