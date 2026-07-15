import { useEffect, useRef, useState, type KeyboardEvent, type ReactElement } from 'react'
import { ArrowUp } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'

export interface CoachComposerProps {
  onSend: (message: string) => void
  isStreaming: boolean
  /** Seeds the textarea's value at mount time only — a prop change on an already-mounted instance does NOT reseed, by design. Defaults to `''`. */
  initialValue?: string
  /** Focuses the textarea on mount when `true`. Defaults to `false`. */
  autoFocus?: boolean
}

/**
 * The interactive chat composer. Enter sends; Shift+Enter inserts a newline.
 * The send control is disabled while a stream is in flight or the message is
 * blank — the textarea stays enabled so the runner can compose the next
 * message while the coach is still answering.
 *
 * `initialValue`/`autoFocus` let a caller seed text or focus into a fresh
 * composer instance. The parent chat panel remounts this component only when
 * a navigation actually delivers router state (a prefill/focus seed) — a
 * plain re-render or same-URL navigation with no state leaves the mounted
 * instance (and any in-progress draft) alone.
 */
export const CoachComposer = ({
  onSend,
  isStreaming,
  initialValue = '',
  autoFocus = false,
}: CoachComposerProps): ReactElement => {
  const [value, setValue] = useState(initialValue)
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const trimmed = value.trim()
  const canSend = trimmed.length > 0 && !isStreaming

  // Mount-only ref-imperative focus call — not a `setState`, so this does not
  // trip `react-hooks/set-state-in-effect`. Empty deps is deliberate: a prop
  // change on an already-mounted instance must NOT re-trigger focus (only a
  // fresh mount, driven by the parent remounting this component with a new
  // `key`, should). `autoFocus` is intentionally omitted from the deps
  // array — including it would re-run the focus call whenever the prop
  // changes on an already-mounted instance, which is exactly the behavior
  // this effect must NOT have.
  useEffect(() => {
    if (autoFocus) {
      textareaRef.current?.focus()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- mount-only by design, see comment above the effect
  }, [])

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
      className="flex items-end gap-[10px]"
      data-testid="coach-composer"
    >
      <Textarea
        ref={textareaRef}
        aria-label="Message your coach"
        placeholder="Ask, or describe a run to log…"
        value={value}
        onChange={(event) => setValue(event.target.value)}
        onKeyDown={handleKeyDown}
        rows={2}
        className="flex-1 resize-none min-h-12"
      />
      <Button
        type="submit"
        size="icon"
        aria-label="Send message"
        disabled={!canSend}
        className="size-12 rounded-md"
      >
        <ArrowUp aria-hidden className="size-5" />
      </Button>
    </form>
  )
}
