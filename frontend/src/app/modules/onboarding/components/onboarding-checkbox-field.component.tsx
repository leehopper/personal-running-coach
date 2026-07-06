import { Checkbox } from '@/components/ui/checkbox'
import { FormControl, FormField, FormItem, FormLabel } from '@/components/ui/form'
import type {
  OnboardingBooleanFieldName,
  OnboardingFormControl,
} from '~/modules/onboarding/schemas/onboarding-form.schema'

export interface OnboardingCheckboxFieldProps {
  control: OnboardingFormControl
  name: OnboardingBooleanFieldName
  label: string
}

/**
 * A single boolean onboarding toggle rendered as a labelled Radix `Checkbox`
 * wired through an RHF `Controller`. `FormControl` stamps the shared form-item id
 * onto the checkbox and `FormLabel` targets it, so clicking the label toggles the
 * control and the association is announced to assistive tech.
 */
export const OnboardingCheckboxField = ({ control, name, label }: OnboardingCheckboxFieldProps) => (
  <FormField
    control={control}
    name={name}
    render={({ field }) => (
      <FormItem className="flex flex-row items-center gap-3">
        <FormControl>
          <Checkbox
            checked={field.value}
            onCheckedChange={(checked) => field.onChange(checked === true)}
            onBlur={field.onBlur}
            data-testid={`${name}-field`}
          />
        </FormControl>
        <FormLabel className="font-normal">{label}</FormLabel>
      </FormItem>
    )}
  />
)
