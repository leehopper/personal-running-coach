import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import {
  buildAbsorbTurn,
  buildAmberSafetyTurn,
  buildCrisisSafetyTurn,
  buildNudgeTurn,
  buildRestructureTurn,
} from './conversation.fixture'
import { ConversationPanel } from './conversation-panel.component'

// Spec 17 § Unit 7 proof artifacts: each escalation render (silent absorb /
// inline nudge / expandable restructure), diff collapse/expand, amber/red
// left-edge accent, Red safety full prominence, no input affordance, copy
// guardrails, and the DEC-063 reduced-motion pairing.

// The coach copy guardrails the panel must never violate (DEC-027 / spec 17
// § Unit 4 prompt contract): trademark, controlling/system language,
// miss-counting, claimed physical observation, feigned emotion, runner
// comparison. The fixtures carry realistic compliant copy; this list guards
// the panel's own static strings alongside them.
const BANNED_PHRASINGS: readonly RegExp[] = [
  /vdot/i,
  /you must|you have to|do not deviate|non-negotiable|forbidden/i,
  /\byou(?:'ve| have)? (?:missed|skipped) \d+\b/i,
  /\bi (?:can see|saw|noticed) (?:you|your body)\b/i,
  /\bi feel\b|\bit hurts me\b/i,
  /\bother runners?\b|\bmost runners?\b/i,
]

describe('ConversationPanel', () => {
  it('renders nothing at all when there are no turns (silent absorb)', () => {
    const { container } = render(<ConversationPanel turns={[]} />)

    expect(screen.queryByTestId('conversation-panel')).toBeNull()
    expect(container.firstChild).toBeNull()
  })

  it('renders nothing for a defensive absorb-kind turn', () => {
    render(<ConversationPanel turns={[buildAbsorbTurn()]} />)

    expect(screen.queryByTestId('conversation-panel')).toBeNull()
  })

  it('renders a nudge as an inline one-liner with no expandable block and no diff', () => {
    const nudge = buildNudgeTurn()
    render(<ConversationPanel turns={[nudge]} />)

    const inline = screen.getByTestId('nudge-turn')
    expect(inline).toHaveTextContent(nudge.content)
    expect(screen.queryByTestId('restructure-turn')).toBeNull()
    expect(screen.queryByTestId('diff-toggle')).toBeNull()
    expect(screen.queryByTestId('before-after-diff')).toBeNull()
  })

  it('renders a restructure as an expandable block with the diff collapsed by default', () => {
    const restructure = buildRestructureTurn()
    render(<ConversationPanel turns={[restructure]} />)

    const block = screen.getByTestId('restructure-turn')
    expect(block).toHaveTextContent('This week becomes a recovery week')
    expect(within(block).getByTestId('diff-toggle')).toHaveTextContent('Show what changed')
    // Radix keeps the closed content mounted but `hidden` — assert
    // visibility, not presence.
    expect(screen.getByTestId('before-after-diff')).not.toBeVisible()
  })

  it('carries a subtle amber left-edge accent on a restructure block with no loud badge', () => {
    render(<ConversationPanel turns={[buildRestructureTurn()]} />)

    const block = screen.getByTestId('restructure-turn')
    expect(block.className).toContain('border-l-warning')
    // No loud G/A/R badge: the panel never names the severity or the
    // internal escalation machinery in its own copy.
    expect(screen.queryByText(/\b(amber|red|green|level \d|l\d)\b/i)).toBeNull()
  })

  it('expands and collapses the before/after diff from the "Show what changed" control', async () => {
    const user = userEvent.setup()
    render(<ConversationPanel turns={[buildRestructureTurn()]} />)

    await user.click(screen.getByTestId('diff-toggle'))
    const diff = screen.getByTestId('before-after-diff')
    expect(diff).toBeVisible()
    expect(within(diff).getByText('Week 1 · Tuesday')).toBeInTheDocument()
    expect(
      within(diff).getByText('Threshold Intervals (10.0 km) → Easy Aerobic Run (8.0 km)'),
    ).toBeInTheDocument()
    expect(within(diff).getByText('Week 1 volume')).toBeInTheDocument()
    expect(within(diff).getByText('36.0 km → 28.0 km')).toBeInTheDocument()

    await user.click(screen.getByTestId('diff-toggle'))
    expect(screen.getByTestId('before-after-diff')).not.toBeVisible()
  })

  it('renders a Red safety turn prominently in full with a red left-edge accent', () => {
    const crisis = buildCrisisSafetyTurn()
    render(<ConversationPanel turns={[crisis]} />)

    const safety = screen.getByTestId('safety-turn')
    expect(safety).toHaveAttribute('data-tier', 'red')
    expect(safety.className).toContain('border-l-destructive')
    expect(safety.className).toContain('border-l-4')
    // Full prominence: the scripted content renders complete and uncollapsed,
    // including the exact crisis resource strings.
    expect(safety).toHaveTextContent('988 Suicide & Crisis Lifeline')
    expect(safety).toHaveTextContent('Crisis Text Line: text 741741')
    expect(within(safety).queryByRole('button')).toBeNull()
  })

  it('renders a Red safety turn regardless of any plan-change turns alongside it', () => {
    render(<ConversationPanel turns={[buildCrisisSafetyTurn(), buildNudgeTurn()]} />)

    expect(screen.getByTestId('safety-turn')).toHaveAttribute('data-tier', 'red')
    expect(screen.getByTestId('nudge-turn')).toBeInTheDocument()
  })

  it('renders an Amber safety turn in full with an amber accent', () => {
    const amber = buildAmberSafetyTurn()
    render(<ConversationPanel turns={[amber]} />)

    const safety = screen.getByTestId('safety-turn')
    expect(safety).toHaveAttribute('data-tier', 'amber')
    expect(safety.className).toContain('border-l-warning')
    expect(safety).toHaveTextContent(amber.content)
  })

  it('preserves the wire order (newest-first) across mixed turns', () => {
    render(<ConversationPanel turns={[buildRestructureTurn(), buildNudgeTurn()]} />)

    const items = screen.getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(within(items[0]).getByTestId('restructure-turn')).toBeInTheDocument()
    expect(within(items[1]).getByTestId('nudge-turn')).toBeInTheDocument()
  })

  it('offers no input affordance of any kind', () => {
    render(
      <ConversationPanel
        turns={[buildCrisisSafetyTurn(), buildNudgeTurn(), buildRestructureTurn()]}
      />,
    )

    const panel = screen.getByTestId('conversation-panel')
    expect(within(panel).queryByRole('textbox')).toBeNull()
    // The diff toggle is the only interactive control in the whole panel —
    // no send, no accept/reject, no override.
    const buttons = within(panel).getAllByRole('button')
    expect(buttons).toHaveLength(1)
    expect(buttons[0]).toHaveTextContent('Show what changed')
  })

  it('contains none of the banned coaching phrasings and never the trademarked term', () => {
    render(
      <ConversationPanel
        turns={[
          buildCrisisSafetyTurn(),
          buildAmberSafetyTurn(),
          buildRestructureTurn(),
          buildNudgeTurn(),
        ]}
      />,
    )

    const copy = screen.getByTestId('conversation-panel').textContent ?? ''
    for (const pattern of BANNED_PHRASINGS) {
      expect(copy).not.toMatch(pattern)
    }
  })

  it('pairs every animated utility with a motion-reduce override for its family', async () => {
    const user = userEvent.setup()
    render(<ConversationPanel turns={[buildRestructureTurn()]} />)
    await user.click(screen.getByTestId('diff-toggle'))

    type AnimationFamily = 'transition' | 'animate'

    // The animation family a class token belongs to, ignoring any variant
    // prefix (`data-[state=open]:animate-in` → `animate`) and the `*-none`
    // disablers themselves.
    const animationFamily = (token: string): AnimationFamily | null => {
      const base = token.slice(token.lastIndexOf(':') + 1)
      if (base === 'transition-none' || base === 'animate-none') return null
      if (/^transition(?:-|$)/.test(base)) return 'transition'
      if (/^animate-/.test(base)) return 'animate'
      return null
    }

    // The family a `motion-reduce:` override neutralises — one
    // `motion-reduce:transition-none` covers every `transition-*` on the
    // element, but it does NOT excuse an unpaired `animate-*`.
    const overrideFamily = (token: string): AnimationFamily | null => {
      if (!token.startsWith('motion-reduce:')) return null
      const base = token.slice('motion-reduce:'.length)
      if (/^transition(?:-|$)/.test(base)) return 'transition'
      if (/^animate(?:-|$)/.test(base)) return 'animate'
      return null
    }

    const panel = screen.getByTestId('conversation-panel')
    let elementsChecked = 0
    for (const element of [panel, ...panel.querySelectorAll('[class]')]) {
      const tokens = (element.getAttribute('class') ?? '').split(/\s+/).filter(Boolean)
      const animated = new Set<AnimationFamily>()
      const overridden = new Set<AnimationFamily>()
      for (const token of tokens) {
        const family = animationFamily(token)
        if (family !== null) animated.add(family)
        const override = overrideFamily(token)
        if (override !== null) overridden.add(override)
      }
      if (animated.size === 0) continue
      for (const family of animated) {
        expect(
          overridden.has(family),
          `${family} animation on <${element.tagName.toLowerCase()}> lacks a motion-reduce:${family}-* override`,
        ).toBe(true)
      }
      elementsChecked += 1
    }
    // Guard against the selector silently matching nothing — a refactor that
    // drops the animated classes would otherwise make the assertions vacuous.
    expect(elementsChecked).toBeGreaterThan(0)
  })
})
