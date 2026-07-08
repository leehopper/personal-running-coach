import { randomUUID } from 'node:crypto'
import { expect, test, type Page, type Route } from '@playwright/test'

// SPLIT/Alpine Slice 1 — TabBar presence/absence per spec § Quality
// requirements: "add a spec asserting TabBar presence on a shell route and
// absence on /login."

// eslint-disable-next-line sonarjs/no-hardcoded-passwords -- static E2E fixture password (matches the sibling e2e specs), not a real credential
const VALID_PASSWORD = 'Correct-Horse-9!'
const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

const OnboardingStatus = { Completed: 2 } as const
const completedPlanId = '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b7777'
const userId = '00000000-0000-0000-0000-000000000099'

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

  // A 404 plan is enough for the shell/TabBar to render — the home page's
  // "no plan yet" defensive state still mounts inside `ShellLayout`.
  await page.route('**/api/v1/plan/current', async (route: Route) => {
    await route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ type: 'about:blank', title: 'Not Found', status: 404 }),
    })
  })
}

test('the TabBar renders on a shell route and is absent on /login', async ({ page }) => {
  await installNavigationStubs(page)

  await page.goto('/register')
  await page.getByLabel('Email').fill(uniqueEmail())
  await page.getByLabel('Password').fill(VALID_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()
  await expect(page).toHaveURL('/')

  const tabBar = page.getByTestId('tab-bar')
  await expect(tabBar).toBeVisible()
  await expect(page.getByTestId('tab-today')).toHaveAttribute('aria-current', 'page')
  await expect(page.getByTestId('tab-coach')).toBeVisible()
  await expect(page.getByTestId('tab-log')).toHaveAccessibleName('Log a workout')
  await expect(page.getByTestId('tab-history')).toBeVisible()
  await expect(page.getByTestId('tab-settings')).toBeVisible()

  // Navigating within the shell keeps the bar mounted and moves the current tab.
  await page.getByTestId('tab-settings').click()
  await expect(page).toHaveURL('/settings')
  await expect(tabBar).toBeVisible()
  await expect(page.getByTestId('tab-settings')).toHaveAttribute('aria-current', 'page')

  // Auth surfaces sit outside the shell entirely — no TabBar there.
  await page.context().clearCookies()
  await page.goto('/login')
  await expect(page).toHaveURL('/login')
  await expect(page.getByTestId('tab-bar')).not.toBeVisible()
})
