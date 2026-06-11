import { randomUUID } from 'node:crypto'
import { expect, test, type Page, type Route } from '@playwright/test'

// End-to-end coverage of the Slice 3 adaptation loop's frontend half (spec 17
// § Unit 7): a logged workout that deviates from prescription surfaces an
// adaptation `ConversationTurn` in the read-only "Explain-the-change" panel
// and re-renders the plan view in the same interaction.
//
// Strategy (the slice-2a/2b wire-stub pattern):
//   1. Use the real backend for `register` so the auth cookie + antiforgery
//      pair are seeded the same way the runtime app expects.
//   2. Stub `GET /api/v1/onboarding/state` with a `Completed` shape so the
//      home redirect-guard lets the SPA land on `/`.
//   3. Stub `GET /api/v1/plan/current` with a state-machine fixture that
//      returns the original plan before the log lands and the adapted plan
//      after — workout titles differ so the re-render assertion compares
//      plan content directly.
//   4. Stub `POST /api/v1/workouts/logs` to flip the shared state machine to
//      "adapted" — this models the backend's synchronous adaptation on the
//      create path (the deterministic nudge needs no LLM; the restructure's
//      LLM call is exactly what "stubbed at the wire" bypasses).
//   5. Stub `GET /api/v1/conversation/turns` to return no turns before the
//      log and the adaptation turn after.
//   6. Drive the UI: home (plan A, silent panel) → /log → submit a deviating
//      log → home shows the turn + the adjusted plan; assert the network
//      sequence POST logs → GET plan/current + GET conversation/turns.

const VALID_PASSWORD = 'Correct-Horse-9!'

// Fresh email per run so the suite is re-runnable against a shared dev
// Postgres without collisions. `npm run e2e:clean` flushes the orphans.
const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

// Wire-format integer enums duplicated inline so the stubs round-trip the
// exact JSON the panel consumes. Holding the e2e suite at arm's length from
// the production model module keeps the integers under explicit test-side
// control.
const OnboardingStatus = { NotStarted: 0, InProgress: 1, Completed: 2 } as const
const ConversationRole = { AssistantAdaptation: 0, SystemSafety: 1 } as const
const EscalationLevel = { Absorb: 0, MicroAdjust: 1, Restructure: 2 } as const
const SafetyTier = { Green: 0, Amber: 1, Red: 2 } as const
const ReferralCategory = { None: 0 } as const
const AdaptationKind = { Absorb: 0, Nudge: 1, Restructure: 2 } as const

const planId = '9c4b9b2a-1d3f-4f1c-9aab-5e2c1f0b4321'
const userId = '00000000-0000-0000-0000-000000000002'
const planEventId = '6f1d9b2a-4c3f-4f1c-9aab-5e2c1f0b1111'
const workoutLogId = '5a1d9b2a-4c3f-4f1c-9aab-5e2c1f0b2222'

// Build one detailed workout for `microWorkoutsByWeek` / diff payloads. The
// title carries the fixture prefix so assertions can prove which plan
// version the surface rendered.
const buildWorkout = (titlePrefix: string, dayOfWeek: number, dayName: string) => ({
  dayOfWeek,
  workoutType: 'Easy',
  title: `${titlePrefix} ${dayName} run`,
  targetDistanceKm: 8,
  targetDurationMinutes: 48,
  targetPaceEasySecPerKm: 360,
  targetPaceFastSecPerKm: 330,
  segments: [
    {
      segmentType: 'Work',
      durationMinutes: 48,
      targetPaceSecPerKm: 360,
      intensity: 'Easy',
      repetitions: 1,
      notes: 'Steady aerobic effort.',
    },
  ],
  warmupNotes: 'Five minutes brisk walk.',
  cooldownNotes: 'Five minutes walk + stretch.',
  coachingNotes: 'Hold pace-zone index easy throughout.',
  perceivedEffort: 4,
})

const DAY_NAMES = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
] as const

