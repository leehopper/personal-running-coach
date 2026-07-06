import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { PreferredUnits } from '~/api/generated'
import { PrimaryGoal } from '~/modules/onboarding/models/onboarding.model'
import type { UsePreferredUnitsResolutionReturn } from '~/modules/settings/hooks/use-preferred-units.hooks'
import type { OnboardingFormProps } from '~/modules/onboarding/components/onboarding-form.component'

const {
  preferredUnitsResolutionMock,
  refetchUnitsMock,
  putUnitPreferenceTrigger,
  putUnitPreferenceUnwrap,
  getOnboardingStateMock,
  refetchStateMock,
  reportClientErrorMock,
} = vi.hoisted(() => {
  const putUnitPreferenceUnwrap = vi.fn()
  return {
    preferredUnitsResolutionMock: vi.fn<() => UsePreferredUnitsResolutionReturn>(),
    refetchUnitsMock: vi.fn(),
    putUnitPreferenceUnwrap,
    putUnitPreferenceTrigger: vi.fn(() => ({ unwrap: putUnitPreferenceUnwrap })),
    getOnboardingStateMock: vi.fn(),
    refetchStateMock: vi.fn(),
    reportClientErrorMock: vi.fn(),
  }
})

vi.mock('~/modules/settings/hooks/use-preferred-units.hooks', () => ({
  usePreferredUnitsResolution: () => preferredUnitsResolutionMock(),
}))

vi.mock('~/api/settings.api', () => ({
  usePutUnitPreferenceMutation: () => [putUnitPreferenceTrigger, {}],
}))

vi.mock('~/api/onboarding.api', () => ({
  useGetOnboardingStateQuery: () => getOnboardingStateMock(),
}))

vi.mock('~/error-boundary/report-client-error', () => ({
  reportClientError: reportClientErrorMock,
}))

// Stub the heavy form so the page test focuses on gating + the units-change
// handler; it echoes the props it received and can fire `onUnitsChange`.
vi.mock('~/modules/onboarding/components/onboarding-form.component', () => ({
  OnboardingForm: ({
    units,
    initialFields,
    onUnitsChange,
    unitsChangePending,
  }: OnboardingFormProps) => (
    <div
      data-testid="onboarding-form-stub"
      data-units={units}
      data-goal={initialFields.goal}
      data-typical-weekly={initialFields.typicalWeekly}
      data-pending={String(unitsChangePending)}
    >
      <button
        type="button"
        onClick={() =>
          onUnitsChange(PreferredUnits.Miles, { ...initialFields, typicalWeekly: '20.0' })
        }
      >
        change-units
      </button>
    </div>
  ),
}))

import { OnboardingPage } from './onboarding.page'

const unitsResolution = (
  overrides: Partial<UsePreferredUnitsResolutionReturn> = {},
): UsePreferredUnitsResolutionReturn => ({
  units: PreferredUnits.Kilometers,
  isResolved: true,
  isError: false,
  refetch: refetchUnitsMock,
  ...overrides,
})

const stateQuery = (overrides: Record<string, unknown> = {}) => ({
  data: undefined,
  isLoading: false,
  isError: false,
  error: undefined,
  refetch: refetchStateMock,
  ...overrides,
})

