import type { Control } from 'react-hook-form'
import { z } from 'zod'

import { PreferredUnits } from '~/api/generated'
import { distanceUnitLabel } from '~/modules/common/utils/unit-format.helpers'
import {
  PrimaryGoal,
  type OnboardingStateDto,
  type SubmitStructuredAnswersRequest,
} from '~/modules/onboarding/models/onboarding.model'
import {
  convertDistanceInput,
  displayDistanceToKm,
  isValidTimeInput,
  isoDurationToTimeInput,
  kmToDisplayDistance,
  timeInputToIsoDuration,
} from '~/modules/onboarding/schemas/onboarding-form.helpers'

// DEC-075 form conventions, applied to the six-topic onboarding intake
// (slice 4C-onboarding). Every field is held as a STRING (the natural `<input>`
// value), a `string[]` (the day ToggleGroup), or a `boolean` (a checkbox); the
// schema transforms each into the validated, typed output the submit handler
// maps down to the wire. `z.input` is the loose form shape, `z.output` the
// strict typed shape — the `useForm<Input, unknown, Output>` split DEC-075
// mandates. Blank optional numerics resolve to `undefined`, never `0`/`NaN`.
//
// Units are display-only: distances are entered in the runner's preferred unit
// and converted to canonical kilometres at the wire by
// `toSubmitStructuredAnswersRequest` — storage and the prompt stay km-native and
// the LLM performs zero conversion (DEC-086).

/** The seven day-slot keys in week order, paired with their user-facing labels. */
export const DAY_OPTIONS = [
  { value: 'monday', label: 'Mon' },
  { value: 'tuesday', label: 'Tue' },
  { value: 'wednesday', label: 'Wed' },
  { value: 'thursday', label: 'Thu' },
  { value: 'friday', label: 'Fri' },
  { value: 'saturday', label: 'Sat' },
  { value: 'sunday', label: 'Sun' },
] as const satisfies ReadonlyArray<{ value: keyof DaySlots; label: string }>

interface DaySlots {
  monday: boolean
  tuesday: boolean
  wednesday: boolean
  thursday: boolean
  friday: boolean
  saturday: boolean
  sunday: boolean
}

const DAY_KEYS = DAY_OPTIONS.map((option) => option.value)

/** Primary-goal options in display order, sourced from the const-paired enum. */
export const GOAL_OPTIONS = [
  { value: PrimaryGoal.RaceTraining, label: 'Train for a race' },
  { value: PrimaryGoal.GeneralFitness, label: 'General fitness' },
  { value: PrimaryGoal.ReturnToRunning, label: 'Return to running' },
  { value: PrimaryGoal.BuildVolume, label: 'Build volume' },
  { value: PrimaryGoal.BuildSpeed, label: 'Build speed' },
] as const satisfies ReadonlyArray<{ value: PrimaryGoal; label: string }>

const GOAL_VALUES = GOAL_OPTIONS.map((option) => option.value as number)

interface NumericFieldOptions {
  min: number
  max: number
  /** When true, `min` is a strict lower bound (value must be > min, not >= min). */
  minExclusive?: boolean
  /** When true, the value must be a whole number. */
  integer?: boolean
  label: string
  /**
   * Defer the min/max range check to a form-level refine. For a
   * conditionally-shown field (the TargetEvent block) so a value left behind
   * when the field is hidden can never hold a blocking range error. Parse,
   * finite, and integer checks still run in-field.
   */
  deferRange?: boolean
}

const lowerBoundPhrase = ({ min, minExclusive }: NumericFieldOptions): string => {
  if (!minExclusive) return `at least ${min}`
  return min === 0 ? 'greater than zero' : `greater than ${min}`
}

const rangeMessage = (options: NumericFieldOptions): string =>
  `Enter a ${options.label} ${lowerBoundPhrase(options)} and at most ${options.max}.`

/**
 * Range predicate shared by the in-field numeric check and the race-gated
 * TargetEvent refine. Returns the range message, or null when in range.
 */
const numericRangeError = (parsed: number, options: NumericFieldOptions): string | null => {
  const tooLow = options.minExclusive ? parsed <= options.min : parsed < options.min
  return tooLow || parsed > options.max ? rangeMessage(options) : null
}

