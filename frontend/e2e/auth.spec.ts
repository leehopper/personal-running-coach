import { randomUUID } from 'node:crypto'
import { expect, test, type Page, type Route } from '@playwright/test'

// Auth e2e — register / session-persists / logout-clears-session.
//
// Slice 1 update (task #118): Slice 1's home redirect-guard
// (`OnboardingRedirectGuard`, `app.component.tsx`) consults
// `GET /api/v1/onboarding/state` after `<RequireAuth>` settles. A
// freshly-registered user has `OnboardingCompletedAt: null`, so the
// guard would now bounce the post-register navigation to `/onboarding`
// — breaking the original "land at `/`" assertion that pre-dated the
// guard.
//
// Strategy (option 2 per task #118): stub the onboarding state endpoint
// with a `Completed` shape so the guard treats this user as already
// onboarded and lets the route through to `/`. The auth e2e tests
// auth — not onboarding gating; `e2e/onboarding.spec.ts` covers the
// guard's incomplete branch end to end and `e2e/plan-render.spec.ts`
// covers the completed-and-plan-rendered branch. With this stub in
// place, the three e2e specs cover the three redirect branches.
//
// Plan-current is also stubbed so `HomePage` does not error on its
// `getCurrentPlan` mount-fetch — the home view is incidental here, the
// target of this spec is the auth contract.

// Session cookie name is fixed by the backend's Identity cookie
// configuration (`__Host-RunCoach`). The `__Host-` prefix requires
// HTTPS + `Secure` + no `Domain=` + `Path=/`; everything below verifies
// that contract end to end.
const SESSION_COOKIE = '__Host-RunCoach'

// SPA-readable half of the antiforgery double-submit pair (DEC-054).
// The SPA echoes its value in the `X-XSRF-TOKEN` header on mutating
// requests; this e2e does the same when it drives logout via the API
// (Slice 1 has no UI logout surface yet — see logout step below).
const XSRF_COOKIE = '__Host-Xsrf-Request'
const XSRF_HEADER = 'X-XSRF-TOKEN'

// Fresh email per run so the suite is re-runnable against a shared
// dev Postgres without collisions. Orphan e2e-*@runcoach.test rows
// can be flushed with `npm run e2e:clean` (see CONTRIBUTING.md).
const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

// Meets the backend Identity policy (12+ chars, upper, lower, digit,
// non-alphanumeric) and the frontend `registerSchema` in
// modules/auth/schemas/auth.schema.ts.
const VALID_PASSWORD = 'Correct-Horse-9!'

// Wire-format integer enum duplicated from
// `frontend/src/app/modules/onboarding/models/onboarding.model.ts` so
// the stub round-trips the exact JSON the page-level Zod schema
// accepts. Keeping the literal inline (vs. importing the model file)
// holds the e2e suite at arm's length from the implementation under
// test, matching the conventions in `onboarding.spec.ts` and
// `plan-render.spec.ts`.
const OnboardingStatus = { NotStarted: 0, InProgress: 1, Completed: 2 } as const

const completedPlanId = '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b1234'
const stubUserId = '00000000-0000-0000-0000-000000000001'

// Installs the two route stubs the home redirect-guard and home page
// fire on mount. Pulled out so the call site stays focused on the auth
// flow being tested; the stub shapes mirror the ones in
// `plan-render.spec.ts` so both specs assert against an identical
// post-completion contract.
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

  // Minimal plan projection so `HomePage` does not surface its error
  // branch on mount. The auth e2e does not assert anything about the
  // plan-render output; `plan-render.spec.ts` owns that contract.
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
  //    With the onboarding-state stub returning Completed, the home
  //    redirect-guard lets the post-login navigation through to `/`.
  await page.goto('/register')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(VALID_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()

  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-page')).toBeVisible()

  // 2. Reload to prove the session cookie survives a hard refresh.
  //    This is the canonical "session persists across navigation"
  //    contract from Slice 0 — Slice 1's redirect guard adds an extra
  //    `getOnboardingState` round-trip on the way through, but the
  //    landing URL must still be `/`.
  await page.reload()
  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-page')).toBeVisible()

  // 3. Logout. Slice 1 has not yet shipped a UI sign-out control
  //    (`useLogoutMutation` exists in `auth.api.ts` but no component
  //    consumes it), so this step drives logout via the API directly,
  //    echoing the SPA-readable antiforgery cookie in the
  //    `X-XSRF-TOKEN` header per DEC-054 / DEC-055. When the UI logout
  //    surface lands in a later slice this should switch back to a
  //    user-driven click on that control.
  const xsrfCookie = (await context.cookies()).find((cookie) => cookie.name === XSRF_COOKIE)
  if (xsrfCookie === undefined) {
    throw new Error('antiforgery double-submit cookie must be present after login')
  }
  const logoutResponse = await page.request.post('/api/v1/auth/logout', {
    headers: { [XSRF_HEADER]: xsrfCookie.value },
  })
  expect(logoutResponse.status()).toBe(204)

  // 4. After logout, the SPA's `<RequireAuth>` sees no session and
  //    redirects any protected-route navigation to `/login`. Use a
  //    fresh `goto('/')` (rather than relying on the previous tab's
  //    in-memory auth slice) so the assertion exercises the cookie
  //    posture, not the Redux state.
  await page.goto('/')
  await expect(page).toHaveURL('/login')

  // 5. Most backends remove the session cookie outright on logout, so
  //    the expected posture is `undefined`. If a `__Host-RunCoach` value
  //    lingers (some browsers retain it with a zeroed `Max-Age`), it
  //    MUST already be expired. Either shape counts as "session
  //    cleared"; neither is a silently-vacuous assertion the way a
  //    single guarded block would be.
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
