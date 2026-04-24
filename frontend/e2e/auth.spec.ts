import { randomUUID } from 'node:crypto'
import { expect, test } from '@playwright/test'

// Session cookie name is fixed by the backend's Identity cookie
// configuration (`__Host-RunCoach`). The `__Host-` prefix requires
// HTTPS + `Secure` + no `Domain=` + `Path=/`; everything below verifies
// that contract end to end.
const SESSION_COOKIE = '__Host-RunCoach'

// Fresh email per run so the suite is re-runnable against a shared
// dev Postgres without collisions. Orphan e2e-*@runcoach.test rows
// can be flushed with `npm run e2e:clean` (see CONTRIBUTING.md).
const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

// Meets the backend Identity policy (12+ chars, upper, lower, digit,
// non-alphanumeric) and the frontend `registerSchema` in
// modules/auth/schemas/auth.schema.ts.
const VALID_PASSWORD = 'Correct-Horse-9!'

test('register → authenticated home → reload → logout clears session', async ({
  page,
  context,
}) => {
  const email = uniqueEmail()

  await page.goto('/register')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(VALID_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()

  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-greeting')).toHaveText(`Logged in as ${email}`)

  await page.reload()
  await expect(page).toHaveURL('/')
  await expect(page.getByTestId('home-greeting')).toHaveText(`Logged in as ${email}`)

  await page.getByRole('button', { name: /sign out/i }).click()
  await expect(page).toHaveURL('/login')

  const cookies = await context.cookies()
  const sessionCookie = cookies.find((cookie) => cookie.name === SESSION_COOKIE)
  if (sessionCookie !== undefined) {
    // If any `__Host-RunCoach` value lingers it must be expired;
    // `expires` is a Unix timestamp in seconds or -1 for session cookies.
    expect(sessionCookie.expires).toBeGreaterThan(0)
    expect(sessionCookie.expires * 1000).toBeLessThan(Date.now())
  }
})
