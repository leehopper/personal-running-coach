import { describe, expect, it } from 'vitest'

import {
  makeDefaultWorkoutLogFormFields,
  toCreateWorkoutLogRequest,
  toIsoDateOnly,
  workoutLogFormSchema,
  type WorkoutLogFormFields,
} from './workout-log-form.schema'

// Local-time Date so toIsoDateOnly is timezone-stable on CI.
const FIXED_DATE = new Date(2026, 5, 6)

const fields = (overrides: Partial<WorkoutLogFormFields> = {}): WorkoutLogFormFields => ({
  ...makeDefaultWorkoutLogFormFields(FIXED_DATE),
  ...overrides,
})

describe('toIsoDateOnly', () => {
  it('formats a date as local YYYY-MM-DD (not UTC)', () => {
    expect(toIsoDateOnly(new Date(2026, 0, 5))).toBe('2026-01-05')
  })
})

describe('makeDefaultWorkoutLogFormFields', () => {
  it('defaults occurredOn to the given date and completion to Complete, everything else blank', () => {
    expect(makeDefaultWorkoutLogFormFields(FIXED_DATE)).toEqual({
      occurredOn: '2026-06-06',
      completionStatus: '0',
      distanceKm: '',
      durationMinutes: '',
      notes: '',
      rpe: '',
      hrAvg: '',
      hrMax: '',
      elevationGain: '',
    })
  })
})

describe('workoutLogFormSchema', () => {
  it('accepts a minimum payload (distance + duration only) with optional metrics undefined', () => {
    const result = workoutLogFormSchema.safeParse(
      fields({ distanceKm: '5', durationMinutes: '30' }),
    )

    expect(result.success).toBe(true)
    if (!result.success) return
    expect(result.data).toMatchObject({
      occurredOn: '2026-06-06',
      completionStatus: 0,
      distanceKm: 5,
      durationMinutes: 30,
    })
    expect(result.data.rpe).toBeUndefined()
    expect(result.data.hrAvg).toBeUndefined()
    expect(result.data.notes).toBeUndefined()
  })

  it('resolves a blank optional numeric to undefined, never 0 or NaN', () => {
    const parsed = workoutLogFormSchema.parse(
      fields({ distanceKm: '5', durationMinutes: '30', rpe: '   ' }),
    )
    expect(parsed.rpe).toBeUndefined()
  })

  it('keeps an explicit 0 (elevation gain) as 0 rather than dropping it', () => {
    const parsed = workoutLogFormSchema.parse(
      fields({ distanceKm: '5', durationMinutes: '30', elevationGain: '0' }),
    )
    expect(parsed.elevationGain).toBe(0)
  })

  it('coerces the completion status string to its numeric enum value', () => {
    const parsed = workoutLogFormSchema.parse(
      fields({ completionStatus: '1', distanceKm: '5', durationMinutes: '30' }),
    )
    expect(parsed.completionStatus).toBe(1)
  })

  it('requires distance when the workout is not Skipped', () => {
    const result = workoutLogFormSchema.safeParse(fields({ distanceKm: '', durationMinutes: '30' }))

    expect(result.success).toBe(false)
    if (result.success) return
    expect(result.error.issues.some((issue) => issue.path[0] === 'distanceKm')).toBe(true)
  })

  it('rejects a zero distance for a completed run (must be greater than zero)', () => {
    const result = workoutLogFormSchema.safeParse(
      fields({ distanceKm: '0', durationMinutes: '30' }),
    )
    expect(result.success).toBe(false)
  })

  it('rejects a non-numeric distance', () => {
    const result = workoutLogFormSchema.safeParse(
      fields({ distanceKm: 'abc', durationMinutes: '30' }),
    )
    expect(result.success).toBe(false)
  })

  it('rejects an RPE outside 1–10', () => {
    const result = workoutLogFormSchema.safeParse(
      fields({ distanceKm: '5', durationMinutes: '30', rpe: '11' }),
    )
    expect(result.success).toBe(false)
  })

  it('allows a Skipped workout to omit distance and duration', () => {
    const result = workoutLogFormSchema.safeParse(
      fields({ completionStatus: '2', distanceKm: '', durationMinutes: '' }),
    )

    expect(result.success).toBe(true)
    if (!result.success) return
    expect(result.data.completionStatus).toBe(2)
    expect(result.data.distanceKm).toBeUndefined()
    expect(result.data.durationMinutes).toBeUndefined()
  })
})

describe('toCreateWorkoutLogRequest', () => {
  it('maps km→meters, minutes→seconds, trims notes, and builds the metrics bag from present values', () => {
    const parsed = workoutLogFormSchema.parse(
      fields({
        distanceKm: '5',
        durationMinutes: '30',
        notes: '  felt strong  ',
        rpe: '6',
        hrAvg: '142',
      }),
    )

    expect(toCreateWorkoutLogRequest(parsed, 'idem-1')).toEqual({
      idempotencyKey: 'idem-1',
      occurredOn: '2026-06-06',
      distanceMeters: 5000,
      durationSeconds: 1800,
      completionStatus: 0,
      notes: 'felt strong',
      metrics: { rpe: 6, hrAvg: 142 },
    })
  })

  it('omits notes and metrics entirely when none are provided', () => {
    const parsed = workoutLogFormSchema.parse(fields({ distanceKm: '5', durationMinutes: '30' }))
    const body = toCreateWorkoutLogRequest(parsed, 'idem-1')

    expect(body.notes).toBeUndefined()
    expect(body.metrics).toBeUndefined()
    expect(Object.keys(body)).toEqual([
      'idempotencyKey',
      'occurredOn',
      'distanceMeters',
      'durationSeconds',
      'completionStatus',
    ])
  })

  it('sends zero distance/duration for a Skipped workout with blank fields', () => {
    const parsed = workoutLogFormSchema.parse(
      fields({ completionStatus: '2', distanceKm: '', durationMinutes: '' }),
    )
    const body = toCreateWorkoutLogRequest(parsed, 'idem-1')

    expect(body.distanceMeters).toBe(0)
    expect(body.durationSeconds).toBe(0)
    expect(body.completionStatus).toBe(2)
  })
})
