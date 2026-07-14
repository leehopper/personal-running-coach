// Shared display constants for the plan rendering components
// (spec § Unit 4 R04.4–R04.7). Centralised here because every component in
// this directory leans on the same canonical phasing, workout, and pace-zone
// labels — and the trademark rule (§ root `CLAUDE.md`) requires those labels
// to use Daniels-Gilbert / pace-zone-index phrasing, never "VDOT".
//
// Keep this file string-literal-only. Component-level snapshot tests rely on
// these maps to assert trademark cleanliness; behaviour belongs in the
// `.component.tsx` files.

import { PreferredUnits } from '~/api/generated'
import { formatDistanceKm } from '~/modules/common/utils/unit-format.helpers'
import type {
  DaySlotType,
  IntensityProfile,
  MesoDaySlotDto,
  MesoWeekTemplateDto,
  MicroWorkoutCardDto,
  PhaseType,
  PlanPhaseDto,
  SegmentType,
  WorkoutSegmentDto,
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
 * Guard predicate `home.page.tsx` uses to locate the workout matching a
 * particular `dayOfWeek` (0-6) when constructing `WorkoutHero`'s
 * `WorkoutHeroContent` (§2.1). Returns `undefined` when no workout is
 * scheduled for that day — typical for the runner's rest day.
 */
export const findWorkoutForDay = (
  workouts: readonly MicroWorkoutCardDto[],
  dayOfWeekIndex: number,
): MicroWorkoutCardDto | undefined =>
  workouts.find((workout) => workout.dayOfWeek === dayOfWeekIndex)

/**
 * Returns the next scheduled workout strictly *after* `fromDayOfWeek`,
 * wrapping past Saturday into the start of the week. Used by `home.page.tsx`
 * to source `WorkoutHero`'s rest-day variant's next-workout line (§1 PR-B
 * branch 3).
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
 *
 * Mirrors the server's `WeekContext.FromMacro` cumulative-sum walk exactly:
 * a zero-week phase produces an EMPTY span (`startWeek > endWeek`, cursor not
 * advanced) rather than a spurious 1-week span that would shift every later
 * phase. The phase stays IN the returned array (not filtered) so
 * `ranges[ranges.length - 1]` still matches `WeekContext.cs`'s
 * `macro.Phases[^1]` — the literal last array element — even when that
 * element is zero-week.
 * Consumers that render one row per phase (THE BLOCK's phase-label row, the
 * header's `phaseForWeek` lookup below) must filter `endWeek >= startWeek`
 * before rendering; this function itself does not filter.
 */
export const computePhaseRanges = (phases: readonly PlanPhaseDto[]): PhaseRange[] => {
  let cursor = 1
  return phases.map((phase) => {
    if (phase.weeks <= 0) {
      return { phase, startWeek: cursor, endWeek: cursor - 1 }
    }
    const startWeek = cursor
    const endWeek = cursor + phase.weeks - 1
    cursor = endWeek + 1
    return { phase, startWeek, endWeek }
  })
}

/**
 * Resolves the macro-phase active during `weekNumber`, falling back to the
 * last phase in `ranges` when no span matches (mirrors `WeekContext.cs`'s own
 * defensive fallback). Correctly agrees with that fallback even when the last
 * phase is zero-week: {@link computePhaseRanges} keeps a zero-week phase's
 * entry in `ranges` (merely unmatchable by span), so `ranges[ranges.length -
 * 1]` still resolves to the true last phase. Uses index arithmetic, not
 * `Array.prototype.at`, to stay within the project's ES2020 TypeScript
 * target (`tsconfig.app.json`) — do not reintroduce `.at()` here.
 */
export const phaseForWeek = (
  ranges: readonly PhaseRange[],
  weekNumber: number,
): PlanPhaseDto | undefined => {
  const match = ranges.find((range) => weekNumber >= range.startWeek && weekNumber <= range.endWeek)
  return match?.phase ?? ranges[ranges.length - 1]?.phase
}

/**
 * Returns the day-of-week index (0 = Sunday … 6 = Saturday) for a given
 * `Date`, mirroring `Date.prototype.getDay()`. Extracted here so date-driven
 * plan-rendering logic stays testable without reaching into global `Date`
 * state directly.
 */
export const dayOfWeekIndex = (date: Date): number => date.getDay()

/**
 * Looks up the `MesoDaySlotDto` for the given `dayIndex` (0 = Sunday …
 * 6 = Saturday) from a `MesoWeekTemplateDto`. Uses the ordered
 * `DAY_SLOT_KEYS` map so callers avoid a switch statement.
 */
export const getSlotForToday = (week: MesoWeekTemplateDto, dayIndex: number): MesoDaySlotDto =>
  week[DAY_SLOT_KEYS[dayIndex]]

// ─────────────────────────────────────────────────────────────────────────
// Single date pipeline (Slice 2 §2.1/§2.2/§2.4) — the shared UTC-midnight-
// epoch primitives every date-driven derivation on the Today screen (and
// `use-plan.hooks.ts`'s `resolveCurrentWeek`) imports rather than
// re-deriving. See the Today spec §2 for the full pipeline contract: a wall-
// clock `Date` is normalized to a UTC-midnight epoch exactly once
// (`toUtcMidnight`), and every downstream read of that epoch uses `getUTC*`
// getters only — never a local getter on the same instant.
// ─────────────────────────────────────────────────────────────────────────

/** Milliseconds in one calendar day — the unit every epoch-based derivation below walks in. */
export const DAY_MS = 86_400_000

/**
 * Normalizes a wall-clock `Date` to a UTC-midnight epoch (milliseconds)
 * using its LOCAL calendar fields (`getFullYear`/`getMonth`/`getDate`). This
 * is the one place a raw `Date`'s local calendar day crosses into UTC epoch
 * arithmetic — every other date-math helper in this module takes the
 * resulting epoch, never a second raw `Date`.
 */
export const toUtcMidnight = (date: Date): number =>
  Date.UTC(date.getFullYear(), date.getMonth(), date.getDate())

/**
 * Parses an ISO `YYYY-MM-DD` date string into a UTC-midnight epoch, or
 * `null` when the string is absent, malformed, or not a valid calendar date.
 * The shared parser for every `planStartDate` / `WorkoutLogDto.occurredOn` /
 * `targetEventDate` consumer — callers propagate the `null` sentinel to
 * their own null-returning contract rather than feeding it into a formatter
 * that expects a `Date`.
 */
export function parseIsoDateUtc(dateString: string): number | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateString)
  if (match === null) return null
  const year = Number(match[1])
  const monthIndex = Number(match[2]) - 1
  const day = Number(match[3])
  const epochMs = Date.UTC(year, monthIndex, day)
  // Round-trip check: `Date.UTC` silently rolls an out-of-range calendar
  // date forward (e.g. Feb 30 -> Mar 2) instead of rejecting it. Re-reading
  // the constructed date's UTC Y/M/D and comparing against the parsed
  // components is what actually enforces "not a valid calendar date" from
  // this function's own doc comment above — the regex alone only checks the
  // string's SHAPE, not whether it names a real day.
  const roundTrip = new Date(epochMs)
  if (
    roundTrip.getUTCFullYear() !== year ||
    roundTrip.getUTCMonth() !== monthIndex ||
    roundTrip.getUTCDate() !== day
  ) {
    return null
  }
  return epochMs
}

