import { PreferredUnits, type StructuredLogDraft } from '~/api/generated'
import { describe, expect, it } from 'vitest'

import { draftToWorkoutLogFormFields } from './draft-to-form.helpers'

// The confirmation card's "Edit" affordance pre-fills the log form from the
// parsed wire draft. The form's single `distance` field is expressed in the
// runner's preferred display unit (slice 4C-units), so the mapper collapses the
// draft's stated unit + h:m:s components INTO that unit and emits the all-strings
// `WorkoutLogFormFields` shape.

const KM = PreferredUnits.Kilometers
const MI = PreferredUnits.Miles

const baseDraft: StructuredLogDraft = {
  occurredOn: '2026-06-20',
  distanceValue: 5,
  distanceUnit: 0, // Kilometers
  durationHours: 0,
  durationMinutes: 25,
  durationSeconds: 0,
  completionStatus: 0, // Complete
  notes: 'legs felt heavy',
}

describe('draftToWorkoutLogFormFields', () => {
  it('passes a kilometers draft straight through under a Kilometers preference', () => {
    const fields = draftToWorkoutLogFormFields(baseDraft, KM)

    expect(fields.distance).toBe('5')
    expect(fields.durationMinutes).toBe('25')
    expect(fields.occurredOn).toBe('2026-06-20')
    expect(fields.completionStatus).toBe('0')
    expect(fields.notes).toBe('legs felt heavy')
  })

  it('converts a miles draft to kilometers under a Kilometers preference', () => {
    const fields = draftToWorkoutLogFormFields(
      { ...baseDraft, distanceValue: 3.1, distanceUnit: 1 },
      KM,
    )

    // 3.1 mi * 1609.344 m/mi / 1000 = 4.9889664 km
    expect(fields.distance).toBe(String(3.1 * 1.609344))
  })

  it('converts a meters draft to kilometers under a Kilometers preference', () => {
    const fields = draftToWorkoutLogFormFields(
      { ...baseDraft, distanceValue: 400, distanceUnit: 2 },
      KM,
    )

    expect(fields.distance).toBe('0.4')
  })

  it('keeps a miles draft in miles under a Miles preference (identity, no round-trip drift)', () => {
    // Draft already in the target unit: the value passes straight through. A
    // unit→metres→unit round trip would yield e.g. "99.99999999999999".
    const fields = draftToWorkoutLogFormFields(
      { ...baseDraft, distanceValue: 100, distanceUnit: 1 },
      MI,
    )

    expect(fields.distance).toBe('100')
  })

  it('converts a kilometers draft to miles under a Miles preference', () => {
    // 8.04672 km = exactly 5 mi.
    const fields = draftToWorkoutLogFormFields(
      { ...baseDraft, distanceValue: 8.04672, distanceUnit: 0 },
      MI,
    )

    expect(Number.parseFloat(fields.distance)).toBeCloseTo(5, 6)
  })

  it('folds hours and seconds into fractional total minutes', () => {
    const fields = draftToWorkoutLogFormFields(
      { ...baseDraft, durationHours: 1, durationMinutes: 22, durationSeconds: 30 },
      KM,
    )

    // 1*60 + 22 + 30/60 = 82.5
    expect(fields.durationMinutes).toBe('82.5')
  })

  it('maps a zero/absent distance or duration to blank, never "0"', () => {
    const fields = draftToWorkoutLogFormFields(
      {
        ...baseDraft,
        distanceValue: 0,
        durationHours: 0,
        durationMinutes: 0,
        durationSeconds: 0,
        completionStatus: 2, // Skipped
      },
      KM,
    )

    // The form's > 0 range check fires regardless of status, so a '0' would be invalid.
    expect(fields.distance).toBe('')
    expect(fields.durationMinutes).toBe('')
    expect(fields.completionStatus).toBe('2')
  })

  it('maps a null note to an empty string', () => {
    const fields = draftToWorkoutLogFormFields({ ...baseDraft, notes: null }, KM)

    expect(fields.notes).toBe('')
  })

  it('leaves the optional metric fields blank (the draft carries no metrics)', () => {
    const fields = draftToWorkoutLogFormFields(baseDraft, KM)

    expect(fields.rpe).toBe('')
    expect(fields.hrAvg).toBe('')
    expect(fields.hrMax).toBe('')
    expect(fields.elevationGain).toBe('')
  })
})
