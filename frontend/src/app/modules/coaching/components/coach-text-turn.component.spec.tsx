import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'
import { CoachTextTurn } from './coach-text-turn.component'

describe('CoachTextTurn', () => {
  it('renders no bubble — just a mono COACH · HH:MM label and bone body text', () => {
    render(<CoachTextTurn content="You ran well." time="06:59" />)

    const root = screen.getByTestId('coach-text-turn')
    expect(root).not.toHaveClass('bg-muted')
    expect(root).not.toHaveClass('bg-primary')
    expect(screen.getByText(/COACH · 06:59/)).toBeInTheDocument()
    expect(screen.getByText(/COACH · 06:59/)).toHaveClass('text-clay-text')
    expect(screen.getByText('You ran well.')).toBeInTheDocument()
  })

  it('renders no markdown — literal asterisks render verbatim', () => {
    render(<CoachTextTurn content="Run **easy** today, not your pace-zone limit." time="06:59" />)
    expect(screen.getByText(/Run \*\*easy\*\* today/)).toBeInTheDocument()
  })

  it('preserves whitespace with no clamp on long content', () => {
    const longContent = 'first line\nsecond line\n' + 'y'.repeat(500)
    render(<CoachTextTurn content={longContent} time="06:59" />)

    const paragraph = screen.getByText(
      (_, element) => element?.textContent?.startsWith(longContent) ?? false,
    )
    expect(paragraph).toHaveClass('whitespace-pre-wrap')
    expect(paragraph.className).not.toMatch(/line-clamp|truncate/)
  })

  it('renders the clay block-cursor inline at the end of the body when streaming', () => {
    render(<CoachTextTurn content="A tempo run is" time="06:59" streaming />)

    const cursor = screen.getByTestId('coach-stream-cursor')
    expect(cursor).toHaveAttribute('aria-hidden')
    expect(cursor).toHaveClass('bg-primary')
  })

  it('renders no cursor when not streaming', () => {
    render(<CoachTextTurn content="You ran well." time="06:59" />)
    expect(screen.queryByTestId('coach-stream-cursor')).not.toBeInTheDocument()
  })

  // The assertions live inside the shared expectDualThemeParity helper —
  // sonarjs's static check can't see through the function call.
  // eslint-disable-next-line sonarjs/assertions-in-tests
  it('holds dual-theme structural parity with no raw hex colour literals', () => {
    const result = renderInBothThemes(<CoachTextTurn content="You ran well." time="06:59" />)
    expectDualThemeParity(result, 'coach-text-turn')
  })
})