/**
 * A string-backed numeric form field (DEC-075). Blank → `undefined` (optional at
 * this level; required-ness is enforced by the form-level refine so it can
 * depend on other fields). A non-blank value must parse to a finite number
 * within range (and be a whole number when `integer`).
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
      if (options.integer === true && !Number.isInteger(parsed)) {
        ctx.addIssue({ code: 'custom', message: `Enter a whole ${options.label}.` })
        return
      }
      if (options.deferRange !== true) {
        const rangeError = numericRangeError(parsed, options)
        if (rangeError !== null) {
          ctx.addIssue({ code: 'custom', message: rangeError })
        }
      }
    })
    .transform((raw) => {
      const value = raw.trim()
      return value === '' ? undefined : Number(value)
    })

/** Optional free-text nuance: trimmed, blank → `undefined`. */
const nuanceField = z.string().transform((raw) => {
  const value = raw.trim()
  return value.length === 0 ? undefined : value
})

/** Optional clock-time field (`MM:SS` / `H:MM:SS`); blank is valid (no value). */
const timeField = z
  .string()
  .superRefine((raw, ctx) => {
    if (!isValidTimeInput(raw)) {
      ctx.addIssue({ code: 'custom', message: 'Enter a time as MM:SS or H:MM:SS.' })
    }
  })
  .transform((raw) => {
    const value = raw.trim()
    return value === '' ? undefined : value
  })

/**
 * A finish-time field whose format check is deferred to a form-level gate (the
 * goal finish time only exists for a race goal, so a stale value must not block
 * submit once the TargetEvent section is hidden). Blank → `undefined`.
 */
const lenientTimeField = z.string().transform((raw) => {
  const value = raw.trim()
  return value === '' ? undefined : value
})

/** Zod ISO calendar-date validator (Zod v4) for the race-date format gate. */
const ISO_DATE = z.iso.date()

/** Goal select: required, must resolve to a defined `PrimaryGoal`. */
const goalField = z
  .string()
  .superRefine((raw, ctx) => {
    if (raw === '') {
      ctx.addIssue({ code: 'custom', message: 'Select your primary goal.' })
      return
    }
    if (!GOAL_VALUES.includes(Number(raw))) {
      ctx.addIssue({ code: 'custom', message: 'Select a valid goal.' })
    }
  })
  .transform((raw) => Number(raw) as PrimaryGoal)

/**
 * The km-native distance ceiling in the runner's active unit — a client-only
 * fat-finger guard mirroring the backend's `value <= 100 000` km cap. Rounded
 * DOWN when converted into the display unit so the client never *accepts* a
 * value whose km-equivalent the backend would reject (a mile-mode entry at the
 * rounded-up ceiling round-trips to just over 100 000 km and 400s server-side).
 */
const MAX_DISTANCE_KM = 100_000

/** The km-native distance ceiling expressed in the runner's display unit. */
const displayDistanceMax = (units: PreferredUnits): number =>
  Math.floor(kmToDisplayDistance(MAX_DISTANCE_KM, units))

/**
 * The numeric options for the event-distance field, shared so the in-field parse
 * and the race-gated range check produce identical messages. Its range is
 * deferred (the field is only shown for a race goal — see makeOnboardingFormSchema).
 */
const eventDistanceOptions = (units: PreferredUnits): NumericFieldOptions => ({
  min: 0,
  minExclusive: true,
  max: displayDistanceMax(units),
  label: `event distance in ${distanceUnitLabel(units)}`,
})

/**
 * Builds the onboarding form schema for a given display unit. `units`
 * parameterises only the distance fields' labels and `max`; the field *shapes*
 * are unit-independent, so the exported form types derive from one canonical
 * instance. The conditional TargetEvent block and the active-injury description
 * are enforced at the form level so they can depend on `goal` / `hasActiveInjury`.
 */
