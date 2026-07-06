import { randomUUID } from 'node:crypto'
import { expect, test, type Page, type Route } from '@playwright/test'

// Form-first onboarding e2e (slice 4C-onboarding, DU-2). Replaces the six-turn
// chat walk with a single structured-form submission. Strategy:
//   1. Real backend `register` seeds the session + antiforgery cookie pair the
//      way the runtime expects (`__Host-RunCoach`). The register page chains
//      login; on success the SPA tries `/`, the home redirect-guard sees the
//      stubbed 404 on `GET /onboarding/state`, and bounces to `/onboarding`.
//   2. Stub `GET /settings/units` (Kilometers), `GET /onboarding/state`
//      (404 pre-submit → Completed after), `POST /onboarding/answers` (capture
//      the single request, return the completed state), and `GET /plan/current`
//      so nothing touches the real LLM / plan generator.
//   3. Fill every field once, submit once, assert the single POST carried the
//      whole record and the guard redirected to `/`.

const SESSION_COOKIE = '__Host-RunCoach'
// Test-fixture credential for the real-backend register step, mirroring the other
// e2e specs; not a secret. Suppress the sonarjs hardcoded-password false positive
// (the sibling specs predate the rule and stay red — repo-wide noise, not this PR).
// eslint-disable-next-line sonarjs/no-hardcoded-passwords
const VALID_PASSWORD = 'Correct-Horse-9!'
const completedPlanId = '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b1234'

// Wire-format integer enums duplicated from the model so the stubs round-trip
// the exact JSON the app expects — keeping the e2e at arm's length from the
// implementation under test.
const OnboardingStatus = { InProgress: 1, Completed: 2 } as const
const PreferredUnits = { Kilometers: 0 } as const
const PrimaryGoal = { GeneralFitness: 1 } as const

const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

interface StubState {
  submitted: boolean
  answersBodies: Array<Record<string, unknown>>
}

const completedStateBody = () => ({
  userId: '00000000-0000-0000-0000-000000000001',
  status: OnboardingStatus.Completed,
  currentTopic: null,
  completedTopics: 6,
  totalTopics: 6,
  isComplete: true,
  outstandingClarifications: [],
  primaryGoal: { goal: PrimaryGoal.GeneralFitness, description: '' },
  targetEvent: null,
  currentFitness: null,
  weeklySchedule: null,
  injuryHistory: null,
  preferences: null,
  currentPlanId: completedPlanId,
})

const installStubs = async (page: Page, state: StubState): Promise<void> => {
  // The units-first control + the page's write-gate resolve the preference here.
  await page.route('**/api/v1/settings/units', async (route: Route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ preferredUnits: PreferredUnits.Kilometers }),
    })
  })

  // 404 before any submit ("no stream yet — start fresh"); Completed afterwards
  // so the post-submit guard refetch redirects to `/`.
  await page.route('**/api/v1/onboarding/state', async (route: Route) => {
    if (state.submitted) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(completedStateBody()),
      })
      return
    }
    await route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ type: 'about:blank', title: 'Not Found', status: 404 }),
    })
  })

  // The single form submission — capture the body, flip the state, return the
  // completed onboarding view (isComplete: true + a plan id).
  await page.route('**/api/v1/onboarding/answers', async (route: Route) => {
    const body = (await route.request().postDataJSON()) as Record<string, unknown>
    state.answersBodies.push(body)
    state.submitted = true
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(completedStateBody()),
    })
  })

  // The post-onboarding home guard fetches this once it lets `/` render.
  await page.route('**/api/v1/plan/current', async (route: Route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        planId: completedPlanId,
        userId: '00000000-0000-0000-0000-000000000001',
        generatedAt: new Date().toISOString(),
        previousPlanId: null,
        macroPhases: [],
        mesoWeeks: [],
        microWorkouts: [],
      }),
    })
  })
}

test('register → fill the form once → single submit → navigate to /', async ({ page }) => {
  const state: StubState = { submitted: false, answersBodies: [] }
  await installStubs(page, state)

  // 1. Register → real session cookie → guard bounces to /onboarding.
  await page.goto('/register')
  await page.getByLabel('Email').fill(uniqueEmail())
  await page.getByLabel('Password').fill(VALID_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()
  await expect(page).toHaveURL('/onboarding')

  // 2. The single-page form renders (units-first control + goal section).
  await expect(page.getByTestId('onboarding-page')).toBeVisible()
  await expect(page.getByTestId('onboarding-units-field')).toBeVisible()

  // 3. Fill every required field once — no per-topic clarification loop.
  await page.getByRole('radio', { name: /general fitness/i }).click()
  await page.getByTestId('typicalWeekly-field').fill('40')
  await page.getByTestId('longestRecentRun-field').fill('18')
  await page.getByTestId('maxRunDays-field').fill('5')
  await page.getByTestId('sessionMinutes-field').fill('60')
  await page.getByRole('button', { name: 'monday' }).click()
  await page.getByRole('button', { name: 'wednesday' }).click()

  // 4. Submit once.
  await page.getByTestId('onboarding-submit').click()

  // 5. The guard redirects to `/` on the completed state.
  await expect(page).toHaveURL('/')

  // 6. Exactly one request carried the whole record.
  expect(state.answersBodies).toHaveLength(1)
  const body = state.answersBodies[0]
  const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
  expect(body.idempotencyKey).toMatch(uuidPattern)
  expect(body.primaryGoal).toMatchObject({ goal: PrimaryGoal.GeneralFitness })
  expect(body.targetEvent).toBeNull()
  expect(body.currentFitness).toMatchObject({ typicalWeeklyKm: 40, longestRecentRunKm: 18 })
  expect(body.weeklySchedule).toMatchObject({
    maxRunDaysPerWeek: 5,
    typicalSessionMinutes: 60,
    monday: true,
    wednesday: true,
  })
  expect(body.preferences).toMatchObject({ preferredUnits: PreferredUnits.Kilometers })

  // 7. The session cookie survived the onboarding flow.
  const cookies = await page.context().cookies()
  expect(cookies.find((cookie) => cookie.name === SESSION_COOKIE)).toBeDefined()
})
