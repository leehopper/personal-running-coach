import type { UseFormReturn } from 'react-hook-form'

import { Button } from '@/components/ui/button'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import type {
  WorkoutLogFormFields,
  WorkoutLogFormValues,
} from '~/modules/logging/schemas/workout-log-form.schema'
import { CompletionStatusField } from './completion-status-field.component'
import { LogNumericField } from './log-numeric-field.component'
import { MoreDetailsSection } from './more-details-section.component'

export interface LogFormProps {
  form: UseFormReturn<WorkoutLogFormFields, unknown, WorkoutLogFormValues>
  onSubmit: (values: WorkoutLogFormValues) => void | Promise<void>
  isLoading: boolean
  formAlert: string | null
  /** The distance field label, unit-aware (e.g. `Distance (km)` / `Distance (mi)`). */
  distanceLabel: string
}

/**
 * Presentational workout-log form: core fields (date, distance, duration,
 * completion, notes) always visible, optional metrics behind "More details".
 * The container owns `useForm` + the create mutation + navigation; this renders
 * the field stack and gates submit on validity / in-flight state.
 */
export const LogForm = ({ form, onSubmit, isLoading, formAlert, distanceLabel }: LogFormProps) => {
  const isSubmitDisabled = !form.formState.isValid || form.formState.isSubmitting || isLoading

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
            className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
          >
            {formAlert}
          </p>
        ) : null}

        <FormField
          control={form.control}
          name="occurredOn"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Date</FormLabel>
              <FormControl>
                <Input type="date" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <LogNumericField control={form.control} name="distance" label={distanceLabel} autoFocus />
        <LogNumericField control={form.control} name="durationMinutes" label="Duration (minutes)" />

        <CompletionStatusField control={form.control} name="completionStatus" />

        <FormField
          control={form.control}
          name="notes"
          render={({ field }) => (
            <FormItem>
              <FormLabel>How did it go?</FormLabel>
              <FormControl>
                <Textarea
                  rows={3}
                  placeholder="Anything worth remembering — how you felt, weather, terrain…"
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
          className="w-full"
          data-testid="log-form-submit"
        >
          {isLoading ? 'Saving…' : 'Save workout'}
        </Button>
      </form>
    </Form>
  )
}
