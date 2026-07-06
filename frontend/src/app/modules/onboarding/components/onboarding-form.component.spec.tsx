import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { PreferredUnits } from '~/api/generated'
import {
  PrimaryGoal,
  type SubmitStructuredAnswersRequest,
} from '~/modules/onboarding/models/onboarding.model'
import { makeDefaultOnboardingFormFields } from '~/modules/onboarding/schemas/onboarding-form.schema'

// vi.mock is hoisted above imports, so the mock fns must come from vi.hoisted.
const { submitTrigger, submitUnwrap, mutationStateRef, reportClientErrorMock } = vi.hoisted(() => {
  const submitUnwrap = vi.fn()
  return {
    submitUnwrap,
    submitTrigger: vi.fn<
      (body: SubmitStructuredAnswersRequest) => { unwrap: () => Promise<unknown> }
    >(() => ({ unwrap: submitUnwrap })),
    mutationStateRef: { isLoading: false },
    reportClientErrorMock: vi.fn(),
  }
})

vi.mock('~/api/onboarding.api', () => ({
  useSubmitStructuredAnswersMutation: () => [submitTrigger, mutationStateRef],
}))

vi.mock('~/error-boundary/report-client-error', () => ({
  reportClientError: reportClientErrorMock,
}))

// Component under test imported after the mocks.
import { OnboardingForm } from './onboarding-form.component'

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

const onUnitsChange = vi.fn()

const renderForm = (
  units: PreferredUnits = PreferredUnits.Kilometers,
  { unitsChangePending = false }: { unitsChangePending?: boolean } = {},
) => {
  const user = userEvent.setup()
  const utils = render(
    <OnboardingForm
      units={units}
      initialFields={makeDefaultOnboardingFormFields()}
      onUnitsChange={onUnitsChange}
      unitsChangePending={unitsChangePending}
    />,
  )
  return { user, ...utils }
}

/** Fills the minimum valid non-race submission (goal + fitness + schedule). */
const fillMinimalValid = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(screen.getByRole('radio', { name: /general fitness/i }))
  await user.type(screen.getByTestId('typicalWeekly-field'), '40')
  await user.type(screen.getByTestId('longestRecentRun-field'), '18')
  await user.type(screen.getByTestId('maxRunDays-field'), '5')
  await user.type(screen.getByTestId('sessionMinutes-field'), '60')
}

/** Waits for the submit button to enable (validation settles) then clicks it. */
const submitForm = async (user: ReturnType<typeof userEvent.setup>) => {
  const submit = screen.getByTestId('onboarding-submit')
  await waitFor(() => expect(submit).toBeEnabled())
  await user.click(submit)
}

