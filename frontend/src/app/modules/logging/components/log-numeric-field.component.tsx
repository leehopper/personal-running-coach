import type { FieldPath } from 'react-hook-form'

import { FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import type {
  WorkoutLogFormControl,
  WorkoutLogFormFields,
} from '~/modules/logging/schemas/workout-log-form.schema'

export interface LogNumericFieldProps {
  control: WorkoutLogFormControl
  name: FieldPath<WorkoutLogFormFields>
  label: string
  autoFocus?: boolean
}

/**
 * A labeled numeric field for the workout-log form. Uses
 * `type="text" inputMode="decimal"` (DEC-075 — avoids the `<input type=number>`
 * spinbutton + locale-decimal footguns) and binds the raw string straight into
 * RHF state; the empty → `undefined` coercion and range validation live in the
 * Zod schema, so this component stays a dumb controlled input. Wired through the
 * shared `FormMessage`, whose `role="alert"` announces validation errors.
 */
export const LogNumericField = ({
  control,
  name,
  label,
  autoFocus = false,
}: LogNumericFieldProps) => (
  <FormField
    control={control}
    name={name}
    render={({ field }) => (
      <FormItem>
        <FormLabel>{label}</FormLabel>
        <FormControl>
          <Input
            type="text"
            inputMode="decimal"
            autoComplete="off"
            autoFocus={autoFocus}
            {...field}
          />
        </FormControl>
        <FormMessage />
      </FormItem>
    )}
  />
)
