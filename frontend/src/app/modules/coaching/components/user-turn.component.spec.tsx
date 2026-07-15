import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'
import { UserTurn } from './user-turn.component'

describe('UserTurn', () => {
  it('renders a right-aligned bubble with the message content and a YOU · HH:MM meta line', () => {
    render(<UserTurn content="how was my run?" time="06:58" />)

    const root = screen.getByTestId('user-turn')
    expect(root).toHaveClass('items-end')
    expect(screen.getByText('how was my run?')).toBeInTheDocument()
    expect(screen.getByTestId('turn-meta')).toHaveTextContent('YOU · 06:58')
    expect(screen.getByTestId('turn-meta')).toHaveClass('text-[var(--alp-faint)]')
  })

  it('applies the bubble shape/surface classes (rounded corner, bg-muted, max-width, no clamp)', () => {
    render(<UserTurn content="a message" time="09:00" />)

    const bubble = screen.getByText('a message').parentElement
    expect(bubble).toHaveClass('max-w-[85%]')
    expect(bubble).toHaveClass('rounded-[10px_10px_4px_10px]')
    expect(bubble).toHaveClass('bg-muted')
  })

  it('preserves the runner’s own line breaks and wraps long unbroken content, never clamping', () => {
    const longContent = 'line one\nline two\n' + 'x'.repeat(500)
    const { container } = render(<UserTurn content={longContent} time="06:58" />)

    const paragraph = container.querySelector('p')
    expect(paragraph?.textContent).toBe(longContent)
    expect(paragraph).toHaveClass('whitespace-pre-wrap')
    expect(paragraph).toHaveClass('break-words')
    expect(paragraph).not.toHaveClass('truncate')
    expect(paragraph?.className).not.toMatch(/line-clamp/)
  })

  it('renders no markdown — literal asterisks pass through verbatim', () => {
    render(<UserTurn content="ran **5k** today" time="06:58" />)
    expect(screen.getByText('ran **5k** today')).toBeInTheDocument()
  })

  // The assertions live inside the shared expectDualThemeParity helper —
  // sonarjs's static check can't see through the function call.
  // eslint-disable-next-line sonarjs/assertions-in-tests
  it('holds dual-theme structural parity with no raw hex colour literals', () => {
    const result = renderInBothThemes(<UserTurn content="how was my run?" time="06:58" />)
    expectDualThemeParity(result, 'user-turn')
  })
})