// Build a plan-projection payload. `titlePrefix` distinguishes the original
// plan ("Planned") from the adapted one ("Adjusted") so the re-render
// assertion can compare content; one workout per day keeps `TodayCard`
// populated regardless of when the suite runs.
const buildPlanProjection = (titlePrefix: string, weeklyTargets: readonly number[]) => ({
  planId,
  userId,
  generatedAt: '2026-06-01T12:00:00.000Z',
  // Deliberately blank: `resolveCurrentWeek` falls back to the lowest
  // populated micro week (week 1), keeping the suite date-independent —
  // only week 1 carries detailed workouts (the regenerate-plan.spec.ts
  // convention).
  planStartDate: '',
  previousPlanId: null,
  promptVersion: 'plan-generation-v1',
  modelId: 'claude-sonnet-4-6',
  macro: {
    totalWeeks: 4,
    goalDescription: 'Build aerobic base for a 10k.',
    rationale: 'Daniels-Gilbert zones drive the pace targets across phases.',
    warnings: '',
    phases: [
      {
        phaseType: 'Base',
        weeks: 4,
        weeklyDistanceStartKm: 30,
        weeklyDistanceEndKm: 40,
        targetPaceEasySecPerKm: 360,
        targetPaceFastSecPerKm: 330,
        intensityDistribution: '80/20',
        allowedWorkoutTypes: ['Easy', 'LongRun', 'Recovery'],
        notes: 'Aerobic foundation — pace-zone index easy.',
        includesDeload: false,
      },
    ],
  },
  mesoWeeks: weeklyTargets.map((weeklyTargetKm, index) => ({
    weekNumber: index + 1,
    phaseType: 'Base',
    weeklyTargetKm,
    isDeloadWeek: false,
    weekSummary: `Week ${index + 1} — pace-zone index easy mileage.`,
    ...Object.fromEntries(
      DAY_NAMES.map((day) => [
        day.toLowerCase(),
        { slotType: 'Run', workoutType: 'Easy', notes: '' },
      ]),
    ),
  })),
  microWorkoutsByWeek: {
    1: {
      workouts: DAY_NAMES.map((day, index) => buildWorkout(titlePrefix, index, day)),
    },
  },
})

const ORIGINAL_PLAN = buildPlanProjection('Planned', [30, 33, 36, 28])
const ADAPTED_PLAN = buildPlanProjection('Adjusted', [28, 33, 36, 28])

// Wire shape of one adaptation turn as the stub serves it. Widened to plain
// numbers (vs the const-map literals) so one fixture type covers both the
// nudge and the restructure variants.
interface AdaptationTurnFixture {
  triggeringPlanEventId: string
  role: number
  content: string
  escalationLevel: number
  safetyTier: number
  referralCategory: number
  adaptationKind: number
  diff: {
    workoutChanges: Array<{
      weekNumber: number
      dayOfWeek: number
      before: ReturnType<typeof buildWorkout> | null
      after: ReturnType<typeof buildWorkout> | null
    }>
    weeklyTargetChanges: Array<{
      weekNumber: number
      beforeWeeklyTargetKm: number
      afterWeeklyTargetKm: number
    }>
  }
  triggeringWorkoutLogId: string
  createdAt: string
}

// The deterministic L1 nudge turn — no LLM anywhere on this path.
const NUDGE_TURN: AdaptationTurnFixture = {
  triggeringPlanEventId: planEventId,
  role: ConversationRole.AssistantAdaptation,
  content:
    "Tuesday's session moves to Thursday so tomorrow stays easy — the week's volume is unchanged.",
  escalationLevel: EscalationLevel.MicroAdjust,
  safetyTier: SafetyTier.Green,
  referralCategory: ReferralCategory.None,
  adaptationKind: AdaptationKind.Nudge,
  diff: {
    workoutChanges: [
      {
        weekNumber: 1,
        dayOfWeek: 2,
        before: buildWorkout('Planned', 2, 'Tuesday'),
        after: buildWorkout('Adjusted', 2, 'Tuesday'),
      },
    ],
    weeklyTargetChanges: [],
  },
  triggeringWorkoutLogId: workoutLogId,
  createdAt: '2026-06-10T09:00:00.000Z',
}

