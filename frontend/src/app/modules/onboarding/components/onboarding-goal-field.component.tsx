import { Label } from '@/components/ui/label'
import { FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import {
  GOAL_OPTIONS,
  type OnboardingFormControl,
} from '~/modules/onboarding/schemas/onboarding-form.schema'

export interface OnboardingGoalFieldProps {
  control: OnboardingFormControl
}

/**
 * The primary-goal single-select, rendered as radio-right rows. Radix values
 * are strings, so the enum is mapped at the group boundary.
 */
export const OnboardingGoalField = ({ control }: OnboardingGoalFieldProps) => (
  <FormField
    control={control}
    name="goal"
    render={({ field }) => (
      <FormItem>
        <FormLabel>What's your primary goal?</FormLabel>
        <FormControl>
          <RadioGroup
            value={field.value}
            onValueChange={field.onChange}
            className="gap-0"
            data-testid="goal-field"
          >
            {GOAL_OPTIONS.map((option) => (
              <Label
                key={option.value}
                htmlFor={`goal-option-${option.value}`}
                className="flex min-h-11 cursor-pointer items-center justify-between gap-3 border-b border-border py-2 font-normal transition-colors first:pt-0 last:border-b-0 last:pb-0 has-[:checked]:text-primary motion-reduce:transition-none"
              >
                {option.label}
                <RadioGroupItem id={`goal-option-${option.value}`} value={String(option.value)} />
              </Label>
            ))}
          </RadioGroup>
        </FormControl>
        <FormMessage />
      </FormItem>
    )}
  />
)
