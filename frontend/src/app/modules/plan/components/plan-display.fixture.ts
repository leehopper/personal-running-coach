// Single test fixture shared by every `*.component.spec.tsx` in this
// directory (Slice 1 § Unit 4). Each component spec drills into the
// projection from a different angle; the shared fixture keeps the
// trademark-cleanliness assertions auditable in one place.

import { CompletionStatus, type WorkoutLogDto } from '~/api/generated'
import type {
  MesoWeekTemplateDto,
  MicroWorkoutCardDto,
  PlanProjectionDto,
} from '~/modules/plan/models/plan.model'

/**
 * {@link buildPlanFixture}'s `planStartDate` — exported so specs that build
 * their own `WorkoutLogDto` fixtures against week/day offsets can anchor to
 * the exact same Sunday without hardcoding a second copy of the literal that
 * could drift out of sync with the fixture.
 */
export const PLAN_START_DATE = '2026-04-19'

const easyMonday: MicroWorkoutCardDto = {
  dayOfWeek: 1,
  workoutType: 'Easy',
  title: 'Easy aerobic shakeout',
  targetDistanceKm: 6,
  targetDurationMinutes: 38,
  targetPaceEasySecPerKm: 360,
  targetPaceFastSecPerKm: 330,
  segments: [],
  warmupNotes: '',
  cooldownNotes: '',
  coachingNotes: 'Keep the pace conversational — Daniels-Gilbert easy zone.',
  perceivedEffort: 3,
}

const intervalsWednesday: MicroWorkoutCardDto = {
  dayOfWeek: 3,
  workoutType: 'Interval',
  title: 'Threshold intervals',
  targetDistanceKm: 9,
  targetDurationMinutes: 55,
  targetPaceEasySecPerKm: 270,
  targetPaceFastSecPerKm: 240,
  segments: [
    {
      segmentType: 'Warmup',
      durationMinutes: 12,
      targetPaceSecPerKm: 360,
      intensity: 'Easy',
      repetitions: 1,
      notes: '',
    },
    {
      segmentType: 'Work',
      durationMinutes: 4,
      targetPaceSecPerKm: 240,
      intensity: 'Threshold',
      repetitions: 5,
      notes: 'Hold the threshold pace; pace-zone index targeting Threshold.',
    },
    {
      segmentType: 'Cooldown',
      durationMinutes: 8,
      targetPaceSecPerKm: 360,
      intensity: 'Easy',
      repetitions: 1,
      notes: '',
    },
  ],
  warmupNotes: 'Easy 12 min',
  cooldownNotes: 'Easy 8 min',
  coachingNotes: 'Threshold session — controlled discomfort.',
  perceivedEffort: 7,
}

const longRunSaturday: MicroWorkoutCardDto = {
  dayOfWeek: 6,
  workoutType: 'LongRun',
  title: 'Long aerobic run',
  targetDistanceKm: 14,
  targetDurationMinutes: 88,
  targetPaceEasySecPerKm: 360,
  targetPaceFastSecPerKm: 345,
  segments: [],
  warmupNotes: '',
  cooldownNotes: '',
  coachingNotes: 'Steady aerobic effort across the run.',
  perceivedEffort: 5,
}

const baseWeek = (weekNumber: number, isDeload: boolean): MesoWeekTemplateDto => ({
  weekNumber,
  phaseType: 'Base',
  weeklyTargetKm: isDeload ? 22 : 30,
  isDeloadWeek: isDeload,
  sunday: { slotType: 'Rest', workoutType: null, notes: 'Full rest' },
  monday: { slotType: 'Run', workoutType: 'Easy', notes: '' },
  tuesday: { slotType: 'Rest', workoutType: null, notes: 'Cross-train optional' },
  wednesday: { slotType: 'Run', workoutType: 'Interval', notes: '' },
  thursday: { slotType: 'Rest', workoutType: null, notes: '' },
  friday: { slotType: 'Run', workoutType: 'Easy', notes: '' },
  saturday: { slotType: 'Run', workoutType: 'LongRun', notes: '' },
  weekSummary:
    weekNumber === 1
      ? 'Build the aerobic base; one threshold session midweek.'
      : 'Maintain volume; introduce a second quality session.',
})

/**
 * A fully populated `PlanProjectionDto` mirroring the wire shape the home
 * surface receives. The structured-output enums use canonical Daniels-
 * Gilbert phrasing throughout — every spec asserts the rendered DOM
 * contains zero matches for the literal `vdot`.
 *
 * Defaults to the general-fitness / no-target-event shape (all three
 * target-event fields `null`) — this is the shared default across every
 * other spec that imports it, so it stays the neutral, no-new-behavior
 * baseline. Specs that want the race-training variant reach for
 * {@link buildRacePlanFixture} explicitly rather than relying on this
 * default carrying non-null values.
 */
