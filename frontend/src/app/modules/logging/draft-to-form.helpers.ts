// Inverse mapper for the conversational-logging "Edit" affordance: a parsed
// `StructuredLogDraft` (the card's wire shape, in the runner's stated units with
// h:m:s components) → the all-strings `WorkoutLogFormFields` the Slice 2b log
// form consumes. It mirrors the backend `WorkoutDraftUnitConverter` in reverse:
// distance is collapsed to the form's km-only field and duration to a single
// total-minutes field. A zero/non-positive distance or duration maps to blank
// (not "0"), because the form's `> 0` range check runs regardless of completion
// status — a Skipped draft can legitimately carry zeros.

import type { RunnerDistanceUnit, StructuredLogDraft } from '~/api/generated'

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
const METERS_PER_MILE = 1609.344

const draftDistanceKm = (value: number, unit: RunnerDistanceUnit): number => {
  switch (unit) {
    case RUNNER_DISTANCE_UNIT.miles:
      return (value * METERS_PER_MILE) / METERS_PER_KILOMETER
    case RUNNER_DISTANCE_UNIT.meters:
      return value / METERS_PER_KILOMETER
    default:
      return value
  }
}

/** Blank for a non-positive value so the form's `> 0` range check stays happy. */
const positiveString = (value: number): string => (value > 0 ? String(value) : '')

/**
 * Maps a parsed workout-log {@link StructuredLogDraft} to the Slice 2b form's
 * input fields, ready to seed `defaultValues`. The optional metric fields
 * (`rpe`/`hrAvg`/`hrMax`/`elevationGain`) stay blank — the draft carries no
 * metrics.
 */
export const draftToWorkoutLogFormFields = (draft: StructuredLogDraft): WorkoutLogFormFields => {
  const distanceKm = draftDistanceKm(draft.distanceValue, draft.distanceUnit)
  const totalMinutes = draft.durationHours * 60 + draft.durationMinutes + draft.durationSeconds / 60

  return {
    ...makeDefaultWorkoutLogFormFields(),
    occurredOn: draft.occurredOn,
    completionStatus: String(draft.completionStatus),
    distanceKm: positiveString(distanceKm),
    durationMinutes: positiveString(totalMinutes),
    notes: draft.notes ?? '',
  }
}
