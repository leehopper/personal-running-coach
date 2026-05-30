import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ThemeProvider } from '@/components/theme-provider'
import { ThemeToggle } from './theme-toggle.component'

const STORAGE_KEY = 'runcoach-theme'

// jsdom ships no `matchMedia`; ThemeProvider needs it to resolve `system`.
let prefersDark = false

const installMatchMedia = () => {
  vi.stubGlobal(
    'matchMedia',
    vi.fn((query: string) => ({
      matches: query.includes('dark') ? prefersDark : false,
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: () => false,
      onchange: null,
    })),
  )
}

const renderToggle = (children: ReactNode = <ThemeToggle />) =>
  render(<ThemeProvider>{children}</ThemeProvider>)

describe('ThemeToggle', () => {
  beforeEach(() => {
    prefersDark = false
    localStorage.clear()
    document.documentElement.className = ''
    installMatchMedia()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders three selectable options: Light, Dark, and System', () => {
    renderToggle()
    expect(screen.getByRole('radio', { name: 'Light' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Dark' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'System' })).toBeInTheDocument()
  })

  it('highlights the active option from the stored preference', () => {
    localStorage.setItem(STORAGE_KEY, 'dark')
    renderToggle()
    expect(screen.getByRole('radio', { name: 'Dark' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Light' })).not.toBeChecked()
  })

  it('defaults the System option as active when nothing is stored', () => {
    renderToggle()
    expect(screen.getByRole('radio', { name: 'System' })).toBeChecked()
  })

  it('switches the app to dark mode immediately when Dark is selected', async () => {
    renderToggle()
    await userEvent.click(screen.getByRole('radio', { name: 'Dark' }))
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(document.documentElement.classList.contains('light')).toBe(false)
  })

  it('switches the app to light mode immediately when Light is selected', async () => {
    localStorage.setItem(STORAGE_KEY, 'dark')
    renderToggle()
    await userEvent.click(screen.getByRole('radio', { name: 'Light' }))
    expect(document.documentElement.classList.contains('light')).toBe(true)
    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })

  it('persists the selected option to localStorage', async () => {
    renderToggle()
    await userEvent.click(screen.getByRole('radio', { name: 'Dark' }))
    expect(localStorage.getItem(STORAGE_KEY)).toBe('dark')
  })

  it('restores OS-driven theming and stores "system" when System is selected', async () => {
    prefersDark = true
    localStorage.setItem(STORAGE_KEY, 'light')
    renderToggle()
    await userEvent.click(screen.getByRole('radio', { name: 'System' }))
    expect(localStorage.getItem(STORAGE_KEY)).toBe('system')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })
})
