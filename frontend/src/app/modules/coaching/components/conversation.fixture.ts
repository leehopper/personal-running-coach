import type { PlanAdaptationDiffDto } from '~/modules/coaching/models/conversation.model'
import type { MicroWorkoutCardDto } from '~/modules/plan/models/plan.model'

// Shared before/after diff fixtures for the coaching module specs (consumed by
// `before-after-diff.component.spec.tsx`). Copy is written to the coach
// guardrails (pace-zone terminology only) since the rendered diff is asserted
// against the banned-phrasing list.

/** A minimal workout shape for diff fixtures. */
export const buildDiffWorkout = (
  overrides: Partial<MicroWorkoutCardDto> = {},
): MicroWorkoutCardDto => ({
  dayOfWeek: 2,
  workoutType: 'Easy',
  title: 'Easy Aerobic Run',
  targetDistanceKm: 8,
  targetDurationMinutes: 48,
  targetPaceEasySecPerKm: 360,
  targetPaceFastSecPerKm: 330,
  segments: [],
  warmupNotes: 'Five minutes brisk walk.',
  cooldownNotes: 'Five minutes walk and stretch.',
  coachingNotes: 'Hold the easy pace-zone throughout.',
  perceivedEffort: 4,
  ...overrides,
})

/** A restructure diff: one workout swap plus two weekly volume edits. */
export const buildDiff = (
  overrides: Partial<PlanAdaptationDiffDto> = {},
): PlanAdaptationDiffDto => ({
  workoutChanges: [
    {
      weekNumber: 1,
      dayOfWeek: 2,
      before: buildDiffWorkout({
        workoutType: 'Interval',
        title: 'Threshold Intervals',
        targetDistanceKm: 10,
      }),
      after: buildDiffWorkout(),
    },
  ],
  weeklyTargetChanges: [
    { weekNumber: 1, beforeWeeklyTargetKm: 36, afterWeeklyTargetKm: 28 },
    { weekNumber: 2, beforeWeeklyTargetKm: 38, afterWeeklyTargetKm: 36 },
  ],
  ...overrides,
})
