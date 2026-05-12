import { randomUUID } from 'node:crypto'
import { expect, test, type Page, type Route } from '@playwright/test'

// `GET /api/v1/onboarding/state` and `GET /api/v1/plan/current` are stubbed
// with completed/no-plan shapes so the home redirect-guard lets a freshly
// registered user land at `/` — this spec covers the auth contract, not the
// onboarding gate (covered by `e2e/onboarding.spec.ts`).

// Session cookie name is fixed by the backend's Identity cookie
// configuration (`__Host-RunCoach`). The `__Host-` prefix requires
// HTTPS + `Secure` + no `Domain=` + `Path=/`; everything below verifies
// that contract end to end.
const SESSION_COOKIE = '__Host-RunCoach'

// SPA-readable half of the antiforgery double-submit pair (DEC-054);
// echoed in the `X-XSRF-TOKEN` header on mutating requests.
const XSRF_COOKIE = '__Host-Xsrf-Request'
const XSRF_HEADER = 'X-XSRF-TOKEN'

// Fresh email per run so the suite is re-runnable against a shared
// dev Postgres without collisions.
const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

// Meets the backend Identity policy (12+ chars, upper, lower, digit,
// non-alphanumeric) and the frontend `registerSchema`.
const VALID_PASSWORD = 'Correct-Horse-9!'

// Wire-format enum inlined (not imported) so the e2e suite stays decoupled
// from the implementation under test.
const OnboardingStatus = { NotStarted: 0, InProgress: 1, Completed: 2 } as const

const completedPlanId = '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b1234'
const stubUserId = '00000000-0000-0000-0000-000000000001'

// Stubs the onboarding-state + plan-current fetches the home redirect-guard
// and `HomePage` issue on mount, so the auth spec is not coupled to either.
const installHomeGuardStubs = async (page: Page): Promise<void> => {
  await page.route('**/api/v1/onboarding/state', async (route: Route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        userId: stubUserId,
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

  // Minimal plan projection so `HomePage` does not surface its error branch.
  await page.route('**/api/v1/plan/current', async (route: Route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        planId: completedPlanId,
        userId: stubUserId,
        generatedAt: new Date().toISOString(),
        previousPlanId: null,
        macro: null,
        mesoWeeks: [],
        microWorkoutsByWeek: {},
      }),
    })
  })
}

test('register → authenticated home → reload → logout clears session', async ({
  page,
  context,
}) => {
  await installHomeGuardStubs(page)
  const email = uniqueEmail()

  // 1. Register a fresh user. The register page chains login on success
  //    so the backend issues `__Host-RunCoach` + the antiforgery pair.
  await page.goto('/register')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(VALID_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()

  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-page')).toBeVisible()

  // 2. Reload to prove the session cookie survives a hard refresh.
  await page.reload()
  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-page')).toBeVisible()

  // 3. Logout via API — no UI sign-out control exists yet. Echoes the
  //    SPA-readable antiforgery cookie in `X-XSRF-TOKEN` per DEC-054 / DEC-055.
  const xsrfCookie = (await context.cookies()).find((cookie) => cookie.name === XSRF_COOKIE)
  if (xsrfCookie === undefined) {
    throw new Error('antiforgery double-submit cookie must be present after login')
  }
  const logoutResponse = await page.request.post('/api/v1/auth/logout', {
    headers: { [XSRF_HEADER]: xsrfCookie.value },
  })
  expect(logoutResponse.status()).toBe(204)

  // 4. Fresh `goto('/')` exercises the cookie posture, not the in-memory
  //    auth slice — `<RequireAuth>` must redirect to `/login`.
  await page.goto('/')
  await expect(page).toHaveURL('/login')

  // 5. Session cookie must either be absent or already expired — some
  //    browsers retain `__Host-RunCoach` with a zeroed `Max-Age` rather
  //    than removing it outright.
  const cookies = await context.cookies()
  const sessionCookie = cookies.find((cookie) => cookie.name === SESSION_COOKIE)
  if (sessionCookie === undefined) {
    expect(sessionCookie).toBeUndefined()
  } else {
    // `expires` is a Unix timestamp in seconds or -1 for session cookies.
    expect(sessionCookie.expires).toBeGreaterThan(0)
    expect(sessionCookie.expires * 1000).toBeLessThan(Date.now())
  }
})
