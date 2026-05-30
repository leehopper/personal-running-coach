import type { ReactElement } from 'react'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Label } from '@/components/ui/label'
import { useTheme, type Theme } from '@/components/theme-context'

// The three selectable options, in display order. `value` is the literal
// stored to localStorage by `ThemeProvider.setTheme`; `label` is the
// user-facing copy.
const THEME_OPTIONS: ReadonlyArray<{ value: Theme; label: string }> = [
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' },
  { value: 'system', label: 'System' },
]

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
    <RadioGroup
      value={theme}
      onValueChange={(value) => setTheme(value as Theme)}
      aria-label="Appearance"
      data-testid="settings-theme-toggle"
      className="mt-2 grid-cols-3 gap-2"
    >
      {THEME_OPTIONS.map((option) => (
        <Label
          key={option.value}
          htmlFor={`theme-option-${option.value}`}
          className="flex cursor-pointer items-center gap-2 rounded-md border border-input p-3 transition-colors has-[:checked]:border-primary has-[:checked]:bg-accent motion-reduce:transition-none"
        >
          <RadioGroupItem id={`theme-option-${option.value}`} value={option.value} />
          {option.label}
        </Label>
      ))}
    </RadioGroup>
  )
}
