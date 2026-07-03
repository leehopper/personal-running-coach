import type { Control } from 'react-hook-form'
import { z } from 'zod'

import {
  CompletionStatus,
  completionStatusSchema,
  createWorkoutLogRequestSchema,
  type CreateWorkoutLogRequest,
  PreferredUnits,
} from '~/api/generated'
import {
  distanceUnitLabel,
  metresToPreferredDistance,
  preferredDistanceToMeters,
} from '~/modules/common/utils/unit-format.helpers'
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

/**
 * The km-native distance ceiling, in canonical metres (1000 km). The `distance`
 * field's `max` is this ceiling expressed in the runner's active display unit —
 * a client-only fat-finger sanity guard (the backend enforces no upper bound).
 * Rounded UP (`Math.ceil`) so the mile-mode cap (622 mi ≈ 1001 km) never rejects
 * an entry the km-native ceiling would accept; the guard is only ever looser, not
 * stricter, than the canonical limit.
 */
const MAX_DISTANCE_METERS = 1_000_000

/**
 * Builds the workout-log form schema for a given display unit (slice 4C-units).
 * The schema is DERIVED from the generated request body via `.pick().extend()`
 * (DEC-075, the first such derivation in the repo). Picking `occurredOn` keeps
 * its ISO-date format honest against the backend; `completionStatus` is rebuilt
 * off the generated `0|1|2` union (via `completionStatusSchema`) so the enum
 * can't drift. The remaining fields are UI-shaped (the entered `distance` is in
 * the runner's preferred unit — km OR miles — and duration is minutes rather than
 * the wire's meters / seconds) and are mapped down to the wire contract by
 * `toCreateWorkoutLogRequest`, which converts `distance` → canonical metres for
 * the active unit. `units` parameterises only the distance field's label and
 * `max`; the field *shape* is unit-independent, so the exported form types are
 * derived from one canonical instance.
 */
export const makeWorkoutLogFormSchema = (units: PreferredUnits) => {
  const distanceLabel = `distance in ${distanceUnitLabel(units)}`
  const distanceMax = Math.ceil(metresToPreferredDistance(MAX_DISTANCE_METERS, units))

  return createWorkoutLogRequestSchema
    .pick({ occurredOn: true })
    .extend({
      completionStatus: z
        .string()
        .transform((raw) => Number(raw))
        .pipe(completionStatusSchema),
      distance: numericField({
        min: 0,
        minExclusive: true,
        max: distanceMax,
        label: distanceLabel,
      }),
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
      if (values.distance === undefined) {
        ctx.addIssue({ code: 'custom', path: ['distance'], message: `Enter a ${distanceLabel}.` })
      }
      if (values.durationMinutes === undefined) {
        ctx.addIssue({
          code: 'custom',
          path: ['durationMinutes'],
          message: 'Enter a duration in minutes.',
        })
      }
    })
}

/** The distance-field shape is unit-independent, so the form types derive from one instance. */
type WorkoutLogFormSchema = ReturnType<typeof makeWorkoutLogFormSchema>

/** Form state shape (every field a string — the raw `<input>` value). */
export type WorkoutLogFormFields = z.input<WorkoutLogFormSchema>

/** Validated, typed output handed to the submit handler. */
export type WorkoutLogFormValues = z.output<WorkoutLogFormSchema>

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
  distance: '',
  durationMinutes: '',
  notes: '',
  rpe: '',
  hrAvg: '',
  hrMax: '',
  elevationGain: '',
})

/**
 * Maps validated form values down to the wire contract: `distance` (in the
 * runner's preferred unit) → canonical metres, minutes → seconds, present
 * optional metrics into the open `metrics` bag (absent ones omitted entirely —
 * never sent as `0`). The `idempotencyKey` is supplied by the caller so it stays
 * stable across retries of the same logical submit (DEC-077).
 *
 * `units` is REQUIRED (not defaulted): it is the interpretation of the runner's
 * typed `distance`, not incidental metadata — under Miles the entered value is
 * miles and is converted `× 1609.344` to metres, under Kilometers `× 1000`
 * (byte-identical to the prior km-only mapping). Storage and the wire stay
 * canonical km/SI; the LLM performs zero conversion (DEC-086).
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
  units: PreferredUnits,
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
    distanceMeters: preferredDistanceToMeters(values.distance ?? 0, units),
    durationSeconds: (values.durationMinutes ?? 0) * 60,
    completionStatus: values.completionStatus,
    ...(values.notes !== undefined ? { notes: values.notes } : {}),
    ...(Object.keys(metrics).length > 0 ? { metrics } : {}),
  }
}
