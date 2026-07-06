import { useMemo, useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'

import { Button } from '@/components/ui/button'
import { Form } from '@/components/ui/form'
import { PreferredUnits } from '~/api/generated'
import { useSubmitStructuredAnswersMutation } from '~/api/onboarding.api'
import { reportClientError } from '~/error-boundary/report-client-error'
import { distanceUnitLabel } from '~/modules/common/utils/unit-format.helpers'
import { PrimaryGoal } from '~/modules/onboarding/models/onboarding.model'
import {
  makeOnboardingFormSchema,
  toSubmitStructuredAnswersRequest,
  type OnboardingFormFields,
  type OnboardingFormValues,
} from '~/modules/onboarding/schemas/onboarding-form.schema'
import { OnboardingFitnessSection } from './onboarding-fitness-section.component'
import { OnboardingGoalSection } from './onboarding-goal-section.component'
import { OnboardingInjurySection } from './onboarding-injury-section.component'
import { OnboardingPreferencesSection } from './onboarding-preferences-section.component'
import { OnboardingScheduleSection } from './onboarding-schedule-section.component'
import { OnboardingTargetEventSection } from './onboarding-target-event-section.component'
import { OnboardingUnitsField } from './onboarding-units-field.component'

export interface OnboardingFormProps {
  /** The runner's resolved display unit — distances are entered and shown in this unit. */
  units: PreferredUnits
  /** Seed values (blank defaults, or hydrated from a resumed `GET /state`). */
  initialFields: OnboardingFormFields
  /** Fired when the runner changes units; the page persists it and re-seeds distances. */
  onUnitsChange: (nextUnits: PreferredUnits, currentValues: OnboardingFormFields) => void
  /** True while a prior unit change is persisting; disables the units control (no dropped click). */
  unitsChangePending?: boolean
}

/**
 * The single-page onboarding form. Owns the RHF form instance (schema derived
 * from the resolved `units`), the `POST /answers` mutation, and the submit
 * orchestration. The whole record is co-submitted in one request, so there is no
 * per-topic clarification loop. On a completed submission the mutation
 * invalidates the `Onboarding` tag and the route guard redirects to `/` — this
 * component only surfaces the in-flight "building" state and any error.
 *
 * The form is mounted keyed on `units` by the page, so a mid-form unit change
 * remounts it against the re-seeded (unit-converted) values — the schema, the
 * distance labels, and the km write-conversion are all computed once against the
 * correct unit (the shipped `/log` pattern), never a mid-form schema swap.
 */
export const OnboardingForm = ({
  units,
  initialFields,
  onUnitsChange,
  unitsChangePending = false,
}: OnboardingFormProps) => {
  const [submitStructuredAnswers, { isLoading }] = useSubmitStructuredAnswersMutation()
  const [formAlert, setFormAlert] = useState<string | null>(null)
  const [isBuilding, setIsBuilding] = useState(false)
  // Idempotency key for the current submit attempt (DEC-077). Stable across
  // retries of the SAME data (a transient failure re-submits and the server
  // dedupes) but rotated after a non-terminal success so an edited resubmit is a
  // genuinely new attempt, not a replay of the memoized prior result.
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())

  const schema = useMemo(() => makeOnboardingFormSchema(units), [units])
  const form = useForm<OnboardingFormFields, unknown, OnboardingFormValues>({
    resolver: zodResolver(schema),
    mode: 'onChange',
    shouldUnregister: false,
    defaultValues: initialFields,
  })

  const goalValue = useWatch({ control: form.control, name: 'goal' })
  const isRaceGoal = goalValue === String(PrimaryGoal.RaceTraining)
  const unitLabel = distanceUnitLabel(units)

  const onValid = async (values: OnboardingFormValues): Promise<void> => {
    setFormAlert(null)
    try {
      const state = await submitStructuredAnswers(
        toSubmitStructuredAnswersRequest(values, idempotencyKey, units),
      ).unwrap()
      if (state.isComplete) {
        // The `Onboarding` tag invalidation refetches the guard's state query,
        // which then redirects to `/`; hold the building state until it unmounts.
        setIsBuilding(true)
      } else {
        // The answers were appended but the gate is not satisfied. Rotate the
        // idempotency key so a subsequent edited resubmit is processed afresh
        // rather than replaying this memoized non-terminal result.
        setIdempotencyKey(crypto.randomUUID())
        setFormAlert('We saved your answers but could not finish. Please review and submit again.')
      }
    } catch (error) {
      // The awaited `.unwrap()` rejection is a *handled* rejection the global
      // reporter/boundary never sees — forward it so a failed onboarding still
      // leaves a diagnostic trail.
      reportClientError({
        kind: 'unhandled-rejection',
        error: error instanceof Error ? error : new Error(String(error)),
      })
      setFormAlert('We could not build your plan. Please try again in a moment.')
    }
  }

  const handleUnitsChange = (nextUnits: PreferredUnits): void => {
    if (nextUnits === units) return
    onUnitsChange(nextUnits, form.getValues())
  }

  if (isBuilding) {
    return (
      <p
        role="status"
        aria-live="polite"
        className="text-sm text-muted-foreground"
        data-testid="onboarding-building"
      >
        Building your plan…
      </p>
    )
  }

  return (
    <Form {...form}>
      {/* noValidate: validation is Zod's, surfaced via FormMessage, not the browser's. */}
      <form onSubmit={form.handleSubmit(onValid)} className="flex flex-col gap-6" noValidate>
        <OnboardingUnitsField
          units={units}
          onChange={handleUnitsChange}
          disabled={unitsChangePending}
        />
        <OnboardingGoalSection control={form.control} />
        {isRaceGoal ? (
          <OnboardingTargetEventSection control={form.control} unitLabel={unitLabel} />
        ) : null}
        <OnboardingFitnessSection control={form.control} unitLabel={unitLabel} />
        <OnboardingScheduleSection control={form.control} />
        <OnboardingInjurySection control={form.control} />
        <OnboardingPreferencesSection control={form.control} />

        {formAlert !== null ? (
          <p role="alert" className="text-sm text-destructive" data-testid="onboarding-form-alert">
            {formAlert}
          </p>
        ) : null}

        {/* Disabled until the whole record validates (repo Forms convention) or a
            submit is in flight. `mode: 'onChange'` keeps `isValid` live as fields fill. */}
        <Button
          type="submit"
          size="lg"
          disabled={isLoading || !form.formState.isValid}
          data-testid="onboarding-submit"
        >
          {isLoading ? 'Building your plan…' : 'Create my plan'}
        </Button>
      </form>
    </Form>
  )
}
