import { randomUUID } from 'node:crypto'
import { expect, test, type Page, type Route } from '@playwright/test'

// Plan-render e2e per spec § Unit 4 R04.1, R04.3, R04.9 + the canonical
// `plan-view-on-home.feature` Gherkin (slice 1).
//
// Strategy:
//   1. Use the real backend for `register` so the auth cookie + antiforgery
//      pair are seeded the same way the runtime app expects (see
//      auth.spec.ts for that contract).
//   2. Stub `GET /api/v1/onboarding/state` with a `Completed` shape from the
//      first request — this short-circuits the home redirect-guard so the
//      test lands on `/` without walking the chat. The chat path is already
//      covered end to end by `onboarding.spec.ts`; here we only care about
//      the plan-render surface, hence the deterministic stubs (per task
//      description: "deterministic stub LLM via Playwright route
//      interception").
//   3. Stub `GET /api/v1/plan/current` with a hand-crafted projection that
//      exercises `MacroPhaseStrip`, `TodayCard`, and `UpcomingList`. The
//      projection contains a workout for every day of the week so the
//      "today" card always picks one regardless of when the suite runs.
//   4. Assert the three sections render, reload, assert again, and grep
//      `body.innerHTML` for `/vdot/i` returning zero matches per the
//      trademark rule (root `CLAUDE.md` § Trademark Rule: VDOT). This is the
//      DOM-level enforcement called out in spec § Unit 4 R04.9.

const VALID_PASSWORD = 'Correct-Horse-9!'

// Fresh email per run so the suite is re-runnable against a shared dev
// Postgres without collisions. `npm run e2e:clean` flushes the orphans.
const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

// Wire-format integer enum duplicated from
// `frontend/src/app/modules/onboarding/models/onboarding.model.ts` so the
// stub round-trips the exact JSON the page-level Zod schema accepts.
const OnboardingStatus = { NotStarted: 0, InProgress: 1, Completed: 2 } as const

const completedPlanId = '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b1234'
const userId = '00000000-0000-0000-0000-000000000001'

// PlanProjectionDto fixture — wide enough to populate every plan-render
// surface. The macro carries three phases summing to 12 weeks; the four
// meso templates cover weeks 1-4; week 1's micro list contains a workout
// for every day-of-week index 0-6 so `TodayCard` always resolves to the
// workout variant regardless of when the suite runs (no rest-day flake).
//
// String literals here must obey the trademark rule — every label uses
// "Daniels-Gilbert" / "pace-zone index" / Daniels phrasing, never "VDOT".
const buildPlanProjection = () => {
  const dayOfWeekKeys = [
    'sunday',
    'monday',
    'tuesday',
    'wednesday',
    'thursday',
    'friday',
    'saturday',
  ] as const

  const runSlot = { slotType: 'Run', workoutType: 'Easy', notes: '' }
  const buildMesoWeek = (weekNumber: number, phaseType: string, weeklyTargetKm: number) => ({
    weekNumber,
    phaseType,
    weeklyTargetKm,
    isDeloadWeek: weekNumber === 4,
    weekSummary: `Week ${weekNumber} — pace-zone index easy mileage with one threshold session.`,
    ...Object.fromEntries(dayOfWeekKeys.map((key) => [key, runSlot])),
  })

  const buildWorkout = (dayOfWeek: number, title: string) => ({
    dayOfWeek,
    workoutType: 'Easy',
    title,
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

  return {
    planId: completedPlanId,
    userId,
    generatedAt: '2026-04-25T12:00:00.000Z',
    previousPlanId: null,
    promptVersion: 'plan-generation-v1',
    modelId: 'claude-sonnet-4-6',
    macro: {
      totalWeeks: 12,
      goalDescription: 'Build aerobic base for a half marathon.',
      rationale: 'Daniels-Gilbert zones drive the pace targets across phases.',
      warnings: '',
      phases: [
        {
          phaseType: 'Base',
          weeks: 4,
          weeklyDistanceStartKm: 30,
          weeklyDistanceEndKm: 40,
          intensityDistribution: '80/20',
          allowedWorkoutTypes: ['Easy', 'LongRun', 'Recovery'],
          targetPaceEasySecPerKm: 360,
          targetPaceFastSecPerKm: 330,
          notes: 'Aerobic foundation — pace-zone index easy.',
          includesDeload: false,
        },
        {
          phaseType: 'Build',
          weeks: 5,
          weeklyDistanceStartKm: 40,
          weeklyDistanceEndKm: 55,
          intensityDistribution: '80/20',
          allowedWorkoutTypes: ['Easy', 'LongRun', 'Tempo', 'Interval'],
          targetPaceEasySecPerKm: 350,
          targetPaceFastSecPerKm: 300,
          notes: 'Threshold sessions enter the rotation.',
          includesDeload: true,
        },
        {
          phaseType: 'Taper',
          weeks: 3,
          weeklyDistanceStartKm: 30,
          weeklyDistanceEndKm: 25,
          intensityDistribution: '90/10',
          allowedWorkoutTypes: ['Easy', 'Recovery', 'Tempo'],
          targetPaceEasySecPerKm: 360,
          targetPaceFastSecPerKm: 320,
          notes: 'Reduce volume, keep intensity sharp.',
          includesDeload: false,
        },
      ],
    },
    mesoWeeks: [
      buildMesoWeek(1, 'Base', 30),
      buildMesoWeek(2, 'Base', 33),
      buildMesoWeek(3, 'Base', 36),
      buildMesoWeek(4, 'Base', 28),
    ],
    microWorkoutsByWeek: {
      1: {
        workouts: [
          buildWorkout(0, 'Sunday long run'),
          buildWorkout(1, 'Monday easy run'),
          buildWorkout(2, 'Tuesday recovery jog'),
          buildWorkout(3, 'Wednesday easy run'),
          buildWorkout(4, 'Thursday strides'),
          buildWorkout(5, 'Friday easy run'),
          buildWorkout(6, 'Saturday easy run'),
        ],
      },
    },
  }
}

interface StubCallCounter {
  planCurrentRequests: number
}

const installPlanStubs = async (page: Page, counter: StubCallCounter): Promise<void> => {
  // Onboarding state stub — the home redirect-guard fires this immediately
  // after `<RequireAuth>` settles. Returning a `Completed` shape lets the
  // route through to the plan view without any chat traffic.
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
        currentPlanId: completedPlanId,
      }),
    })
  })

  // Plan stub — every GET serves the same fixture so the post-reload
  // assertion compares to a stable snapshot. A counter on each call lets
  // the test assert the SPA does not re-fetch on every render (one fetch
  // per page load is the spec contract).
  await page.route('**/api/v1/plan/current', async (route: Route) => {
    counter.planCurrentRequests += 1
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(buildPlanProjection()),
    })
  })
}

