import type { ReactElement } from 'react'
import { toast } from 'sonner'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Label } from '@/components/ui/label'
import { PreferredUnits } from '~/api/generated'
import { useGetUnitPreferenceQuery, usePutUnitPreferenceMutation } from '~/api/settings.api'
import { reportClientError } from '~/error-boundary/report-client-error'
import { parsePreferredUnits, UNIT_OPTIONS } from '~/modules/common/utils/unit-options'

/**
 * 2-state distance-unit control for the Settings page (DEC-086). Reads the
 * persisted preference via `getUnitPreference` and writes the new choice with
 * `putUnitPreference`, whose `UserSettings` tag invalidation refetches the
 * query. While the preference is still loading it falls back to Kilometers so
 * the control always renders with a selection.
 *
 * This preference drives the plan render tree (`TodayCard`, `UpcomingList`,
 * `MesoWeekBlock`, `MicroWorkoutCard`) via `usePreferredUnits`. The
 * logging/history and adaptation surfaces remain km-only.
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
