import type { UseFormReturn } from 'react-hook-form'

import { Button } from '@/components/ui/button'
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Textarea } from '@/components/ui/textarea'
import type { PreferredUnits } from '~/api/generated'
import { deriveDisplayPace } from '~/modules/logging/log-derivations.helpers'
import type {
  WorkoutLogFormFields,
  WorkoutLogFormValues,
} from '~/modules/logging/schemas/workout-log-form.schema'
import { CompletionStatusField } from './completion-status-field.component'
import { DateChip } from './date-chip.component'
import { LogNumericField } from './log-numeric-field.component'
import { MoreDetailsSection } from './more-details-section.component'
import { PrescribedBanner } from './prescribed-banner.component'

export interface LogFormProps {
  form: UseFormReturn<WorkoutLogFormFields, unknown, WorkoutLogFormValues>
  onSubmit: (values: WorkoutLogFormValues) => void | Promise<void>
  isLoading: boolean
  formAlert: string | null
  /** The distance field label, unit-aware (e.g. `Distance (km)` / `Distance (mi)`). */
  distanceLabel: string
  /** The runner's resolved display unit — drives the prescribed-workout banner and the derived-pace preview below the distance/duration cells. */
  units: PreferredUnits
}

/**
 * Presentational workout-log form: core fields (date, distance, duration,
 * completion, notes) always visible, optional metrics behind "More details".
 * The container owns `useForm` + the create mutation + navigation; this renders
 * the field stack and gates submit on validity / in-flight state.
 *
 * The prescribed-workout banner and the derived-pace preview are both
 * display-only reads off the live `watch()`ed form state — neither ever
 * feeds back into what gets submitted (DEC-076: the prescription snapshot is
 * resolved server-side, and the pace preview never gates submit).
 */
export const LogForm = ({
  form,
  onSubmit,
  isLoading,
  formAlert,
  distanceLabel,
  units,
}: LogFormProps) => {
  const isSubmitDisabled = !form.formState.isValid || form.formState.isSubmitting || isLoading
  const occurredOn = form.watch('occurredOn')
  const derivedPace = deriveDisplayPace(
    form.watch('distance'),
    form.watch('durationMinutes'),
    units,
  )

  return (
    <Form {...form}>
      <form
        noValidate
        onSubmit={form.handleSubmit(onSubmit)}
        className="space-y-5"
        data-testid="log-form"
      >
        {formAlert !== null ? (
          <p
            role="alert"
            data-testid="log-form-alert"
            className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 font-mono text-sm text-destructive"
          >
            {formAlert}
          </p>
        ) : null}

        <FormField
          control={form.control}
          name="occurredOn"
          render={({ field }) => (
            <FormItem>
              <FormControl>
                <DateChip
                  ref={field.ref}
                  name={field.name}
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <PrescribedBanner date={occurredOn} units={units} />

        <div className="grid grid-cols-2 gap-3">
          <div className="[&_input]:font-condensed [&_input]:text-lg [&_input]:font-bold">
            <LogNumericField
              control={form.control}
              name="distance"
              label={distanceLabel}
              autoFocus
            />
          </div>
          <div className="[&_input]:font-condensed [&_input]:text-lg [&_input]:font-bold">
            <LogNumericField
              control={form.control}
              name="durationMinutes"
              label="Duration (minutes)"
            />
          </div>
        </div>

        {derivedPace !== null ? (
          <p data-testid="log-derived-pace" className="font-mono text-[11px] text-muted-foreground">
            {derivedPace}
          </p>
        ) : null}

        <CompletionStatusField control={form.control} name="completionStatus" />

        <FormField
          control={form.control}
          name="notes"
          render={({ field }) => (
            <FormItem>
              <FormLabel>How did it go?</FormLabel>
              <FormDescription className="font-mono text-[11px] text-muted-foreground">
                What actually happened — especially where it differed from the plan. The coach
                adapts to what you write here.
              </FormDescription>
              <FormControl>
                <Textarea
                  rows={3}
                  placeholder="Cut to 3 reps, moved to the treadmill, calf felt tight on the last k…"
                  {...field}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <MoreDetailsSection control={form.control} />

        <Button
          type="submit"
          disabled={isSubmitDisabled}
          className="min-h-[54px] w-full"
          data-testid="log-form-submit"
        >
          {isLoading ? 'Saving…' : 'Save run'}
        </Button>
      </form>
    </Form>
  )
}