/**
 * Resolves the calendar date (as a UTC-midnight `Date`) for a given
 * `(weekNumber, dayOfWeek)` pair against a plan's `planStartDate` anchor —
 * the exact inverse of the server's `PlanCalendar.ResolveSlot`:
 * `dayOffset = (weekNumber - 1) * 7 + dayOfWeek`. Returns `null` when
 * `planStartDate` is unparseable. `planStartDate` is a server-enforced
 * Sunday anchor; this function trusts that invariant rather than
 * re-validating it.
 */
export function resolveCalendarDateUtc(
  planStartDate: string,
  weekNumber: number,
  dayOfWeek: number,
): Date | null {
  const planStartUtc = parseIsoDateUtc(planStartDate)
  if (planStartUtc === null) return null
  const epochMs = planStartUtc + ((weekNumber - 1) * 7 + dayOfWeek) * DAY_MS
  return new Date(epochMs)
}

const MONTH_ABBREVIATIONS = [
  'JAN',
  'FEB',
  'MAR',
  'APR',
  'MAY',
  'JUN',
  'JUL',
  'AUG',
  'SEP',
  'OCT',
  'NOV',
  'DEC',
]

/**
 * Formats a UTC-midnight-normalized `Date` as `"{MONTH_ABBR} {DAY}"` (e.g.
 * `"JUL 8"`), no leading zero on the day, no year. `date` MUST already be
 * UTC-midnight-normalized (from {@link resolveCalendarDateUtc},
 * {@link parseIsoDateUtc} → `new Date(epoch)`, or {@link toUtcMidnight} →
 * `new Date(epoch)`) — never a raw wall-clock `Date`, since this reads
 * `getUTC*` getters exclusively.
 */
