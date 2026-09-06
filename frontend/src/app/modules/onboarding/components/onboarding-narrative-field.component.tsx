import { Textarea } from '@/components/ui/textarea'
import {
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import type { OnboardingFormControl } from '~/modules/onboarding/schemas/onboarding-form.schema'

export interface OnboardingNarrativeFieldProps {
  control: OnboardingFormControl
}

/** The always-visible free-text context the coach reads before structured data. */
export const OnboardingNarrativeField = ({ control }: OnboardingNarrativeFieldProps) => (
  <FormField
    control={control}
    name="narrative"
    render={({ field }) => (
      <FormItem>
        <FormLabel className="sr-only">In your own words</FormLabel>
        <FormControl>
          <Textarea
            rows={4}
            maxLength={1000}
            placeholder={
              'Coming back from a calf strain. 10K in October. Tuesdays are impossible, and I hate treadmills\u2026'
            }
            data-testid="narrative-field"
            className="min-h-[96px]"
            {...field}
          />
        </FormControl>
        <FormDescription className="t-data-label text-muted-foreground">
          {
            'The coach reads this first. Plain words beat perfect forms \u2014 the form below keeps the numbers honest.'
          }
        </FormDescription>
        <FormMessage />
      </FormItem>
    )}
  />
)