const makeOnboardingObjectSchema = (units: PreferredUnits) => {
  const unit = distanceUnitLabel(units)
  const distanceMax = displayDistanceMax(units)

  const distanceField = (label: string) =>
    numericField({ min: 0, minExclusive: true, max: distanceMax, label: `${label} in ${unit}` })

  return z.object({
    goal: goalField,
    goalDescription: nuanceField,

    eventName: z.string().transform((raw) => raw.trim()),
    // Range deferred to the race-gated refine so a value left behind when the
    // TargetEvent section is hidden (goal ≠ race) can never hold a blocking error.
    eventDistance: numericField({ ...eventDistanceOptions(units), deferRange: true }),
    eventDate: z.string().transform((raw) => raw.trim()),
    // Format deferred to the race-gated refine for the same reason (the finish
    // time only exists for a race goal).
    targetFinishTime: lenientTimeField,

    typicalWeekly: numericField({ min: 0, max: distanceMax, label: `weekly volume in ${unit}` }),
    longestRecentRun: numericField({ min: 0, max: distanceMax, label: `long run in ${unit}` }),
    recentRaceDistance: distanceField('recent race distance'),
    recentRaceTime: timeField,
    fitnessDescription: nuanceField,

    maxRunDays: numericField({ min: 1, max: 7, integer: true, label: 'number of run days' }),
    sessionMinutes: numericField({
      min: 1,
      max: 600,
      integer: true,
      label: 'session length in minutes',
    }),
    days: z.array(z.string()),
    scheduleDescription: nuanceField,

    hasActiveInjury: z.boolean(),
    activeInjuryDescription: z.string().transform((raw) => raw.trim()),
    pastInjurySummary: nuanceField,

    preferTrail: z.boolean(),
    comfortableWithIntensity: z.boolean(),
    preferencesDescription: nuanceField,
  })
}

/** A pending custom validation issue the form-level refine will raise. */
interface OnboardingRefineIssue {
  path: string[]
  message: string
}

/** CurrentFitness volume / long run / run days / session length are always required. */
const requiredFitnessIssues = (values: OnboardingFormValues): OnboardingRefineIssue[] => {
  const issues: OnboardingRefineIssue[] = []
  if (values.typicalWeekly === undefined) {
    issues.push({ path: ['typicalWeekly'], message: 'Enter your typical weekly volume.' })
  }
  if (values.longestRecentRun === undefined) {
    issues.push({ path: ['longestRecentRun'], message: 'Enter your longest recent run.' })
  }
  if (values.maxRunDays === undefined) {
    issues.push({ path: ['maxRunDays'], message: 'Enter how many days a week you can run.' })
  }
  if (values.sessionMinutes === undefined) {
    issues.push({ path: ['sessionMinutes'], message: 'Enter your typical session length.' })
  }
  return issues
}

/**
 * TargetEvent validation — required fields for a race goal, plus the range/format
 * checks deferred from the fields so a value left behind when the section is
 * hidden (goal ≠ race) can never hold a blocking error. All gated on the race goal.
 */
const targetEventIssues = (
  values: OnboardingFormValues,
  units: PreferredUnits,
): OnboardingRefineIssue[] => {
  if (values.goal !== PrimaryGoal.RaceTraining) return []
  const issues: OnboardingRefineIssue[] = []
  if (values.eventName === '') {
    issues.push({ path: ['eventName'], message: 'Name your goal race.' })
  }
  if (values.eventDistance === undefined) {
    issues.push({ path: ['eventDistance'], message: 'Enter the race distance.' })
  } else {
    const rangeError = numericRangeError(values.eventDistance, eventDistanceOptions(units))
    if (rangeError !== null) {
      issues.push({ path: ['eventDistance'], message: rangeError })
    }
  }
  if (values.eventDate === '') {
    issues.push({ path: ['eventDate'], message: 'Enter the race date.' })
  } else if (!ISO_DATE.safeParse(values.eventDate).success) {
    issues.push({ path: ['eventDate'], message: 'Enter the race date as a valid calendar date.' })
  }
  if (values.targetFinishTime !== undefined && !isValidTimeInput(values.targetFinishTime)) {
    issues.push({ path: ['targetFinishTime'], message: 'Enter a time as MM:SS or H:MM:SS.' })
  }
  return issues
}

/** An active injury needs a description so the plan can accommodate it. */
const activeInjuryIssues = (values: OnboardingFormValues): OnboardingRefineIssue[] =>
  values.hasActiveInjury && values.activeInjuryDescription === ''
    ? [
        {
          path: ['activeInjuryDescription'],
          message: 'Describe your current injury or limitation.',
        },
      ]
    : []

/**
 * The full form schema: the base object plus cross-field validation (required
 * numerics, the conditional TargetEvent block, the active-injury description).
 * `.superRefine` does not change the input/output types, so the exported field
 * types derive from the plain object schema above for clean inference.
 */
