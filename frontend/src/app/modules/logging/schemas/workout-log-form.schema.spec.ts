import { describe, expect, it } from 'vitest'

import { PreferredUnits } from '~/api/generated'
import { METERS_PER_MILE } from '~/modules/common/utils/unit-format.helpers'

import {
  makeDefaultWorkoutLogFormFields,
  makeWorkoutLogFormSchema,
  toCreateWorkoutLogRequest,
  toIsoDateOnly,
  type WorkoutLogFormFields,
} from './workout-log-form.schema'

const KM = PreferredUnits.Kilometers
const MI = PreferredUnits.Miles

// The field shape is unit-independent, so the km-pinned instance drives the
// shape/validation suites; a dedicated block below exercises the Miles instance.
const kmSchema = makeWorkoutLogFormSchema(KM)

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
      distance: '',
      durationMinutes: '',
      notes: '',
      rpe: '',
      hrAvg: '',
      hrMax: '',
      elevationGain: '',
    })
  })
})

describe('makeWorkoutLogFormSchema', () => {
  it('accepts a minimum payload (distance + duration only) with optional metrics undefined', () => {
    const result = kmSchema.safeParse(fields({ distance: '5', durationMinutes: '30' }))

    expect(result.success).toBe(true)
    if (!result.success) return
    expect(result.data).toMatchObject({
      occurredOn: '2026-06-06',
      completionStatus: 0,
      distance: 5,
      durationMinutes: 30,
    })
    expect(result.data.rpe).toBeUndefined()
    expect(result.data.hrAvg).toBeUndefined()
    expect(result.data.notes).toBeUndefined()
  })

  it('resolves a blank optional numeric to undefined, never 0 or NaN', () => {
    const parsed = kmSchema.parse(fields({ distance: '5', durationMinutes: '30', rpe: '   ' }))
    expect(parsed.rpe).toBeUndefined()
  })

  it('keeps an explicit 0 (elevation gain) as 0 rather than dropping it', () => {
    const parsed = kmSchema.parse(
      fields({ distance: '5', durationMinutes: '30', elevationGain: '0' }),
    )
    expect(parsed.elevationGain).toBe(0)
  })

  it('coerces the completion status string to its numeric enum value', () => {
    const parsed = kmSchema.parse(
      fields({ completionStatus: '1', distance: '5', durationMinutes: '30' }),
    )
    expect(parsed.completionStatus).toBe(1)
  })

  it('requires distance when the workout is not Skipped', () => {
    const result = kmSchema.safeParse(fields({ distance: '', durationMinutes: '30' }))

    expect(result.success).toBe(false)
    if (result.success) return
    expect(result.error.issues.some((issue) => issue.path[0] === 'distance')).toBe(true)
  })

  it('rejects a zero distance for a completed run (must be greater than zero)', () => {
    const result = kmSchema.safeParse(fields({ distance: '0', durationMinutes: '30' }))
    expect(result.success).toBe(false)
  })

  it('rejects a non-numeric distance', () => {
    const result = kmSchema.safeParse(fields({ distance: 'abc', durationMinutes: '30' }))
    expect(result.success).toBe(false)
  })

  it('rejects an RPE outside 1–10', () => {
    const result = kmSchema.safeParse(fields({ distance: '5', durationMinutes: '30', rpe: '11' }))
    expect(result.success).toBe(false)
  })

  it('allows a Skipped workout to omit distance and duration', () => {
    const result = kmSchema.safeParse(
      fields({ completionStatus: '2', distance: '', durationMinutes: '' }),
    )

    expect(result.success).toBe(true)
    if (!result.success) return
    expect(result.data.completionStatus).toBe(2)
    expect(result.data.distance).toBeUndefined()
    expect(result.data.durationMinutes).toBeUndefined()
  })
})

