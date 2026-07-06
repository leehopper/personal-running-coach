import { Controller } from 'react-hook-form'
import { ToggleGroup } from 'radix-ui'

import { cn } from '@/lib/utils'
import {
  DAY_OPTIONS,
  type OnboardingFormControl,
} from '~/modules/onboarding/schemas/onboarding-form.schema'

export interface OnboardingDayToggleFieldProps {
  control: OnboardingFormControl
}

/**
 * Day-of-week preferred-run picker: a Radix `ToggleGroup type="multiple"` (roving
 * tabindex + arrow-key navigation, WCAG-labelled by the enclosing
 * `<fieldset>/<legend>`) wired through an RHF `Controller`. The value is the
 * `string[]` of selected day keys the schema maps to the seven named day
 * booleans. No new dependency — the primitive ships with the installed
 * `radix-ui` package (DEC-041 / R-085 R7: a day picker is not a calendar).
 */
export const OnboardingDayToggleField = ({ control }: OnboardingDayToggleFieldProps) => (
  <Controller
    control={control}
    name="days"
    render={({ field }) => (
      <fieldset className="flex flex-col gap-2">
        <legend className="text-sm font-medium">Which days can you usually run?</legend>
        <ToggleGroup.Root
          type="multiple"
          value={field.value}
          onValueChange={field.onChange}
          className="flex flex-wrap gap-2"
          aria-label="Preferred run days"
          data-testid="days-field"
        >
          {DAY_OPTIONS.map((option) => (
            <ToggleGroup.Item
              key={option.value}
              value={option.value}
              aria-label={option.label}
              className={cn(
                'inline-flex h-9 min-w-11 items-center justify-center rounded-md border border-input px-3 text-sm capitalize transition-colors motion-reduce:transition-none outline-none',
                'focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50',
                'data-[state=on]:border-primary data-[state=on]:bg-primary data-[state=on]:text-primary-foreground',
              )}
            >
              {option.label}
            </ToggleGroup.Item>
          ))}
        </ToggleGroup.Root>
      </fieldset>
    )}
  />
)
