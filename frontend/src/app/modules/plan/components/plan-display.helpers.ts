// Shared display constants for the plan rendering components (Slice 1
// § Unit 4 R04.4–R04.7). Centralised here because every component in this
// directory leans on the same canonical phasing, workout, and pace-zone
// labels — and the trademark rule (§ root `CLAUDE.md`) requires those labels
// to use Daniels-Gilbert / pace-zone-index phrasing, never "VDOT".
//
// Keep this file string-literal-only. Component-level snapshot tests rely on
// these maps to assert trademark cleanliness; behaviour belongs in the
// `.component.tsx` files.

import type {
  DaySlotType,
  IntensityProfile,
  MicroWorkoutCard as MicroWorkoutDto,
  PhaseType,
  WorkoutType,
} from '~/modules/plan/models/plan.model'

/**
 * Display labels for the macro periodisation phases.
 *
 * The structured-output schema currently emits `Base | Build | Peak | Taper |
 * Recovery` (`PhaseType` in `plan.model.ts`). The Slice 1 spec also lists
 * `Race` and `Maintenance` as future segments — we surface labels for those
 * here too so the components render gracefully when later slices widen the
 * enum without a paired frontend change.
 */
export const PHASE_LABELS: Record<string, string> = {
  Base: 'Base',
  Build: 'Build',
  Peak: 'Peak',
  Taper: 'Taper',
  Race: 'Race',
  Recovery: 'Recovery',
  Maintenance: 'Maintenance',
}

/**
 * Display labels for the structured workout types. Uses canonical Daniels-
 * Gilbert phrasing (easy / threshold / interval / repetition) — never
 * "VDOT" — per the root `CLAUDE.md` trademark rule.
 */
export const WORKOUT_TYPE_LABELS: Record<WorkoutType, string> = {
  Easy: 'Easy run',
  LongRun: 'Long run',
  Tempo: 'Threshold run',
  Interval: 'Interval session',
  Repetition: 'Repetition session',
  Recovery: 'Recovery run',
  CrossTrain: 'Cross-training',
}

/**
 * Display labels for the intensity-profile enum that drives workout
 * segments. Carries the same trademark hygiene as `WORKOUT_TYPE_LABELS` —
 * the user-facing surface must render pace-zone-index phrasing.
 */
export const INTENSITY_LABELS: Record<IntensityProfile, string> = {
  Easy: 'Easy (pace-zone index)',
  Moderate: 'Moderate (pace-zone index)',
  Threshold: 'Threshold (pace-zone index)',
  VO2Max: 'Interval (pace-zone index)',
  Repetition: 'Repetition (pace-zone index)',
}

/** Display labels for day-slot kinds in the meso weekly templates. */
export const DAY_SLOT_LABELS: Record<DaySlotType, string> = {
  Run: 'Run',
  Rest: 'Rest day',
  CrossTrain: 'Cross-training',
}

/**
 * The seven keys on `MesoWeekTemplate` that hold day slots, in calendar
 * order (Sunday=0 … Saturday=6) so consumers can look a slot up by
 * `Date.getDay()` index without a switch statement.
 */
export const DAY_SLOT_KEYS = [
  'sunday',
  'monday',
  'tuesday',
  'wednesday',
  'thursday',
  'friday',
  'saturday',
] as const

/** Long-form day-of-week labels keyed by the Sunday=0 index. */
export const DAY_OF_WEEK_LABELS: readonly string[] = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
]

export type DaySlotKey = (typeof DAY_SLOT_KEYS)[number]

/**
 * Guard predicate the `TodayCard` and `UpcomingList` components share to
 * locate the workout matching a particular `dayOfWeek` (0-6). Returns
 * `undefined` when no workout is scheduled for that day — typical for the
 * runner's rest day.
 */
export const findWorkoutForDay = (
  workouts: readonly MicroWorkoutDto[],
  dayOfWeekIndex: number,
): MicroWorkoutDto | undefined => workouts.find((workout) => workout.dayOfWeek === dayOfWeekIndex)

/**
 * Returns the next scheduled workout strictly *after* `fromDayOfWeek`,
 * wrapping past Saturday into the start of the week. Used by `TodayCard`'s
 * rest-day variant to call out the upcoming session.
 */
export const findNextWorkoutAfter = (
  workouts: readonly MicroWorkoutDto[],
  fromDayOfWeek: number,
): MicroWorkoutDto | undefined => {
  if (workouts.length === 0) {
    return undefined
  }
  const sorted = [...workouts].sort((left, right) => left.dayOfWeek - right.dayOfWeek)
  const upcomingThisWeek = sorted.find((workout) => workout.dayOfWeek > fromDayOfWeek)
  return upcomingThisWeek ?? sorted[0]
}

/**
 * Resolves a friendly phase label, falling back to the raw value when the
 * structured-output enum widens before the frontend's label map does.
 */
export const labelForPhase = (phase: PhaseType | string): string => PHASE_LABELS[phase] ?? phase
