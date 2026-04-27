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
})
