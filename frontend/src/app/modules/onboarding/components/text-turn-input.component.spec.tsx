import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { OnboardingTopic } from '~/modules/onboarding/models/onboarding.model'
import { TextTurnInput } from './text-turn-input.component'

describe('TextTurnInput', () => {
  it('renders the textarea and send button', () => {
    render(<TextTurnInput onSubmit={vi.fn()} topic={OnboardingTopic.InjuryHistory} />)
    expect(screen.getByTestId('text-turn-input')).toBeInTheDocument()
    expect(screen.getByTestId('text-turn-input-field')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /send/i })).toBeInTheDocument()
  })

  it('send button is disabled when the field is empty (initial state)', () => {
    render(<TextTurnInput onSubmit={vi.fn()} topic={OnboardingTopic.InjuryHistory} />)
    expect(screen.getByRole('button', { name: /send/i })).toBeDisabled()
  })

  it('send button stays disabled when only whitespace is typed', async () => {
    const user = userEvent.setup()
    render(<TextTurnInput onSubmit={vi.fn()} topic={OnboardingTopic.InjuryHistory} />)

    await user.type(screen.getByTestId('text-turn-input-field'), '   \t  ')

    expect(screen.getByRole('button', { name: /send/i })).toBeDisabled()
  })

  it('does not invoke onSubmit when form is submitted with only whitespace', () => {
    const onSubmit = vi.fn()
    render(<TextTurnInput onSubmit={onSubmit} topic={OnboardingTopic.InjuryHistory} />)

    fireEvent.submit(screen.getByTestId('text-turn-input'))

    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('send button becomes enabled once valid text is typed', async () => {
    const user = userEvent.setup()
    render(<TextTurnInput onSubmit={vi.fn()} topic={OnboardingTopic.InjuryHistory} />)

    await user.type(screen.getByTestId('text-turn-input-field'), 'hello')

    expect(screen.getByRole('button', { name: /send/i })).toBeEnabled()
  })

  it('trims surrounding whitespace before passing value to onSubmit', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn().mockResolvedValue(undefined)
    render(<TextTurnInput onSubmit={onSubmit} topic={OnboardingTopic.InjuryHistory} />)

    await user.type(screen.getByTestId('text-turn-input-field'), '  hello world  ')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(onSubmit).toHaveBeenCalledWith({ text: 'hello world' })
  })

  it('resets the field after a successful submit', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn().mockResolvedValue(undefined)
    render(<TextTurnInput onSubmit={onSubmit} topic={OnboardingTopic.InjuryHistory} />)

    await user.type(screen.getByTestId('text-turn-input-field'), 'some text')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(screen.getByTestId('text-turn-input-field')).toHaveValue('')
  })

  it('disables the textarea and shows "Sending…" when isSubmitting is true', () => {
    render(
      <TextTurnInput
        onSubmit={vi.fn()}
        topic={OnboardingTopic.InjuryHistory}
        isSubmitting={true}
      />,
    )

    expect(screen.getByTestId('text-turn-input-field')).toBeDisabled()
    expect(screen.getByRole('button', { name: /sending/i })).toBeDisabled()
  })
})
