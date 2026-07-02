import type { ReactElement } from 'react'
import { toast } from 'sonner'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Label } from '@/components/ui/label'
import { PreferredUnits } from '~/api/generated'
import { useGetUnitPreferenceQuery, usePutUnitPreferenceMutation } from '~/api/settings.api'
import { reportClientError } from '~/error-boundary/report-client-error'

// Selectable options in display order. `value` is the `PreferredUnits` enum
// (`0 | 1` on the wire) stored server-side; `label` is the user-facing copy.
// `PreferredUnits.Kilometers`/`.Miles` come from the generated const-paired enum
// so this control never hard-codes the magic integers. Radix `RadioGroup` values
// are strings, so we map `String(value)` ↔ the enum at the group boundary.
const UNIT_OPTIONS: ReadonlyArray<{ value: PreferredUnits; label: string }> = [
  { value: PreferredUnits.Kilometers, label: 'Kilometers' },
  { value: PreferredUnits.Miles, label: 'Miles' },
]

// Radix `RadioGroup` hands `onValueChange` a raw string; resolve it back to a
// known `PreferredUnits` by matching an option rather than casting an
// unvalidated `Number()` into the `0 | 1` union. An unrecognized value falls
// back to Kilometers.
const parsePreferredUnits = (value: string): PreferredUnits =>
  UNIT_OPTIONS.find((option) => String(option.value) === value)?.value ?? PreferredUnits.Kilometers

/**
 * 2-state distance-unit control for the Settings page (DEC-086). Reads the
 * persisted preference via `getUnitPreference` and writes the new choice with
 * `putUnitPreference`, whose `UserSettings` tag invalidation refetches the
 * query. While the preference is still loading it falls back to Kilometers so
 * the control always renders with a selection.
 *
 * This control only records the preference. It does not affect how distances or
 * paces are currently rendered elsewhere in the app.
 */
export const UnitsToggle = (): ReactElement => {
  const { data } = useGetUnitPreferenceQuery(undefined)
  const [putUnitPreference] = usePutUnitPreferenceMutation()
  const current = data?.preferredUnits ?? PreferredUnits.Kilometers

  // Await `.unwrap()` so a failed PUT is a *handled* rejection we can surface:
  // the bare trigger promise resolves either way and would swallow the error,
  // and the success-gated invalidation means a failure leaves the toggle on the
  // persisted unit with no other signal. Mirrors the log/coach-chat convention.
  const persistPreference = async (preferredUnits: PreferredUnits): Promise<void> => {
    try {
      await putUnitPreference({ preferredUnits }).unwrap()
    } catch (error) {
      reportClientError({
        kind: 'unhandled-rejection',
        error: error instanceof Error ? error : new Error(String(error)),
      })
      toast.error('We could not save your unit preference. Try again in a moment.')
    }
  }

  return (
    <RadioGroup
      value={String(current)}
      onValueChange={(value) => {
        void persistPreference(parsePreferredUnits(value))
      }}
      aria-label="Units"
      data-testid="settings-units-toggle"
      className="mt-2 grid-cols-2 gap-2"
    >
      {UNIT_OPTIONS.map((option) => (
        <Label
          key={option.value}
          htmlFor={`units-option-${option.value}`}
          className="flex cursor-pointer items-center gap-2 rounded-md border border-input p-3 transition-colors has-[:checked]:border-primary has-[:checked]:bg-accent motion-reduce:transition-none"
        >
          <RadioGroupItem id={`units-option-${option.value}`} value={String(option.value)} />
          {option.label}
        </Label>
      ))}
    </RadioGroup>
  )
}
