// Plan wire-format types — paired 1:1 with the backend records under
// `backend/src/RunCoach.Api/Modules/Training/Plan/Models/PlanProjectionDto.cs`
// and `backend/src/RunCoach.Api/Modules/Coaching/Models/Structured/`. The
// projection is materialized inline by `PlanProjection` and rendered directly
// by the frontend via `GET /api/v1/plan/current` — no further server-side
// shaping (spec 13 § Unit 4 R04.1, R04.6).
//
// ASP.NET MVC serializes properties as camelCase. The structured-output enums
// (`PhaseType`, `WorkoutType`, `DaySlotType`, `IntensityProfile`,
// `SegmentType`) carry per-type `JsonStringEnumConverter` attributes on the
// backend, so they cross the wire as **strings**, not integers — these are
// declared as union literal types here. `WorkoutOutput.DayOfWeek` is a raw
// `int` on the backend (0 = Sunday … 6 = Saturday) and stays numeric here.
//
// Renaming or reordering members on either side of the wire requires a paired
// change in this file plus the backend record. Per the trademark rule in the
// root `CLAUDE.md`, every user-facing string this module renders must use
// "Daniels-Gilbert zones" / "pace-zone index" — never "VDOT".

/**
 * Training periodization phase types. Mirrors
 * `RunCoach.Api.Modules.Coaching.Models.Structured.PhaseType` —
 * serialized as a string via `JsonStringEnumConverter<PhaseType>`.
 */
export type PhaseType = 'Base' | 'Build' | 'Peak' | 'Taper' | 'Recovery'

/**
 * The kind of activity assigned to a day slot within a weekly template.
 * Mirrors `RunCoach.Api.Modules.Coaching.Models.Structured.DaySlotType`.
 */
export type DaySlotType = 'Run' | 'Rest' | 'CrossTrain'

/**
 * Workout types that can appear in a training plan. Mirrors
 * `RunCoach.Api.Modules.Coaching.Models.Structured.WorkoutType`.
 */
export type WorkoutType =
  | 'Easy'
  | 'LongRun'
  | 'Tempo'
  | 'Interval'
  | 'Repetition'
  | 'Recovery'
  | 'CrossTrain'

/**
 * Workout-segment kinds. Mirrors
 * `RunCoach.Api.Modules.Coaching.Models.Structured.SegmentType`.
 */
export type SegmentType = 'Warmup' | 'Work' | 'Recovery' | 'Cooldown'

/**
 * Intensity profile aligned with pace-zone-index terminology. Mirrors
 * `RunCoach.Api.Modules.Coaching.Models.Structured.IntensityProfile`.
 */
export type IntensityProfile = 'Easy' | 'Moderate' | 'Threshold' | 'VO2Max' | 'Repetition'

/**
 * A periodized phase within the macro plan. Mirrors
 * `RunCoach.Api.Modules.Coaching.Models.Structured.PlanPhaseOutput`.
 */
export interface PlanPhaseDto {
  phaseType: PhaseType
  weeks: number
  weeklyDistanceStartKm: number
  weeklyDistanceEndKm: number
  intensityDistribution: string
  allowedWorkoutTypes: WorkoutType[]
  targetPaceEasySecPerKm: number
  targetPaceFastSecPerKm: number
  notes: string
  includesDeload: boolean
}

/**
 * Macro phase strip data. Mirrors
 * `RunCoach.Api.Modules.Coaching.Models.Structured.MacroPlanOutput`.
 */
export interface MacroPhaseDto {
  totalWeeks: number
  goalDescription: string
  phases: PlanPhaseDto[]
  rationale: string
  warnings: string
}

/**
 * One day slot within a weekly template. Mirrors
 * `RunCoach.Api.Modules.Coaching.Models.Structured.MesoDaySlotOutput`.
 *
 * Discriminated on `slotType`:
 * - `'Run'` slots carry a non-null `workoutType`.
 * - `'Rest'` and `'CrossTrain'` slots always have `workoutType: null`.
 *
 * Narrow on `slotType` before reading `workoutType` to satisfy the type
 * checker and reflect the backend invariant enforced by
 * `MesoDaySlotOutput.WorkoutType`.
 */
