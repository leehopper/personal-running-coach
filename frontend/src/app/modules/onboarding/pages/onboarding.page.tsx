import { useState, type ReactElement } from 'react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import type { PreferredUnits } from '~/api/generated'
import { useGetOnboardingStateQuery } from '~/api/onboarding.api'
import { usePutUnitPreferenceMutation } from '~/api/settings.api'
import { reportClientError } from '~/error-boundary/report-client-error'
import { usePreferredUnitsResolution } from '~/modules/settings/hooks/use-preferred-units.hooks'
import { OnboardingForm } from '~/modules/onboarding/components/onboarding-form.component'
import {
  hydrateOnboardingFormFields,
  makeDefaultOnboardingFormFields,
  reseedDistancesForUnitChange,
  type OnboardingFormFields,
} from '~/modules/onboarding/schemas/onboarding-form.schema'

/** RTK Query surfaces an opaque error union; treat a 404 as "no stream yet". */
const isErrorStatus = (error: unknown, expected: number): boolean => {
  if (typeof error !== 'object' || error === null) return false
  return 'status' in error && error.status === expected
}

interface RetryPanelProps {
  message: string
  testId: string
  onRetry: () => void
}

const RetryPanel = ({ message, testId, onRetry }: RetryPanelProps) => (
  <div
    role="alert"
    data-testid={testId}
    className="flex flex-col items-center gap-3 py-8 text-center"
  >
    <p className="text-sm text-muted-foreground">{message}</p>
    <Button type="button" size="sm" onClick={onRetry}>
      Retry
    </Button>
  </div>
)

interface OnboardingContentProps {
  unitsError: boolean
  stateFatalError: boolean
  isResolved: boolean
  stateLoading: boolean
  refetchUnits: () => void
  refetchState: () => void
  units: PreferredUnits
  initialFields: OnboardingFormFields
  unitsChangePending: boolean
  onUnitsChange: (nextUnits: PreferredUnits, currentValues: OnboardingFormFields) => void
}

/**
 * Selects what renders below the page header based on the unit-preference /
 * onboarding-state query outcomes: a retry on error, a loading placeholder until
 * both settle, otherwise the form (mounted keyed on `units` so a unit change
 * remounts it against the re-seeded values). Extracted as a component (not an
 * inline render helper) so the branches read as early returns.
 */
const OnboardingContent = ({
  unitsError,
  stateFatalError,
  isResolved,
  stateLoading,
  refetchUnits,
  refetchState,
  units,
  initialFields,
  unitsChangePending,
  onUnitsChange,
}: OnboardingContentProps): ReactElement => {
  if (unitsError) {
    return (
      <RetryPanel
        message="We couldn’t load your unit preference. Check your connection and try again."
        testId="onboarding-units-error"
        onRetry={refetchUnits}
      />
    )
  }
  if (stateFatalError) {
    return (
      <RetryPanel
        message="We couldn’t reach the onboarding service. Check your connection and try again."
        testId="onboarding-state-error"
        onRetry={refetchState}
      />
    )
  }
  if (!isResolved || stateLoading) {
    return (
      <p role="status" className="text-sm text-muted-foreground" data-testid="onboarding-loading">
        Loading…
      </p>
    )
  }

  return (
    <OnboardingForm
      key={units}
      units={units}
      initialFields={initialFields}
      unitsChangePending={unitsChangePending}
      onUnitsChange={onUnitsChange}
    />
  )
}

/**
 * The `/onboarding` route — a single-page, mobile-first structured form
 * (DEC-086). It defers the form until the unit preference has resolved (the
 * numeric write interprets distances in the runner's unit, so it must never fall
 * through to the loading-time km default — the `/log` posture), hydrates the
 * form from `GET /state` on resume, and re-seeds the distances when the runner
 * changes units. Completion + the redirect to `/` are owned by the
 * `OnboardingRedirectGuard` (this route is wrapped by it) once the submit
 * invalidates the `Onboarding` state.
 */
export const OnboardingPage = (): ReactElement => {
  const {
    units,
    isResolved,
    isError: unitsError,
    refetch: refetchUnits,
  } = usePreferredUnitsResolution()
  const [putUnitPreference] = usePutUnitPreferenceMutation()
  const {
    data: stateDto,
    isLoading: stateLoading,
    isError: stateIsError,
    error: stateError,
    refetch: refetchState,
  } = useGetOnboardingStateQuery(undefined)

  // A units change re-seeds the distance fields; the form remounts against this
  // (keyed on `units`) so the schema, labels, and km write-conversion recompute
  // once against the new unit. Null means "use the hydrated defaults".
  const [seed, setSeed] = useState<OnboardingFormFields | null>(null)
  // The unit the runner is switching TO while its `PUT` + refetch round-trip is
  // in flight. The units control is disabled until `units` catches up, so a
  // correction click during the (non-optimistic) round-trip can't be swallowed
  // by a stale-prop equality check. Derived below (not cleared in an effect):
  // once the resolved `units` reaches the target the pending flag falls false on
  // its own; a failed persist clears it explicitly.
  const [pendingUnits, setPendingUnits] = useState<PreferredUnits | null>(null)
  const unitsChangePending = pendingUnits !== null && pendingUnits !== units

  const persistUnitsAndReseed = async (
    nextUnits: PreferredUnits,
    currentValues: OnboardingFormFields,
  ): Promise<void> => {
    const converted = reseedDistancesForUnitChange(currentValues, units, nextUnits)
    setPendingUnits(nextUnits)
    try {
      await putUnitPreference({ preferredUnits: nextUnits }).unwrap()
      // Only re-seed once the preference is persisted; on failure the form stays
      // on the current unit with its typed values intact.
      setSeed(converted)
    } catch (error) {
      setPendingUnits(null)
      reportClientError({
        kind: 'unhandled-rejection',
        error: error instanceof Error ? error : new Error(String(error)),
      })
      toast.error('We could not save your unit preference. Try again in a moment.')
    }
  }

  const stateIs404 = stateIsError && isErrorStatus(stateError, 404)
  const stateFatalError = stateIsError && !stateIs404

  // 404 → no stream yet → a fresh, blank form; 200 → hydrate the resumed answers.
  // A units change overrides with the re-seeded (unit-converted) values.
  const hydrated =
    stateDto !== undefined
      ? hydrateOnboardingFormFields(stateDto, units)
      : makeDefaultOnboardingFormFields()

  return (
    <main
      className="mx-auto flex min-h-screen w-full max-w-md flex-col gap-6 bg-background px-4 py-8"
      data-testid="onboarding-page"
    >
      <header className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-foreground">Let's build your plan</h1>
        <p className="text-sm text-muted-foreground">
          Answer a few questions and your coach will draft a training plan.
        </p>
      </header>
      <OnboardingContent
        unitsError={unitsError}
        stateFatalError={stateFatalError}
        isResolved={isResolved}
        stateLoading={stateLoading}
        refetchUnits={refetchUnits}
        refetchState={() => {
          void refetchState()
        }}
        units={units}
        initialFields={seed ?? hydrated}
        unitsChangePending={unitsChangePending}
        onUnitsChange={(nextUnits, currentValues) => {
          void persistUnitsAndReseed(nextUnits, currentValues)
        }}
      />
    </main>
  )
}
