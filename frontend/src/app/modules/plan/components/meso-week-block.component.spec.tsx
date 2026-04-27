import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { MesoWeekBlock } from './meso-week-block.component'
import { buildPlanFixture } from './plan-display.fixture'

describe('MesoWeekBlock', () => {
  it('renders one card per meso week', () => {
    const plan = buildPlanFixture()
    render(<MesoWeekBlock weeks={plan.mesoWeeks} currentWeek={1} />)
    const cards = screen.getAllByTestId('meso-week-card')
    expect(cards).toHaveLength(plan.mesoWeeks.length)
    expect(cards.map((card) => card.dataset.week)).toEqual(['1', '2', '3', '4'])
  })

  it('highlights the current week and dims future weeks', () => {
    const plan = buildPlanFixture()
    render(<MesoWeekBlock weeks={plan.mesoWeeks} currentWeek={2} />)
    const cards = screen.getAllByTestId('meso-week-card')
    const states = cards.map((card) => card.dataset.state)
    expect(states).toEqual(['past', 'current', 'future', 'future'])
    expect(cards[1].getAttribute('aria-current')).toBe('step')
    expect(cards[2].className).toMatch(/opacity-70/)
  })

  it('flags deload weeks visibly', () => {
    const plan = buildPlanFixture()
    render(<MesoWeekBlock weeks={plan.mesoWeeks} currentWeek={1} />)
    const deloadFlags = screen.getAllByTestId('meso-week-deload-flag')
    expect(deloadFlags).toHaveLength(1)
    expect(deloadFlags[0].textContent).toMatch(/deload/iu)
  })

  it('renders weekly target km and the week summary text', () => {
    const plan = buildPlanFixture()
    render(<MesoWeekBlock weeks={plan.mesoWeeks} currentWeek={1} />)
    expect(screen.getAllByText(/30\.0 km/u).length).toBeGreaterThan(0)
    expect(screen.getByText(/aerobic base/u)).toBeInTheDocument()
  })

  it('renders all cards in the neutral state when currentWeek is null', () => {
    const plan = buildPlanFixture()
    render(<MesoWeekBlock weeks={plan.mesoWeeks} currentWeek={null} />)
    const cards = screen.getAllByTestId('meso-week-card')
    expect(cards.every((card) => card.dataset.state === 'neutral')).toBe(true)
  })

  it('contains zero VDOT references in the rendered DOM (trademark rule)', () => {
    const plan = buildPlanFixture()
    const { container } = render(<MesoWeekBlock weeks={plan.mesoWeeks} currentWeek={1} />)
    expect(container.textContent ?? '').not.toMatch(/vdot/iu)
  })
})
