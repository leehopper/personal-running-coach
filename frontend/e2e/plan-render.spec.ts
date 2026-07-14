import { randomUUID } from 'node:crypto'
import { expect, test, type Page, type Route } from '@playwright/test'

// Plan-render e2e covering the SPLIT/Alpine Today screen's six-section
// recomposition (header, hero, THE WEEK, FROM YOUR COACH, UP NEXT, THE
// BLOCK).
//
// Strategy:
//   1. Use the real backend for `register` so the auth cookie + antiforgery
//      pair are seeded the same way the runtime app expects.
//   2. Stub `GET /api/v1/onboarding/state` with a `Completed` shape from the
//      first request — this short-circuits the home redirect-guard so the
//      test lands on `/` without walking the chat. The chat path is already
//      covered end to end by a dedicated onboarding suite; here we only
//      care about the plan-render surface, hence the deterministic stub of
//      the LLM-backed onboarding state via Playwright route interception.
//   3. Stub `GET /api/v1/plan/current` with a hand-crafted projection that
//      exercises all six Today-screen sections — including a race-training
//      target event so THE BLOCK's goal chip renders. The projection
//      contains a workout for every day of the week so the hero always
//      picks one regardless of when the suite runs.
//   4. Assert the six sections render, reload, assert again, and grep
//      `body.innerHTML` for `/vdot/i` returning zero matches — the DOM-level
//      enforcement of the trademark rule banning the term "VDOT" from any
//      user-facing surface.

// e2e specs; not a secret. Suppress the sonarjs hardcoded-password false positive
// (the sibling specs predate the rule and stay red — repo-wide noise, not this PR).
// eslint-disable-next-line sonarjs/no-hardcoded-passwords
const VALID_PASSWORD = 'Correct-Horse-9!'

// Fresh email per run so the suite is re-runnable against a shared dev
// Postgres without collisions. `npm run e2e:clean` flushes the orphans.
const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

// Wire-format integer enum duplicated inline so the stub round-trips the
// exact JSON the page-level Zod schema accepts. Holding the e2e suite at
// arm's length from the production model module keeps the integers under
// explicit test-side control.
const OnboardingStatus = { NotStarted: 0, InProgress: 1, Completed: 2 } as const

const completedPlanId = '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b1234'
const userId = '00000000-0000-0000-0000-000000000001'

// The Sunday (LOCAL calendar day — getFullYear/getMonth/getDate, matching
// the app's own UTC-midnight-normalization approach for this same
// calculation) on or before "now", formatted `YYYY-MM-DD`. Used as
// `planStartDate` so week 1's Sunday–Saturday span always contains "today"
// regardless of when the suite runs (same no-flake principle the fixture
// already applies to workout selection) — this is what makes THE WEEK's
// `today` day-cell state reachable at all: an absent/unparseable
// `planStartDate` degrades every cell to a non-today, non-done state
// instead of crashing.
const planStartDateForCurrentWeek = (): string => {
  const now = new Date()
  const sunday = new Date(now.getFullYear(), now.getMonth(), now.getDate() - now.getDay())
  const yyyy = sunday.getFullYear()
  const mm = String(sunday.getMonth() + 1).padStart(2, '0')
  const dd = String(sunday.getDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

// PlanProjectionDto fixture — wide enough to populate every plan-render
// surface. The macro carries three phases summing to 12 weeks; the four
// meso templates cover weeks 1-4; week 1's micro list contains a workout
// for every day-of-week index 0-6 so the workout hero always resolves to
// its `run` variant regardless of when the suite runs (no rest-day flake).
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
    // Anchors THE WEEK's calendar-date join to a real Sunday-first span
    // containing "today" — see `planStartDateForCurrentWeek`'s doc comment.
    planStartDate: planStartDateForCurrentWeek(),
    // Race-training target event — exercises THE BLOCK's goal chip
    // (`formatGoalChip`) through a real stub round-trip.
    targetEventName: 'Local Half Marathon',
    targetEventDistanceKm: 21.1,
    targetEventDate: '2026-10-03',
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

  // 2. All six Today-screen sections must be visible on the home page.
  const homePage = page.getByTestId('home-page')
  await expect(homePage).toBeVisible()
  await expect(page.getByTestId('today-header')).toBeVisible()
  await expect(page.getByTestId('workout-hero')).toBeVisible()
  await expect(page.getByTestId('the-week')).toBeVisible()
  await expect(page.getByTestId('coach-digest')).toBeVisible()
  await expect(page.getByTestId('up-next')).toBeVisible()
  await expect(page.getByTestId('the-block')).toBeVisible()

  // THE WEEK's headline states, not just visibility: with a real
  // `planStartDate` anchoring the grid to the current calendar week, exactly
  // one of the 7 day cells must resolve to `today` — the "you are here"
  // marker `resolveDayCells` derives from `todayUtc`. Without a real
  // `planStartDate` this state is structurally unreachable (every cell
  // degrades to `planned`/`rest`), so this assertion is genuine end-to-end
  // coverage of the grid's headline state, not mere DOM-presence coverage.
  const dayCellStates = await page
    .getByTestId('the-week-day-cell')
    .evaluateAll((nodes) => nodes.map((node) => node.getAttribute('data-state')))
  expect(dayCellStates).toHaveLength(7)
  expect(dayCellStates.filter((state) => state === 'today')).toHaveLength(1)

  // Capture THE BLOCK's rendered fill-tier cell sequence before reload so we
  // can prove the post-reload DOM matches — the direct successor to the
  // deleted `MacroPhaseStrip`'s `macro-phase-segment` snapshot proof: same
  // "no double-fetch, same content" invariant, new selectors.
  const tiersBefore = await page
    .getByTestId('the-block-cell')
    .evaluateAll((nodes) => nodes.map((node) => node.getAttribute('data-tier')))
  const homeHtmlBefore = await homePage.innerHTML()

  expect(tiersBefore).toHaveLength(12)
  expect(tiersBefore[0]).toBe('current')
  expect(counter.planCurrentRequests).toBeGreaterThan(0)

  // 3. Reload the page and assert the same content renders. The projection
  //    is persistent on the backend (DEC-046) and the home page only fires
  //    `getCurrentPlan` once per mount — both invariants are exercised
  //    here.
  const requestsBeforeReload = counter.planCurrentRequests
  await page.reload()
  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-page')).toBeVisible()
  await expect(page.getByTestId('today-header')).toBeVisible()
  await expect(page.getByTestId('workout-hero')).toBeVisible()
  await expect(page.getByTestId('the-week')).toBeVisible()
  await expect(page.getByTestId('coach-digest')).toBeVisible()
  await expect(page.getByTestId('up-next')).toBeVisible()
  await expect(page.getByTestId('the-block')).toBeVisible()

  const tiersAfter = await page
    .getByTestId('the-block-cell')
    .evaluateAll((nodes) => nodes.map((node) => node.getAttribute('data-tier')))
  expect(tiersAfter).toEqual(tiersBefore)

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
