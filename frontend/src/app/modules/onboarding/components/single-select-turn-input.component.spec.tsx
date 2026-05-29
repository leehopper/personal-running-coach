import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SingleSelectTurnInput } from './single-select-turn-input.component'
import { OnboardingTopic } from '~/modules/onboarding/models/onboarding.model'

describe('SingleSelectTurnInput', () => {
  it('submits the picked value as text', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn().mockResolvedValue(undefined)

    render(<SingleSelectTurnInput onSubmit={onSubmit} topic={OnboardingTopic.PrimaryGoal} />)

    expect(screen.getByRole('button', { name: /send/i })).toBeDisabled()

    await user.click(screen.getByLabelText('Training for a race'))
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(onSubmit).toHaveBeenCalledWith({ text: 'race-training' })
  })

  it('exposes each option as an accessible radio (locks the role the onboarding e2e selects on)', () => {
    render(<SingleSelectTurnInput onSubmit={vi.fn()} topic={OnboardingTopic.PrimaryGoal} />)

    // The five canned PrimaryGoal fallback options render as Radix
    // `button[role="radio"]`. The onboarding E2E selects the first option via
    // `getByRole('radio')`, so a regression away from that role contract must
    // fail here at the unit tier instead of only in the slow E2E suite.
    expect(screen.getAllByRole('radio')).toHaveLength(5)
    expect(screen.getByRole('radio', { name: 'Training for a race' })).toBeInTheDocument()
  })

  it('disables the Send button and the radio options while submitting', () => {
    render(
      <SingleSelectTurnInput onSubmit={vi.fn()} topic={OnboardingTopic.PrimaryGoal} isSubmitting />,
    )

    expect(screen.getByRole('button', { name: /sending/i })).toBeDisabled()
    for (const radio of screen.getAllByRole('radio')) {
      expect(radio).toBeDisabled()
    }
  })
})
