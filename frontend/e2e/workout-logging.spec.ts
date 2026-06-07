import { randomUUID } from 'node:crypto'
import { expect, test, type Page, type Route } from '@playwright/test'

// Workout-logging end-to-end per spec § Unit 7 + the canonical
// `frontend-history-list-and-e2e.feature` scenario "End-to-end
// log-and-view-history journey".
//
// Strategy (mirrors plan-render.spec.ts):
//   1. `register` hits the REAL backend so the session cookie + antiforgery
//      pair are seeded exactly as the runtime app expects.
//   2. `GET /api/v1/onboarding/state` is stubbed `Completed` so the route
//      guards on `/`, `/log`, and `/history` let the user through without
//      walking the onboarding chat.
//   3. `GET /api/v1/plan/current` is stubbed with a populated projection so the
//      home page renders the plan view (and therefore the "Workout history"
//      link the user clicks to reach the surface under test).
//   4. The slice-2b units under test are NOT stubbed: `POST .../workouts/logs`
//      (create) and `POST .../workouts/logs/query` (history) hit the real
//      backend. The freshly-registered user has no real plan (onboarding/plan
//      are stubbed at the wire only), so both logs persist off-plan with a null
//      prescription snapshot — which is a first-class supported case (DEC-076).
//
// The "minimum-payload" workout carries only the required core fields (no note,
// no metrics) by definition; the "rich" workout adds an Avg HR metric and a
// freeform note. The journey asserts both appear in the week-grouped history
// surface and that the rich workout's note + metric render.

const VALID_PASSWORD = 'Correct-Horse-9!'

// Fresh email per run so the suite is re-runnable against a shared dev Postgres
// without collisions. `npm run e2e:clean` flushes the orphans.
const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

// Wire-format integer enum duplicated from the onboarding model so the stub
// round-trips the exact JSON the page-level Zod schema accepts.
const OnboardingStatus = { NotStarted: 0, InProgress: 1, Completed: 2 } as const

const completedPlanId = '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b9999'
const userId = '00000000-0000-0000-0000-000000000042'

// A populated projection — just enough for the home page to render the plan
// layout (and therefore the "Workout history" link). String literals obey the
// trademark rule: "pace-zone index" / "Daniels-Gilbert", never "VDOT".
const buildPlanProjection = () => {
  const dayKeys = [
    'sunday',
    'monday',
    'tuesday',
    'wednesday',
    'thursday',
    'friday',
    'saturday',
  ] as const
  const runSlot = { slotType: 'Run', workoutType: 'Easy', notes: '' }

  const buildWorkout = (dayOfWeek: number, title: string) => ({
    dayOfWeek,
    workoutType: 'Easy',
    title,
    targetDistanceKm: 8,
    targetDurationMinutes: 48,
    targetPaceEasySecPerKm: 360,
    targetPaceFastSecPerKm: 330,
    segments: [],
    warmupNotes: '',
    cooldownNotes: '',
    coachingNotes: 'Hold pace-zone index easy throughout.',
    perceivedEffort: 4,
  })

  return {
    planId: completedPlanId,
    userId,
    planStartDate: '2026-06-07',
    generatedAt: '2026-06-07T12:00:00.000Z',
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
          weeks: 12,
          weeklyDistanceStartKm: 30,
          weeklyDistanceEndKm: 40,
          intensityDistribution: '80/20',
          allowedWorkoutTypes: ['Easy', 'LongRun', 'Recovery'],
          targetPaceEasySecPerKm: 360,
          targetPaceFastSecPerKm: 330,
          notes: 'Aerobic foundation — pace-zone index easy.',
          includesDeload: false,
        },
      ],
    },
    mesoWeeks: [
      {
        weekNumber: 1,
        phaseType: 'Base',
        weeklyTargetKm: 30,
        isDeloadWeek: false,
        weekSummary: 'Week 1 — pace-zone index easy mileage.',
        ...Object.fromEntries(dayKeys.map((key) => [key, runSlot])),
      },
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

const installNavigationStubs = async (page: Page): Promise<void> => {
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

  await page.route('**/api/v1/plan/current', async (route: Route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(buildPlanProjection()),
    })
  })
}

const registerAndLandHome = async (page: Page): Promise<void> => {
  await page.goto('/register')
  await page.getByLabel('Email').fill(uniqueEmail())
  await page.getByLabel('Password').fill(VALID_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()
  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-page')).toBeVisible()
}

const logWorkout = async (
  page: Page,
  fields: { distanceKm: string; durationMinutes: string; note?: string; avgHr?: string },
): Promise<void> => {
  await page.goto('/log')
  await expect(page.getByTestId('log-form')).toBeVisible()
  await page.getByLabel('Distance (km)').fill(fields.distanceKm)
  await page.getByLabel('Duration (minutes)').fill(fields.durationMinutes)

  if (fields.note !== undefined) {
    await page.getByLabel('How did it go?').fill(fields.note)
  }
  if (fields.avgHr !== undefined) {
    await page.getByRole('button', { name: /more details/i }).click()
    await page.getByLabel('Avg HR (bpm)').fill(fields.avgHr)
  }

  const submit = page.getByTestId('log-form-submit')
  await expect(submit).toBeEnabled()
  await submit.click()
  // Pessimistic create: the page navigates home only after a 201.
  await expect(page).toHaveURL('/')
}

test('register → log a minimum + a rich workout → both appear in week-grouped history', async ({
  page,
}) => {
  const richNote = 'Felt strong — negative-split the back half on pace-zone index easy.'

  await installNavigationStubs(page)
  await registerAndLandHome(page)

  // 1. Minimum-payload workout: required core fields only (no note, no metrics).
  await logWorkout(page, { distanceKm: '5', durationMinutes: '30' })

  // 2. Rich-payload workout: adds an Avg HR metric and a freeform note.
  await logWorkout(page, {
    distanceKm: '8',
    durationMinutes: '50',
    note: richNote,
    avgHr: '150',
  })

  // 3. Navigate to the history surface via the home link (the real UI path).
  await page.getByTestId('home-history-link').click()
  await expect(page).toHaveURL('/history')
  await expect(page.getByTestId('workout-history-page')).toBeVisible()

  // 4. Both logged workouts appear, grouped under a single ISO-week header
  //    (both logged today).
  await expect(page.getByTestId('workout-history-entry')).toHaveCount(2)
  await expect(page.getByTestId('workout-history-week-header')).toHaveCount(1)
  await expect(page.getByText('5.0 km')).toBeVisible()
  await expect(page.getByText('8.0 km')).toBeVisible()

  // 5. The rich workout's note + metric render in its history entry.
  await expect(page.getByText(richNote)).toBeVisible()
  await expect(page.getByText('150 bpm')).toBeVisible()

  // 6. Trademark guard — no "VDOT" anywhere in the rendered history DOM.
  const bodyHtml = await page.locator('body').innerHTML()
  expect(bodyHtml.match(/vdot/gi) ?? []).toHaveLength(0)
})