// The L2 restructure turn — in production this is the first LLM call; here
// the whole response is stubbed at the wire, which is exactly the slice-2a/2b
// pattern for keeping the LLM out of the E2E loop.
const RESTRUCTURE_TURN: AdaptationTurnFixture = {
  triggeringPlanEventId: planEventId,
  role: ConversationRole.AssistantAdaptation,
  content:
    'Backing off after a hard stretch is the right call, not a setback. Your last three runs came in slower than the easy pace-zone band, which usually means accumulated fatigue. This week becomes a recovery week: Tuesday turns into an easy aerobic run and the weekly volume steps down to 28 km. Week 2 builds back to 33 km once your paces settle.',
  escalationLevel: EscalationLevel.Restructure,
  safetyTier: SafetyTier.Green,
  referralCategory: ReferralCategory.None,
  adaptationKind: AdaptationKind.Restructure,
  diff: {
    workoutChanges: [
      {
        weekNumber: 1,
        dayOfWeek: 2,
        before: buildWorkout('Planned', 2, 'Tuesday'),
        after: buildWorkout('Adjusted', 2, 'Tuesday'),
      },
    ],
    weeklyTargetChanges: [{ weekNumber: 1, beforeWeeklyTargetKm: 30, afterWeeklyTargetKm: 28 }],
  },
  triggeringWorkoutLogId: workoutLogId,
  createdAt: '2026-06-10T10:00:00.000Z',
}

interface StubState {
  // Whether the deviating log has landed yet. Drives which plan fixture and
  // which turn list the GET stubs serve on each request.
  hasAdapted: boolean
  // Number of POST /workouts/logs requests served.
  logCreateRequests: number
}

// Route stubs are method-scoped: each path expects exactly one verb, so a
// request that arrives with the wrong method (a frontend→API contract
// regression) is aborted rather than silently served the stub. Returns false
// when the verb mismatched and the route was already aborted.
const expectMethod = async (route: Route, method: string): Promise<boolean> => {
  if (route.request().method() !== method) {
    await route.abort('failed')
    return false
  }
  return true
}

const installStubs = async (
  page: Page,
  state: StubState,
  turn: AdaptationTurnFixture,
): Promise<void> => {
  // Onboarding state stub — Completed from the first request so the home
  // redirect-guard lets `/` and `/log` render without any chat traffic.
  await page.route('**/api/v1/onboarding/state', async (route: Route) => {
    if (!(await expectMethod(route, 'GET'))) return
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        userId,
        status: OnboardingStatus.Completed,
        currentTopic: null,
        completedTopics: 6,
        totalTopics: 6,
        isComplete: true,
        outstandingClarifications: [],
        primaryGoal: null,
        targetEvent: null,
        currentFitness: null,
        weeklySchedule: null,
        injuryHistory: null,
        preferences: null,
        currentPlanId: planId,
      }),
    })
  })

  // Plan stub — original before the log lands, adapted after. Models the
  // backend's synchronous projection mutation on the create path.
  await page.route('**/api/v1/plan/current', async (route: Route) => {
    if (!(await expectMethod(route, 'GET'))) return
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(state.hasAdapted ? ADAPTED_PLAN : ORIGINAL_PLAN),
    })
  })

  // Conversation stub — silent before the log lands, one adaptation turn
  // after (newest-first wire order).
  await page.route('**/api/v1/conversation/turns', async (route: Route) => {
    if (!(await expectMethod(route, 'GET'))) return
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ turns: state.hasAdapted ? [turn] : [] }),
    })
  })

  // Log-create stub — flips the state machine to "adapted" and returns the
  // canonical create shape. The deviation/escalation decision itself is
  // backend-owned and integration-tested there (spec 17 § Units 1/5).
  await page.route('**/api/v1/workouts/logs', async (route: Route) => {
    if (!(await expectMethod(route, 'POST'))) return
    state.logCreateRequests += 1
    state.hasAdapted = true
    await route.fulfill({
      status: 201,
      contentType: 'application/json',
      body: JSON.stringify({ workoutLogId }),
    })
  })
}

