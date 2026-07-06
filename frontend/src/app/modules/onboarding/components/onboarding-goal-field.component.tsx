import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Label } from '@/components/ui/label'
import { FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import {
  GOAL_OPTIONS,
  type OnboardingFormControl,
} from '~/modules/onboarding/schemas/onboarding-form.schema'

export interface OnboardingGoalFieldProps {
  control: OnboardingFormControl
}

/**
 * The primary-goal single-select, rendered as labelled `RadioGroup` cards
 * (Radix values are strings, so the enum is mapped at the group boundary — the
 * same `String(value)` convention as the Settings units toggle). Selecting
 * "Train for a race" reveals the TargetEvent section via the form's `useWatch`.
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
            className="gap-2"
            data-testid="goal-field"
          >
            {GOAL_OPTIONS.map((option) => (
              <Label
                key={option.value}
                htmlFor={`goal-option-${option.value}`}
                className="flex cursor-pointer items-center gap-3 rounded-md border border-input p-3 font-normal transition-colors has-[:checked]:border-primary has-[:checked]:bg-accent motion-reduce:transition-none"
              >
                <RadioGroupItem id={`goal-option-${option.value}`} value={String(option.value)} />
                {option.label}
              </Label>
            ))}
          </RadioGroup>
        </FormControl>
        <FormMessage />
      </FormItem>
    )}
  />
)
