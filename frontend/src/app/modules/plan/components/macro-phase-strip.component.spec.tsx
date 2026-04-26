import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { MacroPhase } from '~/modules/plan/models/plan.model'
import { MacroPhaseStrip } from './macro-phase-strip.component'
import { buildPlanFixture } from './plan-display.fixture'

const macroFromFixture = (): MacroPhase => {
  const plan = buildPlanFixture()
  if (plan.macro === null) {
    throw new Error('Fixture macro must not be null')
  }
  return plan.macro
}

describe('MacroPhaseStrip', () => {
  it('renders one segment per phase with phase labels', () => {
    const macro = macroFromFixture()
    render(<MacroPhaseStrip macro={macro} currentWeek={1} />)
    const segments = screen.getAllByTestId('macro-phase-segment')
    expect(segments).toHaveLength(macro.phases.length)
    expect(segments[0].dataset.phase).toBe('Base')
    expect(segments[1].dataset.phase).toBe('Build')
    expect(segments[2].dataset.phase).toBe('Peak')
    expect(segments[3].dataset.phase).toBe('Taper')
  })

  it('labels start/end weeks per phase', () => {
    render(<MacroPhaseStrip macro={macroFromFixture()} currentWeek={null} />)
    expect(screen.getByText('Weeks 1–4')).toBeInTheDocument()
    expect(screen.getByText('Weeks 5–9')).toBeInTheDocument()
    expect(screen.getByText('Weeks 10–11')).toBeInTheDocument()
    expect(screen.getByText('Week 12')).toBeInTheDocument()
  })

  it('marks the current-week segment via data attribute and aria-current', () => {
    render(<MacroPhaseStrip macro={macroFromFixture()} currentWeek={6} />)
    const segments = screen.getAllByTestId('macro-phase-segment')
    const buildSegment = segments.find((segment) => segment.dataset.phase === 'Build')
    expect(buildSegment?.dataset.current).toBe('true')
    expect(buildSegment?.getAttribute('aria-current')).toBe('step')
    const baseSegment = segments.find((segment) => segment.dataset.phase === 'Base')
    expect(baseSegment?.dataset.current).toBe('false')
  })

  it('renders no current-week marker when currentWeek is null', () => {
    render(<MacroPhaseStrip macro={macroFromFixture()} currentWeek={null} />)
    expect(screen.queryByTestId('macro-phase-current-marker')).not.toBeInTheDocument()
  })

  it('contains zero VDOT references in the rendered DOM (trademark rule)', () => {
    const { container } = render(<MacroPhaseStrip macro={macroFromFixture()} currentWeek={1} />)
    expect(container.textContent ?? '').not.toMatch(/vdot/iu)
  })
})
