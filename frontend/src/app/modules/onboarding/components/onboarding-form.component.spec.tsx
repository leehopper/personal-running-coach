import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { PreferredUnits } from '~/api/generated'
import {
  PrimaryGoal,
  type SubmitStructuredAnswersRequest,
} from '~/modules/onboarding/models/onboarding.model'
import { makeDefaultOnboardingFormFields } from '~/modules/onboarding/schemas/onboarding-form.schema'
import { renderInBothThemes, testidsIn } from '~/modules/common/test-utils/render-in-both-themes'

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

  it('renders the Alpine intake in light and dark themes', () => {
    const result = renderInBothThemes(
      <OnboardingForm
        units={PreferredUnits.Kilometers}
        initialFields={{
          ...makeDefaultOnboardingFormFields(),
          goal: String(PrimaryGoal.RaceTraining),
        }}
        onUnitsChange={onUnitsChange}
      />,
    )

    expect(testidsIn(result.dark.container)).toEqual(testidsIn(result.light.container))
    for (const renderResult of [result.dark, result.light]) {
      for (const label of [
        '00 \u2014 In your own words',
        '01 \u2014 The goal',
        '02 \u2014 The race',
        "03 \u2014 Where you're at",
        '04 \u2014 Your week',
        '05 \u2014 The fine print',
      ]) {
        expect(renderResult.getByRole('heading', { level: 2, name: label })).toBeInTheDocument()
      }
      expect(
        renderResult.getByPlaceholderText(
          'Coming back from a calf strain. 10K in October. Tuesdays are impossible, and I hate treadmills\u2026',
        ),
      ).toBeInTheDocument()
      expect(
        renderResult.getByText(
          'The coach reads this first. Plain words beat perfect forms \u2014 the form below keeps the numbers honest.',
        ),
      ).toBeInTheDocument()
      expect(renderResult.getByRole('button', { name: 'Build my plan' })).toBeInTheDocument()
      expect(
        renderResult.getByText(
          'A recent race sharpens your pace zones. No race \u2014 no problem.',
        ),
      ).toBeInTheDocument()

      expect(renderResult.getByRole('radiogroup', { name: 'Units' })).toBeInTheDocument()
      expect(
        renderResult.getByRole('radiogroup', { name: "What's your primary goal?" }),
      ).toBeInTheDocument()
      expect(renderResult.getByRole('textbox', { name: 'In your own words' })).toBeInTheDocument()
      expect(renderResult.getByRole('toolbar', { name: 'Preferred run days' })).toBeInTheDocument()
      for (const name of [
        'Current injury or limitation',
        'Comfortable with hard, structured work',
        'Prefer trails where possible',
      ]) {
        expect(renderResult.getByRole('switch', { name })).toBeInTheDocument()
      }

      expect(renderResult.container.innerHTML).not.toMatch(/text-\[#/)
      expect(renderResult.container.innerHTML).not.toMatch(/bg-\[#/)
      expect(renderResult.container.innerHTML).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
      expect(renderResult.container.innerHTML).not.toContain('--alp-faint')
    }
  })

  it('renders narrative before the goal with locked copy', () => {
    renderForm()

    const sections = [...document.querySelectorAll('section[data-testid]')].map((section) =>
      section.getAttribute('data-testid'),
    )
    expect(sections).toEqual([
      'onboarding-section-narrative',
      'onboarding-section-goal',
      'onboarding-section-fitness',
      'onboarding-section-schedule',
      'onboarding-section-fine-print',
    ])
    expect(
      screen.getByRole('heading', { level: 2, name: '00 \u2014 In your own words' }),
    ).toBeInTheDocument()
    const narrative = screen.getByPlaceholderText(
      'Coming back from a calf strain. 10K in October. Tuesdays are impossible, and I hate treadmills\u2026',
    )
    expect(narrative).toHaveAttribute('maxLength', '1000')
    expect(narrative).toHaveClass('min-h-[96px]')
    expect(
      screen.getByText(
        'The coach reads this first. Plain words beat perfect forms \u2014 the form below keeps the numbers honest.',
      ),
    ).toBeInTheDocument()
  })

  it('reveals the race and direct active-injury fields', async () => {
    const { user } = renderForm()

    await user.click(screen.getByRole('radio', { name: /train for a race/i }))
    expect(screen.getByTestId('onboarding-section-target-event')).toHaveClass(
      'animate-in',
      'fade-in-0',
      'motion-reduce:animate-none',
    )
    expect(screen.queryByTestId('target-event-detail-trigger')).not.toBeInTheDocument()

    await user.click(screen.getByRole('switch', { name: 'Current injury or limitation' }))
    expect(screen.getByLabelText("What's bothering you right now?")).toBeInTheDocument()
  })

  it('places detail triggers only in the sections with nuance fields', async () => {
    const { user } = renderForm()

    const goalDetailTrigger = screen.getByTestId('goalDescription-trigger')
    expect(goalDetailTrigger).toHaveTextContent('+ Add detail')
    expect(goalDetailTrigger).toHaveClass(
      't-data-label',
      'text-clay-text',
      'min-h-11',
      'focus-visible:ring-[3px]',
      'active:scale-[0.98]',
      'motion-reduce:transition-none',
    )
    const monoLabel = goalDetailTrigger.querySelector('[data-testid="mono-label"]')
    if (monoLabel === null) throw new Error('Expected a mono detail label')
    expect(monoLabel).toHaveClass('t-data-label', 'text-clay-text')
    const detailContent = goalDetailTrigger.parentElement?.querySelector(
      '[data-slot="collapsible-content"]',
    )
    if (detailContent === undefined || detailContent === null) {
      throw new Error('Expected collapsible detail content')
    }
    expect(detailContent).toHaveClass(
      'data-[state=closed]:animate-out',
      'data-[state=closed]:fade-out-0',
      'data-[state=open]:animate-in',
      'data-[state=open]:fade-in-0',
      'motion-reduce:animate-none',
    )
    expect(screen.getByTestId('fitnessDescription-trigger')).toHaveTextContent('+ Add detail')
    expect(screen.getByTestId('scheduleDescription-trigger')).toHaveTextContent('+ Add detail')
    const finePrintDetailTrigger = screen.getByTestId('fine-print-detail-trigger')
    expect(finePrintDetailTrigger).toHaveTextContent(
      '+ Add detail \u2014 past injuries, preferences',
    )
    expect(finePrintDetailTrigger).toHaveAttribute('data-size', 'default')
    expect(finePrintDetailTrigger).toHaveClass(
      't-data-label',
      'min-h-11',
      'focus-visible:ring-[3px]',
      'active:scale-[0.98]',
      'motion-reduce:transition-none',
    )
    const finePrintMonoLabel = finePrintDetailTrigger.querySelector('[data-testid="mono-label"]')
    if (finePrintMonoLabel === null) throw new Error('Expected a mono detail label')
    expect(finePrintMonoLabel).toHaveClass('t-data-label', 'text-clay-text')
    expect(screen.queryByTestId('target-event-detail-trigger')).not.toBeInTheDocument()

    await user.click(screen.getByTestId('goalDescription-trigger'))
    await user.click(screen.getByTestId('fitnessDescription-trigger'))
    await user.click(screen.getByTestId('scheduleDescription-trigger'))
    await user.click(screen.getByTestId('fine-print-detail-trigger'))

    expect(screen.getByTestId('goalDescription-field')).toBeInTheDocument()
    expect(screen.getByTestId('fitnessDescription-field')).toBeInTheDocument()
    expect(screen.getByTestId('scheduleDescription-field')).toBeInTheDocument()
    expect(screen.getByTestId('pastInjurySummary-field')).toBeInTheDocument()
    expect(screen.getByTestId('preferencesDescription-field')).toBeInTheDocument()
  })

  it('renders day chips and switches with stable roles', async () => {
    const { user } = renderForm(PreferredUnits.Miles)

    expect(screen.getByTestId('days-field')).toHaveAttribute('aria-label', 'Preferred run days')
    for (const day of ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']) {
      expect(screen.getByRole('button', { name: day })).toHaveClass('min-h-11')
    }
    expect(screen.getAllByRole('switch')).toHaveLength(3)
    expect(screen.getByRole('switch', { name: 'Current injury or limitation' })).toHaveAttribute(
      'data-testid',
      'hasActiveInjury-field',
    )
    await user.click(screen.getByRole('radio', { name: /train for a race/i }))
    expect(screen.getByLabelText('Distance \u00B7 mi')).toBeInTheDocument()
    expect(screen.getByLabelText('Weekly volume \u00B7 mi')).toBeInTheDocument()
    expect(screen.getByLabelText('Longest recent \u00B7 mi')).toBeInTheDocument()
    expect(screen.getByLabelText('Recent race \u00B7 mi')).toBeInTheDocument()
  })

  it('shows the fixed building surface and makes the form inert while loading', () => {
    mutationStateRef.isLoading = true
    renderForm()

    expect(screen.getByTestId('onboarding-building')).toBeInTheDocument()
    expect(screen.getByTestId('onboarding-building').firstElementChild).toHaveClass(
      'fixed',
      'inset-0',
      'z-50',
    )
    expect(screen.getByTestId('onboarding-submit').closest('form')).toHaveAttribute('inert')
    expect(screen.getByRole('status')).toHaveTextContent('BUILDING YOUR PLAN')
  })

  it('returns every entered value and the same idempotency key after a handled 422', async () => {
    submitUnwrap.mockRejectedValueOnce({ status: 422 })
    const { user } = renderForm()
    await user.click(screen.getByRole('radio', { name: /train for a race/i }))
    await user.type(screen.getByTestId('eventName-field'), 'Chicago Marathon')
    await user.type(screen.getByTestId('eventDistance-field'), '42.2')
    await user.type(screen.getByTestId('eventDate-field'), '2026-10-11')
    await user.type(screen.getByTestId('targetFinishTime-field'), '3:45:00')
    await user.type(screen.getByTestId('typicalWeekly-field'), '37')
    await user.type(screen.getByTestId('longestRecentRun-field'), '19')
    await user.type(screen.getByTestId('recentRaceDistance-field'), '10')
    await user.type(screen.getByTestId('recentRaceTime-field'), '45:00')
    await user.type(screen.getByTestId('maxRunDays-field'), '5')
    await user.type(screen.getByTestId('sessionMinutes-field'), '70')
    await user.click(screen.getByRole('button', { name: 'Mon' }))
    await user.click(screen.getByRole('button', { name: 'Thu' }))
    await user.click(screen.getByRole('button', { name: 'Sat' }))
    await user.click(screen.getByRole('switch', { name: 'Current injury or limitation' }))
    await user.type(
      screen.getByLabelText("What's bothering you right now?"),
      'Right calf tightness',
    )
    await user.click(screen.getByRole('switch', { name: 'Comfortable with hard, structured work' }))
    await user.click(screen.getByRole('switch', { name: 'Prefer trails where possible' }))
    await user.type(
      screen.getByTestId('narrative-field'),
      'Returning carefully after a calf strain.',
    )
    await user.click(screen.getByTestId('goalDescription-trigger'))
    await user.type(screen.getByTestId('goalDescription-field'), 'Chasing a sub-four finish.')
    await submitForm(user)

    await waitFor(() => expect(screen.getByTestId('onboarding-form-alert')).toBeInTheDocument())
    const firstRequest = submitTrigger.mock.calls[0][0]
    expect(screen.getByRole('radio', { name: /train for a race/i })).toBeChecked()
    expect(screen.getByTestId('eventName-field')).toHaveValue('Chicago Marathon')
    expect(screen.getByTestId('eventDistance-field')).toHaveValue('42.2')
    expect(screen.getByTestId('eventDate-field')).toHaveValue('2026-10-11')
    expect(screen.getByTestId('targetFinishTime-field')).toHaveValue('3:45:00')
    expect(screen.getByTestId('typicalWeekly-field')).toHaveValue('37')
    expect(screen.getByTestId('longestRecentRun-field')).toHaveValue('19')
    expect(screen.getByTestId('recentRaceDistance-field')).toHaveValue('10')
    expect(screen.getByTestId('recentRaceTime-field')).toHaveValue('45:00')
    expect(screen.getByTestId('maxRunDays-field')).toHaveValue('5')
    expect(screen.getByTestId('sessionMinutes-field')).toHaveValue('70')
    for (const day of ['Mon', 'Thu', 'Sat']) {
      expect(screen.getByRole('button', { name: day })).toHaveAttribute('data-state', 'on')
    }
    for (const name of [
      'Current injury or limitation',
      'Comfortable with hard, structured work',
      'Prefer trails where possible',
    ]) {
      expect(screen.getByRole('switch', { name })).toHaveAttribute('data-state', 'checked')
    }
    expect(screen.getByLabelText("What's bothering you right now?")).toHaveValue(
      'Right calf tightness',
    )
    expect(screen.getByTestId('narrative-field')).toHaveValue(
      'Returning carefully after a calf strain.',
    )
    expect(screen.getByTestId('goalDescription-field')).toHaveValue('Chasing a sub-four finish.')
    expect(screen.getByTestId('onboarding-submit')).toBeEnabled()
    expect(screen.queryByTestId('onboarding-building')).not.toBeInTheDocument()

    submitUnwrap.mockResolvedValueOnce({ isComplete: true, currentPlanId: 'plan-1' })
    await submitForm(user)
    await waitFor(() => expect(submitTrigger).toHaveBeenCalledTimes(2))
    expect(submitTrigger.mock.calls[1][0].idempotencyKey).toBe(firstRequest.idempotencyKey)
  })

  it('renders the units field first and every numbered section', () => {
    renderForm()
    const form = screen.getByTestId('onboarding-submit').closest('form')
    if (form === null) throw new Error('Expected onboarding form')
    const formNodes = [...form.querySelectorAll('*')]
    const indexOf = (element: Element): number => formNodes.indexOf(element)
    const unitsField = screen.getByTestId('onboarding-units-field')
    const narrativeSection = screen.getByTestId('onboarding-section-narrative')
    const firstNumberedSection = screen.getByTestId('onboarding-section-goal')

    expect(indexOf(unitsField)).toBeLessThan(indexOf(narrativeSection))
    expect(indexOf(unitsField)).toBeLessThan(indexOf(firstNumberedSection))
    const numericInputs = form.querySelectorAll('[inputmode="numeric"], [inputmode="decimal"]')
    expect(numericInputs.length).toBeGreaterThan(0)
    for (const numericInput of numericInputs) {
      expect(indexOf(unitsField)).toBeLessThan(indexOf(numericInput))
    }
  })

  it('reveals the TargetEvent section only for a race-training goal', async () => {
    const { user } = renderForm()
    expect(screen.queryByTestId('onboarding-section-target-event')).toBeNull()

    await user.click(screen.getByRole('radio', { name: /train for a race/i }))
    expect(screen.getByTestId('onboarding-section-target-event')).toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: /general fitness/i }))
    expect(screen.queryByTestId('onboarding-section-target-event')).toBeNull()
  })

  it('does not leave submit stuck-disabled when the goal switches away from race with an invalid distance', async () => {
    // Regression (deep-review): an out-of-range race distance entered then hidden
    // (goal switched away) must not keep formState.isValid false forever.
    const { user } = renderForm()
    await user.click(screen.getByRole('radio', { name: /train for a race/i }))
    await user.type(screen.getByTestId('eventDistance-field'), '0')
    await user.type(screen.getByTestId('typicalWeekly-field'), '40')
    await user.type(screen.getByTestId('longestRecentRun-field'), '18')
    await user.type(screen.getByTestId('maxRunDays-field'), '5')
    await user.type(screen.getByTestId('sessionMinutes-field'), '60')

    // Switch the goal away - the TargetEvent section (and its invalid distance) hides.
    await user.click(screen.getByRole('radio', { name: /general fitness/i }))
    expect(screen.queryByTestId('onboarding-section-target-event')).toBeNull()

    // The hidden invalid distance must NOT keep the submit disabled.
    await waitFor(() => expect(screen.getByTestId('onboarding-submit')).toBeEnabled())
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

  it('creates a fresh idempotency key for each independent form mount', async () => {
    const first = renderForm()
    await fillMinimalValid(first.user)
    await submitForm(first.user)
    await waitFor(() => expect(submitTrigger).toHaveBeenCalledTimes(1))
    const firstKey = submitTrigger.mock.calls[0][0].idempotencyKey
    first.unmount()

    const second = renderForm()
    await fillMinimalValid(second.user)
    await submitForm(second.user)
    await waitFor(() => expect(submitTrigger).toHaveBeenCalledTimes(2))
    const secondKey = submitTrigger.mock.calls[1][0].idempotencyKey

    expect(firstKey).toMatch(UUID_PATTERN)
    expect(secondKey).toMatch(UUID_PATTERN)
    expect(secondKey).not.toBe(firstKey)
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
    expect(screen.getByLabelText('Weekly volume \u00B7 mi')).toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: /general fitness/i }))
    await user.type(screen.getByTestId('typicalWeekly-field'), '10')
    await user.type(screen.getByTestId('longestRecentRun-field'), '5')
    await user.type(screen.getByTestId('maxRunDays-field'), '5')
    await user.type(screen.getByTestId('sessionMinutes-field'), '60')
    await submitForm(user)

    await waitFor(() => expect(submitTrigger).toHaveBeenCalledTimes(1))
    // 10 mi x 1.609344 = 16.09344 km
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

  it('keeps submit disabled while a unit change is pending even once the record is valid', async () => {
    const { user } = renderForm(PreferredUnits.Kilometers, { unitsChangePending: true })
    await fillMinimalValid(user)
    // The record validates, but the pending unit change (form about to reseed)
    // must keep submit blocked.
    expect(screen.getByTestId('onboarding-submit')).toBeDisabled()
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
    const monday = screen.getByRole('button', { name: 'Mon' })
    monday.focus()

    await user.keyboard(' ')
    expect(monday).toHaveAttribute('data-state', 'on')

    await user.keyboard(' ')
    expect(monday).toHaveAttribute('data-state', 'off')
  })

  it('carries the selected run days into the submitted schedule', async () => {
    const { user } = renderForm()
    await fillMinimalValid(user)
    await user.click(screen.getByRole('button', { name: 'Mon' }))
    await user.click(screen.getByRole('button', { name: 'Wed' }))
    await submitForm(user)

    await waitFor(() => expect(submitTrigger).toHaveBeenCalledTimes(1))
    expect(submitTrigger.mock.calls[0][0].weeklySchedule).toMatchObject({
      monday: true,
      tuesday: false,
      wednesday: true,
    })
  })

  it('showsBuildingPlanSurfaceAfterCompleteUntilRedirect', async () => {
    const { user } = renderForm()
    await fillMinimalValid(user)
    await submitForm(user)

    const firstRequest = submitTrigger.mock.calls[0][0]
    expect(await screen.findByTestId('onboarding-building')).toBeInTheDocument()
    expect(screen.getByTestId('onboarding-section-narrative')).toBeInTheDocument()
    expect(screen.getByTestId('onboarding-submit')).toBeInTheDocument()

    await submitForm(user)
    await waitFor(() => expect(submitTrigger).toHaveBeenCalledTimes(2))
    expect(submitTrigger.mock.calls[1][0].idempotencyKey).toBe(firstRequest.idempotencyKey)
    expect(screen.getByTestId('onboarding-building')).toBeInTheDocument()
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
    expect(submit).toHaveTextContent('Building your plan\u2026')
  })
})
