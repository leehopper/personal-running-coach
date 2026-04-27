import { randomUUID } from 'node:crypto'
import { expect, test, type Page, type Route } from '@playwright/test'

// End-to-end coverage of the regenerate-from-settings flow.
//
// Strategy:
//   1. Use the real backend for `register` so the auth cookie + antiforgery
//      pair are seeded the same way the runtime app expects. The
//      `__Host-RunCoach` cookie carries the session straight into the rest
//      of the flow.
//   2. Stub `GET /api/v1/onboarding/state` with a `Completed` shape so the
//      home redirect-guard lets the SPA land on `/` without walking the
//      onboarding chat — onboarding completion is a precondition here, not
//      the unit under test.
//   3. Stub `GET /api/v1/plan/current` with a state-machine-style fixture
//      that returns plan A before the regenerate call lands and plan B
//      after. The two projections differ in macro phase composition + in
//      workout titles so the post-regeneration assertions can compare plan
//      content directly (plan-A "Base/Build/Taper" → plan-B
//      "Base/Recovery").
//   4. Stub `POST /api/v1/plan/regenerate` with deterministic responses
//      that flip the shared state machine to "regenerated" on the first
//      POST. The stub captures every observed `idempotencyKey` +
//      `intent.freeText` so the test can assert the wire contract the
//      dialog upholds.
//   5. Drive the UI: navigate to `/settings`, click "Regenerate plan",
//      submit the dialog with the canonical injury intent text, assert
//      the dialog closes, navigate to `/`, assert plan B's macro/workout
//      content replaces plan A's, and assert the network sequence: one
//      POST `/api/v1/plan/regenerate` followed by at least one GET
//      `/api/v1/plan/current`.

const SESSION_COOKIE = '__Host-RunCoach'
const VALID_PASSWORD = 'Correct-Horse-9!'

// Fresh email per run so the suite is re-runnable against a shared dev
// Postgres without collisions. `npm run e2e:clean` flushes the orphans.
const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

// Wire-format integer enum duplicated inline so the stub round-trips the
// exact JSON the page-level Zod schema accepts. Holding the e2e suite at
// arm's length from the production model module keeps the integers under
// explicit test-side control.
const OnboardingStatus = { NotStarted: 0, InProgress: 1, Completed: 2 } as const

const planAId = '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b1234'
const planBId = '7e1d9b2a-1d3f-4f1c-9aab-5e2c1f0b9876'
const userId = '00000000-0000-0000-0000-000000000001'

// Canonical user-facing copy for the regeneration intent. The test must
// echo it exactly so the stub assertion confirms the dialog passed it
// through to the wire body unchanged.
const INJURY_INTENT = 'I just got injured, please reduce volume'

interface PlanFixture {
  planId: string
  previousPlanId: string | null
  goalDescription: string
  rationale: string
  phases: ReadonlyArray<{
    phaseType: string
    weeks: number
    weeklyDistanceStartKm: number
    weeklyDistanceEndKm: number
    targetPaceEasySecPerKm: number
    targetPaceFastSecPerKm: number
    intensityDistribution: string
    allowedWorkoutTypes: ReadonlyArray<string>
    notes: string
    includesDeload: boolean
  }>
  weeklyTargets: ReadonlyArray<number>
  workoutTitlePrefix: string
}

// Plan A — the user's current plan before regeneration. Three macro phases
// totaling 12 weeks; standard pace-zone-index aerobic build.
const PLAN_A: PlanFixture = {
  planId: planAId,
  previousPlanId: null,
  goalDescription: 'Build aerobic base for a half marathon.',
  rationale: 'Daniels-Gilbert zones drive the pace targets across phases.',
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
    {
      phaseType: 'Build',
      weeks: 5,
      weeklyDistanceStartKm: 40,
      weeklyDistanceEndKm: 55,
      targetPaceEasySecPerKm: 350,
      targetPaceFastSecPerKm: 300,
      intensityDistribution: '80/20',
      allowedWorkoutTypes: ['Easy', 'LongRun', 'Tempo', 'Interval'],
      notes: 'Threshold sessions enter the rotation.',
      includesDeload: true,
    },
    {
      phaseType: 'Taper',
      weeks: 3,
      weeklyDistanceStartKm: 30,
      weeklyDistanceEndKm: 25,
      targetPaceEasySecPerKm: 360,
      targetPaceFastSecPerKm: 320,
      intensityDistribution: '90/10',
      allowedWorkoutTypes: ['Easy', 'Recovery', 'Tempo'],
      notes: 'Reduce volume, keep intensity sharp.',
      includesDeload: false,
    },
  ],
  weeklyTargets: [30, 33, 36, 28],
  workoutTitlePrefix: 'Plan-A',
}

