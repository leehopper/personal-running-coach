import type { ReactElement } from 'react'
import { SegmentedControl, SegmentedControlItem } from '@/components/ui/segmented-control'
import { useTheme, type Theme } from '@/components/theme-context'

// The three selectable options, in display order. `value` is the literal
// stored to localStorage by `ThemeProvider.setTheme`; `label` is the
// user-facing copy.
const THEME_OPTIONS: ReadonlyArray<{ value: Theme; label: string; testId: string }> = [
  { value: 'dark', label: 'Dark', testId: 'theme-option-dark' },
  { value: 'light', label: 'Light', testId: 'theme-option-light' },
  { value: 'system', label: 'System', testId: 'theme-option-system' },
]

const isTheme = (value: string): value is Theme =>
  THEME_OPTIONS.some((option) => option.value === value)

/**
 * 3-state appearance control for the Settings page (DEC-070). Reads and
 * writes the active theme through `useTheme()`, so selecting an option
 * toggles the `.dark`/`.light` class on `documentElement` immediately —
 * the whole app re-themes with no reload — and persists the choice to
 * localStorage for the no-flash script to pick up on the next load.
 *
 * "System" hands theming back to the OS `prefers-color-scheme`.
 */
export const ThemeToggle = (): ReactElement => {
  const { theme, setTheme } = useTheme()

  return (
    <SegmentedControl
      value={theme}
      onValueChange={(value) => {
        if (isTheme(value)) setTheme(value)
      }}
      aria-label="Appearance"
      data-testid="settings-theme-toggle"
      className="grid grid-cols-3 gap-2"
    >
      {THEME_OPTIONS.map((option) => (
        <SegmentedControlItem key={option.value} value={option.value} data-testid={option.testId}>
          {option.label}
        </SegmentedControlItem>
      ))}
    </SegmentedControl>
  )
}
