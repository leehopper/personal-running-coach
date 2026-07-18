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
    bannerMock.mockReturnValue({ data: null, isLoading: false, isError: false })
    renderBanner(PreferredUnits.Kilometers, '2026-07-20')

    expect(bannerMock).toHaveBeenCalledExactlyOnceWith('2026-07-20')
  })

  it('renders the PRESCRIBED line for a present prescription (km)', () => {
    bannerMock.mockReturnValue({ data: buildPrescribedWorkout(), isLoading: false, isError: false })
    renderBanner()

    // The workoutType label and pace range are uppercased in JS
    // (`.toUpperCase()`); the distance fragment relies solely on the span's
    // CSS `uppercase` class for its visual case, so jsdom's raw textContent
    // (which never applies CSS text-transform) still carries a lowercase
    // "km" here even though the design reads all-caps on screen.
    expect(screen.getByTestId('prescribed-banner')).toHaveTextContent(
      'PRESCRIBED — THRESHOLD RUN · 9.0 km · 04:00–04:30/KM',
    )
  })

  it('renders mile-based distance and pace when units is Miles', () => {
    bannerMock.mockReturnValue({ data: buildPrescribedWorkout(), isLoading: false, isError: false })
    renderBanner(PreferredUnits.Miles)

    expect(screen.getByTestId('prescribed-banner')).toHaveTextContent(
      'PRESCRIBED — THRESHOLD RUN · 5.6 mi · 06:26–07:15/MI',
    )
  })

  it('falls back to the raw workoutType when it has no mapped label, without throwing', () => {
    bannerMock.mockReturnValue({
      data: buildPrescribedWorkout({ workoutType: 'Fartlek' }),
      isLoading: false,
      isError: false,
    })

    expect(() => renderBanner()).not.toThrow()
    expect(screen.getByTestId('prescribed-banner')).toHaveTextContent(
      'PRESCRIBED — FARTLEK · 9.0 km · 04:00–04:30/KM',
    )
  })

  it('renders nothing when the date has no prescription (data === null)', () => {
    bannerMock.mockReturnValue({ data: null, isLoading: false, isError: false })
    const { container } = renderBanner()

    expect(container).toBeEmptyDOMElement()
    expect(screen.queryByTestId('prescribed-banner')).not.toBeInTheDocument()
  })

  it('renders nothing while the query is loading (no skeleton)', () => {
    bannerMock.mockReturnValue({ data: undefined, isLoading: true, isError: false })
    const { container } = renderBanner()

    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing when the query errors', () => {
    bannerMock.mockReturnValue({ data: undefined, isLoading: false, isError: true })
    const { container } = renderBanner()

    expect(container).toBeEmptyDOMElement()
  })

  describe('dual-theme parity', () => {
    // The assertions live inside the shared expectDualThemeParity helper —
    // sonarjs's static check can't see through the function call.
    // eslint-disable-next-line sonarjs/assertions-in-tests
    it('renders identically in both themes with zero raw colour literals', () => {
      bannerMock.mockReturnValue({
        data: buildPrescribedWorkout(),
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