// Plan B — the regenerated plan. The user said "I just got injured, please
// reduce volume", so the deterministic-stub response models that: shorter
// timeline, lower mileage targets, no Tempo/Interval phase, distinct
// workout-title prefix. `previousPlanId` references plan A so the post-
// regeneration Settings surface can prove the audit linkage between the
// two plans.
const PLAN_B: PlanFixture = {
  planId: planBId,
  previousPlanId: planAId,
  goalDescription: 'Recovery-first easy mileage following an injury setback.',
  rationale: 'Reduced volume per the regeneration intent provided by user.',
  phases: [
    {
      phaseType: 'Base',
      weeks: 6,
      weeklyDistanceStartKm: 18,
      weeklyDistanceEndKm: 28,
      targetPaceEasySecPerKm: 380,
      targetPaceFastSecPerKm: 360,
      intensityDistribution: '95/5',
      allowedWorkoutTypes: ['Easy', 'Recovery'],
      notes: 'Lower volume — pace-zone index easy only.',
      includesDeload: true,
    },
    {
      phaseType: 'Recovery',
      weeks: 2,
      weeklyDistanceStartKm: 15,
      weeklyDistanceEndKm: 12,
      targetPaceEasySecPerKm: 400,
      targetPaceFastSecPerKm: 380,
      intensityDistribution: '100/0',
      allowedWorkoutTypes: ['Recovery'],
      notes: 'Full deload week — short recovery jogs only.',
      includesDeload: false,
    },
  ],
  weeklyTargets: [18, 21, 24, 20],
  workoutTitlePrefix: 'Plan-B',
}