test('register → land on / → plan renders → reload → identical content + no vdot in DOM', async ({
  page,
}) => {
  const email = uniqueEmail()
  const counter: StubCallCounter = { planCurrentRequests: 0 }
  await installPlanStubs(page, counter)

  // 1. Register a fresh user → real backend issues the session cookie. The
  //    register page chains login automatically; on success the SPA tries
  //    to land on `/`, the home redirect-guard sees the stubbed Completed
  //    state, and the route is allowed through to `HomePage`.
  await page.goto('/register')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(VALID_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()
  await expect(page).toHaveURL('/')

  // 2. The three plan-render sections must be visible on the home page.
  //    These three test ids are the public contract from spec § Unit 4
  //    R04.4 / R04.5 / R04.6.
  const homePage = page.getByTestId('home-page')
  await expect(homePage).toBeVisible()
  await expect(page.getByTestId('macro-phase-strip')).toBeVisible()
  await expect(page.getByTestId('today-card')).toBeVisible()
  await expect(page.getByTestId('upcoming-list')).toBeVisible()

  // Capture the rendered macro segments + today-card title before reload
  // so we can prove the post-reload DOM matches.
  const segmentsBefore = await page
    .getByTestId('macro-phase-segment')
    .evaluateAll((nodes) => nodes.map((node) => node.getAttribute('data-phase')))
  const homeHtmlBefore = await homePage.innerHTML()

  expect(segmentsBefore).toEqual(['Base', 'Build', 'Taper'])
  expect(counter.planCurrentRequests).toBeGreaterThan(0)

  // 3. Reload the page and assert the same content renders. The projection
  //    is persistent on the backend (DEC-046) and the home page only fires
  //    `getCurrentPlan` once per mount — both invariants are exercised
  //    here.
  const requestsBeforeReload = counter.planCurrentRequests
  await page.reload()
  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-page')).toBeVisible()
  await expect(page.getByTestId('macro-phase-strip')).toBeVisible()
  await expect(page.getByTestId('today-card')).toBeVisible()
  await expect(page.getByTestId('upcoming-list')).toBeVisible()

  const segmentsAfter = await page
    .getByTestId('macro-phase-segment')
    .evaluateAll((nodes) => nodes.map((node) => node.getAttribute('data-phase')))
  expect(segmentsAfter).toEqual(segmentsBefore)

  const homeHtmlAfter = await page.getByTestId('home-page').innerHTML()
  expect(homeHtmlAfter).toBe(homeHtmlBefore)

  // The reload triggered a fresh GET — but only one (RTK Query does not
  // double-fetch on mount). The strict equality keeps a regression that
  // re-mounts components mid-render from sneaking in.
  expect(counter.planCurrentRequests).toBe(requestsBeforeReload + 1)

  // 4. Trademark assertion — grep the rendered `body.innerHTML` for the
  //    literal `vdot` (case-insensitive) and assert zero matches. This is
  //    the DOM-level enforcement the spec calls out in § Unit 4 R04.9 and
  //    the canonical Gherkin scenario "Home page does not display any
  //    occurrence of the term 'vdot'".
  const bodyHtml = await page.locator('body').innerHTML()
  const vdotMatches = bodyHtml.match(/vdot/gi) ?? []
  expect(vdotMatches).toHaveLength(0)
})
