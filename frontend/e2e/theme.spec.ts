import { expect, test } from '@playwright/test'
import { STORAGE_KEY } from '../src/components/theme-provider'

// The inline no-flash IIFE in `index.html` runs before React boots and stamps
// `.dark` or `.light` on `<html>` from `localStorage.getItem('runcoach-theme')`,
// falling back to `prefers-color-scheme` (system). When storage throws it
// ALSO consults `prefers-color-scheme` (so it agrees with ThemeProvider's
// storage-throw→`system` fallback and never flashes), and only defaults to
// `dark` (the app's default polarity) when no OS signal is available. This
// spec exercises each branch directly against the dev server's root
// document — no auth, no API, no React state.

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

  // When storage throws, the IIFE follows the OS preference (agreeing with
  // ThemeProvider's `system` fallback) rather than hardcoding a mode — this
  // is what prevents a paint-dark-then-flip-light flash. Both OS branches
  // are asserted so a regression to an unconditional mode is caught.
  const throwOnStorage = () => {
    Object.defineProperty(window, 'localStorage', {
      configurable: true,
      get() {
        throw new Error('disabled')
      },
    })
  }

  test('follows the OS (dark) when localStorage throws', async ({ page }) => {
    await page.addInitScript(throwOnStorage)
    await page.emulateMedia({ colorScheme: 'dark' })

    await page.goto('/')

    const isDark = await page.evaluate(() => document.documentElement.classList.contains('dark'))
    const isLight = await page.evaluate(() => document.documentElement.classList.contains('light'))
    expect(isDark).toBe(true)
    expect(isLight).toBe(false)
  })

  test('follows the OS (light) when localStorage throws — no flash', async ({ page }) => {
    await page.addInitScript(throwOnStorage)
    await page.emulateMedia({ colorScheme: 'light' })

    await page.goto('/')

    const isLight = await page.evaluate(() => document.documentElement.classList.contains('light'))
    const isDark = await page.evaluate(() => document.documentElement.classList.contains('dark'))
    expect(isLight).toBe(true)
    expect(isDark).toBe(false)
  })
})
