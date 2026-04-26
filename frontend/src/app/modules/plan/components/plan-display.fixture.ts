// Single test fixture shared by every `*.component.spec.tsx` in this
// directory (Slice 1 § Unit 4). Each component spec drills into the
// projection from a different angle; the shared fixture keeps the
// trademark-cleanliness assertions auditable in one place.

import type {
  MesoWeekTemplate,
  MicroWorkoutCard as MicroWorkoutDto,
  PlanProjectionDto,
} from '~/modules/plan/models/plan.model'

const easyMonday: MicroWorkoutDto = {
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

const intervalsWednesday: MicroWorkoutDto = {
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

const longRunSaturday: MicroWorkoutDto = {
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

const baseWeek = (weekNumber: number, isDeload: boolean): MesoWeekTemplate => ({
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
 */
export const buildPlanFixture = (): PlanProjectionDto => ({
  planId: '00000000-0000-0000-0000-00000000abcd',
  userId: '00000000-0000-0000-0000-0000000000aa',
  generatedAt: '2026-04-25T10:00:00Z',
  previousPlanId: null,
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

/** Convenience accessor used by component specs. */
export const fixtureWeekOneWorkouts = (): readonly MicroWorkoutDto[] =>
  buildPlanFixture().microWorkoutsByWeek[1]?.workouts ?? []
