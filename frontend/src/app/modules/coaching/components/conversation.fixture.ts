import {
  ADAPTATION_KIND,
  CONVERSATION_ROLE,
  CONVERSATION_TIMELINE_TURN_KIND,
  type ConversationTimelineTurnDto,
  type LoggedRunSummaryDto,
  type PlanAdaptationDiffDto,
} from '~/modules/coaching/models/conversation.model'
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

// ─────────────────────────────────────────────────────────────────────────
// Composed-timeline fixtures — `CoachDigest`'s specs need a real,
// multi-element `turns` array (each user/coach message its own array
// entry, never a single record carrying both — a `ConversationTimelineTurnDto`
// is one turn, one role, always) so the `userLine` one-step-lookback tests
// can control `turns[turns.length - 2]` independently of
// `turns[turns.length - 1]`. Other page-level test surfaces keep their own
// unexported inline builders for their own narrower needs — these are the
// digest's own, centralised here rather than re-inlined a third time.
// ─────────────────────────────────────────────────────────────────────────

/** A `user`-kind timeline turn — always `isErrored: false` per its own wire contract. */
export const buildUserTimelineTurn = (
  content: string,
  turnId = 'user-1',
): ConversationTimelineTurnDto => ({
  kind: CONVERSATION_TIMELINE_TURN_KIND.user,
  turnId,
  createdAt: '2026-06-29T10:00:00Z',
  interactive: { content, isErrored: false, loggedRun: null },
  proactive: null,
})

/**
 * A `coach`-kind timeline turn. Pass `isErrored: true` to model a stream
 * that died mid-flight — `content` is forced empty in that case, matching
 * the wire contract (`InteractiveTurnDto`'s own doc comment).
 */
export const buildCoachTimelineTurn = (
  params: {
    content?: string
    isErrored?: boolean
    turnId?: string
    loggedRun?: LoggedRunSummaryDto | null
  } = {},
): ConversationTimelineTurnDto => {
  const {
    content = 'You ran well.',
    isErrored = false,
    turnId = 'coach-1',
    loggedRun = null,
  } = params
  return {
    kind: CONVERSATION_TIMELINE_TURN_KIND.coach,
    turnId,
    createdAt: '2026-06-29T10:00:01Z',
    interactive: { content: isErrored ? '' : content, isErrored, loggedRun },
    proactive: null,
  }
}

/** A quiet inline `nudge`-kind adaptation timeline turn — used to interpose a non-`user` turn between a runner's message and the coach's reply. */
export const buildNudgeTimelineTurn = (turnId = 'nudge-1'): ConversationTimelineTurnDto => ({
  kind: CONVERSATION_TIMELINE_TURN_KIND.adaptation,
  turnId,
  createdAt: '2026-06-29T10:00:01Z',
  interactive: null,
  proactive: {
    triggeringPlanEventId: turnId,
    role: CONVERSATION_ROLE.assistantAdaptation,
    content: 'Nudged tomorrow easier.',
    escalationLevel: 1,
    safetyTier: 0,
    referralCategory: 0,
    adaptationKind: ADAPTATION_KIND.nudge,
    diff: { workoutChanges: [], weeklyTargetChanges: [] },
    triggeringWorkoutLogId: 'w1',
    createdAt: '2026-06-29T10:00:01Z',
  },
})

/**
 * A restructure-kind `adaptation` timeline turn carrying the caller-supplied
 * `diff` — never hardcoded, so both the restructure-headline test and the
 * units-Miles headline test can each pin their own
 * `composeAdaptationHeadline` input/output pair without a second, competing
 * fixture shape.
 */
export const buildRestructureTimelineTurn = (
  diff: PlanAdaptationDiffDto,
  turnId = 'restructure-1',
): ConversationTimelineTurnDto => ({
  kind: CONVERSATION_TIMELINE_TURN_KIND.adaptation,
  turnId,
  createdAt: '2026-06-29T10:00:02Z',
  interactive: null,
  proactive: {
    triggeringPlanEventId: turnId,
    role: CONVERSATION_ROLE.assistantAdaptation,
    content: 'I adjusted this week to help you recover.',
    escalationLevel: 2,
    safetyTier: 0,
    referralCategory: 0,
    adaptationKind: ADAPTATION_KIND.restructure,
    diff,
    triggeringWorkoutLogId: 'w1',
    createdAt: '2026-06-29T10:00:02Z',
  },
})

/**
 * A full oldest-first composed timeline. Defaults to state 1's normal case —
 * a `[user, coach]` pair. Pass an explicit `turns` array to build any other
 * shape (the errored-latest-turn case, the "no precedent user turn" case, or
 * a restructure-latest sequence) without a second, competing builder shape.
 */
export const buildTimeline = (
  turns: ConversationTimelineTurnDto[] = [
    buildUserTimelineTurn('How was my run?'),
    buildCoachTimelineTurn(),
  ],
): ConversationTimelineTurnDto[] => turns

/** `[user, coach(errored)]` — the coach's stream died mid-flight. */
export const buildErroredLatestTimeline = (): ConversationTimelineTurnDto[] => [
  buildUserTimelineTurn('How was my run?'),
  buildCoachTimelineTurn({ isErrored: true }),
]

/**
 * `[user, adaptation, coach]` — the "no precedent user turn" case:
 * `turns[turns.length - 2]` is an adaptation turn, not a `user` turn, so no
 * "You:" line renders above the coach reply.
 */
export const buildNoPrecedentUserTimeline = (): ConversationTimelineTurnDto[] => [
  buildUserTimelineTurn('How was my run?'),
  buildNudgeTimelineTurn(),
  buildCoachTimelineTurn({ content: 'Also, nice work this week.' }),
]

/** `[user, coach, restructure]` — the latest turn is itself a restructure adaptation (state 3). */
export const buildRestructureLatestTimeline = (
  diff: PlanAdaptationDiffDto,
): ConversationTimelineTurnDto[] => [
  buildUserTimelineTurn('How was my run?'),
  buildCoachTimelineTurn(),
  buildRestructureTimelineTurn(diff),
]
