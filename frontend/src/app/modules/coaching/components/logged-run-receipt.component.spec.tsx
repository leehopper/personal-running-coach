import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'

import { PreferredUnits } from '~/api/generated'
import type { LoggedRunSummaryDto } from '~/modules/coaching/models/conversation.model'
import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'

import { LoggedRunReceipt } from './logged-run-receipt.component'

const baseSummary: LoggedRunSummaryDto = {
  workoutLogId: 'wl-1',
  distanceKm: 9.2,
  durationSeconds: 41 * 60,
  occurredOn: '2026-07-08',
  completionStatus: 0,
}

const renderReceipt = (
  summary: LoggedRunSummaryDto = baseSummary,
  units: PreferredUnits = PreferredUnits.Kilometers,
) =>
  render(
    <MemoryRouter>
      <LoggedRunReceipt summary={summary} units={units} />
    </MemoryRouter>,
  )

describe('LoggedRunReceipt', () => {
  it('renders the LOGGED format string with distance, duration, and humanized date', () => {
    renderReceipt()

    expect(screen.getByTestId('logged-run-receipt')).toHaveTextContent(
      'LOGGED — 9.2 km · 41:00 · JUL 8',
    )
  })

  it('renders a LOG BOOK → link to /history', () => {
    renderReceipt()

    const link = screen.getByTestId('logged-run-receipt-logbook')
    expect(link).toHaveAttribute('href', '/history')
    expect(link).toHaveTextContent('LOG BOOK →')
  })

  it('renders the distance in miles when units=Miles', () => {
    renderReceipt(baseSummary, PreferredUnits.Miles)

    // 9.2 km / 1.609344 = 5.7156... -> 5.7 mi
    expect(screen.getByTestId('logged-run-receipt')).toHaveTextContent('5.7 mi')
  })

  it('omits the date fragment (and never crashes) when occurredOn is unparseable', () => {
    renderReceipt({ ...baseSummary, occurredOn: 'not-a-date' })

    const receipt = screen.getByTestId('logged-run-receipt')
    expect(receipt).toHaveTextContent('LOGGED — 9.2 km · 41:00')
    expect(receipt.textContent).not.toMatch(/JAN 1/)
  })

  it('falls back to an em dash for a non-positive distance', () => {
    renderReceipt({ ...baseSummary, distanceKm: 0 })

    expect(screen.getByTestId('logged-run-receipt')).toHaveTextContent('LOGGED — — · 41:00 · JUL 8')
  })

  // The assertions live inside the shared expectDualThemeParity helper —
  // sonarjs's static check can't see through the function call.
  // eslint-disable-next-line sonarjs/assertions-in-tests
  it('holds dual-theme structural parity with no raw hex colour literals', () => {
    const result = renderInBothThemes(
      <MemoryRouter>
        <LoggedRunReceipt summary={baseSummary} units={PreferredUnits.Kilometers} />
      </MemoryRouter>,
    )
    expectDualThemeParity(result, 'logged-run-receipt')
  })
})
