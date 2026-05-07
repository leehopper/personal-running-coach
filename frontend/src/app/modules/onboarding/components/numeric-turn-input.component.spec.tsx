import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { OnboardingTopic } from '~/modules/onboarding/models/onboarding.model'
import { NumericTurnInput } from './numeric-turn-input.component'

describe('NumericTurnInput', () => {
  it('keeps Send disabled and shows lte(300) error when value exceeds 300', async () => {
    const user = userEvent.setup()
    render(<NumericTurnInput onSubmit={vi.fn()} topic={OnboardingTopic.CurrentFitness} />)

    await user.clear(screen.getByTestId('numeric-turn-input-field'))
    await user.type(screen.getByTestId('numeric-turn-input-field'), '301')

    expect(screen.getByRole('button', { name: /send/i })).toBeDisabled()
    expect(screen.getByRole('alert')).toHaveTextContent('Enter a value at or below 300.')
  })

  it('keeps Send disabled when value is negative', async () => {
    const user = userEvent.setup()
    render(<NumericTurnInput onSubmit={vi.fn()} topic={OnboardingTopic.CurrentFitness} />)

    await user.clear(screen.getByTestId('numeric-turn-input-field'))
    await user.type(screen.getByTestId('numeric-turn-input-field'), '-5')

    expect(screen.getByRole('button', { name: /send/i })).toBeDisabled()
  })

  it('keeps Send disabled when the field is cleared (NaN path)', async () => {
    const user = userEvent.setup()
    render(<NumericTurnInput onSubmit={vi.fn()} topic={OnboardingTopic.CurrentFitness} />)

    await user.clear(screen.getByTestId('numeric-turn-input-field'))

    expect(screen.getByRole('button', { name: /send/i })).toBeDisabled()
  })

  it('enables Send on a valid value and invokes onSubmit with the expected text', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn().mockResolvedValue(undefined)

    render(<NumericTurnInput onSubmit={onSubmit} topic={OnboardingTopic.CurrentFitness} />)

    await user.clear(screen.getByTestId('numeric-turn-input-field'))
    await user.type(screen.getByTestId('numeric-turn-input-field'), '1')

    const sendButton = screen.getByRole('button', { name: /send/i })
    expect(sendButton).not.toBeDisabled()

    await user.click(sendButton)

    expect(onSubmit).toHaveBeenCalledWith({ text: '1' })
  })
})
