import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SuggestedInputType } from '~/api/generated'
import { OnboardingTopic } from '~/modules/onboarding/models/onboarding.model'
import type { OnboardingTurn } from '~/modules/onboarding/store/onboarding.slice'
import { OnboardingChat } from './onboarding-chat.component'

const noTurns: OnboardingTurn[] = []

const baseProps = {
  turns: noTurns,
  currentTopic: OnboardingTopic.PrimaryGoal,
  suggestedInputType: SuggestedInputType.SingleSelect,
  completedTopics: [],
  isSubmitting: false,
  hasFailedTurn: false,
  onSubmit: vi.fn(),
  onRetry: vi.fn(),
}

describe('OnboardingChat — RetryAffordance message rendering', () => {
  it('does not render the retry affordance when there is no failed turn', () => {
    render(<OnboardingChat {...baseProps} hasFailedTurn={false} />)
    expect(screen.queryByTestId('onboarding-retry')).not.toBeInTheDocument()
  })

  it('renders the generic fallback message when hasFailedTurn is true and no errorMessage is provided', () => {
    render(<OnboardingChat {...baseProps} hasFailedTurn={true} />)
    expect(screen.getByTestId('onboarding-retry')).toBeInTheDocument()
    expect(screen.getByText(/that didn't go through/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument()
  })

  it('renders the server error message instead of the fallback when failedTurnMessage is provided', () => {
    const serverMessage = 'Plan generation is not available right now.'
    render(<OnboardingChat {...baseProps} hasFailedTurn={true} failedTurnMessage={serverMessage} />)
    expect(screen.getByTestId('onboarding-retry')).toBeInTheDocument()
    expect(screen.getByText(serverMessage)).toBeInTheDocument()
    expect(screen.queryByText(/that didn't go through/i)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument()
  })

  it('retry button still appears alongside the server error message', async () => {
    const onRetry = vi.fn().mockResolvedValue(undefined)
    const user = userEvent.setup()
    render(
      <OnboardingChat
        {...baseProps}
        hasFailedTurn={true}
        failedTurnMessage="Your training data needs more detail."
        onRetry={onRetry}
      />,
    )
    await user.click(screen.getByRole('button', { name: /retry/i }))
    expect(onRetry).toHaveBeenCalledTimes(1)
  })
})
