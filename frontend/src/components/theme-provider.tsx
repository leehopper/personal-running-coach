import { useCallback, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { ThemeProviderContext, type Theme } from './theme-context'

// Class-based dark/light mode (DEC-089). Dark is the default polarity:
// `index.css`'s `:root` carries the dark Alpine ramp and `.light` is the
// override, but the CLASS mechanism is unchanged from DEC-070 — this
// provider still toggles exactly one of `.dark` / `.light` on
// `documentElement`, and `index.css`'s `dark:` variant still keys off the
// `.dark` class (`@custom-variant dark (&:is(.dark *))`, unchanged).
// `system` follows the OS `prefers-color-scheme`; `light`/`dark` are
// explicit in-app overrides. The choice is persisted to `localStorage` so
// it survives reloads, and the no-flash script in `index.html` reads the
// same key to set the class before first paint (falling back to `dark`,
// not `light`, when storage throws).
//
// The context object and the `useTheme` hook live in `theme-context.ts`
// so this file's only export is the `ThemeProvider` component (keeps
// React Fast Refresh happy).

// Exported so the spec can pin it; the inline no-flash script in `index.html`
// mirrors the same literal and is pinned by a drift assertion in
// `theme-provider.spec.tsx`.
export const STORAGE_KEY = 'runcoach-theme'

const isTheme = (value: string | null): value is Theme =>
  value === 'light' || value === 'dark' || value === 'system'

const readStoredTheme = (defaultTheme: Theme): Theme => {
  if (typeof localStorage === 'undefined') return defaultTheme
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    return isTheme(stored) ? stored : defaultTheme
  } catch {
    return defaultTheme
  }
}

const systemPrefersDark = (): boolean =>
  typeof window !== 'undefined' && window.matchMedia('(prefers-color-scheme: dark)').matches

const resolve = (theme: Theme): 'light' | 'dark' => {
  if (theme === 'system') return systemPrefersDark() ? 'dark' : 'light'
  return theme
}

interface ThemeProviderProps {
  children: ReactNode
  /** Theme applied when nothing is stored. Defaults to `system`. */
  defaultTheme?: Theme
}

export const ThemeProvider = ({ children, defaultTheme = 'system' }: ThemeProviderProps) => {
  const [theme, setThemeState] = useState<Theme>(() => readStoredTheme(defaultTheme))
  const [resolvedTheme, setResolvedTheme] = useState<'light' | 'dark'>(() => resolve(theme))

  // Apply the resolved mode to `documentElement` whenever the choice
  // changes, and — when on `system` — when the OS preference flips.
  useEffect(() => {
    const root = document.documentElement

    const apply = () => {
      const mode = resolve(theme)
      root.classList.toggle('dark', mode === 'dark')
      root.classList.toggle('light', mode === 'light')
      setResolvedTheme(mode)
    }

    apply()

    if (theme !== 'system') return
    const media = window.matchMedia('(prefers-color-scheme: dark)')
    media.addEventListener('change', apply)
    return () => media.removeEventListener('change', apply)
  }, [theme])

  const setTheme = useCallback((next: Theme) => {
    try {
      localStorage.setItem(STORAGE_KEY, next)
    } catch {
      // localStorage can throw in private-mode / storage-disabled browsers;
      // the in-memory state below still drives the current session.
    }
    setThemeState(next)
  }, [])

  return (
    <ThemeProviderContext.Provider value={{ theme, resolvedTheme, setTheme }}>
      {children}
    </ThemeProviderContext.Provider>
  )
}