export const buildPlanFixture = (): PlanProjectionDto => ({
  planId: '00000000-0000-0000-0000-00000000abcd',
  userId: '00000000-0000-0000-0000-0000000000aa',
  generatedAt: '2026-04-25T10:00:00Z',
  // PLAN_START_DATE (2026-04-19) is the Sunday opening the week containing
  // generatedAt (a Saturday).
  planStartDate: PLAN_START_DATE,
  previousPlanId: null,
  targetEventName: null,
  targetEventDistanceKm: null,
  targetEventDate: null,
  promptVersion: 'v1',
  modelId: 'claude-sonnet-test',
  macro: {
    totalWeeks: 12,
    goalDescription: 'Build aerobic base and prepare for a 10k race',
    rationale: 'Three-block periodisation across the macro-cycle.',
    warnings: '',
    phases: [
      {
        phaseType: 'Base',
        weeks: 4,
        weeklyDistanceStartKm: 25,
        weeklyDistanceEndKm: 35,
        intensityDistribution: '80/20 easy/threshold',
        allowedWorkoutTypes: ['Easy', 'LongRun', 'Tempo'],
        targetPaceEasySecPerKm: 360,
        targetPaceFastSecPerKm: 300,
        notes: 'Aerobic foundation block — Daniels-Gilbert easy zone dominates.',
        includesDeload: true,
      },
      {
        phaseType: 'Build',
        weeks: 5,
        weeklyDistanceStartKm: 35,
        weeklyDistanceEndKm: 45,
        intensityDistribution: '75/25',
        allowedWorkoutTypes: ['Easy', 'LongRun', 'Tempo', 'Interval'],
        targetPaceEasySecPerKm: 350,
        targetPaceFastSecPerKm: 240,
        notes: 'Add threshold and interval work; pace-zone index shifts upward.',
        includesDeload: true,
      },
      {
        phaseType: 'Peak',
        weeks: 2,
        weeklyDistanceStartKm: 40,
        weeklyDistanceEndKm: 45,
        intensityDistribution: '70/30',
        allowedWorkoutTypes: ['Easy', 'Tempo', 'Interval', 'Repetition'],
        targetPaceEasySecPerKm: 345,
        targetPaceFastSecPerKm: 220,
        notes: 'Race-pace specificity.',
        includesDeload: false,
      },
      {
        phaseType: 'Taper',
        weeks: 1,
        weeklyDistanceStartKm: 25,
        weeklyDistanceEndKm: 25,
        intensityDistribution: '85/15',
        allowedWorkoutTypes: ['Easy', 'Tempo'],
        targetPaceEasySecPerKm: 360,
        targetPaceFastSecPerKm: 280,
        notes: 'Reduce volume; preserve sharpness.',
        includesDeload: false,
      },
    ],
  },
  mesoWeeks: [baseWeek(1, false), baseWeek(2, false), baseWeek(3, false), baseWeek(4, true)],
  microWorkoutsByWeek: {
    1: { workouts: [easyMonday, intervalsWednesday, longRunSaturday] },
  },
})

/** Fixture micro workouts for week 1, used by component specs. */
export const fixtureWeekOneWorkouts = (): readonly MicroWorkoutCardDto[] =>
  buildPlanFixture().microWorkoutsByWeek[1]?.workouts ?? []

/**
 * A minimal `WorkoutLogDto` for THE WEEK / hero log-join specs — every
 * field except `occurredOn` and `distanceMeters` defaults to an arbitrary
 * fixed value, since those two are what the log-matching predicates under
 * test actually read.
 */
export const buildWorkoutLog = (occurredOn: string, distanceMeters = 6000): WorkoutLogDto => ({
  workoutLogId: `log-${occurredOn}`,
  occurredOn,
  distanceMeters,
  durationSeconds: 1800,
  completionStatus: CompletionStatus.Complete,
})

/**
 * A race-training variant of {@link buildPlanFixture} with all three
 * target-event fields populated — the wire shape a plan takes when the
 * runner has a goal race. Everything else is identical to the base
 * fixture; specs covering the race / goal-chip branch reach for this
 * rather than hand-rolling their own override.
 */
export const buildRacePlanFixture = (): PlanProjectionDto => ({
  ...buildPlanFixture(),
  targetEventName: 'Local 10K',
  targetEventDistanceKm: 10,
  // Lands inside the fixture's week-12 taper window (week 12 opens 2026-07-05).
  targetEventDate: '2026-07-12',
})
