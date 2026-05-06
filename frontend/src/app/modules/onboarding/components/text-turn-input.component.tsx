import { useState, type FormEvent, type ReactElement } from 'react'
import type { InputProps } from './input-for-topic.types'

/**
 * Free-form text input for `suggestedInputType: text` Ask turns. Per the
 * frontend conventions doc, free-text turns use a raw form (no Zod / RHF)
 * because there is no structural validation to apply — the only client-side
 * rule is "don't submit empty", which the disabled-button covers.
 */
export const TextTurnInput = ({ onSubmit, isSubmitting = false }: InputProps): ReactElement => {
  const [value, setValue] = useState('')

  const handleSubmit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault()
    const trimmed = value.trim()
    if (trimmed.length === 0) {
      return
    }
    await onSubmit({ text: trimmed })
    setValue('')
  }

  const isSubmitDisabled = value.trim().length === 0 || isSubmitting

  return (
    <form
      data-testid="text-turn-input"
      onSubmit={handleSubmit}
      className="flex w-full items-end gap-2"
    >
      <label htmlFor="text-turn-input-field" className="sr-only">
        Your reply
      </label>
      <textarea
        id="text-turn-input-field"
        data-testid="text-turn-input-field"
        value={value}
        onChange={(event) => setValue(event.target.value)}
        rows={2}
        disabled={isSubmitting}
        placeholder="Type your reply…"
        className="flex-1 resize-none rounded border border-slate-300 px-3 py-2 text-sm disabled:cursor-not-allowed disabled:opacity-60"
      />
      <button
        type="submit"
        disabled={isSubmitDisabled}
        className="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50"
      >
        {isSubmitting ? 'Sending…' : 'Send'}
      </button>
    </form>
  )
}
