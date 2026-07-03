import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

interface UnitPreference {
  preferredUnits: 0 | 1
}

// Hoisted so the `vi.mock` factories below can close over the same references the
// test body drives. `getQueryRef.data` stands in for the RTK query result;
// `putMock` records the mutation trigger's argument; `toastErrorMock` /
// `reportClientErrorMock` capture the failure-path side effects.
const { getQueryRef, putMock, toastErrorMock, reportClientErrorMock } = vi.hoisted(() => ({
  getQueryRef: { data: undefined as UnitPreference | undefined },
  putMock: vi.fn<(arg: UnitPreference) => { unwrap: () => Promise<UnitPreference> }>(),
  toastErrorMock: vi.fn(),
  reportClientErrorMock: vi.fn(),
}))

vi.mock('~/api/settings.api', () => ({
  useGetUnitPreferenceQuery: () => getQueryRef,
  usePutUnitPreferenceMutation: () => [putMock, { isLoading: false }],
}))

vi.mock('sonner', () => ({ toast: { error: toastErrorMock } }))

vi.mock('~/error-boundary/report-client-error', () => ({
  reportClientError: reportClientErrorMock,
}))

import { UnitsToggle } from './units-toggle.component'

describe('UnitsToggle', () => {
  beforeEach(() => {
    putMock.mockReset()
    putMock.mockReturnValue({ unwrap: () => Promise.resolve({ preferredUnits: 1 }) })
    getQueryRef.data = { preferredUnits: 0 }
    toastErrorMock.mockReset()
    reportClientErrorMock.mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders both unit options with the current one selected', () => {
    render(<UnitsToggle />)
    expect(screen.getByRole('radio', { name: 'Kilometers' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Miles' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Kilometers' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Miles' })).not.toBeChecked()
  })

  it('reflects Miles as selected when the stored preference is Miles', () => {
    getQueryRef.data = { preferredUnits: 1 }
    render(<UnitsToggle />)
    expect(screen.getByRole('radio', { name: 'Miles' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Kilometers' })).not.toBeChecked()
  })

  it('defaults to Kilometers while the preference is loading', () => {
    getQueryRef.data = undefined
    render(<UnitsToggle />)
    expect(screen.getByRole('radio', { name: 'Kilometers' })).toBeChecked()
  })

  it('dispatches the put mutation with { preferredUnits: 1 } when Miles is selected', async () => {
    render(<UnitsToggle />)
    await userEvent.click(screen.getByRole('radio', { name: 'Miles' }))
    expect(putMock).toHaveBeenCalledWith({ preferredUnits: 1 })
  })

  it('reports the error and shows a toast when the save fails', async () => {
    putMock.mockReturnValue({ unwrap: () => Promise.reject(new Error('network down')) })
    render(<UnitsToggle />)
    await userEvent.click(screen.getByRole('radio', { name: 'Miles' }))
    await waitFor(() => expect(toastErrorMock).toHaveBeenCalledTimes(1))
    expect(reportClientErrorMock).toHaveBeenCalledWith(
      expect.objectContaining({ kind: 'unhandled-rejection' }),
    )
  })
})
