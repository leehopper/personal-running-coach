import type { StructuredLogDraft } from '~/api/generated'
import { describe, expect, it } from 'vitest'

import { draftToWorkoutLogFormFields } from './draft-to-form.helpers'

// The confirmation card's "Edit" affordance pre-fills the Slice 2b log form
// from the parsed wire draft. The form is km-only with a single total-minutes
// field, so the mapper must collapse the draft's stated unit + h:m:s components
// and emit the all-strings `WorkoutLogFormFields` shape.

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
  it('passes a kilometers draft straight through to distanceKm', () => {
    const fields = draftToWorkoutLogFormFields(baseDraft)

    expect(fields.distanceKm).toBe('5')
    expect(fields.durationMinutes).toBe('25')
    expect(fields.occurredOn).toBe('2026-06-20')
    expect(fields.completionStatus).toBe('0')
    expect(fields.notes).toBe('legs felt heavy')
  })

  it('converts a miles draft to kilometers', () => {
    const fields = draftToWorkoutLogFormFields({
      ...baseDraft,
      distanceValue: 3.1,
      distanceUnit: 1,
    })

    // 3.1 mi * 1609.344 m/mi / 1000 = 4.9889664 km
    expect(fields.distanceKm).toBe(String(3.1 * 1.609344))
  })

  it('converts a meters draft to kilometers', () => {
    const fields = draftToWorkoutLogFormFields({
      ...baseDraft,
      distanceValue: 400,
      distanceUnit: 2,
    })

    expect(fields.distanceKm).toBe('0.4')
  })

  it('folds hours and seconds into fractional total minutes', () => {
    const fields = draftToWorkoutLogFormFields({
      ...baseDraft,
      durationHours: 1,
      durationMinutes: 22,
      durationSeconds: 30,
    })

    // 1*60 + 22 + 30/60 = 82.5
    expect(fields.durationMinutes).toBe('82.5')
  })

  it('maps a zero/absent distance or duration to blank, never "0"', () => {
    const fields = draftToWorkoutLogFormFields({
      ...baseDraft,
      distanceValue: 0,
      durationHours: 0,
      durationMinutes: 0,
      durationSeconds: 0,
      completionStatus: 2, // Skipped
    })

    // The form's > 0 range check fires regardless of status, so a '0' would be invalid.
    expect(fields.distanceKm).toBe('')
    expect(fields.durationMinutes).toBe('')
    expect(fields.completionStatus).toBe('2')
  })

  it('maps a null note to an empty string', () => {
    const fields = draftToWorkoutLogFormFields({ ...baseDraft, notes: null })

    expect(fields.notes).toBe('')
  })

  it('leaves the optional metric fields blank (the draft carries no metrics)', () => {
    const fields = draftToWorkoutLogFormFields(baseDraft)

    expect(fields.rpe).toBe('')
    expect(fields.hrAvg).toBe('')
    expect(fields.hrMax).toBe('')
    expect(fields.elevationGain).toBe('')
  })
})
