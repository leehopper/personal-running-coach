import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { toast } from 'sonner'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { Toaster } from '@/components/ui/sonner'
import type { CreateWorkoutLogRequest, StructuredLogDraft } from '~/api/generated'
import { PreferredUnits } from '~/api/generated'
import { METERS_PER_MILE } from '~/modules/common/utils/unit-format.helpers'
import type { UsePreferredUnitsResolutionReturn } from '~/modules/settings/hooks/use-preferred-units.hooks'
import { toIsoDateOnly } from '~/modules/logging/schemas/workout-log-form.schema'

// vi.mock is hoisted above imports, so the mock fns must come from vi.hoisted.
const {
  createWorkoutLogTrigger,
  createWorkoutLogUnwrap,
  mutationStateRef,
  navigateMock,
  reportClientErrorMock,
  preferredUnitsResolutionMock,
  refetchUnitsMock,
} = vi.hoisted(() => {
  const createWorkoutLogUnwrap = vi.fn()
  return {
    createWorkoutLogUnwrap,
    // Typed via the generic so `mock.calls[0][0]` is a CreateWorkoutLogRequest;
    // the impl ignores the body (no unused param).
    createWorkoutLogTrigger: vi.fn<
      (body: CreateWorkoutLogRequest) => { unwrap: () => Promise<{ workoutLogId: string }> }
    >(() => ({ unwrap: createWorkoutLogUnwrap })),
    mutationStateRef: { isLoading: false },
    navigateMock: vi.fn(),
    reportClientErrorMock: vi.fn(),
    preferredUnitsResolutionMock: vi.fn<() => UsePreferredUnitsResolutionReturn>(),
    refetchUnitsMock: vi.fn(),
  }
})

// Builds a units-resolution return, defaulting the settled/error flags so each
// test overrides only the field it exercises.
const unitsResolution = (
  overrides: Partial<UsePreferredUnitsResolutionReturn> = {},
): UsePreferredUnitsResolutionReturn => ({
  units: PreferredUnits.Kilometers,
  isResolved: true,
  isError: false,
  refetch: refetchUnitsMock,
  ...overrides,
})

vi.mock('~/api/workout-log.api', () => ({
  useCreateWorkoutLogMutation: () => [createWorkoutLogTrigger, mutationStateRef],
  // The form now mounts `PrescribedBanner` (via `LogForm`), which calls this
  // hook directly. `data: null` keeps the banner hidden across these tests —
  // its own behavior is covered by `prescribed-banner.component.spec.tsx`.
  useGetPrescribedWorkoutQuery: () => ({ data: null, isLoading: false, isError: false }),
}))

vi.mock('~/error-boundary/report-client-error', () => ({
  reportClientError: reportClientErrorMock,
}))

// The page reads the unit preference via this hook (it wraps a real RTK Query
// hook); the page renders here without a Redux store, so stub it through a
// mockable ref — mirroring history.page.spec / coach-chat.component.spec.
vi.mock('~/modules/settings/hooks/use-preferred-units.hooks', () => ({
  usePreferredUnitsResolution: () => preferredUnitsResolutionMock(),
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')
  return { ...actual, useNavigate: () => navigateMock }
})

// Component under test imported after the mocks.
import LogPage from './log.page'

const renderPage = () => {
  const user = userEvent.setup()
  const utils = render(
    <MemoryRouter initialEntries={['/log']}>
      <LogPage />
      {/* App-wide toast outlet (mounted outside the route tree in production);
          included here so the success toast actually renders. */}
      <Toaster />
    </MemoryRouter>,
  )
  return { user, ...utils }
}

const fillCoreFields = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.type(screen.getByLabelText('Distance (km)'), '5')
  await user.type(screen.getByLabelText('Duration (minutes)'), '30')
}

const clickSave = async (user: ReturnType<typeof userEvent.setup>) => {
  const submit = screen.getByTestId('log-form-submit')
  await waitFor(() => expect(submit).toBeEnabled())
  await user.click(submit)
}

