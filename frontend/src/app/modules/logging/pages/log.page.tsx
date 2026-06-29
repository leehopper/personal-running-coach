import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useLocation, useNavigate } from 'react-router-dom'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import type { StructuredLogDraft } from '~/api/generated'
import { useCreateWorkoutLogMutation } from '~/api/workout-log.api'
import { reportClientError } from '~/error-boundary/report-client-error'
import { LogForm } from '~/modules/logging/components/log-form.component'
import { draftToWorkoutLogFormFields } from '~/modules/logging/draft-to-form.helpers'
import {
  makeDefaultWorkoutLogFormFields,
  toCreateWorkoutLogRequest,
  workoutLogFormSchema,
  type WorkoutLogFormFields,
  type WorkoutLogFormValues,
} from '~/modules/logging/schemas/workout-log-form.schema'

/** Router state carried by the conversational-logging "Edit" affordance. */
interface LogPageLocationState {
  draft?: StructuredLogDraft
}

/**
 * The `/log` route — a mobile-first workout-logging form. Owns the form state,
 * the create mutation, and post-submit navigation. Create is pessimistic
 * (DEC-075): await success → success toast → navigate home; the mutation
 * invalidates the `WorkoutLog` tag so history (PR7) refetches. `OccurredOn`
 * defaults to today; the prescription snapshot is resolved server-side, so the
 * form never sends prescribed values (DEC-076).
 */
const LogPage = () => {
  const navigate = useNavigate()
  const location = useLocation()
  const [createWorkoutLog, { isLoading }] = useCreateWorkoutLogMutation()
  const [formAlert, setFormAlert] = useState<string | null>(null)
  // One key per mounted form so a retry after a transient failure reuses it and
  // the server dedupes rather than double-logging (DEC-077).
  const idempotencyKey = useMemo(() => crypto.randomUUID(), [])

  // The conversational-logging "Edit" affordance navigates here with the parsed
  // draft in router state — pre-fill the form from it (Slice 4B). An edited
  // submit still goes through the normal create path with its own idempotency
  // key, not the confirm endpoint.
  const editDraft = (location.state as LogPageLocationState | null)?.draft ?? null
  const defaultValues = useMemo(
    () =>
      editDraft !== null
        ? draftToWorkoutLogFormFields(editDraft)
        : makeDefaultWorkoutLogFormFields(),
    [editDraft],
  )

  const form = useForm<WorkoutLogFormFields, unknown, WorkoutLogFormValues>({
    resolver: zodResolver(workoutLogFormSchema),
    mode: 'onChange',
    shouldUnregister: false,
    defaultValues,
  })

  // `mode: 'onChange'` leaves `isValid` false until the first validation pass, so
  // a pre-filled Edit form needs an explicit trigger to enable Save without the
  // user touching a field.
  useEffect(() => {
    if (editDraft === null) return
    const enableSaveForPrefill = async (): Promise<void> => {
      await form.trigger()
    }
    void enableSaveForPrefill()
  }, [editDraft, form])

  const onSubmit = async (values: WorkoutLogFormValues): Promise<void> => {
    setFormAlert(null)
    try {
      await createWorkoutLog(toCreateWorkoutLogRequest(values, idempotencyKey)).unwrap()
      toast.success('Workout logged')
      navigate('/', { replace: true })
    } catch (error) {
      // The awaited `.unwrap()` rejection is a *handled* rejection, so neither
      // `useGlobalErrorReporter` (window `unhandledrejection`) nor
      // `AppErrorBoundary` (render-phase throw) sees it. Forward it to the
      // fire-and-forget client error reporter so a "could not save" the user
      // reports still leaves a diagnostic trail.
      reportClientError({
        kind: 'unhandled-rejection',
        error: error instanceof Error ? error : new Error(String(error)),
      })
      setFormAlert('We could not save your workout. Please try again in a moment.')
    }
  }

  return (
    <main
      className="mx-auto flex min-h-screen w-full max-w-md flex-col gap-6 bg-background px-4 py-8"
      data-testid="log-page"
    >
      <header className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-foreground">Log a workout</h1>
        <p className="text-sm text-muted-foreground">Record what you actually ran.</p>
      </header>
      <LogForm form={form} onSubmit={onSubmit} isLoading={isLoading} formAlert={formAlert} />
    </main>
  )
}

export default LogPage
export { LogPage }
