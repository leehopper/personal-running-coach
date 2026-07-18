import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PreferredUnits, type PrescribedWorkoutDto } from '~/api/generated'
import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'

const { bannerMock } = vi.hoisted(() => ({
  bannerMock: vi.fn(),
}))

vi.mock('~/api/workout-log.api', () => ({
  useGetPrescribedWorkoutQuery: (date: string) => bannerMock(date),
}))

import { PrescribedBanner } from './prescribed-banner.component'

const buildPrescribedWorkout = (
  overrides: Partial<PrescribedWorkoutDto> = {},
): PrescribedWorkoutDto => ({
  workoutType: 'Tempo',
  distanceMeters: 9000,
  durationSeconds: 2400,
  paceFastSecPerKm: 240,
  paceEasySecPerKm: 270,
  ...overrides,
})

const renderBanner = (units: PreferredUnits = PreferredUnits.Kilometers, date = '2026-07-20') =>
  render(<PrescribedBanner date={date} units={units} />)

describe('PrescribedBanner', () => {
  afterEach(() => {
    vi.clearAllMocks()
  })

  it('threads the date through to the query hook', () => {
    bannerMock.mockReturnValue({ currentData: null, isLoading: false, isError: false })
    renderBanner(PreferredUnits.Kilometers, '2026-07-20')

    expect(bannerMock).toHaveBeenCalledExactlyOnceWith('2026-07-20')
  })

  it('renders the Prescribed line for a present prescription (km)', () => {
    bannerMock.mockReturnValue({
      currentData: buildPrescribedWorkout(),
      isLoading: false,
      isError: false,
    })
    renderBanner()

    // Source copy stays sentence case (WORKOUT_TYPE_LABELS' "Threshold run",
    // the lowercase-suffixed pace/distance formatters, and the static
    // "Prescribed" literal) — the span's CSS `uppercase` class is solely
    // responsible for the all-caps look on screen; jsdom's raw textContent
    // never applies CSS text-transform, so it carries the sentence-case
    // source string.
    expect(screen.getByTestId('prescribed-banner')).toHaveTextContent(
      'Prescribed — Threshold run · 9.0 km · 04:00–04:30/km',
    )
  })

  it('renders mile-based distance and pace when units is Miles', () => {
    bannerMock.mockReturnValue({
      currentData: buildPrescribedWorkout(),
      isLoading: false,
      isError: false,
    })
    renderBanner(PreferredUnits.Miles)

    expect(screen.getByTestId('prescribed-banner')).toHaveTextContent(
      'Prescribed — Threshold run · 5.6 mi · 06:26–07:15/mi',
    )
  })

  it('falls back to the raw workoutType when it has no mapped label, without throwing', () => {
    bannerMock.mockReturnValue({
      currentData: buildPrescribedWorkout({ workoutType: 'Fartlek' }),
      isLoading: false,
      isError: false,
    })

    expect(() => renderBanner()).not.toThrow()
    expect(screen.getByTestId('prescribed-banner')).toHaveTextContent(
      'Prescribed — Fartlek · 9.0 km · 04:00–04:30/km',
    )
  })

  it('renders nothing when the date has no prescription (currentData === null)', () => {
    bannerMock.mockReturnValue({ currentData: null, isLoading: false, isError: false })
    const { container } = renderBanner()

    expect(container).toBeEmptyDOMElement()
    expect(screen.queryByTestId('prescribed-banner')).not.toBeInTheDocument()
  })

  it('renders nothing while the query is loading (no skeleton)', () => {
    bannerMock.mockReturnValue({ currentData: undefined, isLoading: true, isError: false })
    const { container } = renderBanner()

    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing when the query errors', () => {
    bannerMock.mockReturnValue({ currentData: undefined, isLoading: false, isError: true })
    const { container } = renderBanner()

    expect(container).toBeEmptyDOMElement()
  })

  it('does not flash the previous date’s prescription while a new date is still resolving', () => {
    // RTK Query keeps `data` pinned to the last successful result across an
    // arg change; `currentData` is `undefined` for the duration of the new
    // arg's in-flight request. Simulate that by keying the mock off the
    // requested date: the first date resolves immediately, the second is
    // still mid-flight when the component rerenders with it.
    bannerMock.mockImplementation((requestedDate: string) =>
      requestedDate === '2026-07-20'
        ? { currentData: buildPrescribedWorkout(), isLoading: false, isError: false }
        : { currentData: undefined, isLoading: false, isError: false },
    )

    const { rerender, container } = render(
      <PrescribedBanner date="2026-07-20" units={PreferredUnits.Kilometers} />,
    )
    expect(screen.getByTestId('prescribed-banner')).toHaveTextContent(
      'Prescribed — Threshold run · 9.0 km · 04:00–04:30/km',
    )

    rerender(<PrescribedBanner date="2026-07-21" units={PreferredUnits.Kilometers} />)

    expect(container).toBeEmptyDOMElement()
    expect(screen.queryByTestId('prescribed-banner')).not.toBeInTheDocument()
  })

  describe('dual-theme parity', () => {
    // The assertions live inside the shared expectDualThemeParity helper —
    // sonarjs's static check can't see through the function call.
    // eslint-disable-next-line sonarjs/assertions-in-tests
    it('renders identically in both themes with zero raw colour literals', () => {
      bannerMock.mockReturnValue({
        currentData: buildPrescribedWorkout(),
        isLoading: false,
        isError: false,
      })
      const result = renderInBothThemes(
        <PrescribedBanner date="2026-07-20" units={PreferredUnits.Kilometers} />,
      )

      expectDualThemeParity(result, 'prescribed-banner')
    })
  })
})
