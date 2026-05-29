import type { ReactElement } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { InputProps } from './input-for-topic.types'

// Zod schema for numeric input: positive finite number, capped at a sane
// upper bound (300km / week is well above any plausible runner). The form
// state stores a `number` directly — `<input type="number">` is registered
// with `valueAsNumber: true` below so RHF returns the parsed numeric value
// rather than the raw string.
const numericSchema = z.object({
  value: z
    .number({ message: 'Enter a number.' })
    .finite('Enter a number.')
    .gt(0, 'Enter a number greater than zero.')
    .lte(300, 'Enter a value at or below 300.'),
})

type NumericFormValues = z.infer<typeof numericSchema>

/**
 * Numeric input for `suggestedInputType: numeric` Ask turns. The
 * canonical case is "current weekly distance" (km); Slice 2's preferred-
 * units toggle will eventually let users see this as miles, but Slice 1
 * is km-only per spec § Unit 4 R-070.
 */
export const NumericTurnInput = ({ onSubmit, isSubmitting = false }: InputProps): ReactElement => {
  const form = useForm<NumericFormValues>({
    resolver: zodResolver(numericSchema),
    mode: 'onChange',
    // `0` is intentional sentinel — schema's `.gt(0)` fails on it, which
    // keeps the submit button disabled until the runner enters a real
    // value. RHF stores it as a `number` (matches the schema type).
    defaultValues: { value: 0 },
  })

  const submit = async (data: NumericFormValues): Promise<void> => {
    await onSubmit({ text: data.value.toString() })
    form.reset({ value: 0 })
  }

  const valueError = form.formState.errors.value
  const isSubmitDisabled = !form.formState.isValid || isSubmitting

  return (
    <form
      data-testid="numeric-turn-input"
      onSubmit={form.handleSubmit(submit)}
      className="flex w-full flex-col gap-2"
    >
      <label htmlFor="numeric-turn-input-field" className="text-sm font-medium">
        Weekly distance (km)
      </label>
      <div className="flex items-end gap-2">
        <Controller
          control={form.control}
          name="value"
          render={({ field }) => (
            <Input
              {...field}
              id="numeric-turn-input-field"
              data-testid="numeric-turn-input-field"
              type="number"
              inputMode="decimal"
              min={0}
              max={300}
              step={0.1}
              aria-invalid={valueError !== undefined}
              aria-describedby={valueError === undefined ? undefined : 'numeric-turn-input-error'}
              disabled={isSubmitting}
              className="flex-1"
              value={Number.isNaN(field.value) ? '' : field.value}
              onChange={(event) => field.onChange(event.target.valueAsNumber)}
            />
          )}
        />
        <Button type="submit" disabled={isSubmitDisabled}>
          {isSubmitting ? 'Sending…' : 'Send'}
        </Button>
      </div>
      {valueError !== undefined && (
        <p id="numeric-turn-input-error" role="alert" className="text-xs text-destructive">
          {valueError.message}
        </p>
      )}
    </form>
  )
}