// Build a plan-projection JSON payload from the given plan fixture. The
// shape mirrors what the page-level Zod schema parses. Pads
// `microWorkoutsByWeek[1]` with one workout per day-of-week so `TodayCard`
// always picks a workout regardless of when the suite runs (no rest-day
// flake).
const buildPlanProjection = (fixture: PlanFixture) => {
  const dayOfWeekKeys = [
    'sunday',
    'monday',
    'tuesday',
    'wednesday',
    'thursday',
    'friday',
    'saturday',
  ] as const

  const totalWeeks = fixture.phases.reduce((sum, phase) => sum + phase.weeks, 0)

  const runSlot = { slotType: 'Run', workoutType: 'Easy', notes: '' }
  const buildMesoWeek = (weekNumber: number, weeklyTargetKm: number) => ({
    weekNumber,
    phaseType: fixture.phases[0].phaseType,
    weeklyTargetKm,
    isDeloadWeek: weekNumber === 4,
    weekSummary: `Week ${weekNumber} — ${fixture.workoutTitlePrefix} pace-zone index easy mileage.`,
    ...Object.fromEntries(dayOfWeekKeys.map((key) => [key, runSlot])),
  })

  const buildWorkout = (dayOfWeek: number, dayName: string) => ({
    dayOfWeek,
    workoutType: 'Easy',
    title: `${fixture.workoutTitlePrefix} ${dayName} run`,
    targetDistanceKm: 8,
    targetDurationMinutes: 48,
    targetPaceEasySecPerKm: fixture.phases[0].targetPaceEasySecPerKm,
    targetPaceFastSecPerKm: fixture.phases[0].targetPaceFastSecPerKm,
    segments: [
      {
        segmentType: 'Work',
        durationMinutes: 48,
        targetPaceSecPerKm: fixture.phases[0].targetPaceEasySecPerKm,
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

  return {
    planId: fixture.planId,
    userId,
    generatedAt: '2026-04-25T12:00:00.000Z',
    previousPlanId: fixture.previousPlanId,
    promptVersion: 'plan-generation-v1',
    modelId: 'claude-sonnet-4-6',
    macro: {
      totalWeeks,
      goalDescription: fixture.goalDescription,
      rationale: fixture.rationale,
      warnings: '',
      phases: fixture.phases.map((phase) => ({ ...phase })),
    },
    mesoWeeks: fixture.weeklyTargets.map((target, index) => buildMesoWeek(index + 1, target)),
    microWorkoutsByWeek: {
      1: {
        workouts: [
          buildWorkout(0, 'Sunday'),
          buildWorkout(1, 'Monday'),
          buildWorkout(2, 'Tuesday'),
          buildWorkout(3, 'Wednesday'),
          buildWorkout(4, 'Thursday'),
          buildWorkout(5, 'Friday'),
          buildWorkout(6, 'Saturday'),
        ],
      },
    },
  }
}

interface StubState {
  // Whether the regenerate call has landed yet. Drives which plan fixture
  // (A or B) the `GET /plan/current` stub serves on each request.
  hasRegenerated: boolean
  // Number of GET /plan/current requests served. Lets the test prove a
  // post-regenerate refetch happened (RTK Query invalidation).
  planCurrentRequests: number
  // Number of POST /plan/regenerate requests served. The happy-path
  // exercises exactly one.
  regenerateRequests: number
  // Idempotency keys observed on POST /plan/regenerate. Asserts the
  // dialog mints a UUID-shaped key for every distinct submission.
  idempotencyKeys: string[]
  // Free-text intent observed on POST /plan/regenerate. Asserts the
  // dialog passes the textarea contents through unchanged.
  observedIntents: Array<string | null>
}

const installStubs = async (page: Page, state: StubState): Promise<void> => {
  // Onboarding state stub — Completed from the first request so the home
  // redirect-guard lets `/` and `/settings` render without any chat traffic.
  await page.route('**/api/v1/onboarding/state', async (route: Route) => {
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
        currentPlanId: state.hasRegenerated ? planBId : planAId,
      }),
    })
  })

  // Plan stub — flips A → B once `state.hasRegenerated` is true. The
  // shared state machine keeps the projection consistent across the home
  // surface, the settings surface, and the post-invalidation refetch.
  await page.route('**/api/v1/plan/current', async (route: Route) => {
    state.planCurrentRequests += 1
    const fixture = state.hasRegenerated ? PLAN_B : PLAN_A
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(buildPlanProjection(fixture)),
    })
  })

  // Regenerate stub — captures the wire body, flips the state machine to
  // "regenerated", returns the canonical 200 success shape. Modeled after
  // `RegeneratePlanResponseDto` (`{ planId, status }`).
  await page.route('**/api/v1/plan/regenerate', async (route: Route) => {
    const body = (await route.request().postDataJSON()) as {
      idempotencyKey?: string
      intent?: { freeText?: string }
    }
    if (typeof body.idempotencyKey === 'string') {
      state.idempotencyKeys.push(body.idempotencyKey)
    }
    state.observedIntents.push(body.intent?.freeText ?? null)
    state.regenerateRequests += 1
    state.hasRegenerated = true

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ planId: planBId, status: 'generated' }),
    })
  })
}

