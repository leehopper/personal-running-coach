import { useEffect, useRef, useState, type FormEvent, type ReactElement } from 'react'
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
 * The component uses the native `<dialog>` element rather than a Radix
 * primitive because the project does not yet pull in `@radix-ui/react-dialog`
 * — the spec's reference to "shadcn/ui" is aspirational. When that dep
 * lands the markup can be swapped without altering the behaviour contract.
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
  const dialogRef = useRef<HTMLDivElement | null>(null)
  const textareaRef = useRef<HTMLTextAreaElement | null>(null)
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

  const handleSubmit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault()
    setErrorMessage(null)
    const trimmed = intent.trim()
    try {
      await regenerate({
        idempotencyKey: crypto.randomUUID(),
        ...(trimmed.length > 0 ? { intent: { freeText: trimmed } } : {}),
      }).unwrap()
      onClose()
    } catch {
      setErrorMessage('We could not regenerate your plan. Please try again in a moment.')
    }
  }

  const remaining = INTENT_MAX_LENGTH - intent.length

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4"
      onClick={() => {
        if (!isLoading) onClose()
      }}
      data-testid="regenerate-plan-backdrop"
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="regenerate-plan-title"
        aria-describedby="regenerate-plan-description"
        className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl"
        onClick={(event) => event.stopPropagation()}
        data-testid="regenerate-plan-dialog"
      >
        <h2 id="regenerate-plan-title" className="text-lg font-semibold text-slate-900">
          Regenerate plan
        </h2>
        <p id="regenerate-plan-description" className="mt-2 text-sm text-slate-600">
          This replaces your current plan.
        </p>

        <form onSubmit={handleSubmit} className="mt-4 flex flex-col gap-3">
          <label htmlFor="regenerate-plan-intent" className="text-sm font-medium text-slate-700">
            Anything we should know? (optional)
          </label>
          <textarea
            id="regenerate-plan-intent"
            ref={textareaRef}
            value={intent}
            onChange={(event) => setIntent(event.target.value)}
            maxLength={INTENT_MAX_LENGTH}
            rows={4}
            disabled={isLoading}
            placeholder="e.g. coming back from a calf strain, want to focus on long runs…"
            className="w-full resize-none rounded border border-slate-300 px-3 py-2 text-sm disabled:cursor-not-allowed disabled:opacity-60"
            data-testid="regenerate-plan-intent"
          />
          <div className="flex justify-end text-xs text-slate-500" aria-live="polite">
            {remaining} characters remaining
          </div>

          {errorMessage !== null ? (
            <p
              role="alert"
              className="rounded border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700"
            >
              {errorMessage}
            </p>
          ) : null}

          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              disabled={isLoading}
              className="rounded px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isLoading}
              className="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-60"
              data-testid="regenerate-plan-submit"
            >
              {isLoading ? 'Regenerating…' : 'Regenerate'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
