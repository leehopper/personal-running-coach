import { expect, test } from '@playwright/test'

// The inline no-flash IIFE in `index.html` runs before React boots and stamps
// `.dark` or `.light` on `<html>` from `localStorage.getItem('runcoach-theme')`,
// falling back to `prefers-color-scheme` (system) and finally to `light` when
// storage throws. This spec exercises each branch directly against the dev
// server's root document — no auth, no API, no React state — so the storage
// key literal below is intentionally hard-coded to mirror the markup and
// catch any drift between the IIFE and `theme-provider.tsx`.
const STORAGE_KEY = 'runcoach-theme'

test.describe('no-flash theme script', () => {
  test('applies `dark` class before React boots when localStorage holds dark', async ({ page }) => {
    await page.addInitScript((key: string) => {
      localStorage.setItem(key, 'dark')
    }, STORAGE_KEY)

    await page.goto('/')

    const isDark = await page.evaluate(() => document.documentElement.classList.contains('dark'))
    const isLight = await page.evaluate(() => document.documentElement.classList.contains('light'))
    expect(isDark).toBe(true)
    expect(isLight).toBe(false)
  })

  test('applies `light` class when localStorage holds light', async ({ page }) => {
    await page.addInitScript((key: string) => {
      localStorage.setItem(key, 'light')
    }, STORAGE_KEY)

    await page.goto('/')

    const isLight = await page.evaluate(() => document.documentElement.classList.contains('light'))
    const isDark = await page.evaluate(() => document.documentElement.classList.contains('dark'))
    expect(isLight).toBe(true)
    expect(isDark).toBe(false)
  })

  test('falls back to OS preference when no value is stored', async ({ page }) => {
    // No seeded value — IIFE must consult `matchMedia('(prefers-color-scheme: dark)')`.
    await page.emulateMedia({ colorScheme: 'dark' })

    await page.goto('/')

    const isDark = await page.evaluate(() => document.documentElement.classList.contains('dark'))
    expect(isDark).toBe(true)
  })

  test('falls back to system when localStorage holds an invalid value', async ({ page }) => {
    await page.addInitScript((key: string) => {
      localStorage.setItem(key, 'not-a-theme')
    }, STORAGE_KEY)
    await page.emulateMedia({ colorScheme: 'light' })

    await page.goto('/')

    const isLight = await page.evaluate(() => document.documentElement.classList.contains('light'))
    const isDark = await page.evaluate(() => document.documentElement.classList.contains('dark'))
    expect(isLight).toBe(true)
    expect(isDark).toBe(false)
  })

  test('falls back to light when localStorage throws', async ({ page }) => {
    // Replace the `localStorage` accessor so any read throws — exercises the
    // IIFE's catch block, which is the storage-disabled fallback contract.
    await page.addInitScript(() => {
      Object.defineProperty(window, 'localStorage', {
        configurable: true,
        get() {
          throw new Error('disabled')
        },
      })
    })

    await page.goto('/')

    const isLight = await page.evaluate(() => document.documentElement.classList.contains('light'))
    expect(isLight).toBe(true)
  })
})
