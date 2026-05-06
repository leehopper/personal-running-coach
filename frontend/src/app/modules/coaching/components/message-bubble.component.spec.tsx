import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { MessageBubble, type MessageContentBlock } from './message-bubble.component'

describe('MessageBubble', () => {
  it('renders text content blocks', () => {
    const content: MessageContentBlock[] = [{ type: 'text', text: 'Hello, runner.' }]
    render(<MessageBubble role="assistant" content={content} />)
    expect(screen.getByText('Hello, runner.')).toBeInTheDocument()
  })

  it('renders multiple text blocks in order', () => {
    const content: MessageContentBlock[] = [
      { type: 'text', text: 'first' },
      { type: 'text', text: 'second' },
    ]
    render(<MessageBubble role="assistant" content={content} />)
    const paragraphs = screen.getAllByText(/first|second/)
    expect(paragraphs[0]).toHaveTextContent('first')
    expect(paragraphs[1]).toHaveTextContent('second')
  })

  it('ignores thinking blocks (never rendered as text)', () => {
    const content: MessageContentBlock[] = [
      { type: 'thinking', thinking: 'private chain-of-thought' },
      { type: 'text', text: 'visible reply' },
    ]
    render(<MessageBubble role="assistant" content={content} />)
    expect(screen.queryByText(/private chain-of-thought/)).not.toBeInTheDocument()
    expect(screen.getByText('visible reply')).toBeInTheDocument()
  })

  it('ignores tool_use blocks', () => {
    const content: MessageContentBlock[] = [
      { type: 'tool_use', id: 'tu_1', name: 'lookup', input: { q: 'pace' } },
      { type: 'text', text: 'visible' },
    ]
    render(<MessageBubble role="assistant" content={content} />)
    expect(screen.queryByText(/lookup/)).not.toBeInTheDocument()
    expect(screen.queryByText(/pace/)).not.toBeInTheDocument()
    expect(screen.getByText('visible')).toBeInTheDocument()
  })

  it('ignores tool_result blocks', () => {
    const content: MessageContentBlock[] = [
      { type: 'tool_result', tool_use_id: 'tu_1', content: 'opaque' },
      { type: 'text', text: 'reply' },
    ]
    render(<MessageBubble role="assistant" content={content} />)
    expect(screen.queryByText(/opaque/)).not.toBeInTheDocument()
    expect(screen.getByText('reply')).toBeInTheDocument()
  })

  it('ignores signature blocks', () => {
    const content: MessageContentBlock[] = [
      { type: 'signature', signature: 'sig_xyz' },
      { type: 'text', text: 'reply' },
    ]
    render(<MessageBubble role="assistant" content={content} />)
    expect(screen.queryByText(/sig_xyz/)).not.toBeInTheDocument()
    expect(screen.getByText('reply')).toBeInTheDocument()
  })

  it('renders nothing visible when only non-text blocks are present', () => {
    const content: MessageContentBlock[] = [
      { type: 'thinking', thinking: 'x' },
      { type: 'tool_use', id: 't', name: 'n', input: {} },
    ]
    render(<MessageBubble role="assistant" content={content} />)
    const bubble = screen.getByTestId('message-bubble')
    expect(bubble).toBeInTheDocument()
    expect(bubble.textContent).toBe('')
  })

  it('applies user role styling and data attribute', () => {
    render(<MessageBubble role="user" content={[{ type: 'text', text: 'hi' }]} />)
    const bubble = screen.getByTestId('message-bubble')
    expect(bubble.dataset.role).toBe('user')
    expect(bubble.className).toMatch(/ml-auto/)
    expect(bubble.className).toMatch(/bg-slate-900/)
  })

  it('applies assistant role styling and data attribute', () => {
    render(<MessageBubble role="assistant" content={[{ type: 'text', text: 'hi' }]} />)
    const bubble = screen.getByTestId('message-bubble')
    expect(bubble.dataset.role).toBe('assistant')
    expect(bubble.className).toMatch(/mr-auto/)
    expect(bubble.className).toMatch(/bg-slate-100/)
  })

  it('marks pending bubbles with reduced opacity and data attribute', () => {
    render(
      <MessageBubble role="user" content={[{ type: 'text', text: 'sending' }]} pending={true} />,
    )
    const bubble = screen.getByTestId('message-bubble')
    expect(bubble.dataset.pending).toBe('true')
    expect(bubble.className).toMatch(/opacity-60/)
  })

  it('defaults pending to false', () => {
    render(<MessageBubble role="user" content={[{ type: 'text', text: 'hi' }]} />)
    const bubble = screen.getByTestId('message-bubble')
    expect(bubble.dataset.pending).toBe('false')
    expect(bubble.className).not.toMatch(/opacity-60/)
  })
})
