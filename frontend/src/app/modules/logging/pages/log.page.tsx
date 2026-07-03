import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useLocation, useNavigate } from 'react-router-dom'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import { PreferredUnits, type StructuredLogDraft } from '~/api/generated'
import { useCreateWorkoutLogMutation } from '~/api/workout-log.api'
import { reportClientError } from '~/error-boundary/report-client-error'
import { distanceUnitLabel } from '~/modules/common/utils/unit-format.helpers'
import { LogForm } from '~/modules/logging/components/log-form.component'
import { draftToWorkoutLogFormFields } from '~/modules/logging/draft-to-form.helpers'
import {
  makeDefaultWorkoutLogFormFields,
  makeWorkoutLogFormSchema,
  toCreateWorkoutLogRequest,
  type WorkoutLogFormFields,
  type WorkoutLogFormValues,
} from '~/modules/logging/schemas/workout-log-form.schema'
import { usePreferredUnitsResolution } from '~/modules/settings/hooks/use-preferred-units.hooks'

/** Router state carried by the conversational-logging "Edit" affordance. */
interface LogPageLocationState {
  draft?: StructuredLogDraft
}

interface WorkoutLogFormViewProps {
  /** The runner's resolved display unit — the entered distance is read in this unit. */
  units: PreferredUnits
  editDraft: StructuredLogDraft | null
}

/**
 * The form itself, mounted only once the unit preference has resolved (see
 * {@link LogPage}). Because `units` is known and stable before this mounts, the
 * schema, the distance label, the draft pre-fill, and the miles→km write
 * conversion are all computed once against the correct unit — no mid-form schema
 * swap and no reset dance. Owns the form state, the create mutation, and
 * post-submit navigation. Create is pessimistic (DEC-075): await success →
 * success toast → navigate home; the mutation invalidates the `WorkoutLog` tag so
 * history refetches. The prescription snapshot is resolved server-side, so the
 * form never sends prescribed values (DEC-076).
 */
const WorkoutLogFormView = ({ units, editDraft }: WorkoutLogFormViewProps) => {
  const navigate = useNavigate()
  const [createWorkoutLog, { isLoading }] = useCreateWorkoutLogMutation()
  const [formAlert, setFormAlert] = useState<string | null>(null)
  // One key per mounted form so a retry after a transient failure reuses it and
  // the server dedupes rather than double-logging (DEC-077).
  const idempotencyKey = useMemo(() => crypto.randomUUID(), [])

  const schema = useMemo(() => makeWorkoutLogFormSchema(units), [units])
  const defaultValues = useMemo(
    () =>
      editDraft !== null
        ? draftToWorkoutLogFormFields(editDraft, units)
        : makeDefaultWorkoutLogFormFields(),
    [editDraft, units],
  )

  const form = useForm<WorkoutLogFormFields, unknown, WorkoutLogFormValues>({
    resolver: zodResolver(schema),
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
      await createWorkoutLog(toCreateWorkoutLogRequest(values, idempotencyKey, units)).unwrap()
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
    <LogForm
      form={form}
      onSubmit={onSubmit}
      isLoading={isLoading}
      formAlert={formAlert}
      distanceLabel={`Distance (${distanceUnitLabel(units)})`}
    />
  )
}

/**
 * The `/log` route — a mobile-first workout-logging form. The form's numeric
 * write interprets the entered distance in the runner's unit preference, so it
 * defers the form until that preference has resolved (usually instant — the
 * preference is cached app-wide; only a cold `/log` refresh shows the brief
 * loading state). This keeps a miles-preferring runner's input from ever being
 * converted against the loading-time km fallback (DEC-086).
 */
const LogPage = () => {
  const location = useLocation()
  const editDraft = (location.state as LogPageLocationState | null)?.draft ?? null
  const { units, isResolved } = usePreferredUnitsResolution()

  return (
    <main
      className="mx-auto flex min-h-screen w-full max-w-md flex-col gap-6 bg-background px-4 py-8"
      data-testid="log-page"
    >
      <header className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-foreground">Log a workout</h1>
        <p className="text-sm text-muted-foreground">Record what you actually ran.</p>
      </header>
      {isResolved ? (
        <WorkoutLogFormView units={units} editDraft={editDraft} />
      ) : (
        <p role="status" className="text-sm text-muted-foreground" data-testid="log-page-loading">
          Loading…
        </p>
      )}
    </main>
  )
}

export default LogPage
export { LogPage }
