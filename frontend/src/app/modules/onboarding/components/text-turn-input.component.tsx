import type { ReactElement } from 'react'
import { useForm } from 'react-hook-form'

import { Button } from '@/components/ui/button'
import type { InputProps } from './input-for-topic.types'

interface TextFormValues {
  text: string
}

export type TextTurnInputProps = InputProps

/**
 * Free-form text input for `suggestedInputType: text` Ask turns. The only
 * client-side rule is "don't submit empty"; React Hook Form gives us the
 * disabled-submit / reset-on-success ergonomics for free without dragging
 * a Zod schema in for a single non-empty check.
 */
export const TextTurnInput = ({
  onSubmit,
  isSubmitting = false,
}: TextTurnInputProps): ReactElement => {
  const form = useForm<TextFormValues>({
    mode: 'onChange',
    defaultValues: { text: '' },
  })

  const submit = async (data: TextFormValues): Promise<void> => {
    const trimmed = data.text.trim()
    if (trimmed.length === 0) {
      return
    }
    await onSubmit({ text: trimmed })
    form.reset({ text: '' })
  }

  const isSubmitDisabled = !form.formState.isValid || isSubmitting

  return (
    <form
      data-testid="text-turn-input"
      onSubmit={form.handleSubmit(submit)}
      className="flex w-full items-end gap-2"
    >
      <label htmlFor="text-turn-input-field" className="sr-only">
        Your reply
      </label>
      <textarea
        id="text-turn-input-field"
        data-testid="text-turn-input-field"
        rows={2}
        disabled={isSubmitting}
        placeholder="Type your reply…"
        className="flex-1 resize-none rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-xs transition-[color,box-shadow] outline-none placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-input/30"
        {...form.register('text', {
          validate: (value) => value.trim().length > 0,
        })}
      />
      <Button type="submit" disabled={isSubmitDisabled}>
        {isSubmitting ? 'Sending…' : 'Send'}
      </Button>
    </form>
  )
}
