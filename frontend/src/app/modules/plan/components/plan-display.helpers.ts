// Shared display constants for the plan rendering components
// (spec § Unit 4 R04.4–R04.7). Centralised here because every component in
// this directory leans on the same canonical phasing, workout, and pace-zone
// labels — and the trademark rule (§ root `CLAUDE.md`) requires those labels
// to use Daniels-Gilbert / pace-zone-index phrasing, never "VDOT".
//
// Keep this file string-literal-only. Component-level snapshot tests rely on
// these maps to assert trademark cleanliness; behaviour belongs in the
// `.component.tsx` files.

import type {
  DaySlotType,
  IntensityProfile,
  MesoDaySlotDto,
  MesoWeekTemplateDto,
  MicroWorkoutCardDto,
  PhaseType,
  PlanPhaseDto,
  WorkoutType,
} from '~/modules/plan/models/plan.model'

/**
 * Display labels for the macro periodisation phases.
 *
 * The structured-output schema emits `Base | Build | Peak | Taper | Recovery`
 * (`PhaseType` in `plan.model.ts`). Labels are also provided for `Race` and
 * `Maintenance` so components render gracefully if the backend enum widens
 * without a paired frontend change.
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
 * The seven keys on `MesoWeekTemplateDto` that hold day slots, in calendar
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

/** Long-form day-of-week labels indexed by 0 = Sunday … 6 = Saturday. */
export const DAY_OF_WEEK_LABELS: readonly string[] = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
]

/** Union of meso-week day-slot keys (sunday … saturday). */
export type DaySlotKey = (typeof DAY_SLOT_KEYS)[number]

/**
 * Guard predicate the `TodayCard` and `UpcomingList` components share to
 * locate the workout matching a particular `dayOfWeek` (0-6). Returns
 * `undefined` when no workout is scheduled for that day — typical for the
 * runner's rest day.
 */
export const findWorkoutForDay = (
  workouts: readonly MicroWorkoutCardDto[],
  dayOfWeekIndex: number,
): MicroWorkoutCardDto | undefined =>
  workouts.find((workout) => workout.dayOfWeek === dayOfWeekIndex)

/**
 * Returns the next scheduled workout strictly *after* `fromDayOfWeek`,
 * wrapping past Saturday into the start of the week. Used by `TodayCard`'s
 * rest-day variant to call out the upcoming session.
 */
export const findNextWorkoutAfter = (
  workouts: readonly MicroWorkoutCardDto[],
  fromDayOfWeek: number,
): MicroWorkoutCardDto | undefined => {
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
export const labelForPhase = (phase: PhaseType): string => PHASE_LABELS[phase] ?? phase

/** Absolute week boundaries for a single macro periodisation phase. */
export interface PhaseRange {
  phase: PlanPhaseDto
  startWeek: number
  endWeek: number
}

/**
 * Walks the phases in declaration order and assigns each one a 1-based
 * start/end week. The structured-output schema exposes only `weeks` per
 * phase; the strip needs absolute boundaries to label segments.
 */
export const computePhaseRanges = (phases: readonly PlanPhaseDto[]): PhaseRange[] => {
  let cursor = 1
  return phases.map((phase) => {
    const startWeek = cursor
    const endWeek = cursor + Math.max(phase.weeks - 1, 0)
    cursor = endWeek + 1
    return { phase, startWeek, endWeek }
  })
}

/**
 * Returns `true` when `currentWeek` falls within the inclusive
 * `[startWeek, endWeek]` span of `range`. Returns `false` when
 * `currentWeek` is `null` (no active week — e.g. plan preview).
 */
export const isCurrentRange = (range: PhaseRange, currentWeek: number | null): boolean => {
  if (currentWeek === null) {
    return false
  }
  return currentWeek >= range.startWeek && currentWeek <= range.endWeek
}

/**
 * Returns the day-of-week index (0 = Sunday … 6 = Saturday) for a given
 * `Date`, mirroring `Date.prototype.getDay()`. Extracted here so callers
 * in `TodayCard` and future components stay testable without reaching into
 * global `Date` state.
 */
export const dayOfWeekIndex = (date: Date): number => date.getDay()

/**
 * Looks up the `MesoDaySlotDto` for the given `dayIndex` (0 = Sunday …
 * 6 = Saturday) from a `MesoWeekTemplateDto`. Uses the ordered
 * `DAY_SLOT_KEYS` map so callers avoid a switch statement.
 */
export const getSlotForToday = (week: MesoWeekTemplateDto, dayIndex: number): MesoDaySlotDto =>
  week[DAY_SLOT_KEYS[dayIndex]]
