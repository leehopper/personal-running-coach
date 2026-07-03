// Inverse mapper for the conversational-logging "Edit" affordance: a parsed
// `StructuredLogDraft` (the card's wire shape, in the runner's stated units with
// h:m:s components) → the all-strings `WorkoutLogFormFields` the log form
// consumes. It mirrors the backend `WorkoutDraftUnitConverter` in reverse:
// distance is collapsed to the form's single `distance` field — expressed in the
// runner's preferred display unit (km OR miles), matching how the form now reads
// its input (slice 4C-units) — and duration to a single total-minutes field. A
// zero/non-positive distance or duration maps to blank (not "0"), because the
// form's `> 0` range check runs regardless of completion status — a Skipped draft
// can legitimately carry zeros.

import { PreferredUnits, type RunnerDistanceUnit, type StructuredLogDraft } from '~/api/generated'

import {
  METERS_PER_MILE,
  metresToPreferredDistance,
} from '~/modules/common/utils/unit-format.helpers'
import {
  makeDefaultWorkoutLogFormFields,
  type WorkoutLogFormFields,
} from '~/modules/logging/schemas/workout-log-form.schema'

const RUNNER_DISTANCE_UNIT = {
  kilometers: 0,
  miles: 1,
  meters: 2,
} as const satisfies Record<string, RunnerDistanceUnit>

const METERS_PER_KILOMETER = 1000

const draftDistanceMeters = (value: number, unit: RunnerDistanceUnit): number => {
  switch (unit) {
    case RUNNER_DISTANCE_UNIT.miles:
      return value * METERS_PER_MILE
    case RUNNER_DISTANCE_UNIT.meters:
      return value
    default:
      return value * METERS_PER_KILOMETER
  }
}

/** True when the draft's stated unit already IS the form's display unit. */
const draftUnitIsPreferred = (unit: RunnerDistanceUnit, units: PreferredUnits): boolean =>
  (unit === RUNNER_DISTANCE_UNIT.miles && units === PreferredUnits.Miles) ||
  (unit === RUNNER_DISTANCE_UNIT.kilometers && units === PreferredUnits.Kilometers)

/**
 * The draft's distance expressed in the form's preferred display unit. When the
 * draft is already stated in that unit the raw value is passed through untouched
 * — a `unit → metres → unit` round trip would otherwise accrue avoidable
 * floating-point drift (e.g. a 100 mi draft becoming `99.99999999999999`).
 */
const draftDistanceInPreferredUnit = (
  value: number,
  unit: RunnerDistanceUnit,
  units: PreferredUnits,
): number =>
  draftUnitIsPreferred(unit, units)
    ? value
    : metresToPreferredDistance(draftDistanceMeters(value, unit), units)

/** Blank for a non-positive value so the form's `> 0` range check stays happy. */
const positiveString = (value: number): string => (value > 0 ? String(value) : '')

/**
 * Maps a parsed workout-log {@link StructuredLogDraft} to the log form's input
 * fields, ready to seed `defaultValues`. `units` is the form's active display
 * unit, so a miles-preferring runner's draft pre-fills in miles (and is re-read
 * as miles on submit) rather than silently becoming kilometres. The optional
 * metric fields (`rpe`/`hrAvg`/`hrMax`/`elevationGain`) stay blank — the draft
 * carries no metrics.
 */
export const draftToWorkoutLogFormFields = (
  draft: StructuredLogDraft,
  units: PreferredUnits,
): WorkoutLogFormFields => {
  const distance = draftDistanceInPreferredUnit(draft.distanceValue, draft.distanceUnit, units)
  const totalMinutes = draft.durationHours * 60 + draft.durationMinutes + draft.durationSeconds / 60

  return {
    ...makeDefaultWorkoutLogFormFields(),
    occurredOn: draft.occurredOn,
    completionStatus: String(draft.completionStatus),
    distance: positiveString(distance),
    durationMinutes: positiveString(totalMinutes),
    notes: draft.notes ?? '',
  }
}