// Shared journey: register → home (plan A, silent panel) → log a deviating
// workout → home. Returns the observed request sequence for wire assertions.
const registerAndLogDeviatingWorkout = async (page: Page): Promise<string[]> => {
  const observedRequests: string[] = []
  page.on('request', (request) => {
    const url = request.url()
    if (url.includes('/api/v1/workouts/logs') && request.method() === 'POST') {
      observedRequests.push('POST /api/v1/workouts/logs')
    } else if (url.includes('/api/v1/plan/current') && request.method() === 'GET') {
      observedRequests.push('GET /api/v1/plan/current')
    } else if (url.includes('/api/v1/conversation/turns') && request.method() === 'GET') {
      observedRequests.push('GET /api/v1/conversation/turns')
    }
  })

  // 1. Register a fresh user → real backend issues the session cookie; the
  //    stubbed Completed onboarding shape lets home render the plan.
  await page.goto('/register')
  await page.getByLabel('Email').fill(uniqueEmail())
  await page.getByLabel('Password').fill(VALID_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()
  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-page')).toBeVisible()

  // 2. Pre-adaptation: the original plan renders and the panel is silent —
  //    no conversation section exists at all.
  await expect(page.getByTestId('today-card')).toBeVisible()
  expect(await page.getByTestId('home-page').innerHTML()).toContain('Planned')
  await expect(page.getByTestId('conversation-panel')).toHaveCount(0)

  // 3. Log a deviating workout (well short of the 8 km prescription).
  await page.goto('/log')
  await expect(page.getByTestId('log-form')).toBeVisible()
  await page.getByLabel('Distance (km)').fill('3')
  await page.getByLabel('Duration (minutes)').fill('30')
  await page.getByTestId('log-form-submit').click()

  // 4. The create succeeds → the SPA navigates home; the mutation
  //    invalidated Plan + Conversation so both refetch in this interaction.
  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-page')).toBeVisible()

  return observedRequests
}

// Proves the wire contract behind "in the same round-trip": the log POST is
// followed by a refetch of BOTH the plan and the conversation turns.
const expectInvalidationRefetch = (observedRequests: string[]): void => {
  const postIndex = observedRequests.indexOf('POST /api/v1/workouts/logs')
  expect(postIndex).toBeGreaterThanOrEqual(0)
  const after = observedRequests.slice(postIndex + 1)
  expect(after).toContain('GET /api/v1/plan/current')
  expect(after).toContain('GET /api/v1/conversation/turns')
}

test('a deviating log surfaces a nudge turn in the panel and re-renders the plan', async ({
  page,
}) => {
  const state: StubState = { hasAdapted: false, logCreateRequests: 0 }
  await installStubs(page, state, NUDGE_TURN)

  const observedRequests = await registerAndLogDeviatingWorkout(page)

  // The adaptation turn appears as a quiet inline one-liner — no expandable
  // block, no diff control.
  const panel = page.getByTestId('conversation-panel')
  await expect(panel).toBeVisible()
  await expect(panel.getByTestId('nudge-turn')).toHaveText(NUDGE_TURN.content)
  await expect(panel.getByTestId('restructure-turn')).toHaveCount(0)
  await expect(panel.getByTestId('diff-toggle')).toHaveCount(0)

  // The plan view re-rendered onto the adapted projection in the same
  // interaction — the adjusted workout titles replace the planned ones.
  await expect.poll(async () => page.getByTestId('home-page').innerHTML()).toContain('Adjusted')
  expect(await page.getByTestId('home-page').innerHTML()).not.toContain('Planned')

  expect(state.logCreateRequests).toBe(1)
  expectInvalidationRefetch(observedRequests)
})

test('a restructure (LLM stubbed at the wire) surfaces an expandable turn with a before/after diff', async ({
  page,
}) => {
  const state: StubState = { hasAdapted: false, logCreateRequests: 0 }
  await installStubs(page, state, RESTRUCTURE_TURN)

  const observedRequests = await registerAndLogDeviatingWorkout(page)

  // The restructure renders as an expandable block with the diff collapsed.
  const panel = page.getByTestId('conversation-panel')
  await expect(panel).toBeVisible()
  const block = panel.getByTestId('restructure-turn')
  await expect(block).toBeVisible()
  await expect(block).toContainText('This week becomes a recovery week')
  await expect(block.getByTestId('before-after-diff')).toBeHidden()

  // Expanding "Show what changed" reveals the structured before/after diff.
  await block.getByTestId('diff-toggle').click()
  const diff = block.getByTestId('before-after-diff')
  await expect(diff).toBeVisible()
  await expect(diff).toContainText('Week 1 · Tuesday')
  await expect(diff).toContainText('Planned Tuesday run (8 km) → Adjusted Tuesday run (8 km)')
  await expect(diff).toContainText('Week 1 volume')
  await expect(diff).toContainText('30 km → 28 km')

  // Collapsing hides it again.
  await block.getByTestId('diff-toggle').click()
  await expect(diff).toBeHidden()

  // The plan view re-rendered onto the restructured week.
  await expect.poll(async () => page.getByTestId('home-page').innerHTML()).toContain('Adjusted')

  expect(state.logCreateRequests).toBe(1)
  expectInvalidationRefetch(observedRequests)
})
