import { createContext, useContext } from 'react'

// Context, types, and the `useTheme` hook for the dark-mode controller.
// Kept separate from `theme-provider.tsx` so that file exports only the
// `ThemeProvider` component — React Fast Refresh requires a component
// file to export components and nothing else.

export type Theme = 'light' | 'dark' | 'system'

export interface ThemeProviderState {
  /** The user's choice — may be `system`. */
  theme: Theme
  /** The concrete mode currently applied, `system` already resolved. */
  resolvedTheme: 'light' | 'dark'
  setTheme: (theme: Theme) => void
}

export const ThemeProviderContext = createContext<ThemeProviderState | null>(null)

export const useTheme = (): ThemeProviderState => {
  const context = useContext(ThemeProviderContext)
  if (context === null) {
    throw new Error('useTheme must be used within a <ThemeProvider>')
  }
  return context
}
