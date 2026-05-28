import { act, render, renderHook, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ThemeProvider } from './theme-provider'
import { useTheme } from './theme-context'
import type { ReactNode } from 'react'

const STORAGE_KEY = 'runcoach-theme'

// jsdom ships no `matchMedia`; the ThemeProvider needs it to resolve the
// `system` preference. This mock lets each test pin the OS preference.
let prefersDark = false
const mediaListeners = new Set<() => void>()

const installMatchMedia = () => {
  vi.stubGlobal(
    'matchMedia',
    vi.fn((query: string) => ({
      matches: query.includes('dark') ? prefersDark : false,
      media: query,
      addEventListener: (_: string, cb: () => void) => mediaListeners.add(cb),
      removeEventListener: (_: string, cb: () => void) => mediaListeners.delete(cb),
      addListener: (cb: () => void) => mediaListeners.add(cb),
      removeListener: (cb: () => void) => mediaListeners.delete(cb),
      dispatchEvent: () => false,
      onchange: null,
    })),
  )
}

const setOsPreference = (dark: boolean) => {
  prefersDark = dark
  act(() => {
    mediaListeners.forEach((cb) => cb())
  })
}

const wrapper =
  (defaultTheme?: 'light' | 'dark' | 'system') =>
  ({ children }: { children: ReactNode }) => (
    <ThemeProvider defaultTheme={defaultTheme}>{children}</ThemeProvider>
  )

describe('ThemeProvider', () => {
  beforeEach(() => {
    prefersDark = false
    mediaListeners.clear()
    localStorage.clear()
    document.documentElement.className = ''
    installMatchMedia()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('defaults to system and resolves to light when the OS prefers light', () => {
    const { result } = renderHook(() => useTheme(), { wrapper: wrapper() })

    expect(result.current.theme).toBe('system')
    expect(result.current.resolvedTheme).toBe('light')
    expect(document.documentElement.classList.contains('light')).toBe(true)
    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })

  it('resolves system to dark when the OS prefers dark', () => {
    prefersDark = true
    const { result } = renderHook(() => useTheme(), { wrapper: wrapper() })

    expect(result.current.resolvedTheme).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('applies an explicit light/dark choice to documentElement', () => {
    const { result } = renderHook(() => useTheme(), { wrapper: wrapper() })

    act(() => result.current.setTheme('dark'))
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(result.current.resolvedTheme).toBe('dark')

    act(() => result.current.setTheme('light'))
    expect(document.documentElement.classList.contains('light')).toBe(true)
    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })

  it('persists the chosen theme to localStorage', () => {
    const { result } = renderHook(() => useTheme(), { wrapper: wrapper() })

    act(() => result.current.setTheme('dark'))
    expect(localStorage.getItem(STORAGE_KEY)).toBe('dark')
  })

  it('persists chosen theme even when localStorage.setItem throws', () => {
    const spy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('QuotaExceededError')
    })
    const { result } = renderHook(() => useTheme(), { wrapper: wrapper() })

    act(() => result.current.setTheme('dark'))

    expect(result.current.theme).toBe('dark')
    expect(result.current.resolvedTheme).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    spy.mockRestore()
  })

  it('reads the persisted theme on mount', () => {
    localStorage.setItem(STORAGE_KEY, 'dark')
    const { result } = renderHook(() => useTheme(), { wrapper: wrapper() })

    expect(result.current.theme).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('falls back to defaultTheme when localStorage holds an invalid value', () => {
    localStorage.setItem(STORAGE_KEY, 'not-a-theme')
    const { result } = renderHook(() => useTheme(), { wrapper: wrapper() })

    expect(result.current.theme).toBe('system')
  })

  it('falls back to defaultTheme when localStorage.getItem throws', () => {
    const spy = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('SecurityError')
    })
    const { result } = renderHook(() => useTheme(), { wrapper: wrapper() })

    expect(result.current.theme).toBe('system')
    spy.mockRestore()
  })

  it('follows OS preference changes while on system', () => {
    const { result } = renderHook(() => useTheme(), { wrapper: wrapper() })
    expect(result.current.resolvedTheme).toBe('light')

    setOsPreference(true)
    expect(result.current.resolvedTheme).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('removes the matchMedia listener on unmount', () => {
    const { unmount } = renderHook(() => useTheme(), { wrapper: wrapper() })
    expect(mediaListeners.size).toBeGreaterThanOrEqual(1)

    unmount()
    expect(mediaListeners.size).toBe(0)
  })

  it('ignores OS preference changes once an explicit theme is set', () => {
    const { result } = renderHook(() => useTheme(), { wrapper: wrapper() })

    act(() => result.current.setTheme('light'))
    setOsPreference(true)

    expect(result.current.resolvedTheme).toBe('light')
    expect(document.documentElement.classList.contains('light')).toBe(true)
  })

  it('re-subscribes to OS preference when returning to system mode', () => {
    const { result } = renderHook(() => useTheme(), { wrapper: wrapper() })

    act(() => result.current.setTheme('light'))
    act(() => result.current.setTheme('system'))

    setOsPreference(true)

    expect(result.current.resolvedTheme).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('renders its children', () => {
    render(
      <ThemeProvider>
        <span>themed content</span>
      </ThemeProvider>,
    )
    expect(screen.getByText('themed content')).toBeInTheDocument()
  })
})

describe('useTheme', () => {
  it('throws when used outside a ThemeProvider', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => renderHook(() => useTheme())).toThrow(
      'useTheme must be used within a <ThemeProvider>',
    )
    spy.mockRestore()
  })
})
