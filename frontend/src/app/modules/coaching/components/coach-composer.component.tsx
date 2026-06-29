import { useState, type KeyboardEvent, type ReactElement } from 'react'

import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'

export interface CoachComposerProps {
  onSend: (message: string) => void
  isStreaming: boolean
}

/**
 * The interactive chat composer. Enter sends; Shift+Enter inserts a newline.
 * The send control is disabled while a stream is in flight or the message is
 * blank — the textarea stays enabled so the runner can compose the next
 * message while the coach is still answering.
 */
export const CoachComposer = ({ onSend, isStreaming }: CoachComposerProps): ReactElement => {
  const [value, setValue] = useState('')
  const trimmed = value.trim()
  const canSend = trimmed.length > 0 && !isStreaming

  const submit = (): void => {
    if (!canSend) return
    onSend(trimmed)
    setValue('')
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>): void => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      submit()
    }
  }

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault()
        submit()
      }}
      className="flex items-end gap-2"
      data-testid="coach-composer"
    >
      <Textarea
        aria-label="Message your coach"
        placeholder="Ask a question or describe a run to log…"
        value={value}
        onChange={(event) => setValue(event.target.value)}
        onKeyDown={handleKeyDown}
        rows={2}
        className="flex-1 resize-none"
      />
      <Button type="submit" disabled={!canSend}>
        Send
      </Button>
    </form>
  )
}
