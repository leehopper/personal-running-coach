import type { FieldPath } from 'react-hook-form'

import { FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { CompletionStatus } from '~/api/generated'
import type {
  WorkoutLogFormControl,
  WorkoutLogFormFields,
} from '~/modules/logging/schemas/workout-log-form.schema'

const COMPLETION_OPTIONS: ReadonlyArray<{ value: string; label: string }> = [
  { value: String(CompletionStatus.Complete), label: 'Completed' },
  { value: String(CompletionStatus.Partial), label: 'Partial' },
  { value: String(CompletionStatus.Skipped), label: 'Skipped' },
]

export interface CompletionStatusFieldProps {
  control: WorkoutLogFormControl
  name: FieldPath<WorkoutLogFormFields>
}

/**
 * Radio selector for how fully the workout was completed. Values are the
 * `CompletionStatus` wire integers carried as strings in form state; the schema
 * coerces them back to the `0|1|2` union. Defaults to "Completed".
 */
export const CompletionStatusField = ({ control, name }: CompletionStatusFieldProps) => (
  <FormField
    control={control}
    name={name}
    render={({ field }) => (
      <FormItem>
        <FormLabel>Completion</FormLabel>
        <FormControl>
          <RadioGroup
            value={field.value}
            onValueChange={field.onChange}
            className="flex flex-wrap gap-x-6 gap-y-2"
          >
            {COMPLETION_OPTIONS.map((option) => (
              <div key={option.value} className="flex items-center gap-2">
                <RadioGroupItem value={option.value} id={`completion-status-${option.value}`} />
                <Label htmlFor={`completion-status-${option.value}`} className="font-normal">
                  {option.label}
                </Label>
              </div>
            ))}
          </RadioGroup>
        </FormControl>
        <FormMessage />
      </FormItem>
    )}
  />
)
