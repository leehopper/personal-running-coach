import { useEffect, useMemo, useRef, useState, type ReactElement, type SubmitEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { useRegeneratePlanMutation } from '~/api/plan.api'

/**
 * Maximum length of the optional regeneration-intent free-text. Mirrors
 * `RunCoach.Api.Modules.Coaching.Models.RegenerationIntent.RawMaxFreeTextLength`
 * (spec 13 § Unit 5 R05.1). The textarea enforces this with both `maxLength`
 * and a remaining-character counter so the user is never able to over-type.
 */
const INTENT_MAX_LENGTH = 500

export interface RegeneratePlanDialogProps {
  /**
   * Whether the dialog is visible. The Settings page owns this state so
   * the trigger button + dialog can be tested in isolation and so the
   * dialog can fully unmount when closed (resets the textarea + any
   * transient mutation state).
   */
  isOpen: boolean
  /**
   * Called when the dialog requests close — Cancel button, backdrop click,
   * Escape key, or successful regeneration. The parent flips `isOpen` to
   * `false` in response.
   */
  onClose: () => void
}

/**
 * Modal dialog launched from `/settings` to regenerate the runner's plan.
 * Renders an optional free-text textarea (capped at 500 characters) plus a
 * "Regenerate" submit button. The mutation is fired with a freshly-generated
 * idempotency key so retries short-circuit server-side; on success the
 * dialog closes and `invalidatesTags: ["Plan"]` causes the home surface to
 * refetch the new projection (spec 13 § Unit 5 R05.6, R05.7, R05.8).
 *
 * Uses a positioned div rather than the native `<dialog>` element so
 * jsdom-based tests can mount it deterministically.
 */
export const RegeneratePlanDialog = ({
  isOpen,
  onClose,
}: RegeneratePlanDialogProps): ReactElement | null => {
  if (!isOpen) {
    return null
  }
  return <RegeneratePlanDialogBody onClose={onClose} />
}

interface RegeneratePlanDialogBodyProps {
  onClose: () => void
}

/**
 * Inner body of the dialog. Mounted only when the parent has flipped
 * `isOpen` to `true`, so transient state (textarea content, error message)
 * resets simply by virtue of the component remounting — no in-effect
 * `setState` reset path required.
 */
const RegeneratePlanDialogBody = ({ onClose }: RegeneratePlanDialogBodyProps): ReactElement => {
  const textareaRef = useRef<HTMLTextAreaElement | null>(null)
  const idempotencyKey = useMemo(() => crypto.randomUUID(), [])
  const [intent, setIntent] = useState('')
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [regenerate, { isLoading }] = useRegeneratePlanMutation()

  // Move focus to the textarea on first paint so screen-reader users land
  // inside the form immediately.
  useEffect(() => {
    queueMicrotask(() => textareaRef.current?.focus())
  }, [])

  // Close-on-Escape — the native <dialog> element handles this for free,
  // but we are using a positioned div so we wire it up manually to keep
  // the markup stable for tests (jsdom does not implement HTMLDialogElement).
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent): void => {
      if (event.key === 'Escape' && !isLoading) {
        onClose()
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => {
      window.removeEventListener('keydown', handleKeyDown)
    }
  }, [isLoading, onClose])

  const handleSubmit = async (event: SubmitEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault()
    setErrorMessage(null)
    const trimmed = intent.trim()
    try {
      await regenerate({
        idempotencyKey,
        ...(trimmed.length > 0 ? { intent: { freeText: trimmed } } : {}),
      }).unwrap()
      onClose()
    } catch {
      setErrorMessage('We could not regenerate your plan. Please try again in a moment.')
    }
  }

  const remaining = INTENT_MAX_LENGTH - intent.length

  const closeIfIdle = (): void => {
    if (!isLoading) onClose()
  }

  return (
    <div
      role="presentation"
      tabIndex={-1}
      className="fixed inset-0 z-50 flex items-center justify-center bg-foreground/40 px-4"
      onClick={closeIfIdle}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault()
          closeIfIdle()
        }
      }}
      data-testid="regenerate-plan-backdrop"
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="regenerate-plan-title"
        aria-describedby="regenerate-plan-description"
        className="w-full max-w-md rounded-lg border bg-popover p-6 text-popover-foreground shadow-xl"
        onClick={(event) => event.stopPropagation()}
        onKeyDown={(event) => event.stopPropagation()}
        data-testid="regenerate-plan-dialog"
      >
        <h2 id="regenerate-plan-title" className="text-lg font-semibold text-foreground">
          Regenerate plan
        </h2>
        <p id="regenerate-plan-description" className="mt-2 text-sm text-muted-foreground">
          This replaces your current plan.
        </p>

        <form onSubmit={handleSubmit} className="mt-4 flex flex-col gap-3">
          <label htmlFor="regenerate-plan-intent" className="text-sm font-medium text-foreground">
            Anything we should know? (optional)
          </label>
          <Textarea
            id="regenerate-plan-intent"
            ref={textareaRef}
            value={intent}
            onChange={(event) => setIntent(event.target.value)}
            maxLength={INTENT_MAX_LENGTH}
            rows={4}
            disabled={isLoading}
            placeholder="e.g. coming back from a calf strain, want to focus on long runs…"
            className="w-full resize-none"
            data-testid="regenerate-plan-intent"
          />
          <div className="flex justify-end text-xs text-muted-foreground" aria-live="polite">
            {remaining} characters remaining
          </div>

          {errorMessage !== null ? (
            <p
              role="alert"
              className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
            >
              {errorMessage}
            </p>
          ) : null}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" onClick={onClose} disabled={isLoading}>
              Cancel
            </Button>
            <Button type="submit" disabled={isLoading} data-testid="regenerate-plan-submit">
              {isLoading ? 'Regenerating…' : 'Regenerate'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