describe('OnboardingForm', () => {
  beforeEach(() => {
    submitTrigger.mockClear()
    submitUnwrap.mockReset()
    submitUnwrap.mockResolvedValue({ isComplete: true, currentPlanId: 'plan-1' })
    mutationStateRef.isLoading = false
    reportClientErrorMock.mockReset()
    onUnitsChange.mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders the units field first and every topic section', () => {
    renderForm()
    expect(screen.getByTestId('onboarding-units-field')).toBeInTheDocument()
    expect(screen.getByTestId('onboarding-section-goal')).toBeInTheDocument()
    expect(screen.getByTestId('onboarding-section-fitness')).toBeInTheDocument()
    expect(screen.getByTestId('onboarding-section-schedule')).toBeInTheDocument()
    expect(screen.getByTestId('onboarding-section-injury')).toBeInTheDocument()
    expect(screen.getByTestId('onboarding-section-preferences')).toBeInTheDocument()
  })

  it('reveals the TargetEvent section only for a race-training goal', async () => {
    const { user } = renderForm()
    expect(screen.queryByTestId('onboarding-section-target-event')).toBeNull()

    await user.click(screen.getByRole('radio', { name: /train for a race/i }))
    expect(screen.getByTestId('onboarding-section-target-event')).toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: /general fitness/i }))
    expect(screen.queryByTestId('onboarding-section-target-event')).toBeNull()
  })

  it('keeps submit disabled until the whole record is valid', async () => {
    const { user } = renderForm()
    expect(screen.getByTestId('onboarding-submit')).toBeDisabled()
    await fillMinimalValid(user)
    await waitFor(() => expect(screen.getByTestId('onboarding-submit')).toBeEnabled())
  })

  it('surfaces an accessible field error for an out-of-range entry', async () => {
    const { user } = renderForm()
    await user.type(screen.getByTestId('maxRunDays-field'), '8')
    const error = await screen.findByText(/at most 7/i)
    expect(error).toHaveAttribute('role', 'alert')
    expect(submitTrigger).not.toHaveBeenCalled()
  })

  it('submits a non-race profile with targetEvent null, a fresh idempotency key, and km distances', async () => {
    const { user } = renderForm()
    await fillMinimalValid(user)
    await submitForm(user)

    await waitFor(() => expect(submitTrigger).toHaveBeenCalledTimes(1))
    const request = submitTrigger.mock.calls[0][0]
    expect(request.idempotencyKey).toMatch(UUID_PATTERN)
    expect(request.primaryGoal.goal).toBe(PrimaryGoal.GeneralFitness)
    expect(request.targetEvent).toBeNull()
    expect(request.currentFitness.typicalWeeklyKm).toBe(40)
    expect(request.weeklySchedule.maxRunDaysPerWeek).toBe(5)
    expect(request.preferences.preferredUnits).toBe(PreferredUnits.Kilometers)
  })

  it('submits a race profile with the target event populated', async () => {
    const { user } = renderForm()
    await user.click(screen.getByRole('radio', { name: /train for a race/i }))
    await user.type(screen.getByTestId('eventName-field'), 'City Marathon')
    await user.type(screen.getByTestId('eventDistance-field'), '42.2')
    await user.type(screen.getByTestId('eventDate-field'), '2026-10-01')
    await user.type(screen.getByTestId('typicalWeekly-field'), '50')
    await user.type(screen.getByTestId('longestRecentRun-field'), '20')
    await user.type(screen.getByTestId('maxRunDays-field'), '5')
    await user.type(screen.getByTestId('sessionMinutes-field'), '60')
    await submitForm(user)

    await waitFor(() => expect(submitTrigger).toHaveBeenCalledTimes(1))
    expect(submitTrigger.mock.calls[0][0].targetEvent).toEqual({
      eventName: 'City Marathon',
      distanceKm: 42.2,
      eventDateIso: '2026-10-01',
      targetFinishTimeIso: null,
    })
  })

  it('interprets distances in miles and converts them to kilometres', async () => {
    const { user } = renderForm(PreferredUnits.Miles)
    // The distance labels speak miles.
    expect(screen.getByText(/weekly volume \(mi\)/i)).toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: /general fitness/i }))
    await user.type(screen.getByTestId('typicalWeekly-field'), '10')
    await user.type(screen.getByTestId('longestRecentRun-field'), '5')
    await user.type(screen.getByTestId('maxRunDays-field'), '5')
    await user.type(screen.getByTestId('sessionMinutes-field'), '60')
    await submitForm(user)

    await waitFor(() => expect(submitTrigger).toHaveBeenCalledTimes(1))
    // 10 mi × 1.609344 = 16.09344 km
    expect(submitTrigger.mock.calls[0][0].currentFitness.typicalWeeklyKm).toBeCloseTo(16.09344, 4)
  })

  it('reports a chosen unit up to the page without submitting', async () => {
    const { user } = renderForm(PreferredUnits.Kilometers)
    await user.click(screen.getByRole('radio', { name: 'Miles' }))
    expect(onUnitsChange).toHaveBeenCalledTimes(1)
    expect(onUnitsChange.mock.calls[0][0]).toBe(PreferredUnits.Miles)
    expect(submitTrigger).not.toHaveBeenCalled()
  })

  it('disables the units control while a change is pending', () => {
    renderForm(PreferredUnits.Kilometers, { unitsChangePending: true })
    expect(screen.getByRole('radio', { name: 'Miles' })).toBeDisabled()
  })

  it('keeps the free-text nuance boxes optional', async () => {
    const { user } = renderForm()
    await fillMinimalValid(user)
    await submitForm(user)
    await waitFor(() => expect(submitTrigger).toHaveBeenCalledTimes(1))
    expect(submitTrigger.mock.calls[0][0].currentFitness.description).toBe('')
  })

  it('selects and deselects run days with the keyboard', async () => {
    const { user } = renderForm()
    const monday = screen.getByRole('button', { name: 'monday' })
    monday.focus()

    await user.keyboard(' ')
    expect(monday).toHaveAttribute('data-state', 'on')

    await user.keyboard(' ')
    expect(monday).toHaveAttribute('data-state', 'off')
  })

  it('carries the selected run days into the submitted schedule', async () => {
    const { user } = renderForm()
    await fillMinimalValid(user)
    await user.click(screen.getByRole('button', { name: 'monday' }))
    await user.click(screen.getByRole('button', { name: 'wednesday' }))
    await submitForm(user)

    await waitFor(() => expect(submitTrigger).toHaveBeenCalledTimes(1))
    expect(submitTrigger.mock.calls[0][0].weeklySchedule).toMatchObject({
      monday: true,
      tuesday: false,
      wednesday: true,
    })
  })

  it('shows the building state after a completed submission', async () => {
    const { user } = renderForm()
    await fillMinimalValid(user)
    await submitForm(user)
    expect(await screen.findByTestId('onboarding-building')).toBeInTheDocument()
  })

  it('surfaces a partial-progress alert and rotates the idempotency key when the gate is unmet', async () => {
    submitUnwrap.mockResolvedValue({ isComplete: false })
    const { user } = renderForm()
    await fillMinimalValid(user)
    await submitForm(user)

    expect(await screen.findByTestId('onboarding-form-alert')).toHaveTextContent(
      /could not finish/i,
    )
    expect(screen.queryByTestId('onboarding-building')).toBeNull()

    // Resubmitting after the non-terminal result uses a fresh idempotency key.
    submitUnwrap.mockResolvedValue({ isComplete: true, currentPlanId: 'plan-1' })
    await submitForm(user)
    await waitFor(() => expect(submitTrigger).toHaveBeenCalledTimes(2))
    const [firstKey, secondKey] = submitTrigger.mock.calls.map((call) => call[0].idempotencyKey)
    expect(secondKey).not.toBe(firstKey)
    expect(secondKey).toMatch(UUID_PATTERN)
  })

  it('reports a failed submission and surfaces a retry alert', async () => {
    submitUnwrap.mockRejectedValue(new Error('network down'))
    const { user } = renderForm()
    await fillMinimalValid(user)
    await submitForm(user)

    await waitFor(() => expect(reportClientErrorMock).toHaveBeenCalledTimes(1))
    expect(screen.getByTestId('onboarding-form-alert')).toHaveTextContent(
      /could not build your plan/i,
    )
    expect(screen.queryByTestId('onboarding-building')).toBeNull()
  })

  it('disables the submit button and shows a building label while a submit is in flight', () => {
    mutationStateRef.isLoading = true
    renderForm()
    const submit = screen.getByTestId('onboarding-submit')
    expect(submit).toBeDisabled()
    expect(submit).toHaveTextContent('Building your plan…')
  })
})
