import { SegmentedControl, SegmentedControlItem } from '@/components/ui/segmented-control'
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
 * The units-first control: the runner picks km or miles before numeric fields.
 * The enclosing page owns persistence and distance reseeding.
 */
export const OnboardingUnitsField = ({
  units,
  onChange,
  disabled = false,
}: OnboardingUnitsFieldProps) => (
  <fieldset className="flex flex-col gap-2" disabled={disabled}>
    <legend className="t-data-label text-foreground">Units</legend>
    <SegmentedControl
      value={String(units)}
      onValueChange={(value) => onChange(parsePreferredUnits(value))}
      disabled={disabled}
      aria-label="Units"
      data-testid="onboarding-units-field"
      className="grid grid-cols-2 gap-2"
    >
      {UNIT_OPTIONS.map((option) => (
        <SegmentedControlItem key={option.value} value={String(option.value)}>
          {option.label}
        </SegmentedControlItem>
      ))}
    </SegmentedControl>
  </fieldset>
)
