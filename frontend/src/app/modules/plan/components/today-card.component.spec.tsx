import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { TodayCard } from './today-card.component'
import { buildPlanFixture, fixtureWeekOneWorkouts } from './plan-display.fixture'

describe('TodayCard', () => {
  // Local Sunday April 2026-04-26 = day-of-week 0; test seed dates here
  // are constructed via the local-time `new Date(year, monthIndex, day)`
  // overload so `getDay()` resolves identically across CI timezones.
  const sunday = new Date(2026, 3, 26)
  const monday = new Date(2026, 3, 27)
  const wednesday = new Date(2026, 3, 29)
  const thursday = new Date(2026, 3, 30)

  it('renders the workout variant when today is a run day', () => {
    const plan = buildPlanFixture()
    render(
      <TodayCard
        currentWeek={plan.mesoWeeks[0]}
        workouts={fixtureWeekOneWorkouts()}
        today={monday}
      />,
    )
    const card = screen.getByTestId('today-card')
    expect(card.dataset.variant).toBe('workout')
    expect(screen.getByRole('heading', { name: 'Easy aerobic shakeout' })).toBeInTheDocument()
    const inner = screen.getByTestId('micro-workout-card')
    expect(inner.dataset.emphasized).toBe('true')
  })

  it('renders the rest-day variant on a rest slot, calling out the next workout', () => {
    const plan = buildPlanFixture()
    render(
      <TodayCard
        currentWeek={plan.mesoWeeks[0]}
        workouts={fixtureWeekOneWorkouts()}
        today={sunday}
      />,
    )
    const card = screen.getByTestId('today-card')
    expect(card.dataset.variant).toBe('rest')
    const next = screen.getByTestId('today-card-next-workout')
    expect(next.textContent).toMatch(/monday/iu)
    expect(next.textContent).toMatch(/easy aerobic shakeout/iu)
  })

  it('renders the rest-day variant when slot is Rest even on a midweek day', () => {
    const plan = buildPlanFixture()
    render(
      <TodayCard
        currentWeek={plan.mesoWeeks[0]}
        workouts={fixtureWeekOneWorkouts()}
        today={thursday}
      />,
    )
    const card = screen.getByTestId('today-card')
    expect(card.dataset.variant).toBe('rest')
    const next = screen.getByTestId('today-card-next-workout')
    expect(next.textContent).toMatch(/saturday/iu)
  })

  it('shows interval session details when today is the threshold day', () => {
    const plan = buildPlanFixture()
    render(
      <TodayCard
        currentWeek={plan.mesoWeeks[0]}
        workouts={fixtureWeekOneWorkouts()}
        today={wednesday}
      />,
    )
    expect(screen.getByRole('heading', { name: 'Threshold intervals' })).toBeInTheDocument()
  })

  it('contains zero VDOT references in the rendered DOM (trademark rule)', () => {
    const plan = buildPlanFixture()
    const { container } = render(
      <TodayCard
        currentWeek={plan.mesoWeeks[0]}
        workouts={fixtureWeekOneWorkouts()}
        today={wednesday}
      />,
    )
    expect(container.textContent ?? '').not.toMatch(/vdot/iu)
  })
})