export function formatShortDateUtc(date: Date): string {
  return `${MONTH_ABBREVIATIONS[date.getUTCMonth()]} ${date.getUTCDate()}`
}

/**
 * Composes the hero eyebrow's `"{Weekday}, {MONTH_ABBR} {DAY}"` fragment
 * (e.g. `"Wednesday, JUL 8"`, rendered `WEDNESDAY, JUL 8` via the eyebrow's
 * own `uppercase` CSS) from a single pre-normalized `todayUtc` epoch. Both
 * the weekday and the month/day fragment derive from the same UTC-midnight
 * `Date`, so they can never disagree — the bug this replaces mixed a LOCAL
 * `getDay()` read with a UTC-getter-based month/day read on the same raw
 * `Date`, which disagrees for part of every day in a non-UTC timezone.
 */
export function formatHeroEyebrowDate(todayUtc: number): string {
  const d = new Date(todayUtc)
  const weekday = DAY_OF_WEEK_LABELS[d.getUTCDay()]
  return `${weekday}, ${formatShortDateUtc(d)}`
}

// ─────────────────────────────────────────────────────────────────────────
// Hero summary composition (Slice 2 §2.7) — one-sentence workout summary
// composed client-side from a workout's segments + coaching note, and the
// per-`SegmentType` phrase table it reads.
// ─────────────────────────────────────────────────────────────────────────

/** Short lowercase intensity fragments for `describeSegment`'s `Work` phrase — distinct from the longer, "(pace-zone index)"-suffixed `INTENSITY_LABELS` used by the detailed segment row. */
export const SEGMENT_INTENSITY_PHRASES: Record<IntensityProfile, string> = {
  Easy: 'easy',
  Moderate: 'moderate',
  Threshold: 'threshold',
  VO2Max: 'interval effort',
  Repetition: 'repetition effort',
}

/**
 * Short phrase fragments per `SegmentType`, used by {@link describeSegment}.
 * `Work`'s entry (`'at'`) is a connector only — its qualifier is the dynamic
 * {@link SEGMENT_INTENSITY_PHRASES} lookup — kept here so the map stays
 * total over `SegmentType`.
 */
export const SEGMENT_TYPE_LABELS: Record<SegmentType, string> = {
  Warmup: 'easy',
  Recovery: 'easy recovery',
  Cooldown: 'down',
  Work: 'at',
}

/**
 * Composes a short phrase describing one workout segment, e.g. `"12' easy"`,
 * `"5 × 4' at threshold"`, `"3' easy recovery"`. Reps-aware for both `Work`
 * and `Recovery` — the wire contract's "only interval/repetition-style
 * segments ever carry repetitions>1" comment is stated about `Work`
 * specifically, not proven to exclude `Recovery`, so both branches guard
 * defensively rather than assuming.
 */
