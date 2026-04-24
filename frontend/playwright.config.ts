import { defineConfig, devices } from '@playwright/test'

// Vite dev server runs on HTTPS:5173 (see vite.config.ts and
// CONTRIBUTING.md "Local HTTPS is required"). The `__Host-` cookie
// contract requires HTTPS end to end, so Playwright must also speak
// HTTPS and tolerate the self-signed dev cert that
// @vitejs/plugin-basic-ssl mints on each startup.
const baseURL = 'https://localhost:5173'

export default defineConfig({
  testDir: './e2e',
  // Keep scenarios independent so they run in parallel by default; the
  // auth.spec.ts file opts into `describe.serial` where order matters.
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL,
    trace: 'on-first-retry',
    ignoreHTTPSErrors: true,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  // The backend API on https://localhost:5001 must already be running
  // (`dotnet run --project backend/src/RunCoach.Api --launch-profile
  // https`); Playwright only owns the Vite dev server here. See
  // CONTRIBUTING.md "Running the frontend" for the full local recipe.
  webServer: {
    command: 'npm run dev',
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    ignoreHTTPSErrors: true,
  },
})
