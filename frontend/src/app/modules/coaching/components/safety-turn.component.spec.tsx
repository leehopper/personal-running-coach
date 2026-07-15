import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SAFETY_TIER } from '~/modules/coaching/models/conversation.model'
import { SafetyTurn } from './safety-turn.component'

describe('SafetyTurn', () => {
  it('renders the amber tier with its heading, edge, and surface', () => {
    render(<SafetyTurn tier={SAFETY_TIER.amber} content="See a physio about that knee." />)

    const article = screen.getByTestId('safety-turn')
    expect(article).toHaveAttribute('data-tier', 'amber')
    expect(article).not.toHaveAttribute('role')
    expect(article).toHaveClass('border-l-warning')
    // DU-5: the edge is 3px this PR (was border-l-2) — pin the width so a
    // regression to the old width fails, not just the colour.
    expect(article).toHaveClass('border-l-[3px]')
    expect(article).toHaveClass('bg-card')
    expect(screen.getByText('WORTH A PROFESSIONAL LOOK')).toHaveClass('text-warning')
    expect(screen.getByText('See a physio about that knee.')).toBeInTheDocument()
  })

  it('renders the red tier with its heading, edge, and danger surface', () => {
    render(<SafetyTurn tier={SAFETY_TIER.red} content="Call 988 if you are in crisis." />)

    const article = screen.getByTestId('safety-turn')
    expect(article).toHaveAttribute('data-tier', 'red')
    expect(article).toHaveClass('border-l-destructive')
    // DU-5: 3px edge this PR (was border-l-4) — pin the width against a regression.
    expect(article).toHaveClass('border-l-[3px]')
    expect(article).toHaveClass('bg-danger-surface')
    expect(screen.getByText('STOP — GET SEEN')).toHaveClass('text-danger-text')
  })

  it('renders the defensive green tier with a neutral edge and no heading', () => {
    render(<SafetyTurn tier={SAFETY_TIER.green} content="All clear." />)

    const article = screen.getByTestId('safety-turn')
    expect(article).toHaveAttribute('data-tier', 'green')
    expect(article).toHaveClass('border-l-border')
    expect(screen.queryByText('WORTH A PROFESSIONAL LOOK')).not.toBeInTheDocument()
    expect(screen.queryByText('STOP — GET SEEN')).not.toBeInTheDocument()
  })

  // AX-01 — the acceptance bar: a pathological-length scripted message
  // renders in FULL, never clamped/collapsed/truncated, with no competing CTA.
  it('renders a pathological-length message in full with no clamp/truncate/max-h class (AX-01)', () => {
    const paragraphs = Array.from(
      { length: 40 },
      (_, i) =>
        `Paragraph ${i}: this is a long scripted safety message that must never be cut off.`,
    )
    const content = paragraphs.join('\n\n')
    render(<SafetyTurn tier={SAFETY_TIER.red} content={content} />)

    const article = screen.getByTestId('safety-turn')
    // The full text is present verbatim.
    expect(article).toHaveTextContent(paragraphs[0])
    expect(article).toHaveTextContent(paragraphs[paragraphs.length - 1])

    const contentEl = screen.getByText((_, element) => element?.textContent === content)
    expect(contentEl.className).not.toMatch(/line-clamp/)
    expect(contentEl.className).not.toMatch(/truncate/)
    expect(contentEl.className).not.toMatch(/max-h/)
    // No competing CTA rendered alongside the safety content.
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })

  it('renders the live in-flight notice with role="alert" and the coach-safety-notice testid', () => {
    render(
      <SafetyTurn
        tier={SAFETY_TIER.red}
        content="Call 988 now."
        role="alert"
        testId="coach-safety-notice"
      />,
    )

    const article = screen.getByTestId('coach-safety-notice')
    expect(article).toHaveAttribute('role', 'alert')
    expect(screen.queryByTestId('safety-turn')).not.toBeInTheDocument()
  })

  it('renders the persisted timeline turn without role="alert" (not re-announced)', () => {
    render(<SafetyTurn tier={SAFETY_TIER.amber} content="See a physio." />)

    expect(screen.getByTestId('safety-turn')).not.toHaveAttribute('role')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
