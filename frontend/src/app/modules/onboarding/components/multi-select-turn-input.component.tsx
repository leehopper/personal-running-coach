import type { ReactElement } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'

import { Button } from '@/components/ui/button'
import type { InputProps } from './input-for-topic.types'

// Zod schema: at least one option must be checked. Stores the picked
// values as a comma-separated list on the wire so the backend extractor
// sees a parseable string ("monday,wednesday,friday"), matching the
// canned WeeklySchedule extraction behavior.
const multiSelectSchema = z.object({
  values: z.array(z.string()).min(1, 'Pick at least one option.'),
})

type MultiSelectFormValues = z.infer<typeof multiSelectSchema>

const FALLBACK_OPTIONS: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'monday', label: 'Mon' },
  { value: 'tuesday', label: 'Tue' },
  { value: 'wednesday', label: 'Wed' },
  { value: 'thursday', label: 'Thu' },
  { value: 'friday', label: 'Fri' },
  { value: 'saturday', label: 'Sat' },
  { value: 'sunday', label: 'Sun' },
]

/**
 * Checkbox-style multi-pick input for `suggestedInputType: multi-select`
 * Ask turns. The canonical case is the WeeklySchedule turn (which days
 * can the runner train?). Picked values are joined with `,` for the
 * server-bound `text` payload — matches the `WeeklyScheduleAnswer`
 * extraction shape on the backend.
 */
export const MultiSelectTurnInput = ({
  onSubmit,
  isSubmitting = false,
  options,
}: InputProps): ReactElement => {
  const renderedOptions = options !== undefined && options.length > 0 ? options : FALLBACK_OPTIONS

  const form = useForm<MultiSelectFormValues>({
    resolver: zodResolver(multiSelectSchema),
    mode: 'onChange',
    defaultValues: { values: [] },
  })

  const submit = async (data: MultiSelectFormValues): Promise<void> => {
    await onSubmit({ text: data.values.join(',') })
    form.reset({ values: [] })
  }

  const isSubmitDisabled = !form.formState.isValid || isSubmitting

  return (
    <form
      data-testid="multi-select-turn-input"
      onSubmit={form.handleSubmit(submit)}
      className="flex w-full flex-col gap-3"
    >
      <fieldset className="flex flex-wrap gap-2" disabled={isSubmitting}>
        <legend className="sr-only">Pick all that apply</legend>
        {renderedOptions.map((option) => (
          <label
            key={option.value}
            className="flex cursor-pointer items-center gap-2 rounded-md border border-border px-3 py-2 text-sm transition-colors hover:border-ring motion-reduce:transition-none"
          >
            <input
              type="checkbox"
              value={option.value}
              {...form.register('values')}
              className="size-4 accent-primary"
            />
            <span>{option.label}</span>
          </label>
        ))}
      </fieldset>
      <Button type="submit" disabled={isSubmitDisabled} className="self-end">
        {isSubmitting ? 'Sending…' : 'Send'}
      </Button>
    </form>
  )
}
