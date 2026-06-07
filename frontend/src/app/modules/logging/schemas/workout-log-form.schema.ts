import type { Control } from 'react-hook-form'
import { z } from 'zod'

import {
  CompletionStatus,
  completionStatusSchema,
  createWorkoutLogRequestSchema,
  type CreateWorkoutLogRequest,
} from '~/api/generated'
import { FORM_METRIC_KEYS } from '~/modules/logging/metric-meta'

// DEC-075 form conventions. The `/log` form holds every field as a STRING (the
// natural shape of `<input>` values), and the schema transforms each into the
// validated, typed output the submit handler consumes — so `z.input` is the
// loose string form and `z.output` is the strict typed form (the
// `useForm<Input, any, Output>` shape DEC-075 mandates). Blank optional numerics
// resolve to `undefined`, never `0`/`NaN`: the empty check runs BEFORE `Number`,
// so we never hit the `Number('') === 0` / `z.coerce.number()` footgun.

interface NumericFieldOptions {
  min: number
  max: number
  /** When true, `min` is a strict lower bound (value must be > min, not >= min). */
  minExclusive?: boolean
  label: string
}

const lowerBoundPhrase = ({ min, minExclusive }: NumericFieldOptions): string => {
  if (!minExclusive) return `at least ${min}`
  return min === 0 ? 'greater than zero' : `greater than ${min}`
}

const rangeMessage = (options: NumericFieldOptions): string =>
  `Enter a ${options.label} ${lowerBoundPhrase(options)} and at most ${options.max}.`

/**
 * A string-backed numeric form field. Blank → `undefined` (the field is optional
 * at this level; required-ness for distance/duration is enforced by the
 * form-level refine below so it can depend on completion status). A non-blank
 * value must parse to a finite number within the configured range.
 */
const numericField = (options: NumericFieldOptions) =>
  z
    .string()
    .superRefine((raw, ctx) => {
      const value = raw.trim()
      if (value === '') return
      const parsed = Number(value)
      if (!Number.isFinite(parsed)) {
        ctx.addIssue({ code: 'custom', message: `Enter a valid ${options.label}.` })
        return
      }
      const tooLow = options.minExclusive ? parsed <= options.min : parsed < options.min
      if (tooLow || parsed > options.max) {
        ctx.addIssue({ code: 'custom', message: rangeMessage(options) })
      }
    })
    .transform((raw) => {
      const value = raw.trim()
      return value === '' ? undefined : Number(value)
    })

// The form schema is DERIVED from the generated request body via `.pick().extend()`
// (DEC-075, the first such derivation in the repo). Picking `occurredOn` keeps
// its ISO-date format honest against the backend; `completionStatus` is rebuilt
// off the generated `0|1|2` union (via `completionStatusSchema`) so the enum
// can't drift. The remaining fields are UI-shaped (km / minutes rather than the
// wire's meters / seconds, plus the self-reportable optional metrics) and are
// mapped down to the wire contract by `toCreateWorkoutLogRequest`.
export const workoutLogFormSchema = createWorkoutLogRequestSchema
  .pick({ occurredOn: true })
  .extend({
    completionStatus: z
      .string()
      .transform((raw) => Number(raw))
      .pipe(completionStatusSchema),
    distanceKm: numericField({ min: 0, minExclusive: true, max: 1000, label: 'distance in km' }),
    durationMinutes: numericField({
      min: 0,
      minExclusive: true,
      max: 1440,
      label: 'duration in minutes',
    }),
    notes: z.string().transform((raw) => {
      const value = raw.trim()
      return value.length === 0 ? undefined : value
    }),
    rpe: numericField({ min: 1, max: 10, label: 'RPE' }),
    hrAvg: numericField({ min: 1, max: 300, label: 'average heart rate' }),
    hrMax: numericField({ min: 1, max: 300, label: 'maximum heart rate' }),
    elevationGain: numericField({ min: 0, max: 100000, label: 'elevation gain' }),
  })
  .superRefine((values, ctx) => {
    // Distance + duration are required for a Complete/Partial run but optional
    // for a Skipped one (it resolves to 0 m / 0 s on the wire). Spec § Unit 6
    // open-question resolution.
    if (values.completionStatus === CompletionStatus.Skipped) return
    if (values.distanceKm === undefined) {
      ctx.addIssue({ code: 'custom', path: ['distanceKm'], message: 'Enter a distance in km.' })
    }
    if (values.durationMinutes === undefined) {
      ctx.addIssue({
        code: 'custom',
        path: ['durationMinutes'],
        message: 'Enter a duration in minutes.',
      })
    }
  })

/** Form state shape (every field a string — the raw `<input>` value). */
export type WorkoutLogFormFields = z.input<typeof workoutLogFormSchema>

/** Validated, typed output handed to the submit handler. */
export type WorkoutLogFormValues = z.output<typeof workoutLogFormSchema>

/**
 * The form's `control` type. Spelled out with all three RHF generics because
 * input (strings) ≠ output (coerced) — field components take this concrete
 * alias rather than a `Control<TValues>` default that would collapse the
 * transformed generic back to the string fields.
 */
export type WorkoutLogFormControl = Control<WorkoutLogFormFields, unknown, WorkoutLogFormValues>

/** Local-calendar `YYYY-MM-DD` (never UTC — `toISOString` would shift the date). */
export const toIsoDateOnly = (date: Date): string => {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/** Fresh form defaults: today's date, Complete status, every other field blank. */
export const makeDefaultWorkoutLogFormFields = (
  today: Date = new Date(),
): WorkoutLogFormFields => ({
  occurredOn: toIsoDateOnly(today),
  completionStatus: String(CompletionStatus.Complete),
  distanceKm: '',
  durationMinutes: '',
  notes: '',
  rpe: '',
  hrAvg: '',
  hrMax: '',
  elevationGain: '',
})

/**
 * Maps validated form values down to the wire contract: km → meters, minutes →
 * seconds, present optional metrics into the open `metrics` bag (absent ones
 * omitted entirely — never sent as `0`). The `idempotencyKey` is supplied by the
 * caller so it stays stable across retries of the same logical submit (DEC-077).
 *
 * Distance/duration are pass-through, not status-gated: a Skipped workout that
 * still carries a typed distance/duration sends those values unchanged. The
 * fields stay rendered on every status (they are never reset or hidden when
 * Skipped is selected), so this is WYSIWYG — the visible value is what ships.
 * Only a blank field resolves to the `0` wire fallback.
 */
export const toCreateWorkoutLogRequest = (
  values: WorkoutLogFormValues,
  idempotencyKey: string,
): CreateWorkoutLogRequest => {
  const metrics: Record<string, number> = {}
  for (const key of FORM_METRIC_KEYS) {
    const value = values[key]
    if (value !== undefined) {
      metrics[key] = value
    }
  }

  return {
    idempotencyKey,
    occurredOn: values.occurredOn,
    distanceMeters: (values.distanceKm ?? 0) * 1000,
    durationSeconds: (values.durationMinutes ?? 0) * 60,
    completionStatus: values.completionStatus,
    ...(values.notes !== undefined ? { notes: values.notes } : {}),
    ...(Object.keys(metrics).length > 0 ? { metrics } : {}),
  }
}
