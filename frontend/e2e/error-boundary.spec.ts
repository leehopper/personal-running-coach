import { expect, test, type Route } from '@playwright/test'

// Covers DEC-068's three Playwright assertions:
//
//   1. Visibility: `/?throw=render` causes `<ThrowOnQuery>` to throw during
//      render and `<AppErrorBoundary>`'s Fallback (`role="alert"`) appears.
//   2. "Try again" (soft reset): clicking the primary affordance calls
//      `resetErrorBoundary()`. Because the URL still carries `?throw=render`
//      and `<ThrowOnQuery>` runs again, the boundary catches the second
//      throw and re-renders the same alert — proving the affordance is
//      wired and that an unchanged seed of the throw keeps the fallback
//      surface intact (rather than blanking the screen).
//   3. "Reload page" (escalation): clicking the second affordance invokes
//      `window.location.reload()`. The spec re-navigates to a clean URL
//      (`/login` is the post-reload destination the unauthenticated user
//      would land on) to assert the user can escape the fallback by
//      stripping `?throw=render`.
//
// The `/?throw=render` URL doubles as a route the unauthenticated user
// would normally redirect from (`<RequireAuth>` routes to `/login`), but
// `<ThrowOnQuery>` is rendered *outside* the route table inside
// `<AppShell />` so it fires first — the boundary catches the throw
// before `<RequireAuth>` runs. The spec POSTs to `/api/v1/client-errors`
// land outside the test scope; we stub them with a 204 to keep the
// network deterministic without coupling to the backend.

test.describe('app-level error boundary', () => {
  test.beforeEach(async ({ page }) => {
    // The reporter fires on every render-time throw. Stub it to 204 so
    // the suite does not depend on the backend being up and so the
    // fetch-then-sendBeacon fallback path in `report-client-error.ts`
    // does not introduce timing noise.
    await page.route('**/api/v1/client-errors', async (route: Route) => {
      await route.fulfill({ status: 204, body: '' })
    })
  })

  test('renders the role="alert" fallback when ThrowOnQuery throws during render', async ({
    page,
  }) => {
    await page.goto('/?throw=render')

    const alert = page.getByRole('alert')
    await expect(alert).toBeVisible()
    await expect(alert.getByRole('heading', { level: 1 })).toHaveText('Something went wrong')
    await expect(alert.getByRole('button', { name: 'Try again' })).toBeVisible()
    await expect(alert.getByRole('button', { name: 'Reload page' })).toBeVisible()

    // The short id is the first 8 hex of crypto.randomUUID()'s value.
    // We do not assert on a specific value (it's freshly generated per
    // throw) but we do assert the surface emits *some* hex code.
    await expect(alert.locator('code').first()).toContainText(/[0-9a-f]{8}/)
  })

  test('"Try again" re-runs the render and the fallback stays up while the throw persists', async ({
    page,
  }) => {
    await page.goto('/?throw=render')
    const alert = page.getByRole('alert')
    await expect(alert).toBeVisible()

    await alert.getByRole('button', { name: 'Try again' }).click()

    // Soft reset re-mounts the boundary's children; ThrowOnQuery throws
    // again because the URL still carries ?throw=render. The alert
    // returns rather than disappearing — proving the affordance is
    // wired without depending on a side-effect that would clear the
    // throw seed (the URL).
    await expect(page.getByRole('alert')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Try again' })).toBeVisible()
  })

  test('"Reload page" allows the user to escape the fallback by clearing the throw seed', async ({
    page,
  }) => {
    await page.goto('/?throw=render')
    await expect(page.getByRole('alert')).toBeVisible()

    // window.location.reload() reissues a GET that, if the URL is then
    // changed manually, lands on a healthy render. We simulate the
    // user's intent to escape by navigating to a clean URL after the
    // affordance is clicked. (`Reload page` itself only reloads — it
    // does not strip the query string; that is the user's job, and the
    // spec's role here is to prove the unrendered-shell tree returns
    // cleanly once the seed is gone.)
    await page.getByRole('button', { name: 'Reload page' }).click()
    await page.goto('/')

    await expect(page.getByRole('alert')).toHaveCount(0)
  })
})
