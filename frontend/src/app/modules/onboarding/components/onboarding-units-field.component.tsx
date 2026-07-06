import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Label } from '@/components/ui/label'
import { PreferredUnits } from '~/api/generated'
import { parsePreferredUnits, UNIT_OPTIONS } from '~/modules/common/utils/unit-options'

export interface OnboardingUnitsFieldProps {
  units: PreferredUnits
  /** Fired with the newly-chosen unit; the page persists it and re-seeds distances. */
  onChange: (units: PreferredUnits) => void
  /** Disabled while a prior unit change is still persisting, to swallow no correction click. */
  disabled?: boolean
}

/**
 * The units-first control (DEC-086): the runner picks km or miles before the
 * numeric fields, so every distance input speaks their unit from the start. It
 * is presentational — the enclosing page owns persistence
 * (`PUT /settings/units`) and the distance-field re-seed on change, so the
 * standalone Settings toggle stays the only place that reads/writes directly.
 * Mirrors `units-toggle.component.tsx`'s control shape.
 */
export const OnboardingUnitsField = ({
  units,
  onChange,
  disabled = false,
}: OnboardingUnitsFieldProps) => (
  <fieldset className="flex flex-col gap-2" disabled={disabled}>
    <legend className="text-sm font-medium">Which units do you think in?</legend>
    <RadioGroup
      value={String(units)}
      onValueChange={(value) => onChange(parsePreferredUnits(value))}
      disabled={disabled}
      aria-label="Units"
      data-testid="onboarding-units-field"
      className="grid-cols-2 gap-2"
    >
      {UNIT_OPTIONS.map((option) => (
        <Label
          key={option.value}
          htmlFor={`onboarding-units-${option.value}`}
          className="flex items-center gap-2 rounded-md border border-input p-3 font-normal transition-colors has-[:checked]:border-primary has-[:checked]:bg-accent motion-reduce:transition-none has-[:disabled]:cursor-not-allowed has-[:disabled]:opacity-60 [&:not(:has(:disabled))]:cursor-pointer"
        >
          <RadioGroupItem id={`onboarding-units-${option.value}`} value={String(option.value)} />
          {option.label}
        </Label>
      ))}
    </RadioGroup>
  </fieldset>
)