export function describeSegment(segment: WorkoutSegmentDto): string {
  const duration = segment.durationMinutes
  const reps = segment.repetitions
  switch (segment.segmentType) {
    case 'Warmup':
      return `${duration}' ${SEGMENT_TYPE_LABELS.Warmup}`
    case 'Cooldown':
      return `${duration}' ${SEGMENT_TYPE_LABELS.Cooldown}`
    case 'Recovery':
      return reps > 1
        ? `${reps} × ${duration}' ${SEGMENT_TYPE_LABELS.Recovery}`
        : `${duration}' ${SEGMENT_TYPE_LABELS.Recovery}`
    case 'Work':
      return reps > 1
        ? `${reps} × ${duration}' ${SEGMENT_TYPE_LABELS.Work} ${SEGMENT_INTENSITY_PHRASES[segment.intensity]}`
        : `${duration}' ${SEGMENT_TYPE_LABELS.Work} ${SEGMENT_INTENSITY_PHRASES[segment.intensity]}`
  }
}

/**
 * Composes the hero's one-sentence workout summary from `workout.segments`
 * (joined `", then "`) plus `workout.coachingNotes`. When there are no
 * segment phrases (a continuous-effort workout — `segments: []`), the
 * coaching note IS the whole summary rather than a trailing clause tacked
 * onto an empty base (the fix for a leading bare `". "` an earlier draft
 * produced).
 */
export function composeWorkoutSummary(workout: MicroWorkoutCardDto): string {
  const phrases = workout.segments.map(describeSegment)
  const base = phrases.join(', then ')
  const hasCoachingNotes = workout.coachingNotes.length > 0

  if (base === '' && hasCoachingNotes) {
    return workout.coachingNotes
  }
  if (base === '' && !hasCoachingNotes) {
    // Defensive fallback — not reachable under the current fixture set
    // (every continuous-effort fixture carries coachingNotes).
    return `${workout.targetDurationMinutes}' continuous effort.`
  }
  return hasCoachingNotes ? `${base}. ${workout.coachingNotes}` : `${base}.`
}

// ─────────────────────────────────────────────────────────────────────────
// Hero stat band (Slice 2 §2.8) — the 3rd stat cell's reps-or-duration
// derivation, plus cell 1's distance-formatting adapter.
// ─────────────────────────────────────────────────────────────────────────

/** The hero stat band's 3rd cell — either a reps count or a plain duration. */
export interface HeroThirdStat {
  value: string
  label: string
}

/**
 * Derives the hero stat band's 3rd cell: a rep count (`×N`, label
 * `Reps · M min`, rendered `REPS · M MIN` via `StatCell`'s `hero` label's own
 * `uppercase` CSS — source copy stays sentence case, never baked caps) when
 * the workout has an interval/repetition-style segment, else the workout's
 * continuous duration (label `Minutes`). Falls back to a placeholder dash —
 * not reachable under the current DTO, guarded anyway to match the app's
 * established null-value convention.
 */
export function resolveHeroThirdStat(workout: MicroWorkoutCardDto): HeroThirdStat {
  const workSegment = workout.segments.find((segment) => segment.repetitions > 1)
  if (workSegment !== undefined) {
    return {
      value: `×${workSegment.repetitions}`,
      label: `Reps · ${workSegment.durationMinutes} min`,
    }
  }
  if (Number.isFinite(workout.targetDurationMinutes) && workout.targetDurationMinutes > 0) {
    return { value: `${workout.targetDurationMinutes}`, label: 'Minutes' }
  }
  return { value: '—', label: 'Minutes' }
}

/**
 * Derives the hero stat band's 1st cell (distance) as a bare formatted
 * number — the `KILOMETERS`/`MILES` label already conveys the unit, so this
 * strips {@link formatDistanceKm}'s `" km"`/`" mi"` suffix rather than
 * re-implementing the km/mi conversion.
 */
export function resolveHeroDistanceStat(targetDistanceKm: number, units: PreferredUnits): string {
  const formatted = formatDistanceKm(targetDistanceKm, units)
  return formatted === null ? '—' : formatted.split(' ')[0]
}