describe('makeWorkoutLogFormSchema — Miles instance', () => {
  const milesSchema = makeWorkoutLogFormSchema(MI)

  it('phrases the required-distance message in miles', () => {
    const result = milesSchema.safeParse(fields({ distance: '', durationMinutes: '30' }))

    expect(result.success).toBe(false)
    if (result.success) return
    const distanceIssue = result.error.issues.find((issue) => issue.path[0] === 'distance')
    expect(distanceIssue?.message).toBe('Enter a distance in mi.')
  })

  it('caps the miles distance at the ceil-rounded mile-equivalent of the 1000 km guard (622 mi)', () => {
    // Math.ceil(1_000_000 / 1609.344) = 622, so the mile guard is only ever looser
    // than the km-native ceiling, never stricter: 622 mi accepted, 623 mi rejected.
    expect(milesSchema.safeParse(fields({ distance: '622', durationMinutes: '30' })).success).toBe(
      true,
    )
    expect(milesSchema.safeParse(fields({ distance: '623', durationMinutes: '30' })).success).toBe(
      false,
    )
  })

  it('still accepts a normal miles distance the km cap would also allow', () => {
    expect(milesSchema.safeParse(fields({ distance: '5', durationMinutes: '30' })).success).toBe(
      true,
    )
  })
})

describe('toCreateWorkoutLogRequest', () => {
  it('maps km→meters, minutes→seconds, trims notes, and builds the metrics bag from present values', () => {
    const parsed = kmSchema.parse(
      fields({
        distance: '5',
        durationMinutes: '30',
        notes: '  felt strong  ',
        rpe: '6',
        hrAvg: '142',
      }),
    )

    expect(toCreateWorkoutLogRequest(parsed, 'idem-1', KM)).toEqual({
      idempotencyKey: 'idem-1',
      occurredOn: '2026-06-06',
      distanceMeters: 5000,
      durationSeconds: 1800,
      completionStatus: 0,
      notes: 'felt strong',
      metrics: { rpe: 6, hrAvg: 142 },
    })
  })

  it('interprets the entered distance as miles and converts to km/SI metres under a Miles preference', () => {
    // The T06.2 proof: a miles-entered distance round-trips to the correct
    // canonical `distanceMeters` (5 mi * 1609.344 = 8046.72 m). The wire stays
    // km/SI; only the interpretation of the runner's input differs.
    const milesSchema = makeWorkoutLogFormSchema(MI)
    const parsed = milesSchema.parse(fields({ distance: '5', durationMinutes: '30' }))

    const body = toCreateWorkoutLogRequest(parsed, 'idem-1', MI)
    expect(body.distanceMeters).toBe(5 * METERS_PER_MILE)
    // Duration is unit-agnostic; only distance is reinterpreted.
    expect(body.durationSeconds).toBe(1800)
  })

  it('omits notes and metrics entirely when none are provided', () => {
    const parsed = kmSchema.parse(fields({ distance: '5', durationMinutes: '30' }))
    const body = toCreateWorkoutLogRequest(parsed, 'idem-1', KM)

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
    const parsed = kmSchema.parse(
      fields({ completionStatus: '2', distance: '', durationMinutes: '' }),
    )
    const body = toCreateWorkoutLogRequest(parsed, 'idem-1', KM)

    expect(body.distanceMeters).toBe(0)
    expect(body.durationSeconds).toBe(0)
    expect(body.completionStatus).toBe(2)
  })

  it('passes typed distance/duration through for a Skipped workout (WYSIWYG, not zeroed)', () => {
    // The distance/duration inputs stay rendered when Skipped is selected, so a
    // typed value remains visible to the user; the mapper ships what they see
    // rather than silently zeroing it. Pins the deliberate pass-through semantics.
    const parsed = kmSchema.parse(
      fields({ completionStatus: '2', distance: '5', durationMinutes: '30' }),
    )
    const body = toCreateWorkoutLogRequest(parsed, 'idem-1', KM)

    expect(body.distanceMeters).toBe(5000)
    expect(body.durationSeconds).toBe(1800)
    expect(body.completionStatus).toBe(2)
  })
})
