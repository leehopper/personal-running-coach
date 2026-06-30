import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { CoachComposer } from './coach-composer.component'

describe('CoachComposer', () => {
  it('disables the send control until a non-blank message is typed', async () => {
    const user = userEvent.setup()
    render(<CoachComposer onSend={vi.fn()} isStreaming={false} />)

    const send = screen.getByRole('button', { name: /send/i })
    expect(send).toBeDisabled()

    await user.type(screen.getByLabelText(/message your coach/i), '   ')
    expect(send).toBeDisabled()

    await user.type(screen.getByLabelText(/message your coach/i), 'how was my run?')
    expect(send).toBeEnabled()
  })

  it('sends the trimmed message and clears the input on submit', async () => {
    const user = userEvent.setup()
    const onSend = vi.fn()
    render(<CoachComposer onSend={onSend} isStreaming={false} />)

    const input = screen.getByLabelText(/message your coach/i)
    await user.type(input, '  ran 5k  ')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(onSend).toHaveBeenCalledExactlyOnceWith('ran 5k')
    expect(input).toHaveValue('')
  })

  it('disables the send control while a stream is in flight', async () => {
    const user = userEvent.setup()
    const onSend = vi.fn()
    render(<CoachComposer onSend={onSend} isStreaming={true} />)

    await user.type(screen.getByLabelText(/message your coach/i), 'another question')
    const send = screen.getByRole('button', { name: /send/i })
    expect(send).toBeDisabled()

    await user.click(send)
    expect(onSend).not.toHaveBeenCalled()
  })

  it('submits on Enter but inserts a newline on Shift+Enter', async () => {
    const user = userEvent.setup()
    const onSend = vi.fn()
    render(<CoachComposer onSend={onSend} isStreaming={false} />)

    const input = screen.getByLabelText(/message your coach/i)
    await user.type(input, 'first{Shift>}{Enter}{/Shift}second')
    expect(onSend).not.toHaveBeenCalled()

    await user.type(input, '{Enter}')
    expect(onSend).toHaveBeenCalledExactlyOnceWith('first\nsecond')
  })
})
