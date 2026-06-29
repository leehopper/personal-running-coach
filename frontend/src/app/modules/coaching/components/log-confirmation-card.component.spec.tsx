import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import type { CoachCard } from '~/modules/coaching/hooks/use-coach-stream.hooks'

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
  it('renders the parsed distance, duration, and note', () => {
    renderCard()

    expect(screen.getByText(/5 km/i)).toBeInTheDocument()
    expect(screen.getByText('25:00')).toBeInTheDocument()
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

  it('shows the matched on-plan workout type when a prescription is present', () => {
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

    expect(screen.getByText(/easy/i)).toBeInTheDocument()
  })

  it('wires Confirm, Edit, and Cancel', async () => {
    const user = userEvent.setup()
    const props = renderCard()

    await user.click(screen.getByRole('button', { name: /^confirm$/i }))
    expect(props.onConfirm).toHaveBeenCalledOnce()

    await user.click(screen.getByRole('button', { name: /^edit$/i }))
    expect(props.onEdit).toHaveBeenCalledOnce()

    await user.click(screen.getByRole('button', { name: /^cancel$/i }))
    expect(props.onCancel).toHaveBeenCalledOnce()
  })

  it('disables Confirm while a commit is in flight and never mentions VDOT', () => {
    renderCard({ isConfirming: true })

    expect(screen.getByRole('button', { name: /^confirm$/i })).toBeDisabled()
    expect(screen.queryByText(/vdot/i)).toBeNull()
  })
})
