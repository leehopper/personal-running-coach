import { randomUUID } from 'node:crypto'
import { expect, test, type BrowserContext, type Page, type Route } from '@playwright/test'

const SESSION_COOKIE = '__Host-RunCoach'
const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

// eslint-disable-next-line sonarjs/no-hardcoded-passwords
const VALID_PASSWORD = 'Correct-Horse-9!'

const installSettingsStubs = async (page: Page): Promise<void> => {
  await page.route('**/api/v1/onboarding/state', async (route: Route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        userId: '00000000-0000-0000-0000-000000000001',
        status: 2,
        completedTopics: 6,
        totalTopics: 6,
        isComplete: true,
        currentTopic: null,
        outstandingClarifications: [],
        primaryGoal: null,
        targetEvent: null,
        currentFitness: null,
        weeklySchedule: null,
        injuryHistory: null,
        preferences: null,
        currentPlanId: null,
      }),
    })
  })

  await page.route('**/api/v1/plan/current', async (route: Route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        planId: '00000000-0000-0000-0000-000000000002',
        userId: '00000000-0000-0000-0000-000000000001',
        generatedAt: '2026-06-29T12:00:00Z',
        planStartDate: '2026-06-29',
        previousPlanId: null,
        targetEventName: null,
        targetEventDistanceKm: null,
        targetEventDate: null,
        promptVersion: 'plan-generation-v1',
        modelId: 'claude-sonnet-4-6',
        macro: null,
        mesoWeeks: [],
        microWorkoutsByWeek: {},
      }),
    })
  })
}

const register = async (page: Page, email: string): Promise<void> => {
  await page.goto('/register')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password', { exact: true }).fill(VALID_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()
  await expect(page).toHaveURL('/')
}

const expectSessionCleared = async (context: BrowserContext): Promise<void> => {
  const sessionCookie = (await context.cookies()).find((cookie) => cookie.name === SESSION_COOKIE)
  if (sessionCookie === undefined) {
    expect(sessionCookie).toBeUndefined()
  } else {
    expect(sessionCookie.expires).toBeGreaterThan(0)
    expect(sessionCookie.expires * 1000).toBeLessThan(Date.now())
  }
}

test('register -> Settings -> sign out clears the session', async ({ page, context }) => {
  await installSettingsStubs(page)
  const email = uniqueEmail()
  await register(page, email)

  await page.goto('/settings')
  await expect(page.getByTestId('settings-account-email')).toHaveText(email)
  await page.getByTestId('settings-sign-out-button').click()

  await expect(page).toHaveURL('/login')
  await expect(page.getByTestId('tab-bar')).not.toBeVisible()
  await expectSessionCleared(context)
})

test('sign out broadcasts to a second tab', async ({ page, context }) => {
  const secondPage = await context.newPage()
  await installSettingsStubs(page)
  await installSettingsStubs(secondPage)
  const email = uniqueEmail()
  await register(page, email)

  await page.goto('/settings')
  await secondPage.goto('/settings')
  await expect(secondPage.getByTestId('settings-account-email')).toHaveText(email)

  await page.getByTestId('settings-sign-out-button').click()
  await expect(page).toHaveURL('/login')
  await expect(secondPage).toHaveURL('/login')
})
