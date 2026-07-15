import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { CoachComposer } from './coach-composer.component'

describe('CoachComposer', () => {
  it('shows the design placeholder copy and a 48px clay-square send control with an aria-label', () => {
    render(<CoachComposer onSend={vi.fn()} isStreaming={false} />)

    expect(screen.getByPlaceholderText('Ask, or describe a run to log…')).toBeInTheDocument()
    expect(screen.getByLabelText(/message your coach/i)).toHaveClass('min-h-12')
    const send = screen.getByRole('button', { name: 'Send message' })
    expect(send).toHaveClass('size-12')
  })

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

  it('seeds the textarea with initialValue at mount time', () => {
    render(
      <CoachComposer onSend={vi.fn()} isStreaming={false} initialValue="How's my week look?" />,
    )

    expect(screen.getByLabelText(/message your coach/i)).toHaveValue("How's my week look?")
  })

  it('defaults initialValue to an empty textarea when omitted', () => {
    render(<CoachComposer onSend={vi.fn()} isStreaming={false} />)

    expect(screen.getByLabelText(/message your coach/i)).toHaveValue('')
  })

  it('moves focus to the textarea on mount when autoFocus is true', () => {
    render(<CoachComposer onSend={vi.fn()} isStreaming={false} autoFocus />)

    expect(screen.getByLabelText(/message your coach/i)).toHaveFocus()
  })

  it('does not focus the textarea on mount when autoFocus is false (the default)', () => {
    render(<CoachComposer onSend={vi.fn()} isStreaming={false} />)

    expect(screen.getByLabelText(/message your coach/i)).not.toHaveFocus()
  })

  it('stays uncontrolled after mount — typing over a seeded initialValue works normally', async () => {
    const user = userEvent.setup()
    const onSend = vi.fn()
    render(<CoachComposer onSend={onSend} isStreaming={false} initialValue="draft" autoFocus />)

    const input = screen.getByLabelText(/message your coach/i)
    expect(input).toHaveValue('draft')

    await user.clear(input)
    await user.type(input, 'a fresh message')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(onSend).toHaveBeenCalledExactlyOnceWith('a fresh message')
  })
})