describe('LogPage', () => {
  beforeEach(() => {
    createWorkoutLogTrigger.mockClear()
    createWorkoutLogUnwrap.mockReset()
    createWorkoutLogUnwrap.mockResolvedValue({ workoutLogId: 'log-1' })
    mutationStateRef.isLoading = false
    navigateMock.mockReset()
    reportClientErrorMock.mockReset()
    refetchUnitsMock.mockReset()
    // Default: preference resolved to Kilometers (the common case). Individual
    // tests override for Miles / still-loading / errored.
    preferredUnitsResolutionMock.mockReturnValue(unitsResolution())
    vi.spyOn(crypto, 'randomUUID').mockReturnValue('00000000-0000-0000-0000-00000000abcd')
  })

  afterEach(() => {
    toast.dismiss()
    vi.restoreAllMocks()
  })

  it('renders the core fields with optional metrics collapsed by default', () => {
    renderPage()
    expect(screen.getByLabelText('Date')).toBeInTheDocument()
    expect(screen.getByLabelText('Distance (km)')).toBeInTheDocument()
    expect(screen.getByLabelText('Duration (minutes)')).toBeInTheDocument()
    const moreDetails = screen.getByRole('button', { name: /more details/i })
    expect(moreDetails).toHaveAttribute('aria-expanded', 'false')
    // Optional metrics are not in the DOM until "More details" is expanded.
    expect(screen.queryByLabelText('RPE')).toBeNull()
  })

  it('defaults the occurred-on date to today', () => {
    renderPage()
    const dateInput = screen.getByLabelText('Date') as HTMLInputElement
    expect(dateInput.value).toBe(toIsoDateOnly(new Date()))
  })

  it('pins the /log chrome copy verbatim: header, sub-copy, notes helper, placeholder, submit label (DU-7)', () => {
    renderPage()

    // Accessible name is the sentence-case textContent — `.t-screen-title`
    // applies the visual caps via CSS, which doesn't change the DOM text.
    expect(screen.getByRole('heading', { level: 1, name: 'Log run' })).toBeInTheDocument()
    // EM DASH (U+2014).
    expect(
      screen.getByText(
        'Record what you actually ran — the plan adapts to the truth, not the intention.',
      ),
    ).toBeInTheDocument()
    // EM DASH (U+2014).
    expect(
      screen.getByText(
        'What actually happened — especially where it differed from the plan. The coach adapts to what you write here.',
      ),
    ).toBeInTheDocument()
    // Ellipsis (U+2026), not three periods.
    expect(
      screen.getByPlaceholderText(
        'Cut to 3 reps, moved to the treadmill, calf felt tight on the last k…',
      ),
    ).toBeInTheDocument()
    // The Button primitive CSS-uppercases its label; source stays sentence case.
    expect(screen.getByRole('button', { name: 'Save run' })).toBeInTheDocument()
  })

  it('shows the live pace preview once distance and duration are filled (5 km / 30 min -> 06:00/km)', async () => {
    const { user } = renderPage()
    await fillCoreFields(user)

    await waitFor(() =>
      expect(screen.getByTestId('log-derived-pace')).toHaveTextContent('06:00/km'),
    )
  })

  it('hides the pace preview for blank distance, both before typing and after clearing (never NaN/00:00)', async () => {
    const { user } = renderPage()
    // Distance/duration are both blank on mount — no preview yet.
    expect(screen.queryByTestId('log-derived-pace')).toBeNull()

    await fillCoreFields(user)
    await waitFor(() => expect(screen.getByTestId('log-derived-pace')).toBeInTheDocument())

    await user.clear(screen.getByLabelText('Distance (km)'))
    await waitFor(() => expect(screen.queryByTestId('log-derived-pace')).toBeNull())
  })

  it('shows an inline role=alert error for a missing distance and does not submit', async () => {
    renderPage()
    const form = document.querySelector('form') as HTMLFormElement
    fireEvent.submit(form)

    const message = await screen.findByText('Enter a distance in km.')
    expect(message).toHaveAttribute('role', 'alert')
    expect(createWorkoutLogTrigger).not.toHaveBeenCalled()
  })

  it('submits a minimum payload, shows a success toast, and navigates home', async () => {
    const { user } = renderPage()
    await fillCoreFields(user)
    await clickSave(user)

    await waitFor(() => expect(createWorkoutLogTrigger).toHaveBeenCalledTimes(1))
    expect(createWorkoutLogTrigger).toHaveBeenCalledWith({
      idempotencyKey: '00000000-0000-0000-0000-00000000abcd',
      occurredOn: toIsoDateOnly(new Date()),
      distanceMeters: 5000,
      durationSeconds: 1800,
      completionStatus: 0,
    })
    // Exact glyph-pinned copy (DU-7): EM DASH (U+2014) + "5.0 km" under the
    // default km preference and a 5000m submit. Source is sentence case; the
    // toast's `className: 'uppercase'` renders it capitalized via CSS, which
    // does not change the DOM text this query matches against.
    expect(await screen.findByText('Run logged — 5.0 km')).toBeInTheDocument()
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/', { replace: true }))
  })

  it('omits a blank optional metric (rpe) while including a filled one (hrAvg)', async () => {
    const { user } = renderPage()
    await fillCoreFields(user)
    await user.click(screen.getByRole('button', { name: /more details/i }))
    await user.type(screen.getByLabelText('Avg HR (bpm)'), '150')
    // RPE deliberately left blank.
    await clickSave(user)

    await waitFor(() => expect(createWorkoutLogTrigger).toHaveBeenCalledTimes(1))
    expect(createWorkoutLogTrigger.mock.calls[0][0].metrics).toEqual({ hrAvg: 150 })
  })

  it('still submits a metric that was filled then collapsed (shouldUnregister: false)', async () => {
    const { user } = renderPage()
    await fillCoreFields(user)
    const moreDetails = screen.getByRole('button', { name: /more details/i })
    await user.click(moreDetails)
    await user.type(screen.getByLabelText('Avg HR (bpm)'), '150')
    await user.click(moreDetails)
    await waitFor(() => expect(moreDetails).toHaveAttribute('aria-expanded', 'false'))
    await clickSave(user)

    await waitFor(() => expect(createWorkoutLogTrigger).toHaveBeenCalledTimes(1))
    expect(createWorkoutLogTrigger.mock.calls[0][0].metrics).toEqual({ hrAvg: 150 })
  })

  it('shows a form-level alert, reports the error, and does not navigate when the create fails', async () => {
    const failure = new Error('boom')
    createWorkoutLogUnwrap.mockRejectedValue(failure)
    const { user } = renderPage()
    await fillCoreFields(user)
    await clickSave(user)

    // Exact glyph-pinned copy (DU-7): curly apostrophe (U+2019).
    expect(await screen.findByTestId('log-form-alert')).toHaveTextContent(
      'Couldn’t save. Nothing lost.',
    )
    expect(navigateMock).not.toHaveBeenCalled()
    // The handled rejection is invisible to the global reporter + error boundary,
    // so the submit handler forwards it explicitly (keeps backend failures observable).
    expect(reportClientErrorMock).toHaveBeenCalledWith({
      kind: 'unhandled-rejection',
      error: failure,
    })
  })

  it('reuses the same idempotency key across a retry after a transient failure', async () => {
    // Incrementing UUID stub: a per-submit randomUUID() would produce different
    // keys and fail the assertion; useMemo pins it to one value per mounted form.
    let callCount = 0
    vi.spyOn(crypto, 'randomUUID').mockImplementation(
      () =>
        `00000000-0000-0000-0000-${String(++callCount).padStart(12, '0')}` as ReturnType<
          typeof crypto.randomUUID
        >,
    )
    createWorkoutLogUnwrap
      .mockRejectedValueOnce(new Error('transient'))
      .mockResolvedValue({ workoutLogId: 'log-1' })

    const { user } = renderPage()
    await fillCoreFields(user)
    await clickSave(user)
    expect(await screen.findByTestId('log-form-alert')).toBeInTheDocument()
    await clickSave(user)

    await waitFor(() => expect(navigateMock).toHaveBeenCalled())
    expect(createWorkoutLogTrigger).toHaveBeenCalledTimes(2)
    const [first, second] = createWorkoutLogTrigger.mock.calls
    expect(first[0].idempotencyKey).toBe(second[0].idempotencyKey)
  })

  it('disables the submit button and shows progress copy while creating', () => {
    mutationStateRef.isLoading = true
    renderPage()
    const submit = screen.getByTestId('log-form-submit')
    expect(submit).toBeDisabled()
    expect(submit).toHaveTextContent(/saving/i)
  })

  it('pre-fills the form from a conversational-log Edit draft and enables Save', async () => {
    const draft: StructuredLogDraft = {
      occurredOn: '2026-06-20',
      distanceValue: 5,
      distanceUnit: 1, // miles
      durationHours: 0,
      durationMinutes: 25,
      durationSeconds: 0,
      completionStatus: 0,
      notes: 'felt good',
    }
    render(
      <MemoryRouter initialEntries={[{ pathname: '/log', state: { draft } }]}>
        <LogPage />
        <Toaster />
      </MemoryRouter>,
    )

    expect((screen.getByLabelText('Distance (km)') as HTMLInputElement).value).toBe(
      String(5 * 1.609344),
    )
    expect((screen.getByLabelText('Duration (minutes)') as HTMLInputElement).value).toBe('25')
    expect((screen.getByLabelText('Date') as HTMLInputElement).value).toBe('2026-06-20')
    // A pre-filled Edit form is valid without the user touching a field.
    await waitFor(() => expect(screen.getByTestId('log-form-submit')).toBeEnabled())
  })

  it('interprets distance as miles and converts to km/SI on write under a Miles preference', async () => {
    preferredUnitsResolutionMock.mockReturnValue(unitsResolution({ units: PreferredUnits.Miles }))
    const { user } = renderPage()
    await user.type(screen.getByLabelText('Distance (mi)'), '5')
    await user.type(screen.getByLabelText('Duration (minutes)'), '30')
    await clickSave(user)

    await waitFor(() => expect(createWorkoutLogTrigger).toHaveBeenCalledTimes(1))
    // 5 mi -> 5 * 1609.344 m; the wire stays canonical km/SI metres.
    expect(createWorkoutLogTrigger.mock.calls[0][0].distanceMeters).toBe(5 * METERS_PER_MILE)
  })

  it('pre-fills a miles-stated Edit draft in miles under a Miles preference (no km round trip)', async () => {
    preferredUnitsResolutionMock.mockReturnValue(unitsResolution({ units: PreferredUnits.Miles }))
    const draft: StructuredLogDraft = {
      occurredOn: '2026-06-20',
      distanceValue: 5,
      distanceUnit: 1, // miles
      durationHours: 0,
      durationMinutes: 25,
      durationSeconds: 0,
      completionStatus: 0,
      notes: 'felt good',
    }
    render(
      <MemoryRouter initialEntries={[{ pathname: '/log', state: { draft } }]}>
        <LogPage />
        <Toaster />
      </MemoryRouter>,
    )

    // The 5 mi draft pre-fills the miles field as "5" (identity, not converted).
    expect((screen.getByLabelText('Distance (mi)') as HTMLInputElement).value).toBe('5')
    await waitFor(() => expect(screen.getByTestId('log-form-submit')).toBeEnabled())
  })

  it('shows a loading state and no form until the unit preference resolves', () => {
    preferredUnitsResolutionMock.mockReturnValue(unitsResolution({ isResolved: false }))
    renderPage()

    expect(screen.getByTestId('log-page-loading')).toBeInTheDocument()
    expect(screen.queryByTestId('log-form')).toBeNull()
  })

  it('gates the form behind a retry when the unit-preference query errors', async () => {
    // An errored settings GET must NOT fall through to the km default and render
    // the form — a Miles runner would otherwise submit at km magnitude. The page
    // surfaces a retry that re-runs the preference query instead.
    preferredUnitsResolutionMock.mockReturnValue(
      unitsResolution({ isResolved: false, isError: true }),
    )
    const { user } = renderPage()

    expect(screen.getByTestId('log-page-units-error')).toBeInTheDocument()
    expect(screen.queryByTestId('log-form')).toBeNull()
    expect(screen.queryByTestId('log-page-loading')).toBeNull()

    await user.click(screen.getByRole('button', { name: /retry/i }))
    expect(refetchUnitsMock).toHaveBeenCalledTimes(1)
  })
})
