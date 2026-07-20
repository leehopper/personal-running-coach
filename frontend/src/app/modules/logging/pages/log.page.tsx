import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useLocation, useNavigate } from 'react-router-dom'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { PreferredUnits, type StructuredLogDraft } from '~/api/generated'
import { useCreateWorkoutLogMutation } from '~/api/workout-log.api'
import { reportClientError } from '~/error-boundary/report-client-error'
import { distanceUnitLabel, formatDistanceMeters } from '~/modules/common/utils/unit-format.helpers'
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
    // Built once so the success toast's distance readout is derived from the
    // EXACT payload that shipped (not re-derived from form state, which could
    // in principle drift from the sent request).
    const request = toCreateWorkoutLogRequest(values, idempotencyKey, units)
    try {
      await createWorkoutLog(request).unwrap()
      // Source copy stays sentence case; `className: 'uppercase'` sets the
      // toast's outer classname (sonner applies it to the wrapping <li>), which
      // cascades `text-transform: uppercase` onto the title text — the toast
      // renders capitalized without baking that casing into the stored string.
      toast.success(`Run logged — ${formatDistanceMeters(request.distanceMeters, units) ?? '…'}`, {
        className: 'uppercase',
      })
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
      setFormAlert('Couldn’t save. Nothing lost.')
    }
  }

  return (
    <LogForm
      form={form}
      onSubmit={onSubmit}
      isLoading={isLoading}
      formAlert={formAlert}
      distanceLabel={`Distance (${distanceUnitLabel(units)})`}
      units={units}
    />
  )
}

interface UnitPreferenceUnavailableProps {
  onRetry: () => void
}

/**
 * Shown when the unit-preference query errors. The form stays gated rather than
 * falling back to km, because `GET /settings/units` and the log `POST` are
 * independent endpoints — silently defaulting to km could persist a miles
 * runner's distance at ~1.6× the wrong magnitude (DEC-086). Retry re-runs the
 * preference query.
 */
const UnitPreferenceUnavailable = ({ onRetry }: UnitPreferenceUnavailableProps) => (
  <div
    role="alert"
    data-testid="log-page-units-error"
    className="flex flex-col items-center gap-3 py-8 text-center"
  >
    <p className="text-sm text-muted-foreground">
      We couldn’t load your distance-unit preference. Check your connection and try again.
    </p>
    <Button type="button" size="sm" onClick={onRetry}>
      Retry
    </Button>
  </div>
)

interface LogPageContentProps {
  units: PreferredUnits
  isResolved: boolean
  isError: boolean
  refetch: () => void
  editDraft: StructuredLogDraft | null
  /** `location.key` — remounts the form on each fresh /log navigation (see below). */
  formKey: string
}

/**
 * Selects what renders below the `/log` header based on the preference query
 * state: a retry on error, a loading placeholder until settled, otherwise the
 * form. Split out of {@link LogPage} so the three states read as early returns
 * rather than a nested ternary.
 */
const LogPageContent = ({
  units,
  isResolved,
  isError,
  refetch,
  editDraft,
  formKey,
}: LogPageContentProps) => {
  if (isError) {
    return <UnitPreferenceUnavailable onRetry={refetch} />
  }
  if (!isResolved) {
    return (
      <p role="status" className="text-sm text-muted-foreground" data-testid="log-page-loading">
        Loading…
      </p>
    )
  }
  // Key on `location.key` so re-navigating to /log with a different draft remounts
  // the form: `useForm` only seeds `defaultValues` on mount, so a new editDraft
  // would otherwise leave the previous draft's fields in place.
  return <WorkoutLogFormView key={formKey} units={units} editDraft={editDraft} />
}

/**
 * The `/log` route — a mobile-first workout-logging form. The form's numeric
 * write interprets the entered distance in the runner's unit preference, so it
 * defers the form until that preference has resolved (usually instant — the
 * preference is cached app-wide; only a cold `/log` refresh shows the brief
 * loading state). This keeps a miles-preferring runner's input from ever being
 * converted against the loading-time km fallback (DEC-086); an errored read
 * surfaces a retry rather than falling through to km.
 */
const LogPage = () => {
  const location = useLocation()
  const editDraft = (location.state as LogPageLocationState | null)?.draft ?? null
  const { units, isResolved, isError, refetch } = usePreferredUnitsResolution()

  return (
    <main
      className="mx-auto flex min-h-full w-full max-w-md flex-col gap-6 bg-background px-4 py-8"
      data-testid="log-page"
    >
      <header className="flex flex-col gap-1">
        <h1 className="t-screen-title text-foreground">Log run</h1>
        <p className="text-sm text-muted-foreground">
          Record what you actually ran — the plan adapts to the truth, not the intention.
        </p>
      </header>
      <LogPageContent
        units={units}
        isResolved={isResolved}
        isError={isError}
        refetch={refetch}
        editDraft={editDraft}
        formKey={location.key}
      />
    </main>
  )
}

export default LogPage
export { LogPage }