test('register → settings → regenerate with intent → home shows new plan', async ({ page }) => {
  const email = uniqueEmail()
  const stubState: StubState = {
    hasRegenerated: false,
    planCurrentRequests: 0,
    regenerateRequests: 0,
    idempotencyKeys: [],
    observedIntents: [],
  }
  await installStubs(page, stubState)

  // Track the network request order so the post-flow assertion can prove
  // the wire contract: a single POST /api/v1/plan/regenerate followed by
  // at least one GET /api/v1/plan/current (the invalidation refetch).
  const observedRequests: string[] = []
  page.on('request', (request) => {
    const url = request.url()
    if (url.includes('/api/v1/plan/regenerate') && request.method() === 'POST') {
      observedRequests.push('POST /api/v1/plan/regenerate')
    } else if (url.includes('/api/v1/plan/current') && request.method() === 'GET') {
      observedRequests.push('GET /api/v1/plan/current')
    }
  })

  // 1. Register a fresh user → real backend issues the session cookie. The
  //    register page chains login automatically; on success the SPA tries
  //    to land on `/`, the home redirect-guard sees the stubbed Completed
  //    onboarding shape, and `HomePage` renders plan A.
  await page.goto('/register')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(VALID_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()
  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-page')).toBeVisible()

  // Capture plan A's render so we can compare against plan B post-regen.
  // The macro-phase-segment `data-phase` attribute is the most stable
  // selector — workout titles depend on the day-of-week resolution which
  // we deliberately avoid coupling to the calendar.
  const planAPhases = await page
    .getByTestId('macro-phase-segment')
    .evaluateAll((nodes) => nodes.map((node) => node.getAttribute('data-phase')))
  expect(planAPhases).toEqual(['Base', 'Build', 'Taper'])

  const planAHomeHtml = await page.getByTestId('home-page').innerHTML()
  expect(planAHomeHtml).toContain('Plan-A')

  // 2. Navigate to /settings. The route is gated by `<RequireAuth>` and the
  //    page renders the "Plan" section with the Regenerate button + the
  //    current plan's `generatedAt` timestamp.
  await page.goto('/settings')
  await expect(page).toHaveURL('/settings')
  await expect(page.getByTestId('settings-page')).toBeVisible()
  await expect(page.getByTestId('settings-plan-section')).toBeVisible()
  await expect(page.getByTestId('settings-plan-generated-at')).toBeVisible()
  await expect(page.getByTestId('settings-regenerate-button')).toBeVisible()

  // 3. Open the regenerate dialog. The dialog must contain the optional
  //    intent textarea labeled "Anything we should know? (optional)" plus
  //    a "Regenerate" submit button.
  await page.getByTestId('settings-regenerate-button').click()
  const dialog = page.getByTestId('regenerate-plan-dialog')
  await expect(dialog).toBeVisible()
  await expect(page.getByLabel('Anything we should know? (optional)')).toBeVisible()
  const submitButton = page.getByTestId('regenerate-plan-submit')
  await expect(submitButton).toBeVisible()

  // 4. Submit with the canonical injury intent. The dialog must (a) pass
  //    the freeText through unchanged on the wire, (b) close on success,
  //    (c) trigger a Plan-tag invalidation that refetches plan B.
  await page.getByLabel('Anything we should know? (optional)').fill(INJURY_INTENT)
  await submitButton.click()

  // Dialog closes once the mutation resolves. The `regenerate-plan-dialog`
  // markup unmounts entirely (parent `isOpen` flips false), not just hidden.
  await expect(dialog).toBeHidden()

  // 5. Navigate back to / — the SPA already fired the invalidation refetch
  //    when the dialog closed, but we navigate explicitly so the home
  //    surface re-renders with plan B replacing plan A. After the
  //    navigation the home surface must reflect plan B's macro composition
  //    plus workout titles.
  await page.goto('/')
  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-page')).toBeVisible()

  // Plan B's macro phases are deterministically distinct from plan A's:
  // two phases (Base + Recovery) instead of three. This is the strongest
  // assertion that the projection swap actually drives the render.
  await expect
    .poll(
      async () =>
        page
          .getByTestId('macro-phase-segment')
          .evaluateAll((nodes) => nodes.map((node) => node.getAttribute('data-phase'))),
      { timeout: 5_000 },
    )
    .toEqual(['Base', 'Recovery'])

  const planBHomeHtml = await page.getByTestId('home-page').innerHTML()
  expect(planBHomeHtml).toContain('Plan-B')
  expect(planBHomeHtml).not.toContain('Plan-A')
  expect(planBHomeHtml).not.toEqual(planAHomeHtml)

  // 6. Wire-contract assertions. Exactly one regenerate POST landed; the
  //    idempotency key was UUID-shaped; the freeText round-tripped without
  //    mutation; and the network sequence showed POST regenerate followed
  //    by at least one GET plan/current (the invalidation refetch).
  expect(stubState.regenerateRequests).toBe(1)
  expect(stubState.idempotencyKeys).toHaveLength(1)
  const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
  expect(stubState.idempotencyKeys[0]).toMatch(uuidPattern)
  expect(stubState.observedIntents).toEqual([INJURY_INTENT])

  const regenerateIndex = observedRequests.indexOf('POST /api/v1/plan/regenerate')
  expect(regenerateIndex).toBeGreaterThanOrEqual(0)
  const followingPlanGet = observedRequests
    .slice(regenerateIndex + 1)
    .find((entry) => entry === 'GET /api/v1/plan/current')
  expect(followingPlanGet).toBe('GET /api/v1/plan/current')

  // 7. The session cookie is still present — regeneration did not tear
  //    down auth. Belt-and-suspenders against a regression that logs the
  //    user out mid-flow.
  const cookies = await page.context().cookies()
  expect(cookies.find((cookie) => cookie.name === SESSION_COOKIE)).toBeDefined()
})
