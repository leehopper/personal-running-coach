import {
  ADAPTATION_KIND,
  CONVERSATION_ROLE,
  ESCALATION_LEVEL,
  REFERRAL_CATEGORY,
  SAFETY_TIER,
  type AdaptationTurnDto,
  type PlanAdaptationDiffDto,
  type SafetyTurnDto,
} from '~/modules/coaching/models/conversation.model'
import type { MicroWorkoutCardDto } from '~/modules/plan/models/plan.model'

// Shared conversation-turn fixtures for the coaching module specs. Copy is
// written to the coach guardrails (no controlling/system language, no
// miss-counting, no claimed physical observation, no feigned emotion, no
// runner comparison, pace-zone terminology only) because the specs assert
// the rendered panel against the banned-phrasing list.

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

/** An L1 nudge turn — quiet inline one-liner, deterministic swap diff. */
export const buildNudgeTurn = (overrides: Partial<AdaptationTurnDto> = {}): AdaptationTurnDto => ({
  triggeringPlanEventId: '11111111-1111-4111-8111-111111111111',
  role: CONVERSATION_ROLE.assistantAdaptation,
  content:
    "Tuesday's tempo moves to Thursday so tomorrow stays easy — the week's volume is unchanged.",
  escalationLevel: ESCALATION_LEVEL.microAdjust,
  safetyTier: SAFETY_TIER.green,
  referralCategory: REFERRAL_CATEGORY.none,
  adaptationKind: ADAPTATION_KIND.nudge,
  diff: buildDiff({ weeklyTargetChanges: [] }),
  triggeringWorkoutLogId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  createdAt: '2026-06-10T09:00:00.000Z',
  ...overrides,
})

/** An L2 restructure turn — expandable block with a before/after diff. */
export const buildRestructureTurn = (
  overrides: Partial<AdaptationTurnDto> = {},
): AdaptationTurnDto => ({
  triggeringPlanEventId: '22222222-2222-4222-8222-222222222222',
  role: CONVERSATION_ROLE.assistantAdaptation,
  content:
    'Backing off after a hard block is the right call, not a setback. Your last three runs came in slower than the easy pace-zone band, which usually means accumulated fatigue. This week becomes a recovery week: the threshold intervals turn into an easy aerobic run and the weekly volume steps down to 28 km. Week 2 builds back to 36 km with threshold work returning once your paces settle.',
  escalationLevel: ESCALATION_LEVEL.restructure,
  safetyTier: SAFETY_TIER.green,
  referralCategory: REFERRAL_CATEGORY.none,
  adaptationKind: ADAPTATION_KIND.restructure,
  diff: buildDiff(),
  triggeringWorkoutLogId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
  createdAt: '2026-06-10T10:00:00.000Z',
  ...overrides,
})

/**
 * The scripted Red crisis turn. The content mirrors the backend's versioned
 * `CrisisResponseContent` contract: it must contain the exact resource
 * strings `988 Suicide & Crisis Lifeline` and `Crisis Text Line: text 741741`.
 */
export const buildCrisisSafetyTurn = (overrides: Partial<SafetyTurnDto> = {}): SafetyTurnDto => ({
  triggeringPlanEventId: '33333333-3333-4333-8333-333333333333',
  role: CONVERSATION_ROLE.systemSafety,
  content:
    'What you wrote matters more than any training plan, and it took strength to put it into words. Please reach out to people who can help right now: call or text the 988 Suicide & Crisis Lifeline, or reach the Crisis Text Line: text 741741. Your plan will be here whenever you are ready to come back to it.',
  escalationLevel: null,
  safetyTier: SAFETY_TIER.red,
  referralCategory: REFERRAL_CATEGORY.crisis,
  adaptationKind: null,
  diff: null,
  triggeringWorkoutLogId: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
  createdAt: '2026-06-10T11:00:00.000Z',
  ...overrides,
})

/** An Amber injury-referral turn — full content, amber accent. */
export const buildAmberSafetyTurn = (overrides: Partial<SafetyTurnDto> = {}): SafetyTurnDto => ({
  triggeringPlanEventId: '44444444-4444-4444-8444-444444444444',
  role: CONVERSATION_ROLE.systemSafety,
  content:
    'That sharp pain you described is worth taking seriously. Please have a physiotherapist or sports doctor look at it before the next hard session — training holds at or below the current load until then.',
  escalationLevel: null,
  safetyTier: SAFETY_TIER.amber,
  referralCategory: REFERRAL_CATEGORY.injury,
  adaptationKind: null,
  diff: null,
  triggeringWorkoutLogId: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
  createdAt: '2026-06-10T12:00:00.000Z',
  ...overrides,
})

/** A defensive absorb turn — never persisted in production, renders nothing. */
export const buildAbsorbTurn = (overrides: Partial<AdaptationTurnDto> = {}): AdaptationTurnDto =>
  buildNudgeTurn({
    triggeringPlanEventId: '55555555-5555-4555-8555-555555555555',
    content: 'Right on target — nothing to change.',
    escalationLevel: ESCALATION_LEVEL.absorb,
    adaptationKind: ADAPTATION_KIND.absorb,
    diff: { workoutChanges: [], weeklyTargetChanges: [] },
    ...overrides,
  })