describe('OnboardingPage gating', () => {
  beforeEach(() => {
    preferredUnitsResolutionMock.mockClear()
    getOnboardingStateMock.mockClear()
    refetchUnitsMock.mockClear()
    refetchStateMock.mockClear()
    preferredUnitsResolutionMock.mockReturnValue(unitsResolution())
    getOnboardingStateMock.mockReturnValue(stateQuery({ isError: true, error: { status: 404 } }))
    putUnitPreferenceTrigger.mockClear()
    putUnitPreferenceUnwrap.mockReset()
    putUnitPreferenceUnwrap.mockResolvedValue({ preferredUnits: PreferredUnits.Miles })
    reportClientErrorMock.mockReset()
  })

  afterEach(() => {
    toast.dismiss()
    vi.restoreAllMocks()
  })

  it('shows a retry when the unit preference fails to load', () => {
    preferredUnitsResolutionMock.mockReturnValue(unitsResolution({ isError: true }))
    render(<OnboardingPage />)
    expect(screen.getByTestId('onboarding-units-error')).toBeInTheDocument()
    expect(screen.queryByTestId('onboarding-form-stub')).toBeNull()
  })

  it('shows a loading placeholder until the unit preference resolves', () => {
    preferredUnitsResolutionMock.mockReturnValue(unitsResolution({ isResolved: false }))
    render(<OnboardingPage />)
    expect(screen.getByTestId('onboarding-loading')).toBeInTheDocument()
  })

  it('shows a retry on a non-404 onboarding-state error', () => {
    getOnboardingStateMock.mockReturnValue(stateQuery({ isError: true, error: { status: 500 } }))
    render(<OnboardingPage />)
    expect(screen.getByTestId('onboarding-state-error')).toBeInTheDocument()
  })

  it('renders a blank form when no onboarding stream exists yet (404)', () => {
    render(<OnboardingPage />)
    const form = screen.getByTestId('onboarding-form-stub')
    expect(form).toHaveAttribute('data-units', String(PreferredUnits.Kilometers))
    expect(form).toHaveAttribute('data-goal', '')
  })

  it('hydrates the form from a resumed onboarding state', () => {
    getOnboardingStateMock.mockReturnValue(
      stateQuery({
        data: {
          userId: 'u1',
          status: 1,
          currentTopic: null,
          completedTopics: 1,
          totalTopics: 6,
          isComplete: false,
          outstandingClarifications: [],
          primaryGoal: { goal: PrimaryGoal.GeneralFitness, description: '' },
          targetEvent: null,
          currentFitness: null,
          weeklySchedule: null,
          injuryHistory: null,
          preferences: null,
          currentPlanId: null,
        },
      }),
    )
    render(<OnboardingPage />)
    expect(screen.getByTestId('onboarding-form-stub')).toHaveAttribute(
      'data-goal',
      String(PrimaryGoal.GeneralFitness),
    )
  })

  it('persists a unit change via PUT /settings/units', async () => {
    const user = userEvent.setup()
    render(<OnboardingPage />)
    await user.click(screen.getByRole('button', { name: 'change-units' }))

    await waitFor(() => expect(putUnitPreferenceTrigger).toHaveBeenCalledTimes(1))
    expect(putUnitPreferenceTrigger).toHaveBeenCalledWith({ preferredUnits: PreferredUnits.Miles })
    expect(reportClientErrorMock).not.toHaveBeenCalled()
  })

  it('reseeds the entered distances and remounts against the new unit after a successful change', async () => {
    // Hydrate with a km weekly distance so the reseed has a value to convert.
    getOnboardingStateMock.mockReturnValue(
      stateQuery({
        data: {
          userId: 'u1',
          status: 1,
          currentTopic: null,
          completedTopics: 6,
          totalTopics: 6,
          isComplete: false,
          outstandingClarifications: [],
          primaryGoal: { goal: PrimaryGoal.GeneralFitness, description: '' },
          targetEvent: null,
          currentFitness: {
            typicalWeeklyKm: 10,
            longestRecentRunKm: 5,
            recentRaceDistanceKm: null,
            recentRaceTimeIso: null,
            description: '',
          },
          weeklySchedule: null,
          injuryHistory: null,
          preferences: null,
          currentPlanId: null,
        },
      }),
    )
    const user = userEvent.setup()
    const { rerender } = render(<OnboardingPage />)

    const before = screen.getByTestId('onboarding-form-stub')
    expect(before).toHaveAttribute('data-units', String(PreferredUnits.Kilometers))
    // 10 km hydrates as '10.0' in km display.
    expect(before).toHaveAttribute('data-typical-weekly', '10.0')

    // The stub reports the runner edited weekly volume to 20 km, then switched to miles.
    await user.click(screen.getByRole('button', { name: 'change-units' }))
    // Once the PUT resolves, setSeed applies the reseeded (20 km → 12.4 mi) values;
    // the form is still keyed on km here, so it re-renders (not remounts) with the seed.
    await waitFor(() =>
      expect(screen.getByTestId('onboarding-form-stub')).toHaveAttribute(
        'data-typical-weekly',
        '12.4',
      ),
    )

    // The persisted preference now resolves to miles; the form remounts (key=units)
    // against the reseeded seed, NOT a re-hydration of the km state.
    preferredUnitsResolutionMock.mockReturnValue(unitsResolution({ units: PreferredUnits.Miles }))
    rerender(<OnboardingPage />)

    const after = screen.getByTestId('onboarding-form-stub')
    expect(after).toHaveAttribute('data-units', String(PreferredUnits.Miles))
    // 20 km ÷ 1.609344 ≈ 12.4 mi — the reseeded (edited) value, NOT the hydrated
    // 10 km → 6.2 mi. This guards the `initialFields={seed ?? hydrated}` wiring:
    // switching it to `{hydrated}` (or dropping setSeed) would show '6.2' and fail.
    expect(after).toHaveAttribute('data-typical-weekly', '12.4')
  })

  it('marks the units control pending while a change is in flight', async () => {
    const user = userEvent.setup()
    render(<OnboardingPage />)
    expect(screen.getByTestId('onboarding-form-stub')).toHaveAttribute('data-pending', 'false')
    await user.click(screen.getByRole('button', { name: 'change-units' }))
    // The resolved preference stays Kilometers in this mock, so `pendingUnits`
    // never clears — the control is marked pending after the change is issued.
    await waitFor(() =>
      expect(screen.getByTestId('onboarding-form-stub')).toHaveAttribute('data-pending', 'true'),
    )
  })

  it('surfaces an error when persisting the unit change fails', async () => {
    putUnitPreferenceUnwrap.mockRejectedValue(new Error('save failed'))
    const errorSpy = vi.spyOn(toast, 'error')
    const user = userEvent.setup()
    render(<OnboardingPage />)
    await user.click(screen.getByRole('button', { name: 'change-units' }))

    await waitFor(() => expect(reportClientErrorMock).toHaveBeenCalledTimes(1))
    expect(errorSpy).toHaveBeenCalled()
  })
})
