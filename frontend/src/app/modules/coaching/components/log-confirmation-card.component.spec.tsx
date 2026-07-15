import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { PreferredUnits } from '~/api/generated'
import type { CoachCard } from '~/modules/coaching/hooks/use-coach-stream.hooks'
import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'

import { LogConfirmationCard } from './log-confirmation-card.component'

const baseCard: CoachCard = {
  clientMessageId: 'c-1',
  prescription: null,
  draft: {
    occurredOn: '2026-06-29',
    distanceValue: 5,
    distanceUnit: 0,
    durationHours: 0,
    durationMinutes: 25,
    durationSeconds: 0,
    completionStatus: 0,
    notes: 'legs felt heavy',
  },
}

const renderCard = (overrides: Partial<Parameters<typeof LogConfirmationCard>[0]> = {}) => {
  const props = {
    card: baseCard,
    units: PreferredUnits.Kilometers,
    onConfirm: vi.fn(),
    onEdit: vi.fn(),
    onCancel: vi.fn(),
    isConfirming: false,
    ...overrides,
  }
  render(<LogConfirmationCard {...props} />)
  return props
}

describe('LogConfirmationCard', () => {
  it('renders the LOG THIS RUN? heading, distance, duration, and humanized date', () => {
    renderCard()

    expect(screen.getByText('LOG THIS RUN?')).toBeInTheDocument()
    expect(screen.getByText(/5 km/i)).toBeInTheDocument()
    expect(screen.getByText('25:00')).toBeInTheDocument()
    // 2026-06-29 humanized via formatReceiptDate, not the raw ISO string.
    expect(screen.getByText('JUN 29')).toBeInTheDocument()
    expect(screen.queryByText('2026-06-29')).not.toBeInTheDocument()
  })

  it('falls back to the raw ISO string when occurredOn is unparseable', () => {
    renderCard({
      card: {
        ...baseCard,
        draft: { ...baseCard.draft, occurredOn: 'not-a-date' },
      },
    })

    expect(screen.getByText('not-a-date')).toBeInTheDocument()
  })

  it('renders the STATUS value in the positive (moss) token', () => {
    renderCard()

    const status = screen.getByText('Completed')
    expect(status).toHaveClass('text-positive')
  })

  it('renders the note', () => {
    renderCard()
    expect(screen.getByText(/legs felt heavy/i)).toBeInTheDocument()
  })

  it('formats hours and a miles distance', () => {
    renderCard({
      card: {
        ...baseCard,
        draft: {
          ...baseCard.draft,
          distanceValue: 13.1,
          distanceUnit: 1,
          durationHours: 1,
          durationMinutes: 45,
          durationSeconds: 30,
        },
      },
    })

    expect(screen.getByText(/13\.1 mi/i)).toBeInTheDocument()
    expect(screen.getByText('1:45:30')).toBeInTheDocument()
  })

  it('shows the on-plan workout type AND the unit-aware target distance when a prescription is present', () => {
    renderCard({
      card: {
        ...baseCard,
        prescription: {
          workoutType: 'Easy',
          distanceMeters: 8000,
          durationSeconds: 2400,
          paceFastSecPerKm: 300,
          paceEasySecPerKm: 360,
        },
      },
    })

    expect(screen.getByText(/ON-PLAN — EASY · TARGET 8\.0 km/i)).toBeInTheDocument()
  })

  it('renders the on-plan target distance in miles when units=Miles', () => {
    renderCard({
      units: PreferredUnits.Miles,
      card: {
        ...baseCard,
        prescription: {
          workoutType: 'Easy',
          distanceMeters: 8000,
          durationSeconds: 2400,
          paceFastSecPerKm: 300,
          paceEasySecPerKm: 360,
        },
      },
    })

    // 8 km / 1.609344 = 4.9709... -> 5.0 mi
    expect(screen.getByText(/TARGET 5\.0 mi/i)).toBeInTheDocument()
  })

  it('renders no on-plan line when prescription is null', () => {
    renderCard()
    expect(screen.queryByText(/ON-PLAN/i)).not.toBeInTheDocument()
  })

  it('carries the log-confirm/log-edit/log-cancel testids', () => {
    renderCard()

    expect(screen.getByTestId('log-confirm')).toBeInTheDocument()
    expect(screen.getByTestId('log-edit')).toBeInTheDocument()
    expect(screen.getByTestId('log-cancel')).toBeInTheDocument()
  })

  it('wires Confirm, Edit, and Cancel', async () => {
    const user = userEvent.setup()
    const props = renderCard()

    await user.click(screen.getByTestId('log-confirm'))
    expect(props.onConfirm).toHaveBeenCalledOnce()

    await user.click(screen.getByTestId('log-edit'))
    expect(props.onEdit).toHaveBeenCalledOnce()

    await user.click(screen.getByTestId('log-cancel'))
    expect(props.onCancel).toHaveBeenCalledOnce()
  })

  describe('saving state (isConfirming)', () => {
    it('dims the card and disables Confirm and Edit', () => {
      renderCard({ isConfirming: true })

      expect(screen.getByTestId('log-confirmation-card')).toHaveClass('opacity-75')
      expect(screen.getByTestId('log-confirm')).toBeDisabled()
      expect(screen.getByTestId('log-edit')).toBeDisabled()
    })

    it('swaps the CONFIRM label to Saving…', () => {
      renderCard({ isConfirming: true })

      expect(screen.getByTestId('log-confirm')).toHaveTextContent('Saving…')
      expect(screen.queryByText(/^confirm$/i)).not.toBeInTheDocument()
    })

    it('hides Cancel entirely while saving', () => {
      renderCard({ isConfirming: true })

      expect(screen.queryByTestId('log-cancel')).not.toBeInTheDocument()
    })
  })

  // The assertions live inside the shared expectDualThemeParity helper —
  // sonarjs's static check can't see through the function call.
  // eslint-disable-next-line sonarjs/assertions-in-tests
  it('holds dual-theme structural parity with no raw hex colour literals', () => {
    const result = renderInBothThemes(
      <LogConfirmationCard
        card={baseCard}
        units={PreferredUnits.Kilometers}
        onConfirm={vi.fn()}
        onEdit={vi.fn()}
        onCancel={vi.fn()}
        isConfirming={false}
      />,
    )
    expectDualThemeParity(result, 'log-confirmation-card')
  })
})