export const makeOnboardingFormSchema = (units: PreferredUnits) =>
  makeOnboardingObjectSchema(units).superRefine((values, ctx) => {
    const issues = [
      ...requiredFitnessIssues(values),
      ...targetEventIssues(values, units),
      ...activeInjuryIssues(values),
    ]
    for (const issue of issues) {
      ctx.addIssue({ code: 'custom', path: issue.path, message: issue.message })
    }
  })

/** The field shapes are unit-independent, so the form types derive from one instance. */
type OnboardingObjectSchema = ReturnType<typeof makeOnboardingObjectSchema>

/** Form state shape (raw `<input>` values). */
export type OnboardingFormFields = z.input<OnboardingObjectSchema>

/** Validated, typed output handed to the submit mapper. */
export type OnboardingFormValues = z.output<OnboardingObjectSchema>

/**
 * The form's `control` type, spelled out with all three RHF generics because
 * input (strings) ≠ output (coerced) — field components take this concrete alias.
 */
export type OnboardingFormControl = Control<OnboardingFormFields, unknown, OnboardingFormValues>

/** Names of the string-valued form fields (text/date/numeric/nuance inputs). */
export type OnboardingStringFieldName = {
  [K in keyof OnboardingFormFields]: OnboardingFormFields[K] extends string ? K : never
}[keyof OnboardingFormFields]

/** Names of the boolean-valued form fields (checkboxes). */
export type OnboardingBooleanFieldName = {
  [K in keyof OnboardingFormFields]: OnboardingFormFields[K] extends boolean ? K : never
}[keyof OnboardingFormFields]

/** Fresh form defaults: everything blank, no days selected, all toggles off. */
export const makeDefaultOnboardingFormFields = (): OnboardingFormFields => ({
  goal: '',
  goalDescription: '',
  eventName: '',
  eventDistance: '',
  eventDate: '',
  targetFinishTime: '',
  typicalWeekly: '',
  longestRecentRun: '',
  recentRaceDistance: '',
  recentRaceTime: '',
  fitnessDescription: '',
  maxRunDays: '',
  sessionMinutes: '',
  days: [],
  scheduleDescription: '',
  hasActiveInjury: false,
  activeInjuryDescription: '',
  pastInjurySummary: '',
  preferTrail: false,
  comfortableWithIntensity: false,
  preferencesDescription: '',
})

/**
 * Pre-fills the form from a resumed `GET /state` view (FR-2.7). Populated slots
 * hydrate their fields (distances converted from canonical km into `units`,
 * ISO durations into clock strings); absent slots stay blank.
 */
export const hydrateOnboardingFormFields = (
  state: OnboardingStateDto,
  units: PreferredUnits,
): OnboardingFormFields => {
  const distance = (km: number): string => kmToDisplayDistance(km, units).toFixed(1)
  const { primaryGoal, targetEvent, currentFitness, weeklySchedule, injuryHistory, preferences } =
    state

  return {
    goal: primaryGoal ? String(primaryGoal.goal) : '',
    goalDescription: primaryGoal?.description ?? '',
    eventName: targetEvent?.eventName ?? '',
    eventDistance: targetEvent ? distance(targetEvent.distanceKm) : '',
    eventDate: targetEvent?.eventDateIso ?? '',
    targetFinishTime: isoDurationToTimeInput(targetEvent?.targetFinishTimeIso),
    typicalWeekly: currentFitness ? distance(currentFitness.typicalWeeklyKm) : '',
    longestRecentRun: currentFitness ? distance(currentFitness.longestRecentRunKm) : '',
    recentRaceDistance:
      currentFitness?.recentRaceDistanceKm != null
        ? distance(currentFitness.recentRaceDistanceKm)
        : '',
    recentRaceTime: isoDurationToTimeInput(currentFitness?.recentRaceTimeIso),
    fitnessDescription: currentFitness?.description ?? '',
    maxRunDays: weeklySchedule ? String(weeklySchedule.maxRunDaysPerWeek) : '',
    sessionMinutes: weeklySchedule ? String(weeklySchedule.typicalSessionMinutes) : '',
    days: weeklySchedule ? DAY_KEYS.filter((key) => weeklySchedule[key]) : [],
    scheduleDescription: weeklySchedule?.description ?? '',
    hasActiveInjury: injuryHistory?.hasActiveInjury ?? false,
    activeInjuryDescription: injuryHistory?.activeInjuryDescription ?? '',
    pastInjurySummary: injuryHistory?.pastInjurySummary ?? '',
    preferTrail: preferences?.preferTrail ?? false,
    comfortableWithIntensity: preferences?.comfortableWithIntensity ?? false,
    preferencesDescription: preferences?.description ?? '',
  }
}

