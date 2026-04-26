import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { UpcomingList } from './upcoming-list.component'
import { buildPlanFixture, fixtureWeekOneWorkouts } from './plan-display.fixture'

describe('UpcomingList', () => {
  // Local-time constructors keep day-of-week deterministic across runners.
  const monday = new Date(2026, 3, 27) // dayOfWeek = 1
  const friday = new Date(2026, 3, 24) // dayOfWeek = 5

  it('renders only the workouts strictly after today within the current week', () => {
    const plan = buildPlanFixture()
    render(
      <UpcomingList
        currentWeekWorkouts={fixtureWeekOneWorkouts()}
        weeks={plan.mesoWeeks}
        currentWeek={1}
        today={monday}
      />,
    )
    const items = screen.getAllByTestId('upcoming-workout-item')
    // After Monday: Wednesday (interval) + Saturday (long run) survive.
    expect(items).toHaveLength(2)
    const cards = screen.getAllByTestId('micro-workout-card')
    const titles = cards.map((card) => card.querySelector('h3')?.textContent ?? '')
    expect(titles).toEqual(['Threshold intervals', 'Long aerobic run'])
  })

  it('omits the rest-of-week section when no workouts remain', () => {
    const plan = buildPlanFixture()
    // Friday in fixture week-1 has an Easy run; pick Saturday-after to wash
    // out everything past today (saturday is the last run, dayOfWeek 6).
    const lateSaturday = new Date(2026, 3, 25) // dayOfWeek 6
    render(
      <UpcomingList
        currentWeekWorkouts={fixtureWeekOneWorkouts()}
        weeks={plan.mesoWeeks}
        currentWeek={1}
        today={lateSaturday}
      />,
    )
    expect(screen.queryByTestId('upcoming-week-remainder')).not.toBeInTheDocument()
    // The meso block still renders.
    expect(screen.getByTestId('meso-week-block')).toBeInTheDocument()
  })

  it('renders the meso week block alongside the upcoming workouts', () => {
    const plan = buildPlanFixture()
    render(
      <UpcomingList
        currentWeekWorkouts={fixtureWeekOneWorkouts()}
        weeks={plan.mesoWeeks}
        currentWeek={1}
        today={friday}
      />,
    )
    expect(screen.getByTestId('meso-week-block')).toBeInTheDocument()
    expect(screen.getAllByTestId('meso-week-card')).toHaveLength(plan.mesoWeeks.length)
  })

  it('contains zero VDOT references in the rendered DOM (trademark rule)', () => {
    const plan = buildPlanFixture()
    const { container } = render(
      <UpcomingList
        currentWeekWorkouts={fixtureWeekOneWorkouts()}
        weeks={plan.mesoWeeks}
        currentWeek={1}
        today={monday}
      />,
    )
    expect(container.textContent ?? '').not.toMatch(/vdot/iu)
  })
})
