import type { FieldPath } from 'react-hook-form'

import { FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { SegmentedControl, SegmentedControlItem } from '@/components/ui/segmented-control'
import { CompletionStatus } from '~/api/generated'
import type {
  WorkoutLogFormControl,
  WorkoutLogFormFields,
} from '~/modules/logging/schemas/workout-log-form.schema'

const COMPLETION_OPTIONS: ReadonlyArray<{ value: string; label: string; testId: string }> = [
  { value: String(CompletionStatus.Complete), label: 'Completed', testId: 'completion-completed' },
  { value: String(CompletionStatus.Partial), label: 'Partial', testId: 'completion-partial' },
  { value: String(CompletionStatus.Skipped), label: 'Skipped', testId: 'completion-skipped' },
]

export interface CompletionStatusFieldProps {
  control: WorkoutLogFormControl
  name: FieldPath<WorkoutLogFormFields>
}

/**
 * Segmented-control selector for how fully the workout was completed. Values are
 * the `CompletionStatus` wire integers carried as strings in form state; the
 * schema coerces them back to the `0|1|2` union. Defaults to "Completed". Source
 * labels stay sentence-case ("Completed"/"Partial"/"Skipped") — `SegmentedControlItem`
 * applies the uppercase presentation via CSS.
 */
export const CompletionStatusField = ({ control, name }: CompletionStatusFieldProps) => (
  <FormField
    control={control}
    name={name}
    render={({ field }) => (
      <FormItem>
        <FormLabel>Completion</FormLabel>
        <FormControl>
          <SegmentedControl value={field.value} onValueChange={field.onChange}>
            {COMPLETION_OPTIONS.map((option) => (
              <SegmentedControlItem
                key={option.value}
                value={option.value}
                data-testid={option.testId}
              >
                {option.label}
              </SegmentedControlItem>
            ))}
          </SegmentedControl>
        </FormControl>
        <FormMessage />
      </FormItem>
    )}
  />
)