/** The four distance-valued form fields, re-seeded when the runner changes units. */
const DISTANCE_FIELD_NAMES = [
  'eventDistance',
  'typicalWeekly',
  'longestRecentRun',
  'recentRaceDistance',
] as const satisfies ReadonlyArray<keyof OnboardingFormFields>

/**
 * Re-expresses every entered distance field from one unit to another when the
 * runner changes units mid-form, preserving the physical distance (a typed
 * `10 km` becomes `6.2 mi`) so nothing is silently reinterpreted. All other
 * answers pass through untouched. Used by the page to compute the seed the form
 * remounts against on a unit change.
 */
export const reseedDistancesForUnitChange = (
  fields: OnboardingFormFields,
  from: PreferredUnits,
  to: PreferredUnits,
): OnboardingFormFields => {
  const next = { ...fields }
  for (const name of DISTANCE_FIELD_NAMES) {
    next[name] = convertDistanceInput(fields[name], from, to)
  }
  return next
}

const toDaySlots = (days: string[]): DaySlots => ({
  monday: days.includes('monday'),
  tuesday: days.includes('tuesday'),
  wednesday: days.includes('wednesday'),
  thursday: days.includes('thursday'),
  friday: days.includes('friday'),
  saturday: days.includes('saturday'),
  sunday: days.includes('sunday'),
})

/**
 * Maps validated form values down to the `POST /answers` wire request. Distances
 * (entered in the runner's unit) → canonical km; clock times → ISO-8601
 * durations; `preferredUnits` is the resolved display preference (populated but
 * non-authoritative — `UserSettings` is canonical, DP-4). TargetEvent is sent
 * only for a race-training goal so the backend's `TargetEvent ⇒ RaceTraining`
 * cross-field rule is satisfied and no stale race metadata is originated.
 *
 * The `?? 0` / `?? 1` fallbacks are unreachable in practice (the form is
 * submit-gated on validity, which requires these fields) — they only keep the
 * types total.
 */
export const toSubmitStructuredAnswersRequest = (
  values: OnboardingFormValues,
  idempotencyKey: string,
  units: PreferredUnits,
): SubmitStructuredAnswersRequest => {
  const isRace = values.goal === PrimaryGoal.RaceTraining

  return {
    idempotencyKey,
    primaryGoal: { goal: values.goal, description: values.goalDescription ?? '' },
    targetEvent: isRace
      ? {
          eventName: values.eventName,
          distanceKm: displayDistanceToKm(values.eventDistance ?? 0, units),
          eventDateIso: values.eventDate,
          targetFinishTimeIso: timeInputToIsoDuration(values.targetFinishTime ?? ''),
        }
      : null,
    currentFitness: {
      typicalWeeklyKm: displayDistanceToKm(values.typicalWeekly ?? 0, units),
      longestRecentRunKm: displayDistanceToKm(values.longestRecentRun ?? 0, units),
      recentRaceDistanceKm:
        values.recentRaceDistance !== undefined
          ? displayDistanceToKm(values.recentRaceDistance, units)
          : null,
      recentRaceTimeIso: timeInputToIsoDuration(values.recentRaceTime ?? ''),
      description: values.fitnessDescription ?? '',
    },
    weeklySchedule: {
      maxRunDaysPerWeek: values.maxRunDays ?? 1,
      typicalSessionMinutes: values.sessionMinutes ?? 1,
      ...toDaySlots(values.days),
      description: values.scheduleDescription ?? '',
    },
    injuryHistory: {
      hasActiveInjury: values.hasActiveInjury,
      activeInjuryDescription: values.hasActiveInjury ? values.activeInjuryDescription : '',
      pastInjurySummary: values.pastInjurySummary ?? '',
    },
    preferences: {
      preferredUnits: units,
      preferTrail: values.preferTrail,
      comfortableWithIntensity: values.comfortableWithIntensity,
      description: values.preferencesDescription ?? '',
    },
  }
}
