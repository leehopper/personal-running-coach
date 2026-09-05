import { FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Switch } from '@/components/ui/switch'
import type {
  OnboardingBooleanFieldName,
  OnboardingFormControl,
} from '~/modules/onboarding/schemas/onboarding-form.schema'

export interface OnboardingSwitchFieldProps {
  control: OnboardingFormControl
  name: OnboardingBooleanFieldName
  label: string
}

/** A labelled Alpine switch wired to a boolean onboarding field. */
export const OnboardingSwitchField = ({ control, name, label }: OnboardingSwitchFieldProps) => (
  <FormField
    control={control}
    name={name}
    render={({ field }) => (
      <FormItem className="grid grid-cols-[1fr_auto] items-center gap-x-3">
        <FormLabel className="min-h-11 font-normal">{label}</FormLabel>
        <FormControl>
          <Switch
            checked={field.value}
            onCheckedChange={(checked) => field.onChange(checked)}
            onBlur={field.onBlur}
            data-testid={`${name}-field`}
          />
        </FormControl>
        <FormMessage className="col-span-2" />
      </FormItem>
    )}
  />
)
