import type { ReactElement } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { InputProps } from './input-for-topic.types'

// Zod-validated form: exactly one option must be picked. The schema
// returns a non-empty string so the dispatcher can ship the picked
// value as the `text` payload without any further coercion.
const singleSelectSchema = z.object({
  value: z.string().min(1, 'Pick an option to continue.'),
})

type SingleSelectFormValues = z.infer<typeof singleSelectSchema>

const FALLBACK_OPTIONS: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'race-training', label: 'Training for a race' },
  { value: 'general-fitness', label: 'General fitness' },
  { value: 'return-to-running', label: 'Returning to running' },
  { value: 'build-volume', label: 'Building weekly volume' },
  { value: 'build-speed', label: 'Improving speed' },
]

/**
 * Radio-style single-pick input for `suggestedInputType: single-select`
 * Ask turns. Uses React Hook Form + Zod (per frontend conventions —
 * structural validation belongs in a schema). Falls back to a canned
 * PrimaryGoal option list when the server does not attach `options` to
 * the Ask turn; that fallback covers the very first onboarding turn,
 * which is fixed to the PrimaryGoal topic.
 */
export const SingleSelectTurnInput = ({
  onSubmit,
  isSubmitting = false,
  options,
}: InputProps): ReactElement => {
  const renderedOptions = options !== undefined && options.length > 0 ? options : FALLBACK_OPTIONS

  const form = useForm<SingleSelectFormValues>({
    resolver: zodResolver(singleSelectSchema),
    mode: 'onChange',
    defaultValues: { value: '' },
  })

  const submit = async (values: SingleSelectFormValues): Promise<void> => {
    await onSubmit({ text: values.value })
    form.reset({ value: '' })
  }

  const isSubmitDisabled = !form.formState.isValid || isSubmitting

  return (
    <form
      data-testid="single-select-turn-input"
      onSubmit={form.handleSubmit(submit)}
      className="flex w-full flex-col gap-3"
    >
      <fieldset className="flex flex-col gap-2" disabled={isSubmitting}>
        <legend className="sr-only">Pick one</legend>
        {renderedOptions.map((option) => (
          <label
            key={option.value}
            className="flex cursor-pointer items-center gap-2 rounded border border-slate-200 px-3 py-2 text-sm hover:border-slate-400"
          >
            <input
              type="radio"
              value={option.value}
              {...form.register('value')}
              className="h-4 w-4"
            />
            <span>{option.label}</span>
          </label>
        ))}
      </fieldset>
      <button
        type="submit"
        disabled={isSubmitDisabled}
        className="self-end rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50"
      >
        {isSubmitting ? 'Sending…' : 'Send'}
      </button>
    </form>
  )
}