export type MesoDaySlotDto =
  | { slotType: 'Run'; workoutType: WorkoutType; notes: string }
  | { slotType: 'Rest' | 'CrossTrain'; workoutType: null; notes: string }

/**
 * One pre-generated weekly template (Slice 1 always emits exactly four — one
 * per week-index 1-4). Mirrors
 * `RunCoach.Api.Modules.Coaching.Models.Structured.MesoWeekOutput`.
 */
export interface MesoWeekTemplateDto {
  weekNumber: number
  phaseType: PhaseType
  weeklyTargetKm: number
  isDeloadWeek: boolean
  sunday: MesoDaySlotDto
  monday: MesoDaySlotDto
  tuesday: MesoDaySlotDto
  wednesday: MesoDaySlotDto
  thursday: MesoDaySlotDto
  friday: MesoDaySlotDto
  saturday: MesoDaySlotDto
  weekSummary: string
}

/**
 * One segment within a structured workout. Mirrors
 * `RunCoach.Api.Modules.Coaching.Models.Structured.WorkoutSegmentOutput`.
 */
export interface WorkoutSegmentDto {
  segmentType: SegmentType
  durationMinutes: number
  targetPaceSecPerKm: number
  intensity: IntensityProfile
  repetitions: number
  notes: string
}

/**
 * A single detailed workout prescription rendered by the `MicroWorkoutCard`
 * component. Mirrors
 * `RunCoach.Api.Modules.Coaching.Models.Structured.WorkoutOutput`.
 *
 * `dayOfWeek` is the **raw integer** 0 (Sunday) … 6 (Saturday) — the backend
 * model uses an `int` here rather than a `DayOfWeek` enum.
 */
export interface MicroWorkoutCardDto {
  dayOfWeek: number
  workoutType: WorkoutType
  title: string
  targetDistanceKm: number
  targetDurationMinutes: number
  targetPaceEasySecPerKm: number
  targetPaceFastSecPerKm: number
  segments: WorkoutSegmentDto[]
  warmupNotes: string
  cooldownNotes: string
  coachingNotes: string
  perceivedEffort: number
}

/**
 * The per-week detailed workout list. Mirrors
 * `RunCoach.Api.Modules.Coaching.Models.Structured.MicroWorkoutListOutput`.
 */
export interface MicroWorkoutListDto {
  workouts: MicroWorkoutCardDto[]
}

/**
 * GET /api/v1/plan/current response payload. Mirrors
 * `RunCoach.Api.Modules.Training.Plan.Models.PlanProjectionDto`.
 *
 * `microWorkoutsByWeek` is keyed by 1-based week index. Slice 1 only
 * populates the entry for week 1; later slices may attach further weeks
 * additively without breaking the Slice 1 frontend's `microWorkoutsByWeek[1]`
 * access path.
 *
 * `macro` is nullable on the wire because the projection's defaults predate
 * the `PlanGenerated` apply method — in practice the controller only returns
 * 200 once the stream has been projected, so consumers may treat `macro` as
 * present when the response status is 200.
 */
export interface PlanProjectionDto {
  planId: string
  userId: string
  generatedAt: string
  previousPlanId: string | null
  promptVersion: string
  modelId: string
  macro: MacroPhaseDto | null
  mesoWeeks: MesoWeekTemplateDto[]
  /**
   * Per-week detailed workout lists, keyed by the **stringified** week index
   * (e.g. `"1"`, `"2"`). The backend serializes a `Dictionary<int, ...>` whose
   * JSON keys arrive as strings after `JSON.parse`.
   *
   * Indexing with a numeric value (`record[1]`) works correctly — JavaScript
   * coerces the number to a string before the property lookup, so
   * `microWorkoutsByWeek[1]` and `microWorkoutsByWeek["1"]` are equivalent.
   *
   * Explicit coercion is only needed when iterating `Object.keys()` or
   * `Object.entries()` and bridging the string key back to a numeric week
   * index: use `Number.parseInt(key, 10)` in that case.
   */
  microWorkoutsByWeek: Record<string, MicroWorkoutListDto>
}
