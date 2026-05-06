import type { ReactElement } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { InputProps } from './input-for-topic.types'

// Zod schema: ISO `YYYY-MM-DD` per the HTML `<input type="date">`
// contract. Past dates aren't blocked at the component layer because the
// "did you race already?" follow-up flow lives in the Ask-turn copy and
// the backend extractor handles the semantics — keeping the component
// dumb keeps it reusable across hypothetical "date of last injury" turns
// that legitimately want a past date.
const datePattern = /^\d{4}-\d{2}-\d{2}$/

const dateSchema = z.object({
  value: z.string().min(1, 'Pick a date.').regex(datePattern, 'Pick a valid date.'),
})

type DateFormValues = z.infer<typeof dateSchema>

/**
 * Date input for `suggestedInputType: date` Ask turns. The canonical
 * case is "what's the date of your target event?". Submits the ISO
 * `YYYY-MM-DD` string verbatim so the backend's `TargetEventAnswer`
 * extractor sees a stable parseable form.
 */
export const DateTurnInput = ({ onSubmit, isSubmitting = false }: InputProps): ReactElement => {
  const form = useForm<DateFormValues>({
    resolver: zodResolver(dateSchema),
    mode: 'onChange',
    defaultValues: { value: '' },
  })

  const submit = async (data: DateFormValues): Promise<void> => {
    await onSubmit({ text: data.value })
    form.reset({ value: '' })
  }

  const valueError = form.formState.errors.value
  const isSubmitDisabled = !form.formState.isValid || isSubmitting

  return (
    <form
      data-testid="date-turn-input"
      onSubmit={form.handleSubmit(submit)}
      className="flex w-full flex-col gap-2"
    >
      <label htmlFor="date-turn-input-field" className="text-sm font-medium">
        Date
      </label>
      <div className="flex items-end gap-2">
        <input
          id="date-turn-input-field"
          data-testid="date-turn-input-field"
          type="date"
          aria-invalid={valueError !== undefined}
          aria-describedby={valueError === undefined ? undefined : 'date-turn-input-error'}
          disabled={isSubmitting}
          className="flex-1 rounded border border-slate-300 px-3 py-2 text-sm disabled:cursor-not-allowed disabled:opacity-60"
          {...form.register('value')}
        />
        <button
          type="submit"
          disabled={isSubmitDisabled}
          className="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50"
        >
          {isSubmitting ? 'Sending…' : 'Send'}
        </button>
      </div>
      {valueError !== undefined && (
        <p id="date-turn-input-error" role="alert" className="text-xs text-red-700">
          {valueError.message}
        </p>
      )}
    </form>
  )
}
